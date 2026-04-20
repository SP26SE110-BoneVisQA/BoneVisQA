using System.Text.RegularExpressions;
using BoneVisQA.Repositories.Models;

namespace BoneVisQA.Services.Helpers;

public static class VisualQaReviewFeedbackRouting
{
    private static readonly Regex TargetPrefixRegex = new(
        @"^\[TARGET:\s*(?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\s*\]\s*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static (Guid? TargetAssistantId, string DisplayContent) Resolve(QAMessage message)
    {
        if (message.TargetAssistantMessageId.HasValue)
            return (message.TargetAssistantMessageId.Value, message.Content ?? string.Empty);

        var raw = message.Content ?? string.Empty;
        var match = TargetPrefixRegex.Match(raw);
        if (match.Success && Guid.TryParse(match.Groups["id"].Value, out var parsed))
            return (parsed, raw[match.Length..]);

        return (null, raw);
    }
}
