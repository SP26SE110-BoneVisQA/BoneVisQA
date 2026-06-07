namespace BoneVisQA.Services.Interfaces;

/// <summary>Python chunk enrichment mode (maps to <c>enrich_phase</c> on AI service).</summary>
public enum DocumentEnrichPhase
{
    Metadata = 1,
    Embeddings = 2,
    All = 3
}
