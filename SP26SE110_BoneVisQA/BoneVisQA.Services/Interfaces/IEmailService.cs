namespace BoneVisQA.Services.Interfaces;

public interface IEmailService
{
    Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetLink);
    Task<bool> SendWelcomeEmailAsync(string toEmail, string fullName);
    Task<bool> SendRoleAssignedEmailAsync(string toEmail, string fullName, string roleName, bool accountActivated = false);
    Task<bool> SendAccountActivatedEmailAsync(string toEmail, string fullName);
    Task<bool> SendAccountDeactivatedEmailAsync(string toEmail, string fullName);

    // Medical Student Verification Emails
    Task<bool> SendMedicalVerificationRequestedEmailAsync(string toEmail, string fullName);
    Task<bool> SendMedicalVerificationApprovedEmailAsync(string toEmail, string fullName);
    Task<bool> SendMedicalVerificationRejectedEmailAsync(string toEmail, string fullName, string? reason);

    // Welcome Email with Role (sent after both verification AND role assignment)
    Task<bool> SendWelcomeWithRoleEmailAsync(string toEmail, string fullName, string roleName);

    // Announcement Emails
    Task<bool> SendAnnouncementEmailAsync(string toEmail, string studentName, string lecturerName, string className, string announcementTitle, string announcementContent);

    // Assignment notification emails
    Task<bool> SendAssignmentEmailAsync(
        string toEmail,
        string studentName,
        string className,
        string assignmentTitle,
        string assignmentType,
        DateTime? dueDate,
        string? dueDateDisplay);

    //Task SendAssignmentEmailsToClassAsync(Guid classId, string className, string assignmentTitle, string assignmentType, DateTime? dueDate);
}
