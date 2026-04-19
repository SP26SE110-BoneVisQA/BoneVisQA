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
    /// <summary>Get assignment details by ID.</summary>
    Task<AssignmentDetailDto> GetAssignmentByIdAsync(Guid assignmentId);

    /// <summary>Update assignment information.</summary>
    Task<AssignmentDetailDto> UpdateAssignmentAsync(Guid assignmentId, UpdateAssignmentRequestDto request);

    /// <summary>Delete an assignment.</summary>
    Task DeleteAssignmentAsync(Guid assignmentId);

    /// <summary>Get submission list for an assignment.</summary>
    Task<IReadOnlyList<AssignmentSubmissionDto>> GetAssignmentSubmissionsAsync(Guid assignmentId);

    /// <summary>Update scores for multiple submissions.</summary>
    Task<IReadOnlyList<AssignmentSubmissionDto>> UpdateAssignmentSubmissionsAsync(Guid assignmentId, UpdateSubmissionsRequestDto request);
}
