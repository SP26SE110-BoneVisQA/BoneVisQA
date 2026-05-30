using System.Text.Json;
using BoneVisQA.API.Controllers;
using BoneVisQA.API.Tests.Support;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.VisualQA;
using BoneVisQA.Services.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace BoneVisQA.API.Tests.VisualQa;

public sealed class VisualQaEndpointTests
{
    private static readonly byte[] MinimalZipHeader = [0x50, 0x4B, 0x03, 0x04];

    [Fact]
    public async Task UploadPersonal_CorruptZip_Returns400BadRequest()
    {
        var python = new Mock<IPythonAiConnectorService>();
        python.Setup(p => p.TriggerIngestAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                "personal",
                VisualQaControllerTestHost.DefaultStudentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IngestResultDto(
                Success: false,
                StatusCode: 400,
                ErrorMessage: "{\"detail\":\"File is not a zip file\"}",
                CaseId: null,
                MediaId: null,
                CatalogImageId: null,
                PreviewImageUrl: null,
                DicomMetadata: null,
                RawJson: null));

        var controller = VisualQaControllerTestHost.Create(python: python);
        var file = TestFormFileFactory.Zip("corrupt.zip", MinimalZipHeader);

        var result = await controller.UploadPersonalStudy(file, diagnosisText: null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var body = Assert.IsType<StudentPersonalStudyUploadResponse>(badRequest.Value);
        Assert.False(body.IngestOk);
        Assert.False(string.IsNullOrWhiteSpace(body.IngestError));
    }

    [Fact]
    public async Task UploadPersonal_EmptyZip_Returns400BadRequest()
    {
        var python = new Mock<IPythonAiConnectorService>();
        python.Setup(p => p.TriggerIngestAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                "personal",
                VisualQaControllerTestHost.DefaultStudentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IngestResultDto(
                Success: false,
                StatusCode: 422,
                ErrorMessage: "{\"detail\":\"No DICOM instances found in archive\"}",
                CaseId: null,
                MediaId: null,
                CatalogImageId: null,
                PreviewImageUrl: null,
                DicomMetadata: null,
                RawJson: null));

        var controller = VisualQaControllerTestHost.Create(python: python);
        var file = TestFormFileFactory.Zip("empty.zip", MinimalZipHeader);

        var result = await controller.UploadPersonalStudy(file, diagnosisText: null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var body = Assert.IsType<StudentPersonalStudyUploadResponse>(badRequest.Value);
        Assert.False(body.IngestOk);
    }

    [Fact]
    public async Task AskJson_InvalidSessionId_Returns404NotFound()
    {
        var sessionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var student = new Mock<IStudentService>();
        student.Setup(s => s.ValidateVisualQaCaseAccessAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        student.Setup(s => s.CreateOrGetVisualQaSessionAsync(
                VisualQaControllerTestHost.DefaultStudentId,
                It.IsAny<VisualQARequestDto>()))
            .ThrowsAsync(new KeyNotFoundException("Q&A session not found."));

        var controller = VisualQaControllerTestHost.Create(student: student);
        var request = new VisualQARequestDto
        {
            SessionId = sessionId,
            QuestionText = "What fracture pattern is visible?",
            ImageUrl = "https://example.test/preview.png",
        };

        var result = await controller.AskJson(request, locale: null, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task AskJson_ConcurrentRequests_EnforcesTurnLimit()
    {
        var sessionId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var caseId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var turnsCompleted = 0;

        var student = new Mock<IStudentService>();
        student.Setup(s => s.ValidateVisualQaCaseAccessAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        student.Setup(s => s.CreateOrGetVisualQaSessionAsync(
                VisualQaControllerTestHost.DefaultStudentId,
                It.IsAny<VisualQARequestDto>()))
            .ReturnsAsync(sessionId);
        student.Setup(s => s.ValidateSessionStateAsync(
                VisualQaControllerTestHost.DefaultStudentId,
                sessionId,
                It.IsAny<int>()))
            .Returns(() =>
            {
                if (Volatile.Read(ref turnsCompleted) >= 3)
                    throw new InvalidOperationException("TURN_LIMIT_EXCEEDED");
                return Task.CompletedTask;
            });
        student.Setup(s => s.HydrateVisualQaFollowUpContextAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<VisualQARequestDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid _, VisualQARequestDto req, CancellationToken _) => req);
        student.Setup(s => s.SaveVisualQAMessagesAsync(
                sessionId,
                It.IsAny<VisualQARequestDto>(),
                It.IsAny<VisualQAResponseDto>()))
            .Returns(() =>
            {
                Interlocked.Increment(ref turnsCompleted);
                return Task.CompletedTask;
            });
        student.Setup(s => s.GetVisualQaSessionCapabilitiesAsync(
                VisualQaControllerTestHost.DefaultStudentId,
                sessionId,
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns((Guid _, Guid _, int _, CancellationToken _) =>
            {
                var used = Volatile.Read(ref turnsCompleted);
                return Task.FromResult(new VisualQaCapabilitiesDto
                {
                    TurnsUsed = used,
                    TurnLimit = 3,
                    CanAskNext = used < 3,
                });
            });

        var ai = new Mock<IVisualQaAiService>();
        ai.Setup(a => a.RunPipelineAsync(It.IsAny<VisualQARequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VisualQAResponseDto
            {
                AnswerText = "Stub answer",
                ResponseKind = "answer",
            });

        var gate = new InMemoryVisualQaSessionConcurrencyGate();
        var baseRequest = new VisualQARequestDto
        {
            SessionId = sessionId,
            CaseId = caseId,
            QuestionText = "Describe the finding.",
            ImageUrl = "https://example.test/study.png",
        };

        // Each parallel call needs its own controller (HttpContext is not thread-safe).
        var tasks = Enumerable.Range(0, 6)
            .Select(i =>
            {
                var controller = VisualQaControllerTestHost.Create(student: student, ai: ai, sessionGate: gate);
                return controller.AskJson(CloneRequest(baseRequest, i), locale: null, CancellationToken.None);
            })
            .ToArray();

        var results = await Task.WhenAll(tasks);

        var okCount = results.Count(r => r.Result is OkObjectResult);
        var turnLimitCount = results.Count(r =>
        {
            if (r.Result is not BadRequestObjectResult br)
                return false;
            var json = JsonSerializer.Serialize(br.Value);
            return json.Contains("turn_limit_exceeded", StringComparison.OrdinalIgnoreCase)
                   || json.Contains("all question turns", StringComparison.OrdinalIgnoreCase);
        });

        Assert.Equal(3, okCount);
        Assert.Equal(3, turnLimitCount);
    }

    private static VisualQARequestDto CloneRequest(VisualQARequestDto source, int index) => new()
    {
        SessionId = source.SessionId,
        CaseId = source.CaseId,
        QuestionText = $"{source.QuestionText} [{index}]",
        ImageUrl = source.ImageUrl,
    };
}
