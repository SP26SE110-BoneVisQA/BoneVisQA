using BoneVisQA.Services.Models.Lecturer;
using System;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Interfaces;

public interface ILecturerProfileService
{
    Task<LecturerProfileDto> GetProfileAsync(Guid lecturerId);
    Task<LecturerProfileDto> UpdateProfileAsync(Guid lecturerId, UpdateLecturerProfileRequestDto request);
}
