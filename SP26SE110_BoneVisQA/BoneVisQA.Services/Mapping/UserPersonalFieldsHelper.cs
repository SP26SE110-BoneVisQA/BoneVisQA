using BoneVisQA.Repositories.Models;

namespace BoneVisQA.Services.Mapping;

public static class UserPersonalFieldsHelper
{
    public static void Apply(User user,
        DateOnly? dateOfBirth,
        string? phoneNumber,
        string? gender,
        string? studentSchoolId,
        string? classCode,
        string? address,
        string? bio,
        string? emergencyContact)
    {
        user.DateOfBirth = dateOfBirth;
        user.PhoneNumber = TrimOrNull(phoneNumber);
        user.Gender = TrimOrNull(gender);
        user.StudentSchoolId = TrimOrNull(studentSchoolId);
        user.ClassCode = TrimOrNull(classCode);
        user.Address = TrimOrNull(address);
        user.Bio = TrimOrNull(bio);
        user.EmergencyContact = TrimOrNull(emergencyContact);
    }

    private static string? TrimOrNull(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
