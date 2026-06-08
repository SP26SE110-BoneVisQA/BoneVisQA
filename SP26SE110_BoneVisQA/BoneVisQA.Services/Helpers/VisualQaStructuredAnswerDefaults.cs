using BoneVisQA.Services.Models.VisualQA;

namespace BoneVisQA.Services.Helpers;

/// <summary>Fixed placeholder copy when the AI returns empty structured fields.</summary>
public static class VisualQaStructuredAnswerDefaults
{
    public const string Diagnosis =
        "Insufficient image or clinical context to suggest a main diagnosis. Please add ROI, modality details, or a more specific question.";

    public const string Differential =
        "Not enough information to list reliable differential diagnoses for this image.";

    public const string Finding =
        "No definitive imaging signs were identified from the available context.";

    public const string ReflectiveQuestion =
        "What additional clinical history or projection would help narrow the differential diagnosis?";

    public const string CitationLabel =
        "No knowledge-base citation was retrieved for this answer. Reasoning is based on general musculoskeletal principles.";

    public static void ApplyToApiResponse(VisualQaApiResponseDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Diagnosis))
            dto.Diagnosis = Diagnosis;

        if (dto.DifferentialDiagnoses == null || dto.DifferentialDiagnoses.Count == 0)
            dto.DifferentialDiagnoses = new List<string> { Differential };

        if (dto.Findings == null || dto.Findings.Count == 0)
            dto.Findings = new List<string> { Finding };

        if (dto.ReflectiveQuestions == null || dto.ReflectiveQuestions.Count == 0)
            dto.ReflectiveQuestions = new List<string> { ReflectiveQuestion };

        if (dto.Citations == null || dto.Citations.Count == 0)
        {
            dto.Citations = new List<CitationItemDto>
            {
                new()
                {
                    DisplayLabel = CitationLabel,
                    Snippet = CitationLabel,
                    Kind = "info"
                }
            };
        }

        if (dto.LatestTurn != null)
            ApplyToTurn(dto.LatestTurn);
    }

    public static void ApplyToTurn(VisualQaTurnDto turn)
    {
        if (string.IsNullOrWhiteSpace(turn.Diagnosis))
            turn.Diagnosis = Diagnosis;

        if (turn.DifferentialDiagnoses == null || turn.DifferentialDiagnoses.Count == 0)
            turn.DifferentialDiagnoses = new List<string> { Differential };

        if (turn.Findings == null || turn.Findings.Count == 0)
            turn.Findings = new List<string> { Finding };

        if (turn.ReflectiveQuestions == null || turn.ReflectiveQuestions.Count == 0)
            turn.ReflectiveQuestions = new List<string> { ReflectiveQuestion };

        if (turn.Citations == null || turn.Citations.Count == 0)
        {
            turn.Citations = new List<CitationItemDto>
            {
                new()
                {
                    DisplayLabel = CitationLabel,
                    Snippet = CitationLabel,
                    Kind = "info"
                }
            };
        }
    }
}
