using System;
using System.Collections.Generic;
using System.Linq;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Services.Models.Expert;

namespace BoneVisQA.Services.Helpers;

/// <summary>Shared defaults and derived display fields for expert medical case APIs and dashboard.</summary>
public static class ExpertMedicalCaseDisplayHelper
{
    public const string DefaultCategory = "General";
    public const string DefaultDifficulty = "Medium";
    public const string DefaultExpertName = "Unknown";
    public const string DefaultBoneLocation = "General";

    /// <summary>Resolves bone / anatomy location from case tags (<c>Tag.Type</c> Location or BoneLocation).</summary>
    public static string ResolveBoneLocationFromTags(IEnumerable<CaseTag>? caseTags)
    {
        if (caseTags == null)
            return DefaultBoneLocation;

        var names = caseTags
            .Where(ct => ct.Tag != null)
            .Where(ct =>
                string.Equals(ct.Tag!.Type, "Location", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ct.Tag.Type, "BoneLocation", StringComparison.OrdinalIgnoreCase))
            .Select(ct => ct.Tag!.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return names.Count > 0 ? string.Join(", ", names) : DefaultBoneLocation;
    }

    public static string ComputeStatus(bool? isApproved, bool? isActive)
    {
        if (isApproved == true)
            return "approved";
        if (isActive == true)
            return "pending";
        return "draft";
    }

    public static void ApplyListDefaults(GetMedicalCaseDTO dto, bool expertScoped = false)
    {
        dto.Title = ResolveDisplayTitle(dto);
        dto.Description ??= string.Empty;
        dto.CategoryName ??= DefaultCategory;
        dto.Difficulty ??= DefaultDifficulty;
        if (!expertScoped)
            dto.ExpertName ??= DefaultExpertName;
        else
            dto.ExpertName = null;
        if (string.IsNullOrWhiteSpace(dto.BoneLocation))
            dto.BoneLocation = DefaultBoneLocation;
        dto.Status = ComputeStatus(dto.IsApproved, dto.IsActive);
        dto.CreatedAt ??= DateTime.UtcNow;
    }

    private static string ResolveDisplayTitle(GetMedicalCaseDTO dto)
    {
        var title = dto.Title?.Trim();
        if (!string.IsNullOrWhiteSpace(title)
            && !(Guid.TryParse(title, out var parsed) && parsed == dto.Id))
            return title;

        var description = dto.Description?.Trim();
        if (!string.IsNullOrWhiteSpace(description))
        {
            const int maxLen = 80;
            return description.Length <= maxLen ? description : description[..maxLen].TrimEnd() + "…";
        }

        return "Untitled case";
    }

    public static void ApplyDetailDefaults(GetExpertMedicalCaseDetailDto dto, bool expertScoped = false)
    {
        ApplyListDefaults(dto, expertScoped);
    }
}
