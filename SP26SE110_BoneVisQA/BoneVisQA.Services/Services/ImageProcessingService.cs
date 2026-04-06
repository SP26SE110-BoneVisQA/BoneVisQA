using System.Text.Json;
using BoneVisQA.Services.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BoneVisQA.Services.Services;

public class ImageProcessingService : IImageProcessingService
{
    private readonly HttpClient _httpClient;

    public ImageProcessingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string?> DrawBoundingBoxAsBase64JpegAsync(string? imageUrl, string? coordinatesJson, CancellationToken cancellationToken = default)
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

            var box = ParseBox(coordinatesJson);
            if (box.HasValue)
            {
                var iw = image.Width;
                var ih = image.Height;
                var (x, y, w, h) = ToPixelBox(box.Value, iw, ih);
                x = Math.Clamp(x, 0, Math.Max(0, iw - 1));
                y = Math.Clamp(y, 0, Math.Max(0, ih - 1));
                w = Math.Clamp(w, 1, Math.Max(1, iw - x));
                h = Math.Clamp(h, 1, Math.Max(1, ih - y));

                image.Mutate(ctx =>
                {
                    ctx.Draw(Color.Red, 3f, new Rectangle(x, y, w, h));
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

    private static (double X, double Y, double W, double H)? ParseBox(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (!r.TryGetProperty("x", out var x) || !r.TryGetProperty("y", out var y) ||
                !r.TryGetProperty("w", out var w) || !r.TryGetProperty("h", out var h))
                return null;

            var xd = x.GetDouble();
            var yd = y.GetDouble();
            var wd = w.GetDouble();
            var hd = h.GetDouble();
            return (xd, yd, wd, hd);
        }
        catch
        {
            return null;
        }
    }

    private static (int X, int Y, int W, int H) ToPixelBox((double X, double Y, double W, double H) box, int imageWidth, int imageHeight)
    {
        var (x, y, w, h) = box;
        var looksNormalized = x <= 1d && y <= 1d && w <= 1d && h <= 1d;
        var looksPercent = !looksNormalized && x <= 100d && y <= 100d && w <= 100d && h <= 100d;

        if (looksNormalized)
        {
            x *= imageWidth;
            y *= imageHeight;
            w *= imageWidth;
            h *= imageHeight;
        }
        else if (looksPercent)
        {
            x = x / 100d * imageWidth;
            y = y / 100d * imageHeight;
            w = w / 100d * imageWidth;
            h = h / 100d * imageHeight;
        }

        return ((int)Math.Round(x), (int)Math.Round(y), (int)Math.Round(w), (int)Math.Round(h));
    }
}
