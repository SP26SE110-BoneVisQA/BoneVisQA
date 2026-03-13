using System.Net.Http.Json;
using System.Threading.Tasks;
using BoneVisQA.Services.Interfaces;
using BoneVisQA.Services.Models.VisualQA;
using Microsoft.Extensions.Logging;

namespace BoneVisQA.Services.Services;

public class AIService : IAIService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AIService> _logger;

    public AIService(HttpClient httpClient, ILogger<AIService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<VisualQAResponseDto> AskVisualQuestionAsync(VisualQARequestDto request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/visual-rag", request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<VisualQAResponseDto>();
        return result ?? new VisualQAResponseDto();
    }
}
