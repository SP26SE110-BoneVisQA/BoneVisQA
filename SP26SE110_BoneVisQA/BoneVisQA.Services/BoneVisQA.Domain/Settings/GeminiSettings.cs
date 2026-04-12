using System.Collections.Generic;

namespace BoneVisQA.Domain.Settings;

public class GeminiSettings
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Prioritized model ids bound from <c>Gemini:Models</c>. The API client should try each id in order and advance on 429 / transient failures.
    /// </summary>
    /// <remarks>
    /// Example 2026 technical ids (high → low RPD): <c>gemini-2.5-flash-lite</c>, <c>gemini-3.1-flash-lite-preview</c>,
    /// <c>gemini-2.5-flash</c>, <c>gemini-3-flash-preview</c>, <c>gemini-2.5-pro</c>. Keep <c>appsettings.json</c> in sync; the client rotates on 404/429/transient errors.
    /// </remarks>
    public List<string>? Models { get; set; }

    /// <summary>Legacy single model; used when <see cref="Models"/> is null or empty (e.g. <c>Gemini:ModelId</c>).</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>Google Generative Language API base URL; set in <c>Gemini:BaseUrl</c>.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Ordered, non-empty model ids: <see cref="Models"/> first, else <see cref="ModelId"/>.</summary>
    public IReadOnlyList<string> GetResolvedModelIds()
    {
        var list = new List<string>();
        if (Models != null)
        {
            foreach (var m in Models)
            {
                if (!string.IsNullOrWhiteSpace(m))
                    list.Add(m.Trim());
            }
        }

        if (list.Count == 0 && !string.IsNullOrWhiteSpace(ModelId))
            list.Add(ModelId.Trim());

        return list;
    }
}

