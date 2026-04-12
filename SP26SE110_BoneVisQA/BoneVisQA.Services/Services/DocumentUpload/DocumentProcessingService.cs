using BoneVisQA.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace BoneVisQA.Services.Services.DocumentUpload;

public class DocumentProcessingService : IDocumentProcessingService
{
    private readonly IDocumentService _documentService;

    public DocumentProcessingService(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public async Task<IReadOnlyList<DocumentUploadResultItemDto>> UploadDocumentsAsync(
        IReadOnlyList<IFormFile> files,
        DocumentUploadDto baseMetadata,
        CancellationToken cancellationToken = default)
    {
        var results = new List<DocumentUploadResultItemDto>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (file.Length == 0)
            {
                results.Add(new DocumentUploadResultItemDto
                {
                    FileName = file.FileName,
                    Success = false,
                    Error = "Empty file."
                });
                continue;
            }

            try
            {
                var title = string.IsNullOrWhiteSpace(baseMetadata.Title)
                    ? Path.GetFileNameWithoutExtension(file.FileName)
                    : files.Count > 1
                        ? $"{baseMetadata.Title.Trim()} — {Path.GetFileName(file.FileName)}"
                        : baseMetadata.Title.Trim();

                var meta = new DocumentUploadDto
                {
                    Title = title,
                    CategoryId = baseMetadata.CategoryId,
                    TagIds = baseMetadata.TagIds
                };

                var doc = await _documentService.UploadDocumentAsync(file, meta, cancellationToken);
                results.Add(new DocumentUploadResultItemDto
                {
                    FileName = file.FileName,
                    Success = true,
                    Document = doc
                });
            }
            catch (Exception ex)
            {
                results.Add(new DocumentUploadResultItemDto
                {
                    FileName = file.FileName,
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        return results;
    }
}
