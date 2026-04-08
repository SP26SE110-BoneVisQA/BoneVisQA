using System.Text.Json;
using BoneVisQA.Services.Models.VisualQA;

namespace BoneVisQA.Services.Helpers;

/// <summary>
/// Parses and serializes polygon / legacy coordinate JSON stored in <c>jsonb</c> columns.
/// </summary>
public static class PolygonAnnotationParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static string SerializePolygon(IReadOnlyList<PointDto> points)
    {
        return JsonSerializer.Serialize(points, JsonOptions);
    }

    /// <summary>
    /// Accepts: <c>[{"x":0.1,"y":0.2},...]</c>, <c>{"points":[...]}</c>, or <c>{"polygon":[...]}</c>.
    /// Returns null if fewer than 3 vertices or legacy box-only JSON.
    /// </summary>
    public static List<PointDto>? TryParsePolygonFromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;

            if (r.ValueKind == JsonValueKind.Array)
                return ParsePointArray(r);

            if (r.TryGetProperty("points", out var pts))
            {
                if (pts.ValueKind == JsonValueKind.Array)
                    return ParsePointArray(pts);
            }

            if (r.TryGetProperty("polygon", out var poly))
            {
                if (poly.ValueKind == JsonValueKind.Array)
                    return ParsePointArray(poly);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static List<PointDto>? ParsePointArray(JsonElement arr)
    {
        var list = new List<PointDto>();
        foreach (var el in arr.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object)
                continue;
            if (!el.TryGetProperty("x", out var xEl) || !el.TryGetProperty("y", out var yEl))
                continue;
            list.Add(new PointDto { X = xEl.GetDouble(), Y = yEl.GetDouble() });
        }

        return list.Count >= 3 ? list : null;
    }
}
