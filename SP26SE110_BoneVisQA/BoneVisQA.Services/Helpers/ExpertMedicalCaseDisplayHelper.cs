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
    public const string DefaultAnatomySite = "Other";
    public const string DefaultPathologyGroup = "Trauma";

    /// <summary>Resolves bone / anatomy location from case tags (<c>Tag.Type</c> Location or BoneLocation).</summary>
    public static string ResolveBoneLocationFromTags(IEnumerable<CaseTag>? caseTags)
    {
        if (caseTags == null)
            return string.Empty;

        var names = caseTags
            .Where(ct => ct.Tag != null)
            .Where(ct =>
                string.Equals(ct.Tag!.Type, "Location", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ct.Tag.Type, "BoneLocation", StringComparison.OrdinalIgnoreCase))
            .Select(ct => ct.Tag!.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return names.Count > 0 ? string.Join(", ", names) : string.Empty;
    }

    public static string ResolveAnatomySite(MedicalCase entity)
    {
        var fromTags = ResolveBoneLocationFromTags(entity.CaseTags);
        if (!string.IsNullOrWhiteSpace(fromTags))
            return fromTags;

        var anatomySite = entity.CaseMetadata?.AnatomySite?.Trim();
        if (!string.IsNullOrWhiteSpace(anatomySite))
            return anatomySite;

        return DefaultAnatomySite;
    }

    public static string ResolvePathologyGroup(MedicalCase entity)
    {
        var fromTags = entity.CaseTags?
            .Where(ct => ct.Tag != null)
            .Where(ct =>
                string.Equals(ct.Tag!.Type, "Lesion Type", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ct.Tag.Type, "Lesion", StringComparison.OrdinalIgnoreCase))
            .Select(ct => ct.Tag!.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(fromTags))
            return fromTags;

        var pathology = entity.CaseMetadata?.PathologyGroup?.Trim();
        if (!string.IsNullOrWhiteSpace(pathology))
            return pathology;

        return DefaultPathologyGroup;
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
        dto.ExpertName ??= DefaultExpertName;
        if (string.IsNullOrWhiteSpace(dto.AnatomySite))
            dto.AnatomySite = string.IsNullOrWhiteSpace(dto.BoneLocation) ? DefaultAnatomySite : dto.BoneLocation;
        if (string.IsNullOrWhiteSpace(dto.PathologyGroup))
            dto.PathologyGroup = DefaultPathologyGroup;
        dto.BoneLocation = dto.AnatomySite;
        if (string.IsNullOrWhiteSpace(dto.CaseOrigin))
            dto.CaseOrigin = ExpertCaseOriginValues.ExpertCreated;
        dto.Status = expertScoped ? string.Empty : ComputeStatus(dto.IsApproved, dto.IsActive);
        dto.CreatedAt ??= DateTime.UtcNow;
        _ = expertScoped;
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
        if (expertScoped && dto.Tags.Count > 0)
            dto.CaseOrigin = CaseOriginHelper.ResolveExpertCaseOrigin(dto.Tags);
        ApplyListDefaults(dto, expertScoped);
    }
}
