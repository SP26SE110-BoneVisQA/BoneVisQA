using BoneVisQA.Services.Models.Lecturer;

namespace BoneVisQA.Services.Interfaces;

public interface ITeachingObjectiveService
{
    // Expert methods
    Task<ExpertTeachingObjectivesDto?> GetClassObjectivesForExpertAsync(Guid expertId, Guid classId);
    Task<List<ExpertTeachingObjectivesDto>> GetAssignedClassesObjectivesAsync(Guid expertId);
    Task<TeachingObjectiveSuggestionDto> SuggestObjectiveAsync(Guid expertId, SuggestObjectiveRequestDto request);
    Task<List<TeachingObjectiveSuggestionDto>> GetMyPendingSuggestionsAsync(Guid expertId);

    // Lecturer methods (delegated from ILecturerService)
    Task<TeachingObjectivesDto?> GetTeachingObjectivesAsync(Guid lecturerId, Guid? classId = null);
    Task<TeachingObjectivesDto> UpdateTeachingObjectivesAsync(Guid lecturerId, Guid classId, UpdateTeachingObjectivesRequestDto request);
    Task<List<TeachingObjectiveSuggestionDto>> GetExpertSuggestionsAsync(Guid classId);
    Task<TeachingObjectiveSuggestionDto> ConfirmSuggestionAsync(Guid lecturerId, Guid suggestionId, ConfirmSuggestionRequestDto request);
}
