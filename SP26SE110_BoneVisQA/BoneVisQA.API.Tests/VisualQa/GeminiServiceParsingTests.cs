using BoneVisQA.Domain.Settings;
using BoneVisQA.Services.Exceptions;
using BoneVisQA.Services.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BoneVisQA.API.Tests.VisualQa;

public sealed class GeminiServiceParsingTests
{
    private static GeminiService CreateService() =>
        new(
            Options.Create(new GeminiSettings()),
            new HttpClientFactoryStub(),
            NullLogger<GeminiService>.Instance);

    [Fact]
    public void ParseMedicalAnswerFromRawResponse_ExtractsJsonInsideMarkdownWrapper()
    {
        var service = CreateService();
        var raw = """
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  {
                    "text": "Here is the structured answer:\n```json\n{\"diagnosis\":\"Probable distal radius fracture\",\"differential_diagnoses\":[\"Buckle fracture\"],\"findings\":[\"Cortical disruption at distal radius\"],\"reflective_questions\":[\"What feature confirms cortical break?\"],\"citations\":[]}\n```\nUse clinical correlation."
                  }
                ]
              }
            }
          ]
        }
        """;

        var parsed = service.ParseMedicalAnswerFromRawResponse(raw);

        Assert.Equal("Probable distal radius fracture", parsed.SuggestedDiagnosis);
        Assert.Equal("analysis", parsed.ResponseKind);
        Assert.Contains("Cortical disruption at distal radius", parsed.KeyImagingFindings);
    }

    [Fact]
    public void ParseMedicalAnswerFromRawResponse_ThrowsControlledException_WhenJsonObjectMissing()
    {
        var service = CreateService();
        var raw = """
        {
          "candidates": [
            {
              "content": {
                "parts": [
                  {
                    "text": "I think the answer is a distal radius fracture, but I forgot to return JSON."
                  }
                ]
              }
            }
          ]
        }
        """;

        var ex = Assert.Throws<AiResponseFormatException>(() => service.ParseMedicalAnswerFromRawResponse(raw));
        Assert.Contains("JSON object", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class HttpClientFactoryStub : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
