using BoneVisQA.Repositories.Models;
using Microsoft.AspNetCore.Http;

namespace BoneVisQA.Services.Interfaces;

public class DocumentUploadDto
{
    public string Title { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
}

public class DocumentDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public Guid? CategoryId { get; set; }
    public string IndexingStatus { get; set; } = "Pending";
    public int Version { get; set; }
    public bool IsOutdated { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public interface IDocumentService
{
    /// <summary>
    /// Downloads the file from storage and runs RAG ingestion. Intended for use inside a new DI scope (e.g. background <c>Task.Run</c>), not from the HTTP request scope.
    /// </summary>
    Task IngestDocumentInBackgroundAsync(Guid documentId, string fileUrl);

    Task<DocumentDto> UploadDocumentAsync(IFormFile file, DocumentUploadDto metadata);
    Task<IEnumerable<DocumentDto>> GetAllDocumentsAsync();
    Task<DocumentDto?> GetDocumentByIdAsync(Guid id);
    Task<bool> DeleteDocumentAsync(Guid id);
    Task<bool> TriggerReindexAsync(Guid id);
    Task UpdateIndexingStatusAsync(Guid id, string status);
}
