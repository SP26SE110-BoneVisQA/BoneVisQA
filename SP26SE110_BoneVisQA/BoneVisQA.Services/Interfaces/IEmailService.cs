namespace BoneVisQA.Services.Interfaces;

public interface IEmailService
{
    Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetLink);
    Task<bool> SendWelcomeEmailAsync(string toEmail, string fullName);
}
