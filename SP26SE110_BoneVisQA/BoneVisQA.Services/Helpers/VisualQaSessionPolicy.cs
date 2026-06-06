namespace BoneVisQA.Services.Helpers;

/// <summary>Production/demo Visual QA session limits (unlimited turns, no inactivity lock).</summary>
public static class VisualQaSessionPolicy
{
    /// <summary>When null, turn caps are not enforced.</summary>
    public static int? MaxUserQuestions => null;

    /// <summary>When null, sessions never expire due to inactivity.</summary>
    public static int? InactivityLockHours => null;

    public static bool IsTurnLimitEnabled => MaxUserQuestions.HasValue;

    public static bool IsInactivityLockEnabled => InactivityLockHours.HasValue;
}
