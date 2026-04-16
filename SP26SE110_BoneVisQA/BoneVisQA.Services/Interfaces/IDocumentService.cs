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

public class DocumentChunkCitationFrequencyDto
{
    public Guid ChunkId { get; set; }
    public Guid DocumentId { get; set; }
    public int RetrievalCount { get; set; }
}

public class DocumentDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public Guid? CategoryId { get; set; }
    public string? Category { get; set; }
    public string IndexingStatus { get; set; } = "Pending";
    public int IndexingProgress { get; set; }
    public string? ContentHash { get; set; }
    /// <summary>Semantic version Major.Minor.Patch (e.g. 1.2.0).</summary>
    public string Version { get; set; } = "1.0.0";
    public bool IsOutdated { get; set; }
    /// <summary>UTC display timestamp without seconds (yyyy-MM-dd HH:mm).</summary>
    public string? CreatedAt { get; set; }
    /// <summary>UTC display timestamp without seconds (yyyy-MM-dd HH:mm).</summary>
    public string? UpdatedAt { get; set; }
    public int TotalPages { get; set; }
    public int TotalChunks { get; set; }
    public int CurrentPageIndexing { get; set; }
}

public class DocumentIngestionStatusDto
{
    public string Status { get; set; } = "Processing";
    public int ProgressPercentage { get; set; }
    public string CurrentOperation { get; set; } = string.Empty;
    public int TotalPages { get; set; }
    public int TotalChunks { get; set; }
    public int CurrentPageIndexing { get; set; }
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
    Task<DocumentDto> UploadDocumentAsync(
        IFormFile file,
        DocumentUploadDto metadata,
        CancellationToken cancellationToken = default);

    /// <summary>Uploads a new PDF for an existing document when content hash changes; clears vectors and sets status to Pending.</summary>
    Task<DocumentDto> UpdateDocumentFileAsync(
        Guid id,
        IFormFile file,
        DocumentUploadDto metadata,
        CancellationToken cancellationToken = default);
    /// <param name="isNewFile">True when uploading a new PDF (minor version bump). False to re-embed the same file (patch bump).</param>
    Task<DocumentDto> UpdateDocumentVersionAsync(
        Guid id,
        IFormFile? file,
        bool isNewFile,
        CancellationToken cancellationToken = default);
    Task<IEnumerable<DocumentDto>> GetAllDocumentsAsync();
    Task<DocumentDto?> GetDocumentByIdAsync(Guid id);
    Task<bool> DeleteDocumentAsync(Guid id);
    Task UpdateIndexingStatusAsync(Guid id, string status);
    Task<DocumentIngestionStatusDto?> GetIngestionStatusAsync(Guid id);
    Task<IReadOnlyList<DocumentChunkCitationFrequencyDto>> GetChunkCitationFrequencyAsync(
        Guid? documentId = null,
        int top = 100,
        CancellationToken cancellationToken = default);
    string MapStatusForApi(string? rawStatus);
}
