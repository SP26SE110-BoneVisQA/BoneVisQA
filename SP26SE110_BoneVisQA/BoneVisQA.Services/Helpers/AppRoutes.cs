namespace BoneVisQA.Services.Helpers;

/// <summary>SPA-relative routes shared by notifications and deep links.</summary>
public static class AppRoutes
{
    public static string StudentVisualQaWorkspace(Guid sessionId) =>
        $"/student/visual-qa/workspace?sessionId={sessionId}";

    public static string LecturerQaTriage(Guid classId) =>
        $"/lecturer/qa-triage?classId={classId}";
}
