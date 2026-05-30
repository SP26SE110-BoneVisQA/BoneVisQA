using System.Text.Json;
using BoneVisQA.API.ExceptionHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace BoneVisQA.API.Tests.ExceptionHandling;

public sealed class GlobalExceptionHandlerTests
{
    private static async Task<(int StatusCode, JsonDocument Body)> HandleAsync(Exception exception)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.Setup(e => e.EnvironmentName).Returns(Environments.Production);

        var handler = new GlobalExceptionHandler(
            Mock.Of<Microsoft.Extensions.Logging.ILogger<GlobalExceptionHandler>>(),
            environment.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/api/student/visual-qa/ask-json";

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);
        Assert.True(handled);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        var doc = JsonDocument.Parse(json);
        return (context.Response.StatusCode, doc);
    }

    [Fact]
    public async Task KeyNotFoundException_Returns404ProblemDetails()
    {
        var (_, body) = await HandleAsync(new KeyNotFoundException("Q&A session not found."));
        Assert.Equal(404, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Resource not found", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.5", body.RootElement.GetProperty("type").GetString());
        Assert.True(body.RootElement.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task TurnLimit_Returns400ProblemDetails()
    {
        var (_, body) = await HandleAsync(new InvalidOperationException("TURN_LIMIT_EXCEEDED"));
        Assert.Equal(400, body.RootElement.GetProperty("status").GetInt32());
        Assert.True(
            body.RootElement.TryGetProperty("reason", out var reason)
            && reason.GetString() == "TURN_LIMIT_EXCEEDED");
    }

    [Fact]
    public async Task AiOverload_Returns503ProblemDetails()
    {
        var (_, body) = await HandleAsync(
            new InvalidOperationException("The AI system is overloaded. Please try again later."));
        Assert.Equal(503, body.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Timeout_Returns503ProblemDetails()
    {
        var (_, body) = await HandleAsync(new TimeoutException("Gemini call timed out"));
        Assert.Equal(503, body.RootElement.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task UnhandledException_ReturnsGeneric500ProblemDetails()
    {
        var (_, body) = await HandleAsync(new Exception("42703 column does not exist"));
        Assert.Equal(500, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Server error", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("Something went wrong. Please try again later.", body.RootElement.GetProperty("detail").GetString());
        Assert.Equal("https://datatracker.ietf.org/doc/html/rfc9110#section-15.6.1", body.RootElement.GetProperty("type").GetString());
        Assert.True(body.RootElement.TryGetProperty("traceId", out _));
    }

}
