using System;

namespace BoneVisQA.Services.Models.Lecturer;

/// <summary>
/// Body tạo quiz cho giảng viên. Không cần gửi Id — server tự tạo.
/// Gửi <see cref="ClassId"/> để gán quiz vào lớp (tạo bản ghi class_quiz_sessions).
/// </summary>
public class CreateQuizRequestDto
{
    public string Title { get; set; } = string.Empty;

    /// <summary>Chủ đề quiz (ví dụ: Long Bone Fractures, Spine Lesions...)</summary>
    public string? Topic { get; set; }

    /// <summary>Quiz được tạo bởi AI hay tự tạo thủ công</summary>
    public bool IsAiGenerated { get; set; }

    /// <summary>Độ khó: Easy, Medium, Hard</summary>
    public string? Difficulty { get; set; }

    /// <summary>Phân loại: Resident Year 1, Resident Year 2, Advanced Diagnostics, Continuing Med Ed</summary>
    public string? Classification { get; set; }

    public DateTime? OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public int? TimeLimit { get; set; }
    public int? PassingScore { get; set; }

    /// <summary>Lớp cần gán quiz (optional). Để trống / Guid.Empty nếu chỉ tạo quiz chưa gán lớp.</summary>
    public Guid ClassId { get; set; }
}

