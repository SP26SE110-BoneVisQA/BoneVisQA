using System.Text.Json;

namespace BoneVisQA.Services.Helpers;

/// <summary>
/// Parses and serializes normalized ROI rectangles for <c>jsonb</c> coordinates:
/// <c>{"x":0.1,"y":0.2,"width":0.3,"height":0.4}</c> (0–1). Also accepts legacy <c>w</c>/<c>h</c> property names.
/// </summary>
public static class BoundingBoxParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public readonly record struct NormalizedBoundingBox(double X, double Y, double Width, double Height)
    {
        /// <summary>Gemini-style spatial box: [ymin, xmin, ymax, xmax] on 0–1000 scale.</summary>
        public (int Ymin, int Xmin, int Ymax, int Xmax) ToGeminiSpatialBox1000()
        {
            var xmin = Math.Clamp(X, 0d, 1d);
            var ymin = Math.Clamp(Y, 0d, 1d);
            var xmax = Math.Clamp(X + Width, 0d, 1d);
            var ymax = Math.Clamp(Y + Height, 0d, 1d);

            static int Scale(double n) => (int)Math.Round(n * 1000d, MidpointRounding.AwayFromZero);
            return (Scale(ymin), Scale(xmin), Scale(ymax), Scale(xmax));
        }
    }

    private sealed class RawBoxDto
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double? Width { get; set; }
        public double? W { get; set; }
        public double? Height { get; set; }
        public double? H { get; set; }
    }

    public static string Serialize(NormalizedBoundingBox box)
    {
        var canonical = new { x = box.X, y = box.Y, width = box.Width, height = box.Height };
        return JsonSerializer.Serialize(canonical, JsonOptions);
    }

    /// <summary>Returns null if JSON is not a valid rectangle with positive width/height.</summary>
    public static NormalizedBoundingBox? TryParseFromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var model = JsonSerializer.Deserialize<RawBoxDto>(json, JsonOptions);
            if (model == null)
                return null;

            var w = model.Width ?? model.W;
            var h = model.Height ?? model.H;
            if (w is null or <= 0 || h is null or <= 0)
                return null;

            var x = model.X;
            var y = model.Y;
            if (double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(w.Value) || double.IsNaN(h.Value))
                return null;

            x = Math.Clamp(x, 0d, 1d);
            y = Math.Clamp(y, 0d, 1d);
            var width = Math.Clamp(w.Value, 0d, 1d);
            var height = Math.Clamp(h.Value, 0d, 1d);

            if (x + width > 1d + 1e-9)
                width = Math.Max(0, 1d - x);
            if (y + height > 1d + 1e-9)
                height = Math.Max(0, 1d - y);

            if (width <= 0 || height <= 0)
                return null;

            return new NormalizedBoundingBox(x, y, width, height);
        }
        catch
        {
            return null;
        }
    }
}
