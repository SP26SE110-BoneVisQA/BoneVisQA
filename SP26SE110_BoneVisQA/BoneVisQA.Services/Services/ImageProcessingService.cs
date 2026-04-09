using System.Text.Json;
using BoneVisQA.Services.Helpers;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.VisualQA;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BoneVisQA.Services.Services;

public class ImageProcessingService : IImageProcessingService
{
    private const float PolygonStrokeWidth = 4f;
    private static readonly Color PolygonOutlineColor = Color.FromRgb(0, 255, 65);

    private readonly HttpClient _httpClient;

    public ImageProcessingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string?> DrawAnnotationOverlayAsBase64JpegAsync(
        string? imageUrl,
        string? coordinatesJson,
        IReadOnlyList<PointDto>? customPolygon,
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

            var polygonPoints = NormalizePolygonForDrawing(customPolygon, coordinatesJson, iw, ih);

            if (polygonPoints != null && polygonPoints.Length >= 3)
            {
                image.Mutate(ctx => DrawClosedPolygon(ctx, polygonPoints));
            }
            else
            {
                var box = ParseLegacyBox(coordinatesJson);
                if (box.HasValue)
                {
                    var (x, y, w, h) = ToPixelBox(box.Value, iw, ih);
                    x = Math.Clamp(x, 0, Math.Max(0, iw - 1));
                    y = Math.Clamp(y, 0, Math.Max(0, ih - 1));
                    w = Math.Clamp(w, 1, Math.Max(1, iw - x));
                    h = Math.Clamp(h, 1, Math.Max(1, ih - y));

                    image.Mutate(ctx =>
                    {
                        ctx.Draw(PolygonOutlineColor, PolygonStrokeWidth, new Rectangle(x, y, w, h));
                    });
                }
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

    private static PointF[]? NormalizePolygonForDrawing(
        IReadOnlyList<PointDto>? customPolygon,
        string? coordinatesJson,
        int imageWidth,
        int imageHeight)
    {
        List<PointDto>? list = null;
        if (customPolygon is { Count: >= 3 })
            list = customPolygon.ToList();
        else
            list = PolygonAnnotationParser.TryParsePolygonFromJson(coordinatesJson);

        if (list == null || list.Count < 3)
            return null;

        var mode = InferCoordinateMode(list);
        var pts = new PointF[list.Count];
        for (var i = 0; i < list.Count; i++)
        {
            var (px, py) = ToPixelPoint(list[i].X, list[i].Y, imageWidth, imageHeight, mode);
            px = Math.Clamp(px, 0f, imageWidth - 1f);
            py = Math.Clamp(py, 0f, imageHeight - 1f);
            pts[i] = new PointF(px, py);
        }

        return pts;
    }

    private static void DrawClosedPolygon(IImageProcessingContext ctx, PointF[] points)
    {
        var segment = new LinearLineSegment(points);
        var polygon = new Polygon(segment);
        ctx.Draw(PolygonOutlineColor, PolygonStrokeWidth, polygon);
    }

    private enum CoordinateMode
    {
        Normalized01,
        Percent0To100,
        Pixels
    }

    private static CoordinateMode InferCoordinateMode(IReadOnlyList<PointDto> points)
    {
        var max = 0d;
        foreach (var p in points)
        {
            max = Math.Max(max, Math.Abs(p.X));
            max = Math.Max(max, Math.Abs(p.Y));
        }

        if (max <= 1.0001d)
            return CoordinateMode.Normalized01;
        if (max <= 100.0001d)
            return CoordinateMode.Percent0To100;
        return CoordinateMode.Pixels;
    }

    private static float ToPixelPoint(double v, int imageSize, CoordinateMode mode)
    {
        return mode switch
        {
            CoordinateMode.Normalized01 => (float)(v * imageSize),
            CoordinateMode.Percent0To100 => (float)(v / 100d * imageSize),
            _ => (float)v
        };
    }

    private static (float X, float Y) ToPixelPoint(double x, double y, int imageWidth, int imageHeight, CoordinateMode mode)
    {
        return (ToPixelPoint(x, imageWidth, mode), ToPixelPoint(y, imageHeight, mode));
    }

    private static (double X, double Y, double W, double H)? ParseLegacyBox(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            if (r.ValueKind == JsonValueKind.Array)
                return null;
            if (!r.TryGetProperty("x", out var x) || !r.TryGetProperty("y", out var y) ||
                !r.TryGetProperty("w", out var w) || !r.TryGetProperty("h", out var h))
                return null;

            return (x.GetDouble(), y.GetDouble(), w.GetDouble(), h.GetDouble());
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
