namespace BoneVisQA.Services.Helpers;

/// <summary>Five-step document indexing pipeline for admin UI and progress APIs.</summary>
public static class DocumentIndexingPhases
{
    public const int DownloadPdf = 1;
    public const int ExtractPages = 2;
    public const int PersistChunks = 3;
    public const int EnrichMetadata = 4;
    public const int GenerateEmbeddings = 5;

    public const int MinPhase = DownloadPdf;
    public const int MaxPhase = GenerateEmbeddings;

    public static string Label(int phase) => phase switch
    {
        DownloadPdf => "Download PDF",
        ExtractPages => "Extract text",
        PersistChunks => "Chunk & persist",
        EnrichMetadata => "Enrich metadata",
        GenerateEmbeddings => "Generate embeddings",
        _ => "Processing"
    };

    /// <summary>Map phase-local fraction [0,1] to overall 0–100 progress.</summary>
    public static int OverallProgress(int phase, double phaseFraction)
    {
        phaseFraction = Math.Clamp(phaseFraction, 0d, 1d);
        var (start, weight) = phase switch
        {
            DownloadPdf => (0, 8),
            ExtractPages => (8, 32),
            PersistChunks => (40, 25),
            EnrichMetadata => (65, 15),
            GenerateEmbeddings => (80, 20),
            _ => (0, 0)
        };
        return Math.Clamp(start + (int)Math.Round(phaseFraction * weight), 0, 99);
    }
}
