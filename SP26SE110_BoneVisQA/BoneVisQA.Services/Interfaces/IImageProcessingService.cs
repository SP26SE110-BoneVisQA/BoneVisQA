namespace BoneVisQA.Services.Interfaces;

public interface IImageProcessingService
{
    /// <summary>
    /// Downloads image, draws red bounding box from JSON {"x","y","w","h"}, returns Base64 JPEG or null.
    /// </summary>
    Task<string?> DrawBoundingBoxAsBase64JpegAsync(string? imageUrl, string? coordinatesJson, CancellationToken cancellationToken = default);
}
