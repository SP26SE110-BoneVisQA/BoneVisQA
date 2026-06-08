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
        if (session?.CaseId is not { } caseId || caseId == Guid.Empty || linkedCase == null)
            return false;

        return linkedCase.IsApproved == true
               && linkedCase.OwnerStudentId == null
               && linkedCase.CreatedByExpertId != null;
    }

    public static string ResolveStudyMode(VisualQASession? session, MedicalCase? linkedCase) =>
        IsCatalogCaseStudySession(session, linkedCase) ? CatalogCaseStudy : PersonalDicom;
}
