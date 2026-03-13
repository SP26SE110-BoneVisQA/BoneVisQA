using System.Text;
using System.Text.Json;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Repositories.UnitOfWork;
using BoneVisQA.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace BoneVisQA.Services.Services;

public class DocumentService : IDocumentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISupabaseStorageService _storageService;
    private readonly HttpClient _httpClient;
    private readonly string _aiServiceBaseUrl;

    public DocumentService(
        IUnitOfWork unitOfWork,
        ISupabaseStorageService storageService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _unitOfWork = unitOfWork;
        _storageService = storageService;
        _httpClient = httpClientFactory.CreateClient();
        _aiServiceBaseUrl = configuration["AIService:BaseUrl"] ?? "http://localhost:8000";
    }

    public async Task<DocumentDto> UploadDocumentAsync(IFormFile file, DocumentUploadDto metadata)
    {
        var fileUrl = await _storageService.UploadFileAsync(file, "knowledge_base", "documents");

        var document = new Document
        {
            Id = Guid.NewGuid(),
            Title = metadata.Title,
            FilePath = fileUrl,
            CategoryId = metadata.CategoryId,
            IndexingStatus = "Processing",
            Version = 1,
            IsOutdated = false,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.DocumentRepository.AddAsync(document);
        await _unitOfWork.SaveAsync();

        _ = Task.Run(async () =>
        {
            try
            {
                await TriggerPythonIngestionAsync(document.Id, fileUrl);
            }
            catch
            {
                await UpdateIndexingStatusAsync(document.Id, "Failed");
            }
        });

        return MapToDto(document);
    }

    public async Task<IEnumerable<DocumentDto>> GetAllDocumentsAsync()
    {
        var documents = await _unitOfWork.DocumentRepository.GetAllAsync();
        return documents.Select(MapToDto);
    }

    public async Task<DocumentDto?> GetDocumentByIdAsync(Guid id)
    {
        var document = await _unitOfWork.DocumentRepository.GetByIdAsync(id);
        return document == null ? null : MapToDto(document);
    }

    public async Task<bool> DeleteDocumentAsync(Guid id)
    {
        var document = await _unitOfWork.DocumentRepository.GetByIdAsync(id);
        if (document == null) return false;

        await _unitOfWork.DocumentRepository.DeleteAsync(id);
        await _unitOfWork.SaveAsync();
        return true;
    }

    public async Task<bool> TriggerReindexAsync(Guid id)
    {
        var document = await _unitOfWork.DocumentRepository.GetByIdAsync(id);
        if (document == null || string.IsNullOrEmpty(document.FilePath)) return false;

        document.IndexingStatus = "Processing";
        document.Version += 1;
        await _unitOfWork.DocumentRepository.UpdateAsync(document);
        await _unitOfWork.SaveAsync();

        _ = Task.Run(async () =>
        {
            try
            {
                await TriggerPythonIngestionAsync(document.Id, document.FilePath);
            }
            catch
            {
                await UpdateIndexingStatusAsync(document.Id, "Failed");
            }
        });

        return true;
    }

    public async Task UpdateIndexingStatusAsync(Guid id, string status)
    {
        var document = await _unitOfWork.DocumentRepository.GetByIdAsync(id);
        if (document != null)
        {
            document.IndexingStatus = status;
            await _unitOfWork.DocumentRepository.UpdateAsync(document);
            await _unitOfWork.SaveAsync();
        }
    }

    private async Task TriggerPythonIngestionAsync(Guid docId, string fileUrl)
    {
        var payload = new
        {
            docId = docId.ToString(),
            fileUrl = fileUrl
        };

        var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(
            $"{_aiServiceBaseUrl}/api/v1/documents/ingest",
            content);

        if (response.IsSuccessStatusCode)
        {
            await UpdateIndexingStatusAsync(docId, "Completed");
        }
        else
        {
            await UpdateIndexingStatusAsync(docId, "Failed");
        }
    }

    private static DocumentDto MapToDto(Document doc) => new()
    {
        Id = doc.Id,
        Title = doc.Title,
        FilePath = doc.FilePath,
        CategoryId = doc.CategoryId,
        IndexingStatus = doc.IndexingStatus,
        Version = doc.Version,
        IsOutdated = doc.IsOutdated,
        CreatedAt = doc.CreatedAt
    };
}
