using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace BoneVisQA.Services.Helpers;

/// <summary>
/// Resolves Visual QA response language from query <c>locale</c>, captured <see cref="ItemsKey"/>, or <c>Accept-Language</c>.
/// Optional explicit client override was removed for production; call sites pass <c>null</c> for <paramref name="preferredLanguageFromClient"/>.
/// </summary>
public static class VisualQaRequestLanguage
{
    /// <summary>Stored by Accept-Language capture middleware (API layer) before controller runs.</summary>
    public const string ItemsKey = "BoneVisQA.PrimaryLanguageTag";

    /// <summary>Default primary subtag when nothing is specified (product default).</summary>
    public const string DefaultPrimaryTag = "vi";

    /// <summary>
    /// Priority: <paramref name="queryLocale"/> → <paramref name="preferredLanguageFromClient"/> (unused; pass null) → middleware item → <c>Accept-Language</c> header → default Vietnamese.
    /// </summary>
    public static string Resolve(HttpRequest request, string? preferredLanguageFromClient, string? queryLocale)
    {
        var fromQuery = NormalizePrimaryTag(queryLocale);
        if (fromQuery != null)
            return fromQuery;

        var fromBody = NormalizePrimaryTag(preferredLanguageFromClient);
        if (fromBody != null)
            return fromBody;

        if (request.HttpContext.Items.TryGetValue(ItemsKey, out var itemObj)
            && itemObj is string ctxTag
            && NormalizePrimaryTag(ctxTag) is { } fromMw)
            return fromMw;

        var fromHeader = ParsePrimaryLanguageTag(request.Headers.AcceptLanguage.ToString());
        return string.IsNullOrEmpty(fromHeader) ? DefaultPrimaryTag : fromHeader;
    }

    /// <summary>
    /// Resolves response language for Visual QA: explicit <c>?locale=</c> wins; otherwise infer from question text (Vietnamese vs English); else header/default.
    /// </summary>
    public static string ResolveResponseLanguage(HttpRequest request, string? questionText, string? queryLocale)
    {
        var fromQuery = NormalizePrimaryTag(queryLocale);
        if (fromQuery != null)
            return fromQuery;

        if (QuestionTextLooksVietnamese(questionText))
            return "vi";

        if (QuestionTextLooksEnglish(questionText))
            return "en";

        return Resolve(request, null, queryLocale);
    }

    /// <summary>Backward-compatible alias; prefer <see cref="ResolveResponseLanguage"/>.</summary>
    public static string ApplyVietnameseQuestionHeuristic(string? questionText, string? queryLocale, string resolvedPrimaryTag)
    {
        if (NormalizePrimaryTag(queryLocale) != null)
            return resolvedPrimaryTag;
        if (QuestionTextLooksVietnamese(questionText))
            return "vi";
        if (QuestionTextLooksEnglish(questionText))
            return "en";
        return resolvedPrimaryTag;
    }

    private static bool QuestionTextLooksEnglish(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var hasLatin = false;
        foreach (var ch in text)
        {
            if (VietnameseDiacriticChars.Contains(ch))
                return false;
            if (ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z')
                hasLatin = true;
        }

        return hasLatin;
    }

    private static readonly HashSet<char> VietnameseDiacriticChars = new(
        "àáảãạăằắẳẵặâầấẩẫậèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵđÀÁẢÃẠĂẰẮẲẴẶÂẦẤẨẪẬÈÉẺẼẸÊỀẾỂỄỆÌÍỈĨỊÒÓỎÕỌÔỒỐỔỖỘƠỜỚỞỠỢÙÚỦŨỤƯỪỨỬỮỰỲÝỶỸỴĐ");

    private static bool QuestionTextLooksVietnamese(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;
        foreach (var ch in text)
        {
            if (VietnameseDiacriticChars.Contains(ch))
                return true;
        }
        return false;
    }

    /// <summary>Parses the first language-range in an Accept-Language header value (RFC 7231).</summary>
    public static string? ParsePrimaryLanguageTag(string? acceptLanguageHeader)
    {
        if (string.IsNullOrWhiteSpace(acceptLanguageHeader))
            return null;

        var first = acceptLanguageHeader.Split(',')[0].Trim();
        var semi = first.IndexOf(';');
        if (semi >= 0)
            first = first[..semi].Trim();
        return NormalizePrimaryTag(first);
    }

    /// <summary>Returns ISO 639-1 lowercase primary subtag or null if invalid.</summary>
    public static string? NormalizePrimaryTag(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var token = raw.Trim();
        // Support "zh-Hans", "en-US"
        var hyphen = token.IndexOf('-');
        if (hyphen > 0)
            token = token[..hyphen];

        token = token.ToLowerInvariant();
        if (token.Length is < 2 or > 8)
            return null;

        return token;
    }
}

/// <summary>Maps resolved language tags to English names used inside LLM system prompts.</summary>
public static class VisualQaPromptLanguage
{
    /// <summary>Returns a human-readable language name for prompt instructions (e.g. <c>Vietnamese</c>, <c>English</c>).</summary>
    public static string GetInstructionLanguageName(string? primaryTag)
    {
        var t = string.IsNullOrWhiteSpace(primaryTag)
            ? VisualQaRequestLanguage.DefaultPrimaryTag
            : primaryTag.Trim().ToLowerInvariant();

        return t switch
        {
            "vi" => "Vietnamese",
            "en" => "English",
            "fr" => "French",
            "de" => "German",
            "es" => "Spanish",
            "pt" => "Portuguese",
            "it" => "Italian",
            "ja" => "Japanese",
            "ko" => "Korean",
            "zh" => "Chinese",
            "nl" => "Dutch",
            "pl" => "Polish",
            "ru" => "Russian",
            "th" => "Thai",
            "id" => "Indonesian",
            "ms" => "Malay",
            _ => TryCultureEnglishName(t) ?? "English"
        };
    }

    private static string? TryCultureEnglishName(string isoPrimary)
    {
        try
        {
            var ci = CultureInfo.GetCultureInfo(isoPrimary);
            return ci.EnglishName;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
