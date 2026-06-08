using BoneVisQA.Repositories.Models;
using BoneVisQA.Services.Models.Expert;

namespace BoneVisQA.Services.Helpers;

/// <summary>Promotion rules for expert review queue → case library.</summary>
public static class VisualQaPromotionHelper
{
  /// <summary>
  /// Student personal DICOM / escalated review sessions (not expert raster self-upload).
  /// </summary>
  public static bool IsStudentRequestPromotion(VisualQASession session, PromoteToLibraryRequestDto? request = null)
  {
    if (request?.FromStudentRequest == true)
      return true;

    if (string.Equals(request?.CaseOrigin, ExpertCaseOriginValues.FromStudentRequest, StringComparison.OrdinalIgnoreCase))
      return true;

    if (session.Case?.OwnerStudentId != null)
      return true;

    if (string.Equals(session.StudyMode, VisualQaSessionFlowHelper.PersonalDicom, StringComparison.OrdinalIgnoreCase)
        && !VisualQaSessionFlowHelper.IsCatalogCaseStudySession(session, session.Case))
      return true;

    return false;
  }

  /// <summary>Preview URL for library <c>medical_images</c> row (custom upload, linked image, or case media).</summary>
  public static string? ResolvePromotableImageUrl(VisualQASession session)
  {
    if (!string.IsNullOrWhiteSpace(session.CustomImageUrl))
      return session.CustomImageUrl.Trim();

    if (!string.IsNullOrWhiteSpace(session.Image?.ImageUrl))
      return session.Image.ImageUrl.Trim();

    return CaseMediaDicomMetadataHelper.ResolveFirstPreviewUrl(session.Case);
  }

  public static bool HasCopyableCaseMedia(VisualQASession session) =>
    session.Case?.CaseMedia is { Count: > 0 };
}
