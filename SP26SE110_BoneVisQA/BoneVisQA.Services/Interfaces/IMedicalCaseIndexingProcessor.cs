namespace BoneVisQA.Services.Interfaces;

/// <summary>Builds text embedding for approved <see cref="BoneVisQA.Repositories.Models.MedicalCase"/> rows.</summary>
public interface IMedicalCaseIndexingProcessor
{
    Task ProcessMedicalCaseAsync(Guid medicalCaseId, CancellationToken cancellationToken = default);
}
