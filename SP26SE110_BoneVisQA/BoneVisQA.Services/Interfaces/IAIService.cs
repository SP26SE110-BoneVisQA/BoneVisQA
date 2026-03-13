using System.Threading.Tasks;
using BoneVisQA.Services.Models.VisualQA;

namespace BoneVisQA.Services.Interfaces;

public interface IAIService
{
    Task<VisualQAResponseDto> AskVisualQuestionAsync(VisualQARequestDto request);
}
