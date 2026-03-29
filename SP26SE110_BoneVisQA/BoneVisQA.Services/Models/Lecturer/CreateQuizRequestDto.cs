using System;

namespace BoneVisQA.Services.Models.Lecturer;

/// <summary>
/// Body tạo quiz cho giảng viên. Không cần gửi Id — server tự tạo.
/// Gửi <see cref="ClassId"/> để gán quiz vào lớp (tạo bản ghi class_quizzes).
/// </summary>
public class CreateQuizRequestDto
{
    public string Title { get; set; } = string.Empty;
    public DateTime? OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public int? TimeLimit { get; set; }
    public int? PassingScore { get; set; }

    /// <summary>Lớp cần gán quiz (optional). Để trống / Guid.Empty nếu chỉ tạo quiz chưa gán lớp.</summary>
    public Guid ClassId { get; set; }
}

