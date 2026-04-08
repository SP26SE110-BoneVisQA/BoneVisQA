using BoneVisQA.Repositories.Models;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading;

namespace BoneVisQA.Services.Interfaces;

public class DocumentUploadDto
{
    public string Title { get; set; } = string.Empty;
    public Guid? CategoryId { get; set; }
    public List<Guid> TagIds { get; set; } = new();
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

public class DocumentIngestionStatusDto
{
    public string Status { get; set; } = "Processing";
    public int ProgressPercentage { get; set; }
    public string CurrentOperation { get; set; } = string.Empty;
}

/// <summary>One row from a batch admin document upload.</summary>
public class DocumentUploadResultItemDto
{
    public string FileName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DocumentDto? Document { get; set; }
}

/// <summary>Batch PDF upload orchestration (delegates to <see cref="IDocumentService"/> per file).</summary>
public interface IDocumentProcessingService
{
    Task<IReadOnlyList<DocumentUploadResultItemDto>> UploadDocumentsAsync(
        IReadOnlyList<IFormFile> files,
        DocumentUploadDto baseMetadata,
        CancellationToken cancellationToken = default);
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
    Task<DocumentIngestionStatusDto?> GetIngestionStatusAsync(Guid id);
    string MapStatusForApi(string? rawStatus);
}
