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
    Task<DocumentDto> UploadDocumentAsync(IFormFile file, DocumentUploadDto metadata);
    Task<IEnumerable<DocumentDto>> GetAllDocumentsAsync();
    Task<DocumentDto?> GetDocumentByIdAsync(Guid id);
    Task<bool> DeleteDocumentAsync(Guid id);
    Task<bool> TriggerReindexAsync(Guid id);
    Task UpdateIndexingStatusAsync(Guid id, string status);
}
