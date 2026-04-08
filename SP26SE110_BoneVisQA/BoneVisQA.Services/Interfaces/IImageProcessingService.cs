using BoneVisQA.Services.Models.VisualQA;

namespace BoneVisQA.Services.Interfaces;

public interface IImageProcessingService
{
    /// <summary>
    /// Downloads the image, draws a polygon outline (preferred) or legacy bounding box from JSON, returns Base64 JPEG or null.
    /// </summary>
    Task<string?> DrawAnnotationOverlayAsBase64JpegAsync(
        string? imageUrl,
        string? coordinatesJson,
        IReadOnlyList<PointDto>? customPolygon,
        CancellationToken cancellationToken = default);
}
