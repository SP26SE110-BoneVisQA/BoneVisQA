using BoneVisQA.Services.Models.Lecturer;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces;

public interface ILecturerService
{
    Task<ClassDto> CreateClassAsync(CreateClassRequestDto request);
    Task<IReadOnlyList<ClassDto>> GetClassesForLecturerAsync(Guid lecturerId);

    Task<bool> EnrollStudentAsync(Guid classId, Guid studentId);
    Task<IReadOnlyList<StudentEnrollmentDto>> EnrollStudentsAsync(Guid classId, EnrollStudentsRequestDto request);
    Task<bool> RemoveStudentAsync(Guid classId, Guid studentId);
    Task<IReadOnlyList<StudentEnrollmentDto>> GetStudentsInClassAsync(Guid classId);
    Task<IReadOnlyList<StudentEnrollmentDto>> GetAvailableStudentsAsync(Guid classId);
    Task<AnnouncementDto> CreateAnnouncementAsync(Guid classId, CreateAnnouncementRequestDto request);


    //Task<QuizDto> CreateQuizAsync(Guid classId, CreateQuizRequestDto request);
    //Task<QuizQuestionDto> AddQuizQuestionAsync(CreateQuizQuestionRequestDto request);
    //Task<IReadOnlyList<QuizQuestionDto>> GetQuizQuestionsAsync(Guid quizId);
    //Task<bool> UpdateQuizQuestionAsync(Guid questionId, UpdateQuizQuestionRequestDto request)


    Task<bool> DeleteQuizQuestionAsync(Guid questionId);
    Task<IReadOnlyList<CaseDto>> GetAllCasesAsync();
    Task<IReadOnlyList<CaseDto>> AssignCasesToClassAsync(Guid classId, AssignCasesToClassRequestDto request);
    Task<bool> ApproveCaseAsync(Guid caseId, ApproveCaseRequestDto request);
    Task<IReadOnlyList<LectStudentQuestionDto>> GetStudentQuestionsAsync(Guid classId, Guid? caseId, Guid? studentId);
    Task<IReadOnlyList<AnnouncementDto>> GetClassAnnouncementsAsync(Guid classId);
    Task<ClassStatsDto> GetClassStatsAsync(Guid classId);

    Task<QuizDto> CreateQuizAsync(CreateQuizRequestDto request);
    Task<QuizQuestionDto> AddQuizQuestionAsync(Guid quizId, CreateQuizQuestionDto request);
    Task<UpdateQuizsQuestionResponseDto> UpdateQuizQuestionAsync(Guid questionId, UpdateQuizsQuestionRequestDto request);
    Task<List<QuizQuestionDto>> GetQuizQuestionsAsync(Guid quizId);
    Task<QuizQuestionDto?> GetQuizQuestionByIdAsync(Guid questionId);

    Task<IReadOnlyList<ClassQuizDto>> GetQuizzesByLecturerAsync(Guid lecturerId);
    Task<IReadOnlyList<QuizDto>> GetQuizzesForClassAsync(Guid classId);
    Task<QuizDto?> GetQuizByIdAsync(Guid quizId);
    Task<IReadOnlyList<QuizDto>> GetQuizzesByIdsAsync(IReadOnlyList<Guid> quizIds);
    Task<ClassQuizDto> AssignQuizToClassAsync(Guid classId, Guid quizId);
    Task<ImportStudentsSummaryDto> ImportStudentsFromExcelAsync(Guid classId, Stream fileStream, string fileName);
}

