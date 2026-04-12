using BoneVisQA.Services.Helpers;
using BoneVisQA.Services.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BoneVisQA.Services.Services;

public class ImageProcessingService : IImageProcessingService
{
    private const float BoxStrokeWidth = 4f;
    private static readonly Color RoiOutlineColor = Color.FromRgb(0, 255, 65);

    private readonly HttpClient _httpClient;

    public ImageProcessingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string?> DrawAnnotationOverlayAsBase64JpegAsync(
        string? imageUrl,
        string? coordinatesJson,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        byte[] imageBytes;
        try
        {
            imageBytes = await _httpClient.GetByteArrayAsync(imageUrl, cancellationToken);
        }
        catch
        {
            return null;
        }

        if (imageBytes.Length == 0)
            return null;

        try
        {
            using var image = Image.Load<Rgba32>(imageBytes);
            var iw = image.Width;
            var ih = image.Height;

            var box = BoundingBoxParser.TryParseFromJson(coordinatesJson);
            if (box.HasValue)
            {
                var b = box.Value;
                var x = (int)Math.Round(b.X * iw);
                var y = (int)Math.Round(b.Y * ih);
                var w = (int)Math.Round(b.Width * iw);
                var h = (int)Math.Round(b.Height * ih);

                x = Math.Clamp(x, 0, Math.Max(0, iw - 1));
                y = Math.Clamp(y, 0, Math.Max(0, ih - 1));
                w = Math.Clamp(w, 1, Math.Max(1, iw - x));
                h = Math.Clamp(h, 1, Math.Max(1, ih - y));

                image.Mutate(ctx =>
                {
                    ctx.Draw(RoiOutlineColor, BoxStrokeWidth, new Rectangle(x, y, w, h));
                });
            }

            await using var outStream = new MemoryStream();
            await image.SaveAsJpegAsync(outStream, cancellationToken);
            return Convert.ToBase64String(outStream.ToArray());
        }
        catch
        {
            return null;
        }
    }
}
