namespace BoneVisQA.Services.Interfaces;

public interface IEmailService
{
    Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetLink);
}
