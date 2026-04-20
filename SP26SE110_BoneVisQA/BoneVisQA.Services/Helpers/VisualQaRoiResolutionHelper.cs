using BoneVisQA.Repositories.Models;
using BoneVisQA.Services.Models.VisualQA;

namespace BoneVisQA.Services.Helpers;

/// <summary>
/// Picks ROI JSON for triage/expert overlays when the DB user message row is empty but turns carry <see cref="VisualQaTurnDto.QuestionCoordinates"/>.
/// </summary>
public static class VisualQaRoiResolutionHelper
{
    public static string? ResolvePreferredUserRoiJson(
        QAMessage? primaryUserMessage,
        Guid? requestedReviewAssistantMessageId,
        IReadOnlyList<VisualQaTurnDto> turns)
    {
        var direct = primaryUserMessage?.Coordinates;
        if (!string.IsNullOrWhiteSpace(direct))
            return direct;

        if (requestedReviewAssistantMessageId.HasValue)
        {
            var targeted = turns.FirstOrDefault(t =>
                t.AssistantMessageId.HasValue &&
                t.AssistantMessageId.Value == requestedReviewAssistantMessageId.Value);
            if (!string.IsNullOrWhiteSpace(targeted?.QuestionCoordinates))
                return targeted.QuestionCoordinates;
        }

        for (var i = turns.Count - 1; i >= 0; i--)
        {
            var q = turns[i].QuestionCoordinates;
            if (!string.IsNullOrWhiteSpace(q))
                return q;
        }

        return null;
    }
}
