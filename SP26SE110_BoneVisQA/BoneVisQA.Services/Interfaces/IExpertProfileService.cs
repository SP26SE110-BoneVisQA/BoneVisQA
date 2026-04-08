using BoneVisQA.Services.Models.Expert;
using System;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces;

public interface IExpertProfileService
{
    Task<ExpertProfileDto> GetProfileAsync(Guid expertId);
    Task<ExpertProfileDto> UpdateProfileAsync(Guid expertId, UpdateExpertProfileRequestDto request);
}
