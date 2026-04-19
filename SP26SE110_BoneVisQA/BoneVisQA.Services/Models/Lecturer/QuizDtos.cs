using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BoneVisQA.Services.Models.Lecturer;

public class QuizDto
{
    public Guid Id { get; set; }

    public Guid ClassId { get; set; }

    public string Title { get; set; } = null!;

    public string? Topic { get; set; }

    public bool IsAiGenerated { get; set; }

    public bool IsVerifiedCurriculum { get; set; }

    public string? Difficulty { get; set; }

    public string? Classification { get; set; }

    public DateTime? OpenTime { get; set; }

    public DateTime? CloseTime { get; set; }

    public int? TimeLimit { get; set; }

    public int? PassingScore { get; set; }

    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// True nếu quiz này từ Expert Library (CreatedByExpertId != null và != lecturer hiện tại).
    /// Lecturer có thể xem và gán vào lớp, nhưng KHÔNG được sửa/xóa câu hỏi.
    /// </summary>
    public bool IsFromExpertLibrary { get; set; }
}

// Update Quiz Request Dto — dùng JsonPropertyName để nhận cả PascalCase (BE) lẫn camelCase (FE)
public class UpdateQuizRequestDto
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("openTime")]
    public DateTime? OpenTime { get; set; }

    [JsonPropertyName("closeTime")]
    public DateTime? CloseTime { get; set; }

    [JsonPropertyName("timeLimit")]
    public int? TimeLimit { get; set; }

    [JsonPropertyName("passingScore")]
    public int? PassingScore { get; set; }
}

public class UpdateQuizQuestionRequestDto
{
    public string QuestionText { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? CorrectAnswer { get; set; }
    public List<string>? Options { get; set; }
}

// ========== COPIED FROM Expert/QuizDTO ==========

// ClassQuizDto - For quiz-class assignment
public class ClassQuizDto
{
    public Guid ClassId { get; set; }
    public Guid QuizId { get; set; }
    public string? QuizName { get; set; }
    public string? ClassName { get; set; }
    /// <summary>Chủ đề quiz (Quiz.Topic), khác với tên lớp.</summary>
    public string? Topic { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public int QuestionCount { get; set; }
    /// <summary>
    /// True nếu quiz này đã được assign cho lớp trước đó (trả về khi gọi AssignQuizToClassAsync với quiz đã assign).
    /// Dùng để thông báo cho frontend biết là đã assign rồi.
    /// </summary>
    public bool IsAlreadyAssigned { get; set; }
    /// <summary>Tên người tạo quiz: "You" nếu do lecturer tạo, tên Expert nếu copy từ Expert Library.</summary>
    public string? CreatorName { get; set; }
    /// <summary>Loại người tạo: "Lecturer" hoặc "Expert"</summary>
    public string? CreatorType { get; set; }
}

// CreateQuizQuestionDto - For creating questions (with individual options)
public class CreateQuizQuestionDto
{
    [JsonPropertyName("quizId")]
    public Guid QuizId { get; set; }

    [JsonPropertyName("caseId")]
    public Guid? CaseId { get; set; }

    [JsonPropertyName("questionText")]
    public string QuestionText { get; set; } = null!;

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("optionA")]
    public string? OptionA { get; set; }

    [JsonPropertyName("optionB")]
    public string? OptionB { get; set; }

    [JsonPropertyName("optionC")]
    public string? OptionC { get; set; }

    [JsonPropertyName("optionD")]
    public string? OptionD { get; set; }

    [JsonPropertyName("correctAnswer")]
    public string? CorrectAnswer { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("referenceAnswer")]
    public string? ReferenceAnswer { get; set; }

    [JsonPropertyName("maxScore")]
    public int MaxScore { get; set; } = 10;
}

// UpdateQuizsQuestionRequestDto - For updating questions (expert style)
public class UpdateQuizsQuestionRequestDto
{
    [JsonPropertyName("questionText")]
    public string QuestionText { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("correctAnswer")]
    public string? CorrectAnswer { get; set; }

    [JsonPropertyName("optionA")]
    public string? OptionA { get; set; }

    [JsonPropertyName("optionB")]
    public string? OptionB { get; set; }

    [JsonPropertyName("optionC")]
    public string? OptionC { get; set; }

    [JsonPropertyName("optionD")]
    public string? OptionD { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("referenceAnswer")]
    public string? ReferenceAnswer { get; set; }

    [JsonPropertyName("maxScore")]
    public int MaxScore { get; set; } = 10;
}

// UpdateQuizsQuestionResponseDto - Response for updating questions
public class UpdateQuizsQuestionResponseDto
{
    public string? QuizTitle { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? CorrectAnswer { get; set; }
    public string? OptionA { get; set; }
    public string? OptionB { get; set; }
    public string? OptionC { get; set; }
    public string? OptionD { get; set; }
    public string? ImageUrl { get; set; }
}

public class AssignedQuizDto
{
    public Guid AssignmentId { get; set; } // ClassQuizSession.Id
    public Guid ClassId { get; set; }
    public Guid QuizId { get; set; }
    public string? QuizName { get; set; }
    public string? ClassName { get; set; }
    public string? Topic { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public int QuestionCount { get; set; }
    public bool IsFromExpertLibrary { get; set; }
    public string? CreatorName { get; set; }
    public string? CreatorType { get; set; }
}

// QuizQuestionDto - For quiz questions with individual options
public class QuizQuestionDto
{
    public Guid Id { get; set; }
    public Guid QuizId { get; set; }
    public string? QuizTitle { get; set; }
    public Guid? CaseId { get; set; }
    public string? CaseTitle { get; set; }
    public string QuestionText { get; set; } = null!;
    public string? Type { get; set; }
    public string? OptionA { get; set; }
    public string? OptionB { get; set; }
    public string? OptionC { get; set; }
    public string? OptionD { get; set; }
    public string? CorrectAnswer { get; set; }
    public string? ImageUrl { get; set; }
    public string? ReferenceAnswer { get; set; }
    public int MaxScore { get; set; } = 10;
}

// QuizScoreResultDto - Used for quiz score calculation
public class QuizScoreResultDto
{
    public Guid AttemptId { get; set; }
    public Guid StudentId { get; set; }
    public Guid QuizId { get; set; }
    public string QuizTitle { get; set; } = null!;
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public float Score { get; set; }
    public int? PassingScore { get; set; }
    public bool IsPassed { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>
    /// Số câu essay chưa được giảng viên chấm.
    /// Nếu > 0, điểm hiện tại chưa phải điểm cuối cùng và có thể thay đổi.
    /// </summary>
    public int UngradedEssayCount { get; set; }
}

// StudentSubmitQuestionDto - Used for student quiz submission
public class StudentSubmitQuestionDto
{
    public Guid StudentId { get; set; }
    public Guid AttemptId { get; set; }
    public Guid QuestionId { get; set; }
    public string? StudentAnswer { get; set; }
}

// StudentSubmitQuestionResponseDto - Response for student quiz submission
public class StudentSubmitQuestionResponseDto
{
    public string? QuizTitle { get; set; }
    public string? QuestionText { get; set; }
    public string? OptionA { get; set; }
    public string? OptionB { get; set; }
    public string? OptionC { get; set; }
    public string? OptionD { get; set; }
    public string? StudentAnswer { get; set; }
    public string? StudentAnswerText { get; set; }
    public string? CorrectAnswer { get; set; }
    public string? CorrectAnswerText { get; set; }
    public bool? IsCorrect { get; set; }
}

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

    /// <summary>Quiz được align với verified curriculum/standard radiology board learning objectives</summary>
    public bool IsVerifiedCurriculum { get; set; } = false;

    public DateTime? OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public int? TimeLimit { get; set; }
    public int? PassingScore { get; set; }

    /// <summary>Lớp cần gán quiz (optional). Để trống / Guid.Empty nếu chỉ tạo quiz chưa gán lớp.</summary>
    public Guid ClassId { get; set; }
}

/// <summary>
/// DTO returning quiz details with questions — used when lecturer selects quiz to assign as assignment.
/// </summary>
public class QuizWithQuestionsDto
{
    public QuizDto Quiz { get; set; } = null!;
    public List<QuizQuestionDto> Questions { get; set; } = new();
    public int TotalQuestions => Questions.Count;
}

/// <summary>
/// Request DTO for copying an Expert Quiz
/// </summary>
public class CopyExpertQuizRequestDto
{
    /// <summary>Tiêu đề mới cho quiz copy (optional - nếu null sẽ dùng tiêu đề gốc)</summary>
    public string? Title { get; set; }
}

/// <summary>
/// Response DTO for copied Expert Quiz
/// </summary>
public class CopiedExpertQuizDto
{
    public Guid NewQuizId { get; set; }
    public string NewQuizTitle { get; set; } = null!;
    public Guid OriginalQuizId { get; set; }
    public string OriginalQuizTitle { get; set; } = null!;
    public int QuestionCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO cho My Quizzes tab - hiển thị quiz kèm danh sách lớp đã gán
/// </summary>
public class MyQuizWithClassesDto
{
    public Guid QuizId { get; set; }
    public string QuizName { get; set; } = null!;
    public string? Topic { get; set; }
    public DateTime? OpenTime { get; set; }
    public DateTime? CloseTime { get; set; }
    public int? TimeLimit { get; set; }
    public int? PassingScore { get; set; }
    public DateTime? CreatedAt { get; set; }
    public int QuestionCount { get; set; }
    public bool IsAiGenerated { get; set; }
    public bool IsFromExpertLibrary { get; set; }
    public string? Difficulty { get; set; }
    /// <summary>Danh sách lớp đã gán quiz này</summary>
    public List<MyQuizClassInfoDto> Classes { get; set; } = new();
}

/// <summary>
/// Thông tin lớp đã gán cho quiz (dùng trong MyQuizWithClassesDto)
/// </summary>
public class MyQuizClassInfoDto
{
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = null!;
    public DateTime? AssignedAt { get; set; }
    public Guid AssignmentId { get; set; }
}
