namespace BoneVisQA.Services.Interfaces;

public interface IEmailService
{
    Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetLink);
    Task<bool> SendWelcomeEmailAsync(string toEmail, string fullName);
    Task<bool> SendRoleAssignedEmailAsync(string toEmail, string fullName, string roleName, bool accountActivated = false);
    Task<bool> SendAccountActivatedEmailAsync(string toEmail, string fullName);
    Task<bool> SendAccountDeactivatedEmailAsync(string toEmail, string fullName);
}
