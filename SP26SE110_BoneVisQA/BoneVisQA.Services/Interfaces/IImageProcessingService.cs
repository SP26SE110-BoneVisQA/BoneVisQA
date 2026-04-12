namespace BoneVisQA.Services.Interfaces;

public interface IImageProcessingService
{
    /// <summary>
    /// Downloads the image, draws a green rectangle from normalized bounding-box JSON <c>{"x","y","width","height"}</c>, returns Base64 JPEG or null.
    /// </summary>
    Task<string?> DrawAnnotationOverlayAsBase64JpegAsync(
        string? imageUrl,
        string? coordinatesJson,
        CancellationToken cancellationToken = default);
}
