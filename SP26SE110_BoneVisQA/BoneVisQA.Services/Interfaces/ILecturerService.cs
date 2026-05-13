using BoneVisQA.Services.Models;
using BoneVisQA.Services.Models.Lecturer;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces;

public interface ILecturerService
{
    Task<ClassDto> CreateClassAsync(Guid lecturerId, CreateClassRequestDto request);
    Task<ClassDto?> GetClassByIdAsync(Guid lecturerId, Guid classId);
    Task<IReadOnlyList<ClassDto>> GetClassesForLecturerAsync(Guid lecturerId);
    Task<ClassDto> UpdateClassAsync(Guid lecturerId, Guid classId, UpdateClassRequestDto request);
    Task<bool> DeleteClassAsync(Guid lecturerId, Guid classId);

    Task<bool> EnrollStudentAsync(Guid lecturerId, Guid classId, Guid studentId);
    Task<IReadOnlyList<StudentEnrollmentDto>> EnrollStudentsAsync(Guid lecturerId, Guid classId, EnrollStudentsRequestDto request);
    Task<bool> RemoveStudentAsync(Guid lecturerId, Guid classId, Guid studentId);
    Task<IReadOnlyList<StudentEnrollmentDto>> GetStudentsInClassAsync(Guid lecturerId, Guid classId);
    Task<IReadOnlyList<StudentEnrollmentDto>> GetAvailableStudentsAsync(Guid lecturerId, Guid classId);
    Task<AnnouncementDto> CreateAnnouncementAsync(Guid lecturerId, Guid classId, CreateAnnouncementRequestDto request);
    Task<AnnouncementDto> UpdateAnnouncementAsync(Guid classId, Guid announcementId, UpdateAnnouncementRequestDto request);
    Task<bool> DeleteAnnouncementAsync(Guid classId, Guid announcementId);


    //Task<QuizDto> CreateQuizAsync(Guid classId, CreateQuizRequestDto request);
    //Task<QuizQuestionDto> AddQuizQuestionAsync(CreateQuizQuestionRequestDto request);
    //Task<IReadOnlyList<QuizQuestionDto>> GetQuizQuestionsAsync(Guid quizId);
    //Task<bool> UpdateQuizQuestionAsync(Guid questionId, UpdateQuizQuestionRequestDto request)


    Task<bool> DeleteQuizQuestionAsync(Guid questionId);
    Task<bool> DeleteQuizAsync(Guid quizId, Guid lecturerId);
    Task RemoveQuizFromClassAsync(Guid classId, Guid quizId);
    Task<IReadOnlyList<CaseDto>> GetAllCasesAsync();
    Task<PagedResultDTO<CaseDto>> GetAllCasesPagedAsync(int pageIndex, int pageSize);
    Task<IReadOnlyList<CaseDto>> AssignCasesToClassAsync(Guid classId, AssignCasesToClassRequestDto request);
    Task<bool> ApproveCaseAsync(Guid caseId, ApproveCaseRequestDto request);
    Task<IReadOnlyList<LecturerTriageRowDto>> GetTriageListAsync(Guid lecturerId, Guid classId, string? source = null);
    Task<LectStudentQuestionDetailDto?> GetQuestionDetailAsync(Guid lecturerId, Guid classId, Guid questionId);
    Task<LecturerAnswerDto> RespondToQuestionAsync(Guid lecturerId, Guid classId, Guid questionId, RespondToQuestionRequestDto request);
    Task<IReadOnlyList<ClassStudentProgressDto>> GetClassStudentProgressAsync(Guid classId);
    Task<IReadOnlyList<LectStudentQuestionDto>> GetStudentQuestionsAsync(Guid lecturerId, Guid classId, Guid? caseId, Guid? studentId, string? source = null);
    Task<IReadOnlyList<AnnouncementDto>> GetClassAnnouncementsAsync(Guid classId);
    Task<List<ClassAssignmentDto>> GetClassAssignmentsAsync(Guid classId);
    Task<List<ClassAssignmentDto>> GetAllAssignmentsForLecturerAsync(Guid lecturerId);
    Task<ClassStatsDto> GetClassStatsAsync(Guid classId);

    /// <param name="creatingUserId">User id từ JWT (giảng viên) — ghi vào Quiz.CreatedByExpertId để tách quiz SV vs GV.</param>
    Task<QuizDto> CreateQuizAsync(CreateQuizRequestDto request, Guid? creatingUserId = null);
    Task<QuizQuestionDto> AddQuizQuestionAsync(Guid quizId, CreateQuizQuestionDto request);
    Task<List<QuizQuestionDto>> AddQuizQuestionsBatchAsync(Guid quizId, List<CreateQuizQuestionDto> requests);
    Task<UpdateQuizsQuestionResponseDto> UpdateQuizQuestionAsync(Guid questionId, UpdateQuizsQuestionRequestDto request);
    Task<List<QuizQuestionDto>> GetQuizQuestionsAsync(Guid quizId);
    Task<QuizQuestionDto?> GetQuizQuestionByIdAsync(Guid questionId);
    Task<QuizWithQuestionsDto> GetQuizWithQuestionsAsync(Guid quizId);

    Task<IReadOnlyList<ClassQuizDto>> GetQuizzesByLecturerAsync(Guid lecturerId);
    Task<IReadOnlyList<QuizDto>> GetUnassignedLecturerQuizzesAsync(Guid lecturerId);
    Task<IReadOnlyList<QuizDto>> GetAllLecturerQuizzesAsync(Guid lecturerId);
    Task<IReadOnlyList<MyQuizWithClassesDto>> GetMyQuizzesWithClassesAsync(Guid lecturerId);
    Task<IReadOnlyList<AssignedQuizDto>> GetAssignedQuizzesAsync(Guid lecturerId);
    Task<IReadOnlyList<QuizDto>> GetQuizzesForClassAsync(Guid classId);
    Task<QuizDto?> GetQuizByIdAsync(Guid quizId);
    Task<IReadOnlyList<QuizDto>> GetQuizzesByIdsAsync(IReadOnlyList<Guid> quizIds);
    Task<QuizDto> UpdateQuizAsync(Guid quizId, UpdateQuizRequestDto request);
    Task<ClassQuizDto> AssignQuizToClassAsync(Guid classId, Guid quizId);
    Task<ImportStudentsSummaryDto> ImportStudentsFromExcelAsync(Guid classId, Stream fileStream, string fileName);

    Task<IReadOnlyList<ExpertOptionDto>> GetExpertsAsync();
    Task<ClassDto> AssignExpertToClassAsync(Guid lecturerId, Guid classId, Guid? expertId);

    // Assignment CRUD methods (delegates to LecturerAssignmentService)
    Task<AssignmentDetailDto> GetAssignmentByIdAsync(Guid assignmentId);
    Task<AssignmentDetailDto> UpdateAssignmentAsync(Guid assignmentId, UpdateAssignmentRequestDto request);
    Task DeleteAssignmentAsync(Guid assignmentId);
    Task<IReadOnlyList<AssignmentSubmissionDto>> GetAssignmentSubmissionsAsync(Guid assignmentId);
    Task<IReadOnlyList<AssignmentSubmissionDto>> UpdateAssignmentSubmissionsAsync(Guid assignmentId, UpdateSubmissionsRequestDto request);

    // Expert medical case images
    Task<bool> DeleteMedicalImageAsync(Guid imageId);

    /// <summary>Move an announcement to a different class.</summary>
    Task<AnnouncementDto> MoveAnnouncementAsync(Guid lecturerId, Guid announcementId, Guid targetClassId);

    /// <summary>Export all quiz results for a lecturer into a single Excel file with multiple sheets.</summary>
    Task<(byte[] FileBytes, string FileName)> ExportAllQuizResultsAsync(Guid lecturerId);

    // Teaching Objectives
    Task<TeachingObjectivesDto?> GetTeachingObjectivesAsync(Guid lecturerId, Guid? classId = null);
    Task<TeachingObjectivesDto> UpdateTeachingObjectivesAsync(Guid lecturerId, Guid classId, UpdateTeachingObjectivesRequestDto request);
    Task<List<TeachingObjectiveSuggestionDto>> GetExpertSuggestionsAsync(Guid classId);
    Task<TeachingObjectiveSuggestionDto> ConfirmSuggestionAsync(Guid lecturerId, Guid suggestionId, ConfirmSuggestionRequestDto request);
}

