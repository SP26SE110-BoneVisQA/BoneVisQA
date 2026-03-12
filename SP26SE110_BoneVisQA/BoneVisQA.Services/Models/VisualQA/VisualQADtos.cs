using System;
using System.Collections.Generic;

namespace BoneVisQA.Services.Models.VisualQA;

public class VisualQARequestDto
{
    public Guid StudentId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? Coordinates { get; set; }
    public Guid? CaseId { get; set; }
    public Guid? AnnotationId { get; set; }
    public string? Language { get; set; }
}

public class CitationItemDto
{
    public Guid ChunkId { get; set; }
    public double SimilarityScore { get; set; }
    public string? SourceText { get; set; }
}

public class VisualQAResponseDto
{
    public string? AnswerText { get; set; }
    public string? SuggestedDiagnosis { get; set; }
    public string? DifferentialDiagnoses { get; set; }
    public List<CitationItemDto> Citations { get; set; } = new();
}
