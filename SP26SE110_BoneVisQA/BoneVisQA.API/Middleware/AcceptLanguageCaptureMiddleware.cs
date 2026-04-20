using BoneVisQA.Services.Helpers;

namespace BoneVisQA.API.Middleware;

/// <summary>
/// Parses <c>Accept-Language</c> once per request so controllers can compose response locale together with query/body overrides.
/// </summary>
public sealed class AcceptLanguageCaptureMiddleware
{
    private readonly RequestDelegate _next;

    public AcceptLanguageCaptureMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var tag = VisualQaRequestLanguage.ParsePrimaryLanguageTag(context.Request.Headers.AcceptLanguage.ToString());
        if (!string.IsNullOrEmpty(tag))
            context.Items[VisualQaRequestLanguage.ItemsKey] = tag;

        return _next(context);
    }
}
