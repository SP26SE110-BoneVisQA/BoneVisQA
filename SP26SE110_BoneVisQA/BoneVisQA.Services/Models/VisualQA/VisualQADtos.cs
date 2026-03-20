using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace BoneVisQA.Services.Models.VisualQA;

public class VisualQARequestDto
{
    [DefaultValue("Vùng khoanh đỏ trên ảnh có dấu hiệu gãy xương không?")]
    public string QuestionText { get; set; } = string.Empty;

    [DefaultValue("https://example.com/sample-xray.jpg")]
    public string? ImageUrl { get; set; }

    [DefaultValue("{\"x\": 10, \"y\": 20, \"w\": 100, \"h\": 150}")]
    public string? Coordinates { get; set; }

    [DefaultValue("3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    public Guid? CaseId { get; set; }

    [DefaultValue("3fa85f64-5717-4562-b3fc-2c963f66afa6")]
    public Guid? AnnotationId { get; set; }
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
