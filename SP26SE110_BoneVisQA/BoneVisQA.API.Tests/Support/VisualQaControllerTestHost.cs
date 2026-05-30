using System.Security.Claims;
using BoneVisQA.API.Controllers;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BoneVisQA.API.Tests.Support;

internal static class VisualQaControllerTestHost
{
    public static readonly Guid DefaultStudentId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static VisualQAController Create(
        Mock<IStudentService>? student = null,
        Mock<IVisualQaAiService>? ai = null,
        Mock<IPythonAiConnectorService>? python = null,
        IVisualQaSessionConcurrencyGate? sessionGate = null,
        Guid? studentId = null)
    {
        student ??= new Mock<IStudentService>();
        ai ??= new Mock<IVisualQaAiService>();
        python ??= new Mock<IPythonAiConnectorService>();

        var controller = new VisualQAController(
            student.Object,
            ai.Object,
            new Mock<ISupabaseStorageService>().Object,
            python.Object,
            new MemoryCache(new MemoryCacheOptions()),
            sessionGate ?? new InMemoryVisualQaSessionConcurrencyGate(),
            NullLogger<VisualQAController>.Instance);

        var sid = studentId ?? DefaultStudentId;
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, sid.ToString()),
            new Claim(ClaimTypes.Role, "Student"),
        };
        var identity = new ClaimsIdentity(claims, authenticationType: "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };

        return controller;
    }
}
