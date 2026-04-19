using System.Globalization;

namespace BoneVisQA.Services.Helpers;

/// <summary>Semantic version helpers for document library versioning (Major.Minor.Patch).</summary>
public static class SemanticDocumentVersion
{
    public const string Initial = "1.0.0";

    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Initial;
        var parts = raw.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return Initial;
        var major = ParsePart(parts, 0);
        var minor = parts.Length > 1 ? ParsePart(parts, 1) : 0;
        var patch = parts.Length > 2 ? ParsePart(parts, 2) : 0;
        return $"{major}.{minor}.{patch}";
    }

    public static string BumpMinor(string? current)
    {
        var (major, minor, _) = ParseTriple(current);
        return $"{major}.{minor + 1}.0";
    }

    public static string BumpPatch(string? current)
    {
        var (major, minor, patch) = ParseTriple(current);
        return $"{major}.{minor}.{patch + 1}";
    }

    /// <summary>Maps legacy integer versions from DB migrations (e.g. 1 → 1.0.0, 3 → 2.0.0 as minor steps).</summary>
    public static string FromLegacyInt(int legacy)
    {
        if (legacy <= 1)
            return Initial;
        return $"1.{legacy - 1}.0";
    }

    public static string SanitizeForStoragePath(string version) =>
        Normalize(version).Replace(".", "_", StringComparison.Ordinal);

    private static int ParsePart(string[] parts, int index) =>
        index < parts.Length && int.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= 0
            ? n
            : 0;

    private static (int Major, int Minor, int Patch) ParseTriple(string? raw)
    {
        var n = Normalize(raw);
        var parts = n.Split('.');
        var major = ParsePart(parts, 0);
        var minor = parts.Length > 1 ? ParsePart(parts, 1) : 0;
        var patch = parts.Length > 2 ? ParsePart(parts, 2) : 0;
        return (major, minor, patch);
    }
}
