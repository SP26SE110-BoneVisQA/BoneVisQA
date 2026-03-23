using System.Threading.Tasks;
using BoneVisQA.Services.Models.Auth;

namespace BoneVisQA.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResultDto> RegisterAsync(RegisterRequestDto request);
    Task<AuthResultDto> LoginAsync(LoginRequestDto request);
    Task<AuthResultDto> ForgotPasswordAsync(ForgotPasswordRequestDto request);
    Task<AuthResultDto> ResetPasswordAsync(ResetPasswordRequestDto request);
}

