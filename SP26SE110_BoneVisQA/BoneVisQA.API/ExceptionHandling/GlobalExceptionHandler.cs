using System.Text.Json;
using BoneVisQA.Services.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace BoneVisQA.API.ExceptionHandling;

public class GlobalExceptionHandler : IExceptionHandler
{
    private const string AiOverloadMessage = "The AI system is overloaded. Please try again later.";
    private const string AiBusyClientMessage = "AI is currently busy. Please try again in a moment.";

    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception at {Path}", httpContext.Request.Path);

        var (statusCode, title, type, detail, extensions) = MapException(exception);
        extensions["traceId"] = httpContext.TraceIdentifier;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = httpContext.Request.Path,
            Extensions = extensions
        };

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }

    private (int StatusCode, string Title, string Type, string Detail, Dictionary<string, object?> Extensions) MapException(Exception exception)
    {
        return exception switch
        {
            ArgumentException arg => (
                StatusCodes.Status400BadRequest,
                "Invalid request",
                ProblemTypeFor(StatusCodes.Status400BadRequest),
                ClientMessage(arg.Message, "We could not process your request."),
                EmptyExtensions()),

            KeyNotFoundException knf => (
                StatusCodes.Status404NotFound,
                "Resource not found",
                ProblemTypeFor(StatusCodes.Status404NotFound),
                ClientMessage(knf.Message, "The requested resource was not found."),
                EmptyExtensions()),

            ConflictException conflict => (
                StatusCodes.Status409Conflict,
                "Conflict",
                ProblemTypeFor(StatusCodes.Status409Conflict),
                ClientMessage(conflict.Message, "The request could not be completed due to a conflict."),
                EmptyExtensions()),

            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                ProblemTypeFor(StatusCodes.Status401Unauthorized),
                "Access denied.",
                EmptyExtensions()),

            OperationCanceledException or TaskCanceledException => (
                StatusCodes.Status503ServiceUnavailable,
                "Service unavailable",
                ProblemTypeFor(StatusCodes.Status503ServiceUnavailable),
                AiBusyClientMessage,
                ReasonExtension("request_cancelled_or_timeout")),

            TimeoutException => (
                StatusCodes.Status503ServiceUnavailable,
                "Service unavailable",
                ProblemTypeFor(StatusCodes.Status503ServiceUnavailable),
                AiBusyClientMessage,
                ReasonExtension("timeout")),

            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "Conflict",
                ProblemTypeFor(StatusCodes.Status409Conflict),
                "The resource was updated by another operation. Please reload and try again.",
                ReasonExtension("db_concurrency")),

            DbUpdateException dbUpdate when dbUpdate.InnerException is PostgresException postgres => MapPostgres(postgres),

            PostgresException postgres => MapPostgres(postgres),

            InvalidOperationException op => MapInvalidOperation(op),

            BadHttpRequestException badHttp => (
                StatusCodes.Status400BadRequest,
                "Invalid request",
                ProblemTypeFor(StatusCodes.Status400BadRequest),
                ClientMessage(badHttp.Message, "The request was invalid or too large."),
                EmptyExtensions()),

            JsonException => (
                StatusCodes.Status400BadRequest,
                "Invalid request",
                ProblemTypeFor(StatusCodes.Status400BadRequest),
                "The request body could not be parsed.",
                EmptyExtensions()),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Server error",
                ProblemTypeFor(StatusCodes.Status500InternalServerError),
                _environment.IsDevelopment()
                    ? exception.Message
                    : "Something went wrong. Please try again later.",
                ReasonExtension("unhandled"))
        };
    }

    private (int StatusCode, string Title, string Type, string Detail, Dictionary<string, object?> Extensions) MapInvalidOperation(
        InvalidOperationException exception)
    {
        var message = exception.Message ?? string.Empty;

        if (message.Contains(AiOverloadMessage, StringComparison.Ordinal)
            || message.Contains("VISUAL_QA_SESSION_BUSY", StringComparison.Ordinal)
            || message.Contains("Another question is being processed", StringComparison.Ordinal))
        {
            return (
                StatusCodes.Status503ServiceUnavailable,
                "Service unavailable",
                ProblemTypeFor(StatusCodes.Status503ServiceUnavailable),
                ClientMessage(message, AiBusyClientMessage),
                ReasonExtension("ai_or_session_busy"));
        }

        if (message is "SESSION_EXPIRED" or "SESSION_READ_ONLY" or "TURN_LIMIT_EXCEEDED")
        {
            return (
                StatusCodes.Status400BadRequest,
                "Session blocked",
                ProblemTypeFor(StatusCodes.Status400BadRequest),
                MapSessionPolicyMessage(message),
                ReasonExtension(message));
        }

        return (
            StatusCodes.Status400BadRequest,
            "Invalid operation",
            ProblemTypeFor(StatusCodes.Status400BadRequest),
            ClientMessage(message, "We could not process your request."),
            EmptyExtensions());
    }

    private (int StatusCode, string Title, string Type, string Detail, Dictionary<string, object?> Extensions) MapPostgres(
        PostgresException exception)
    {
        return exception.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation => (
                StatusCodes.Status409Conflict,
                "Conflict",
                ProblemTypeFor(StatusCodes.Status409Conflict),
                "The request could not be completed because the resource already exists.",
                ReasonExtension("db_unique_violation")),

            PostgresErrorCodes.ForeignKeyViolation => (
                StatusCodes.Status400BadRequest,
                "Invalid request",
                ProblemTypeFor(StatusCodes.Status400BadRequest),
                "The request references related data that does not exist or cannot be used.",
                ReasonExtension("db_foreign_key_violation")),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Server error",
                ProblemTypeFor(StatusCodes.Status500InternalServerError),
                _environment.IsDevelopment()
                    ? exception.MessageText
                    : "Something went wrong. Please try again later.",
                ReasonExtension("db_error"))
        };
    }

    private static string MapSessionPolicyMessage(string reason) => reason switch
    {
        "SESSION_EXPIRED" => "This Visual QA session expired after 24 hours of inactivity.",
        "SESSION_READ_ONLY" => "This session is locked and cannot accept new questions.",
        "TURN_LIMIT_EXCEEDED" => "You have used all question turns for this Visual QA session.",
        _ => "This session cannot accept new questions."
    };

    private string ClientMessage(string? specific, string fallback) =>
        _environment.IsDevelopment() && !string.IsNullOrWhiteSpace(specific)
            ? specific.Trim()
            : fallback;

    private static Dictionary<string, object?> EmptyExtensions() => new();

    private static Dictionary<string, object?> ReasonExtension(string reason) =>
        new() { ["reason"] = reason };

    private static string ProblemTypeFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
        StatusCodes.Status401Unauthorized => "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.2",
        StatusCodes.Status404NotFound => "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.5",
        StatusCodes.Status409Conflict => "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.10",
        StatusCodes.Status500InternalServerError => "https://datatracker.ietf.org/doc/html/rfc9110#section-15.6.1",
        StatusCodes.Status503ServiceUnavailable => "https://datatracker.ietf.org/doc/html/rfc9110#section-15.6.4",
        _ => "about:blank"
    };
}
