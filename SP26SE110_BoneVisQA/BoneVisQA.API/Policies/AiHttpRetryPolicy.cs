using System.Net;
using System.Net.Http;
using Polly;
using Polly.Extensions.Http;

namespace BoneVisQA.API.Policies;

/// <summary>Exponential backoff for Gemini / HuggingFace HTTP calls (429, 5xx, transient failures).</summary>
public static class AiHttpRetryPolicy
{
    /// <summary>3 retries with waits 2s, 4s, 8s.</summary>
    public static IAsyncPolicy<HttpResponseMessage> CreatePolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == HttpStatusCode.TooManyRequests)
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}
