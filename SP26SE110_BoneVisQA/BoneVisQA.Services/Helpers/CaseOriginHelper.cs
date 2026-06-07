using System;
using System.Collections.Generic;
using System.Linq;
using BoneVisQA.Repositories.Models;
using BoneVisQA.Services.Models.Expert;
using BoneVisQA.Services.Models.Student;

namespace BoneVisQA.Services.Helpers;

/// <summary>Resolves library case origin from tags / metadata (no dedicated DB column).</summary>
public static class CaseOriginHelper
{
    public const string StudentQaSourceTagName = "Student Q&A";

    public static bool HasStudentRequestTag(IEnumerable<CaseTag>? caseTags) =>
        caseTags?.Any(ct =>
            string.Equals(ct.Tag?.Name, StudentQaSourceTagName, StringComparison.Ordinal)) == true;

    public static bool HasStudentRequestTag(IEnumerable<ExpertCaseTagSummaryDto>? tags) =>
        tags?.Any(t =>
            string.Equals(t.Name, StudentQaSourceTagName, StringComparison.Ordinal)) == true;

    public static string ResolveExpertCaseOrigin(IEnumerable<CaseTag>? caseTags) =>
        HasStudentRequestTag(caseTags)
            ? ExpertCaseOriginValues.FromStudentRequest
            : ExpertCaseOriginValues.ExpertCreated;

    public static string ResolveExpertCaseOrigin(IEnumerable<ExpertCaseTagSummaryDto>? tags) =>
        HasStudentRequestTag(tags)
            ? ExpertCaseOriginValues.FromStudentRequest
            : ExpertCaseOriginValues.ExpertCreated;

    public static string ResolveStudentCatalogOrigin(MedicalCase c) =>
        HasStudentRequestTag(c.CaseTags)
            ? StudentCaseOriginValues.CommunityPromoted
            : StudentCaseOriginValues.ExpertCreated;
}
