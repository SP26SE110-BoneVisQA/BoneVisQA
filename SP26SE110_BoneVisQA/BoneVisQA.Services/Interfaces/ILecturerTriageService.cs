using BoneVisQA.Services.Models.Lecturer;

namespace BoneVisQA.Services.Interfaces;

public interface ILecturerTriageService
{
    Task<EscalatedAnswerDto> EscalateAnswerAsync(Guid lecturerId, Guid sessionId, EscalateAnswerRequestDto? request);
}
