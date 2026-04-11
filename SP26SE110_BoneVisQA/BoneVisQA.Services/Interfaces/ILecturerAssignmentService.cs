using BoneVisQA.Services.Models.Lecturer;

namespace BoneVisQA.Services.Interfaces;

public interface ILecturerAssignmentService
{
    Task<IReadOnlyList<ClassCaseAssignmentDto>> AssignCasesAsync(Guid lecturerId, Guid classId, AssignCasesRequestDto request);
    Task<ClassQuizSessionDto> AssignQuizSessionAsync(Guid lecturerId, Guid classId, AssignQuizSessionRequestDto request);

    // Quiz review methods
    Task<IReadOnlyList<StudentQuizAttemptDto>> GetClassQuizAttemptsAsync(Guid lecturerId, Guid classId, Guid quizId);
    Task<QuizAttemptDetailDto> GetQuizAttemptDetailAsync(Guid lecturerId, Guid classId, Guid quizId, Guid attemptId);
    Task<QuizAttemptDetailDto> UpdateQuizAttemptAsync(Guid lecturerId, Guid classId, Guid quizId, Guid attemptId, UpdateQuizAttemptRequestDto request);

    // Retake methods
    Task AllowRetakeForAttemptAsync(Guid lecturerId, Guid attemptId);
    Task AllowRetakeAllAsync(Guid lecturerId, Guid classId, Guid quizId);

    // Assignment CRUD methods
    /// <summary>Lấy chi tiết một assignment theo ID.</summary>
    Task<AssignmentDetailDto> GetAssignmentByIdAsync(Guid assignmentId);

    /// <summary>Cập nhật thông tin assignment.</summary>
    Task<AssignmentDetailDto> UpdateAssignmentAsync(Guid assignmentId, UpdateAssignmentRequestDto request);

    /// <summary>Xóa một assignment.</summary>
    Task DeleteAssignmentAsync(Guid assignmentId);

    /// <summary>Lấy danh sách submissions của một assignment.</summary>
    Task<IReadOnlyList<AssignmentSubmissionDto>> GetAssignmentSubmissionsAsync(Guid assignmentId);

    /// <summary>Cập nhật điểm cho nhiều submissions.</summary>
    Task<IReadOnlyList<AssignmentSubmissionDto>> UpdateAssignmentSubmissionsAsync(Guid assignmentId, UpdateSubmissionsRequestDto request);
}
