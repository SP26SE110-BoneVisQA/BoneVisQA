using System.Threading.Tasks;
using BoneVisQA.Services.Models.Auth;

namespace BoneVisQA.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResultDto> RegisterAsync(RegisterRequestDto request);
    Task<AuthResultDto> LoginAsync(LoginRequestDto request);
}

