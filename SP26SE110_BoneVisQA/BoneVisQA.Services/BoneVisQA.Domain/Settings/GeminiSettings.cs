namespace BoneVisQA.Domain.Settings;

public class GeminiSettings
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    public string ModelId { get; set; } = "gemini-1.5-flash";

    // Use stable v1 base URL for broader model availability.
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1";
}

