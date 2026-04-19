namespace BoneVisQA.Domain.Settings;

public sealed class HuggingFaceSettings
{
    public const string SectionName = "HuggingFace";

    public string ApiKey { get; set; } = string.Empty;
    public string EmbeddingUrl { get; set; } = string.Empty;
    public int BatchSize { get; set; } = 50;
}
