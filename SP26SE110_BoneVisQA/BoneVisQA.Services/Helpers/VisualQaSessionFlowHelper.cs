using BoneVisQA.Repositories.Models;

namespace BoneVisQA.Services.Helpers;

/// <summary>Distinguishes personal DICOM upload Q&amp;A from catalog case study Q&amp;A.</summary>
public static class VisualQaSessionFlowHelper
{
    public const string PersonalDicom = "personal_dicom";
    public const string CatalogCaseStudy = "catalog_case_study";

    /// <summary>
    /// Catalog study: student opened an approved library case (not a personal ingest row).
    /// These sessions skip lecturer/expert review workflows.
    /// </summary>
    public static bool IsCatalogCaseStudySession(VisualQASession? session, MedicalCase? linkedCase)
    {
        if (!string.IsNullOrWhiteSpace(session?.StudyMode))
            return string.Equals(session.StudyMode, CatalogCaseStudy, StringComparison.OrdinalIgnoreCase);

        if (session?.CaseId is not { } caseId || caseId == Guid.Empty || linkedCase == null)
            return false;

        return linkedCase.IsApproved == true
               && linkedCase.OwnerStudentId == null
               && linkedCase.CreatedByExpertId != null;
    }

    public static string ResolveStudyMode(VisualQASession? session, MedicalCase? linkedCase)
    {
        if (!string.IsNullOrWhiteSpace(session?.StudyMode))
            return session.StudyMode.Trim().ToLowerInvariant() switch
            {
                "catalog_case_study" => CatalogCaseStudy,
                "personal_dicom" => PersonalDicom,
                var other when !string.IsNullOrWhiteSpace(other) => other,
                _ => PersonalDicom
            };

        return IsCatalogCaseStudySession(session, linkedCase) ? CatalogCaseStudy : PersonalDicom;
    }

    /// <summary>Persist on new sessions so history survives medical case deletion.</summary>
    public static string ResolveStudyModeForNewSession(MedicalCase? linkedCase, Guid? caseId)
    {
        if (caseId is not { } cid || cid == Guid.Empty || linkedCase == null)
            return PersonalDicom;

        return linkedCase.IsApproved == true
               && linkedCase.OwnerStudentId == null
               && linkedCase.CreatedByExpertId != null
            ? CatalogCaseStudy
            : PersonalDicom;
    }

    public static bool IsCaseRemoved(VisualQASession session, MedicalCase? linkedCase) =>
        string.Equals(ResolveStudyMode(session, linkedCase), CatalogCaseStudy, StringComparison.OrdinalIgnoreCase)
        && session.CaseId == null;
}
