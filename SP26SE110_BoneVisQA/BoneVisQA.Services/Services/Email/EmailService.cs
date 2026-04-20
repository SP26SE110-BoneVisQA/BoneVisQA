using BoneVisQA.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace BoneVisQA.Services.Services.Email;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _smtpUsername;
    private readonly string _smtpPassword;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly string _appUrl;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        _appUrl = _configuration["App:FrontendUrl"] ?? _configuration["App:BaseUrl"] ?? "https://localhost:3000";

        var smtpHost = _configuration["Email:SmtpHost"];
        var smtpPort = _configuration["Email:SmtpPort"];
        var smtpUsername = _configuration["Email:Username"];
        var smtpPassword = _configuration["Email:Password"];
        var fromEmail = _configuration["Email:FromEmail"];
        var fromName = _configuration["Email:FromName"];

        _logger.LogInformation("[EmailService] Config loaded - Host: {Host}, Port: {Port}, Username: {Username}, FromEmail: {FromEmail}",
            smtpHost ?? "NULL", smtpPort ?? "NULL", smtpUsername ?? "NULL", fromEmail ?? "NULL");

        _smtpHost = smtpHost ?? "smtp.gmail.com";
        _smtpPort = int.TryParse(smtpPort, out var port) ? port : 587;
        _smtpUsername = smtpUsername ?? "";
        // Gmail App Password thường copy kèm dấu cách giữa 4 nhóm — SMTP cần chuỗi liền
        _smtpPassword = (smtpPassword ?? "").Replace(" ", "", StringComparison.Ordinal);
        _fromEmail = fromEmail ?? _smtpUsername;
        _fromName = fromName ?? "BoneVisQA";

        _logger.LogInformation("[EmailService] Initialized - SmtpHost: {Host}, SmtpPort: {Port}, From: {From}",
            _smtpHost, _smtpPort, _fromEmail);
    }

    public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string resetLink)
    {
        _logger.LogInformation("[SendPasswordResetEmailAsync] Attempting to send reset email to {ToEmail}", toEmail);

        if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
        {
            _logger.LogError("[SendPasswordResetEmailAsync] FAIL: Email config missing. Username: {Username}, Password empty: {IsEmpty}",
                _smtpUsername ?? "NULL", string.IsNullOrEmpty(_smtpPassword));
            return false;
        }

        _logger.LogInformation("[SendPasswordResetEmailAsync] SMTP config OK - connecting to {Host}:{Port}", _smtpHost, _smtpPort);

        try
        {
            var subject = "Reset Password - BoneVisQA";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #2c3e50; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; background-color: #f9f9f9; }}
        .button {{ display: inline-block; padding: 12px 30px; background-color: #3498db; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
        .warning {{ background-color: #fff3cd; padding: 10px; border-radius: 5px; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>BoneVisQA</h1>
        </div>
        <div class='content'>
            <h2>Password Reset Request</h2>
            <p>Hello,</p>
            <p>We received a request to reset your account password.</p>
            <p>Please click the button below to reset your password:</p>
            <p style='text-align: center;'>
                <a href='{resetLink}' class='button'>Reset Password</a>
            </p>
            <p>Or copy and paste the following link into your browser:</p>
            <p style='word-break: break-all; color: #3498db;'>{resetLink}</p>
            <div class='warning'>
                <strong>Note:</strong> The password reset link will expire in 1 hour. If you did not request a password reset, please ignore this email.
            </div>
            <p>Best regards,<br>The BoneVisQA Team</p>
        </div>
        <div class='footer'>
            <p>This email was sent automatically by the BoneVisQA system.</p>
            <p>Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                Timeout = 15000
            };

            var message = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
            return true;
        }
        catch (SmtpException smtpEx)
        {
            _logger.LogError(smtpEx, "[SendPasswordResetEmailAsync] SMTP ERROR sending to {ToEmail}: {Code} - {Message}",
                toEmail, smtpEx.StatusCode, smtpEx.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SendPasswordResetEmailAsync] GENERAL ERROR sending reset email to {ToEmail}: {Message}", toEmail, ex.Message);
            return false;
        }
    }

    public async Task<bool> SendWelcomeEmailAsync(string toEmail, string fullName)
    {
        _logger.LogInformation("[SendWelcomeEmailAsync] Attempting to send welcome email to {ToEmail} for {FullName}", toEmail, fullName);

        if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
        {
            _logger.LogError("[SendWelcomeEmailAsync] FAIL: Email config missing. Username: {Username}, Password empty: {IsEmpty}",
                _smtpUsername ?? "NULL", string.IsNullOrEmpty(_smtpPassword));
            return false;
        }

        _logger.LogInformation("[SendWelcomeEmailAsync] SMTP config OK - connecting to {Host}:{Port}", _smtpHost, _smtpPort);

        try
        {
            var subject = "Welcome to BoneVisQA";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #2c3e50; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; background-color: #f9f9f9; }}
        .button {{ display: inline-block; padding: 12px 30px; background-color: #3498db; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
        .success {{ background-color: #d4edda; padding: 15px; border-radius: 5px; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>BoneVisQA</h1>
        </div>
        <div class='content'>
            <h2>Welcome {fullName}!</h2>
            <p>Hello {fullName},</p>
            <p>Thank you for registering an account at <strong>BoneVisQA</strong>.</p>
            <div class='success'>
                <p><strong>Your account is pending approval.</strong></p>
                <p>Please wait for admin confirmation before logging in and using the system.</p>
            </div>
            <p>If you have any questions, please contact us.</p>
            <p>Best regards,<br>The BoneVisQA Team</p>
        </div>
        <div class='footer'>
            <p>This email was sent automatically by the BoneVisQA system.</p>
            <p>Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

            _logger.LogInformation("[SendWelcomeEmailAsync] SMTP client ready, sending email to {ToEmail}...", toEmail);

            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                Timeout = 15000
            };

            var message = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
            _logger.LogInformation("[SendWelcomeEmailAsync] SUCCESS: Welcome email sent to {ToEmail}", toEmail);
            return true;
        }
        catch (SmtpException smtpEx)
        {
            _logger.LogError(smtpEx, "[SendWelcomeEmailAsync] SMTP ERROR sending to {ToEmail}: {Code} - {Message}",
                toEmail, smtpEx.StatusCode, smtpEx.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SendWelcomeEmailAsync] GENERAL ERROR sending welcome email to {ToEmail}: {Message}", toEmail, ex.Message);
            return false;
        }
    }

    public async Task<bool> SendRoleAssignedEmailAsync(
        string toEmail, string fullName, string roleName, bool accountActivated = false)
    {
        _logger.LogInformation(
            "[SendRoleAssignedEmailAsync] Attempting to send role assignment email to {ToEmail} "
                + "for {FullName} with role {Role}, accountActivated={Activated}",
            toEmail, fullName, roleName, accountActivated);

        if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
        {
            _logger.LogError(
                "[SendRoleAssignedEmailAsync] FAIL: Email config missing. "
                + "Username: {Username}, Password empty: {IsEmpty}",
                _smtpUsername ?? "NULL", string.IsNullOrEmpty(_smtpPassword));
            return false;
        }

        string roleDisplayName = roleName;
        string roleDescription = "";

        switch (roleName.ToLower())
        {
            case "student":
                roleDisplayName = "Student";
                roleDescription = "You can access the case library, take quizzes, and ask questions about clinical cases.";
                break;
            case "lecturer":
                roleDisplayName = "Lecturer";
                roleDescription = "You can manage classes, create quizzes, and monitor student progress.";
                break;
            case "expert":
                roleDisplayName = "Expert";
                roleDescription = "You can review and respond to student questions about clinical cases.";
                break;
            case "admin":
                roleDisplayName = "Administrator";
                roleDescription = "You have full access to system administration, user management, and content.";
                break;
            default:
                roleDescription = $"Your assigned role is: {roleName}.";
                break;
        }

        string statusLine = accountActivated
            ? "Your account has been <strong>approved and activated</strong> by the administrator."
            : "Your role has been updated by the administrator.";

        string loginLine = accountActivated
            ? "You can now log in to BoneVisQA with your email and start using the platform."
            : "Your account remains active — you can continue using BoneVisQA as usual.";

        try
        {
            var subject = accountActivated
                ? "BoneVisQA - Your account has been approved!"
                : $"BoneVisQA - Role updated: {roleDisplayName}";

            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: {(accountActivated ? "#27ae60" : "#2980b9")}; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; background-color: #f9f9f9; }}
        .status-box {{ background-color: #d4edda; border: 1px solid #c3e6cb; padding: 20px; border-radius: 10px; margin: 20px 0; }}
        .role-badge {{ display: inline-block; background-color: {(accountActivated ? "#27ae60" : "#2980b9")}; color: white; padding: 10px 25px; border-radius: 20px; font-size: 18px; font-weight: bold; margin: 15px 0; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
        .features {{ background-color: #e8f4f8; padding: 15px; border-radius: 5px; margin: 15px 0; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>BoneVisQA</h1>
        </div>
        <div class='content'>
            <h2>Hello, {fullName}!</h2>
            <p>{statusLine}</p>

            <div class='status-box' style='text-align: center;'>
                <p><strong>Your role:</strong></p>
                <div class='role-badge'>{roleDisplayName}</div>
            </div>

            <div class='features'>
                <p><strong>What you can do:</strong></p>
                <p>{roleDescription}</p>
            </div>

            <p>{loginLine}</p>

            <p>If you have any questions, please contact us.</p>
            <p>Best regards,<br>The BoneVisQA Team</p>
        </div>
        <div class='footer'>
            <p>This is an automated email from the BoneVisQA system.</p>
            <p>Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

            _logger.LogInformation(
                "[SendRoleAssignedEmailAsync] SMTP config OK — connecting to {Host}:{Port}",
                _smtpHost, _smtpPort);

            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                Timeout = 15000
            };

            var message = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
            _logger.LogInformation(
                "[SendRoleAssignedEmailAsync] SUCCESS: Role assignment email sent to {ToEmail}", toEmail);
            return true;
        }
        catch (SmtpException smtpEx)
        {
            _logger.LogError(smtpEx,
                "[SendRoleAssignedEmailAsync] SMTP ERROR sending to {ToEmail}: {Code} - {Message}",
                toEmail, smtpEx.StatusCode, smtpEx.Message);
            return false;
        }
        catch (Exception ex)
        {
                _logger.LogError(ex,
                "[SendRoleAssignedEmailAsync] GENERAL ERROR sending role assignment email to {ToEmail}: {Message}",
                toEmail, ex.Message);
            return false;
        }
    }

    // ── Account activated by admin (without role change) ───────────────────
    public async Task<bool> SendAccountActivatedEmailAsync(string toEmail, string fullName)
    {
        _logger.LogInformation("[SendAccountActivatedEmailAsync] Sending to {ToEmail}", toEmail);

        if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
        {
            _logger.LogError("[SendAccountActivatedEmailAsync] FAIL: SMTP credentials not configured.");
            return false;
        }

        try
        {
            var subject = "BoneVisQA - Your account has been approved!";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #27ae60; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; background-color: #f9f9f9; }}
        .status-box {{ background-color: #d4edda; border: 1px solid #c3e6cb; padding: 20px; border-radius: 10px; margin: 20px 0; text-align: center; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>BoneVisQA</h1>
        </div>
        <div class='content'>
            <h2>Hello, {fullName}!</h2>
            <p>Great news — your BoneVisQA account has been <strong>approved and activated</strong> by the administrator.</p>
            <div class='status-box'>
                <p>Your account is now active.</p>
            </div>
            <p>You can now log in to BoneVisQA with your email and start using the platform.</p>
            <p>If you have any questions, please contact us.</p>
            <p>Best regards,<br>The BoneVisQA Team</p>
        </div>
        <div class='footer'>
            <p>This is an automated email from BoneVisQA.</p>
            <p>Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                Timeout = 15000
            };

            var message = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
            _logger.LogInformation("[SendAccountActivatedEmailAsync] SUCCESS: sent to {ToEmail}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SendAccountActivatedEmailAsync] ERROR sending to {ToEmail}: {Message}", toEmail, ex.Message);
            return false;
        }
    }

    // ── Account deactivated by admin ──────────────────────────────────────
    public async Task<bool> SendAccountDeactivatedEmailAsync(string toEmail, string fullName)
    {
        _logger.LogInformation("[SendAccountDeactivatedEmailAsync] Sending to {ToEmail}", toEmail);

        if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
        {
            _logger.LogError("[SendAccountDeactivatedEmailAsync] FAIL: SMTP credentials not configured.");
            return false;
        }

        try
        {
            var subject = "BoneVisQA - Account Deactivated";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #c0392b; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; background-color: #f9f9f9; }}
        .status-box {{ background-color: #f8d7da; border: 1px solid #f5c6cb; padding: 20px; border-radius: 10px; margin: 20px 0; text-align: center; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>BoneVisQA</h1>
        </div>
        <div class='content'>
            <h2>Hello, {fullName}!</h2>
            <p>Your BoneVisQA account has been <strong>deactivated</strong> by the administrator.</p>
            <div class='status-box'>
                <p>You no longer have access to the platform.</p>
            </div>
            <p>If you believe this was a mistake, please contact the administrator for assistance.</p>
            <p>Best regards,<br>The BoneVisQA Team</p>
        </div>
        <div class='footer'>
            <p>This is an automated email from BoneVisQA.</p>
            <p>Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                Timeout = 15000
            };

            var message = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
            _logger.LogInformation("[SendAccountDeactivatedEmailAsync] SUCCESS: sent to {ToEmail}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SendAccountDeactivatedEmailAsync] ERROR sending to {ToEmail}: {Message}", toEmail, ex.Message);
            return false;
        }
    }

    // ── Medical Student Verification Emails ────────────────────────────────────

    public async Task<bool> SendMedicalVerificationRequestedEmailAsync(string toEmail, string fullName)
    {
        _logger.LogInformation("[SendMedicalVerificationRequestedEmailAsync] Sending to {ToEmail}", toEmail);

        if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
        {
            _logger.LogError("[SendMedicalVerificationRequestedEmailAsync] FAIL: SMTP credentials not configured.");
            return false;
        }

        try
        {
            var subject = "BoneVisQA - Medical student verification request submitted";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #2980b9; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; background-color: #f9f9f9; }}
        .status-box {{ background-color: #d6eaf8; border: 1px solid #aed6f1; padding: 20px; border-radius: 10px; margin: 20px 0; text-align: center; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>BoneVisQA</h1>
        </div>
        <div class='content'>
            <h2>Hello, {fullName}!</h2>
            <p>We have received your <strong>medical student</strong> verification request.</p>
            <div class='status-box'>
                <p><strong>Pending review</strong></p>
                <p>Your request is being reviewed by the admin team.</p>
            </div>
            <p>You will receive an email notification when your account is approved.</p>
            <p>Best regards,<br>The BoneVisQA Team</p>
        </div>
        <div class='footer'>
            <p>This email was sent automatically by the BoneVisQA system.</p>
            <p>Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                Timeout = 15000
            };

            var message = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
            _logger.LogInformation("[SendMedicalVerificationRequestedEmailAsync] SUCCESS: sent to {ToEmail}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SendMedicalVerificationRequestedEmailAsync] ERROR sending to {ToEmail}: {Message}", toEmail, ex.Message);
            return false;
        }
    }

    public async Task<bool> SendMedicalVerificationApprovedEmailAsync(string toEmail, string fullName)
    {
        _logger.LogInformation("[SendMedicalVerificationApprovedEmailAsync] Sending to {ToEmail}", toEmail);

        if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
        {
            _logger.LogError("[SendMedicalVerificationApprovedEmailAsync] FAIL: SMTP credentials not configured.");
            return false;
        }

        try
        {
            var subject = "BoneVisQA - Medical Student Verification Approved";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #27ae60; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; background-color: #f9f9f9; }}
        .status-box {{ background-color: #d4edda; border: 1px solid #c3e6cb; padding: 20px; border-radius: 10px; margin: 20px 0; text-align: center; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>BoneVisQA</h1>
        </div>
        <div class='content'>
            <h2>Hello, {fullName}!</h2>
            <p>Congratulations! Your <strong>medical student</strong> information has been <strong>approved</strong>.</p>
            <div class='status-box'>
                <p>✓ Medical student verification successful</p>
                <p>Your account has been activated.</p>
            </div>
            <p>You can now log in to BoneVisQA and access medical learning materials.</p>
            <p>If you have any questions, please contact us.</p>
            <p>Best regards,<br>The BoneVisQA Team</p>
        </div>
        <div class='footer'>
            <p>This email was sent automatically by the BoneVisQA system.</p>
            <p>Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                Timeout = 15000
            };

            var message = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
            _logger.LogInformation("[SendMedicalVerificationApprovedEmailAsync] SUCCESS: sent to {ToEmail}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SendMedicalVerificationApprovedEmailAsync] ERROR sending to {ToEmail}: {Message}", toEmail, ex.Message);
            return false;
        }
    }

    public async Task<bool> SendMedicalVerificationRejectedEmailAsync(string toEmail, string fullName, string? reason)
    {
        _logger.LogInformation("[SendMedicalVerificationRejectedEmailAsync] Sending to {ToEmail}", toEmail);

        if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
        {
            _logger.LogError("[SendMedicalVerificationRejectedEmailAsync] FAIL: SMTP credentials not configured.");
            return false;
        }

        string reasonText = string.IsNullOrWhiteSpace(reason)
            ? "Verification information is invalid or incomplete."
            : reason;

        try
        {
            var subject = "BoneVisQA - Medical Student Verification Rejected";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #e74c3c; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; background-color: #f9f9f9; }}
        .status-box {{ background-color: #f8d7da; border: 1px solid #f5c6cb; padding: 20px; border-radius: 10px; margin: 20px 0; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>BoneVisQA</h1>
        </div>
        <div class='content'>
            <h2>Hello, {fullName}!</h2>
            <p>Your <strong>medical student</strong> information has been <strong>rejected</strong>.</p>
            <div class='status-box'>
                <p><strong>Reason:</strong></p>
                <p>{reasonText}</p>
            </div>
            <p>Please contact an administrator for support or resubmit with accurate information.</p>
            <p>Best regards,<br>The BoneVisQA Team</p>
        </div>
        <div class='footer'>
            <p>This email was sent automatically by the BoneVisQA system.</p>
            <p>Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                Timeout = 15000
            };

            var message = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
            _logger.LogInformation("[SendMedicalVerificationRejectedEmailAsync] SUCCESS: sent to {ToEmail}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SendMedicalVerificationRejectedEmailAsync] ERROR sending to {ToEmail}: {Message}", toEmail, ex.Message);
            return false;
        }
    }

    // ── Welcome Email with Role (sent after both verification AND role assignment) ──

    public async Task<bool> SendWelcomeWithRoleEmailAsync(string toEmail, string fullName, string roleName)
    {
        _logger.LogInformation("[SendWelcomeWithRoleEmailAsync] Sending to {ToEmail} for {FullName} with role {Role}",
            toEmail, fullName, roleName);

        if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
        {
            _logger.LogError("[SendWelcomeWithRoleEmailAsync] FAIL: SMTP credentials not configured.");
            return false;
        }

        string roleDisplayName = roleName;
        string roleDescription = "";

        switch (roleName.ToLower())
        {
            case "student":
                roleDisplayName = "Student";
                roleDescription = "You can access the case library, take quizzes, and ask questions about clinical cases.";
                break;
            case "lecturer":
                roleDisplayName = "Lecturer";
                roleDescription = "You can manage classes, create quizzes, and track student learning progress.";
                break;
            case "expert":
                roleDisplayName = "Expert";
                roleDescription = "You can review and answer student questions about clinical cases.";
                break;
            case "admin":
                roleDisplayName = "Administrator";
                roleDescription = "You have full access to the administration system, including user and content management.";
                break;
            default:
                roleDisplayName = roleName;
                roleDescription = $"Your role is: {roleName}.";
                break;
        }

        try
        {
            var subject = "BoneVisQA - Your account has been activated!";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #27ae60; color: white; padding: 20px; text-align: center; }}
        .content {{ padding: 30px; background-color: #f9f9f9; }}
        .status-box {{ background-color: #d4edda; border: 1px solid #c3e6cb; padding: 20px; border-radius: 10px; margin: 20px 0; text-align: center; }}
        .role-badge {{ display: inline-block; background-color: #2980b9; color: white; padding: 10px 25px; border-radius: 20px; font-size: 18px; font-weight: bold; margin: 15px 0; }}
        .features {{ background-color: #e8f4f8; padding: 15px; border-radius: 5px; margin: 15px 0; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>BoneVisQA</h1>
        </div>
        <div class='content'>
            <h2>Hello, {fullName}!</h2>
            <p>Congratulations! Your account has been <strong>activated</strong>.</p>
            <div class='status-box'>
                <p>✓ Medical student verification successful</p>
                <p>✓ Account activated</p>
            </div>
            <p>Your role: <span class='role-badge'>{roleDisplayName}</span></p>
            <div class='features'>
                <h3>Your features:</h3>
                <p>{roleDescription}</p>
            </div>
            <p>You can now log in to BoneVisQA and start using the platform.</p>
            <p>If you have any questions, please contact us.</p>
            <p>Best regards,<br>The BoneVisQA Team</p>
        </div>
        <div class='footer'>
            <p>This email was sent automatically by the BoneVisQA system.</p>
            <p>Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                Timeout = 15000
            };

            var message = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
            _logger.LogInformation("[SendWelcomeWithRoleEmailAsync] SUCCESS: sent to {ToEmail}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SendWelcomeWithRoleEmailAsync] ERROR sending to {ToEmail}: {Message}", toEmail, ex.Message);
            return false;
        }
    }

    // ── Announcement Emails ──────────────────────────────────────────────────────

    public async Task<bool> SendAnnouncementEmailAsync(
        string toEmail,
        string studentName,
        string lecturerName,
        string className,
        string announcementTitle,
        string announcementContent,
        string? assignmentTitle = null,
        string? assignmentType = null,
        DateTime? dueDate = null,
        string? dueDateDisplay = null,
        string? assignmentUrl = null)
    {
        _logger.LogInformation("[SendAnnouncementEmailAsync] Sending announcement email to {ToEmail} from {LecturerName}", toEmail, lecturerName);

        if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
        {
            _logger.LogError("[SendAnnouncementEmailAsync] FAIL: SMTP credentials not configured.");
            return false;
        }

        // Build assignment info if provided
        var hasAssignment = !string.IsNullOrEmpty(assignmentTitle);
        var dueDateText = dueDate.HasValue
            ? $"<strong>{dueDateDisplay ?? dueDate.Value.ToString("dd/MM/yyyy HH:mm")}</strong>"
            : null;

        try
        {
            var subject = $"[BoneVisQA] Thông báo mới từ lớp {className}: {announcementTitle}";
            
            // Build assignment card HTML if assignment is linked
            var assignmentCardHtml = hasAssignment ? $@"
            <div style='margin: 20px 0; background: linear-gradient(135deg, #f8f9fa, #e8f4f8); border-left: 5px solid #1a5f7a; padding: 20px; border-radius: 0 8px 8px 0;'>
                <p style='margin: 0 0 8px; font-size: 11px; color: #888; text-transform: uppercase; letter-spacing: 1px; font-weight: bold;'>
                    Bài tập liên kết
                </p>
                <div style='display: flex; align-items: center; gap: 10px; margin-bottom: 12px;'>
                    <span style='display: inline-block; background: #f39c12; color: white; padding: 4px 12px; border-radius: 12px; font-size: 11px; font-weight: bold; text-transform: uppercase;'>
                        {assignmentType ?? "assignment"}
                    </span>
                    <strong style='font-size: 16px; color: #2c3e50;'>{assignmentTitle}</strong>
                </div>
                " + (dueDateText != null ? $@"
                <div style='background: #fff3cd; border: 1px solid #ffc107; padding: 8px 12px; border-radius: 6px; font-size: 13px; color: #856404;'>
                    Hạn nộp: {dueDateText}
                </div>" : "") + @"
            </div>" : "";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{subject}</title>
</head>
<body style='margin:0;padding:0;background:#f4f4f4;font-family:Segoe UI,Tahoma,Geneva,Verdana,sans-serif;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background:#f4f4f4;padding:30px 0;'>
        <tr>
            <td align='center'>
                <table width='600' cellpadding='0' cellspacing='0' style='background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.1);'>
                    <!-- Header -->
                    <tr>
                        <td style='background:linear-gradient(135deg,#1a5f7a,#2c3e50);padding:30px 40px;text-align:center;'>
                            <div style='display:inline-block;background:#e74c3c;color:white;padding:4px 16px;border-radius:20px;font-size:12px;font-weight:bold;text-transform:uppercase;letter-spacing:1px;margin-bottom:12px;'>
                                Thông báo mới
                            </div>
                            <h1 style='color:#ffffff;margin:10px 0 0;font-size:24px;'>BoneVisQA</h1>
                        </td>
                    </tr>
                    <!-- Content -->
                    <tr>
                        <td style='padding:35px 40px;'>
                            <p style='margin:0 0 20px;font-size:16px;color:#333;'>
                                Xin chào <strong>{studentName}</strong>,
                            </p>
                            <p style='margin:0 0 25px;font-size:14px;color:#555;line-height:1.7;'>
                                Bạn có một thông báo mới từ giảng viên <strong>{lecturerName}</strong> trong lớp <strong>{className}</strong>:
                            </p>
                            
                            <!-- Announcement Card -->
                            <div style='background:#fef9e7;border-left:5px solid #f39c12;padding:20px;border-radius:0 8px 8px 0;margin:20px 0;'>
                                <h2 style='margin:0 0 10px;font-size:18px;font-weight:bold;color:#2c3e50;'>{announcementTitle}</h2>
                                <p style='margin:0;font-size:15px;color:#555;line-height:1.8;white-space:pre-wrap;'>{announcementContent}</p>
                            </div>
                            
                            {assignmentCardHtml}
                            
                            <!-- Meta Info -->
                            <div style='margin-top:25px;padding-top:20px;border-top:1px solid #eee;color:#888;font-size:13px;'>
                                <strong>Giảng viên:</strong> {lecturerName} &nbsp;|&nbsp; <strong>Lớp:</strong> {className}
                            </div>
                            
                            <!-- CTA Button -->
                            <div style='margin-top:25px;text-align:center;'>
                                <a href='{_appUrl}' target='_blank' style='display:inline-block;background:linear-gradient(135deg,#1a5f7a,#2980b9);color:white !important;padding:14px 35px;border-radius:25px;text-decoration:none;font-weight:bold;font-size:15px;'>
                                    Mở BoneVisQA
                                </a>
                            </div>
                            
                            <p style='margin:25px 0 0;font-size:14px;color:#555;line-height:1.7;'>
                                Trân trọng,<br><strong>Đội ngũ BoneVisQA</strong>
                            </p>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style='background:#f8f9fa;padding:20px 40px;border-top:1px solid #eee;'>
                            <p style='margin:0;font-size:12px;color:#aaa;text-align:center;'>
                                Email này được gửi tự động từ BoneVisQA — Radiology Education Platform.<br>
                                Vui lòng không trả lời trực tiếp email này.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

            _logger.LogInformation("[SendAnnouncementEmailAsync] SMTP config OK - connecting to {Host}:{Port}", _smtpHost, _smtpPort);

            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                Timeout = 15000
            };

            var message = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
            _logger.LogInformation("[SendAnnouncementEmailAsync] SUCCESS: sent to {ToEmail}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SendAnnouncementEmailAsync] ERROR sending to {ToEmail}: {Message}", toEmail, ex.Message);
            return false;
        }
    }

    // ── Assignment notification emails ───────────────────────────────────────────

    public async Task<bool> SendAssignmentEmailAsync(
        string toEmail,
        string studentName,
        string className,
        string assignmentTitle,
        string assignmentType,
        DateTime? dueDate,
        string? dueDateDisplay,
        string? description)  // ← thêm parameter
    {
        _logger.LogInformation("[SendAssignmentEmailAsync] Sending assignment email to {ToEmail} - {Title}", toEmail, assignmentTitle);

        if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
        {
            _logger.LogError("[SendAssignmentEmailAsync] FAIL: SMTP credentials not configured.");
            return false;
        }

        var dueDateText = dueDate.HasValue
            ? $"Due date: <strong>{dueDateDisplay ?? dueDate.Value.ToString("dd/MM/yyyy HH:mm")}</strong>"
            : "No specific due date";

        var subject = $"[BoneVisQA] New assignment: {assignmentTitle} ({assignmentType})";
        var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #1a5f7a, #2c3e50); color: white; padding: 30px; text-align: center; border-radius: 8px 8px 0 0; }}
        .header h1 {{ margin: 0; font-size: 22px; }}
        .type-badge {{ display: inline-block; background-color: #f39c12; color: white; padding: 4px 16px; border-radius: 20px; font-size: 12px; font-weight: bold; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 10px; }}
        .content {{ padding: 30px; background-color: #ffffff; }}
        .assignment-card {{ background: linear-gradient(135deg, #f8f9fa, #e8f4f8); border-left: 5px solid #1a5f7a; padding: 20px; border-radius: 0 8px 8px 0; margin: 20px 0; }}
        .assignment-title {{ font-size: 18px; font-weight: bold; color: #2c3e50; margin-bottom: 5px; }}
        .assignment-type {{ font-size: 13px; color: #888; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 15px; }}
        .assignment-description {{ font-size: 14px; color: #555; line-height: 1.6; margin-top: 12px; padding: 12px; background: #f8f9fa; border-left: 3px solid #1a5f7a; }}
        .due-date {{ background-color: #fff3cd; border: 1px solid #ffc107; padding: 10px 15px; border-radius: 6px; margin: 15px 0; font-size: 14px; color: #856404; }}
        .cta-button {{ display: inline-block; background: linear-gradient(135deg, #1a5f7a, #2980b9); color: white !important; padding: 12px 30px; border-radius: 25px; text-decoration: none; font-weight: bold; margin: 15px 0; }}
        .meta {{ color: #888; font-size: 13px; margin-top: 15px; padding-top: 15px; border-top: 1px solid #eee; }}
        .footer {{ padding: 20px; text-align: center; font-size: 12px; color: #666; background-color: #f5f5f5; border-radius: 0 0 8px 8px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <div class='type-badge'>{assignmentType}</div>
            <h1>BoneVisQA</h1>
        </div>
        <div class='content'>
            <p>Hello <strong>{studentName}</strong>,</p>
            <p>A lecturer has assigned a new task to class <strong>{className}</strong>:</p>
            <div class='assignment-card'>
                <div class='assignment-type'>{assignmentType}</div>
                <div class='assignment-title'>{assignmentTitle}</div>
                {(string.IsNullOrEmpty(description) ? "" : $@"
                <div class='assignment-description'>{description}</div>
                ")}
                <div class='due-date'>⏰ {dueDateText}</div>
            </div>
            <p>Log in to <strong>BoneVisQA</strong> to view details and complete the assignment.</p>
            <p>Best regards,<br>The BoneVisQA Team</p>
        </div>
        <div class='footer'>
            <p>This email was sent automatically by the BoneVisQA system.</p>
            <p>Please do not reply directly to this email.</p>
        </div>
    </div>
</body>
</html>";

        try
        {
            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                Timeout = 15000
            };

            var message = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
            _logger.LogInformation("[SendAssignmentEmailAsync] SUCCESS: sent to {ToEmail}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SendAssignmentEmailAsync] ERROR sending to {ToEmail}: {Message}", toEmail, ex.Message);
            return false;
        }
    }

    public async Task<bool> SendAssignmentUpdateEmailAsync(
        string toEmail,
        string studentName,
        string className,
        string assignmentTitle,
        string assignmentType,
        DateTime? dueDate,
        string? dueDateDisplay)
    {
        _logger.LogInformation("[SendAssignmentUpdateEmailAsync] Sending assignment update email to {ToEmail} - {Title}", toEmail, assignmentTitle);

        if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
        {
            _logger.LogError("[SendAssignmentUpdateEmailAsync] FAIL: SMTP credentials not configured.");
            return false;
        }

        var dueDateText = dueDate.HasValue
            ? $"Due date: <strong>{dueDateDisplay ?? dueDate.Value.ToString("dd/MM/yyyy HH:mm")}</strong>"
            : "No specific due date";

        var subject = $"[BoneVisQA] Assignment update: {assignmentTitle} ({assignmentType})";
        var body = $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{subject}</title>
</head>
<body style='margin:0;padding:0;background:#f4f4f4;font-family:Segoe UI,Tahoma,Geneva,Verdana,sans-serif;'>
    <table width='100%' cellpadding='0' cellspacing='0' style='background:#f4f4f4;padding:30px 0;'>
        <tr>
            <td align='center'>
                <table width='600' cellpadding='0' cellspacing='0' style='background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.1);'>
                    <tr>
                        <td style='background:linear-gradient(135deg,#e74c3c,#c0392b);padding:30px 40px;text-align:center;'>
                            <h1 style='color:#ffffff;margin:0;font-size:24px;'>🔔 Assignment Updated</h1>
                            <p style='color:rgba(255,255,255,0.85);margin:8px 0 0;font-size:14px;'>Assignment details have been updated</p>
                        </td>
                    </tr>
                    <tr>
                        <td style='padding:35px 40px;'>
                            <p style='margin:0 0 20px;font-size:16px;color:#333;'>
                                Hello <strong>{studentName}</strong>,
                            </p>
                            <p style='margin:0 0 25px;font-size:14px;color:#555;line-height:1.7;'>
                                A lecturer has <strong style='color:#e74c3c;'>updated</strong> the assignment below in class <strong>{className}</strong>. Please review the changes.
                            </p>
                            <div style='background:#f8f9fa;border-left:4px solid #e74c3c;padding:20px;border-radius:0 8px 8px 0;margin:0 0 25px;'>
                                <h2 style='margin:0 0 12px;font-size:18px;color:#2c3e50;'>{assignmentTitle}</h2>
                                <p style='margin:4px 0;font-size:13px;color:#555;'><strong>Type:</strong> {assignmentType}</p>
                                <p style='margin:4px 0;font-size:13px;color:#555;'><strong>Class:</strong> {className}</p>
                                <p style='margin:4px 0;font-size:13px;color:#555;'>{dueDateText}</p>
                            </div>
                            <p style='margin:0 0 25px;font-size:14px;color:#555;line-height:1.7;'>
                                Log in to BoneVisQA to view details and complete the assignment.
                            </p>
                            <table cellpadding='0' cellspacing='0'>
                                <tr>
                                    <td style='background:linear-gradient(135deg,#e74c3c,#c0392b);border-radius:8px;'>
                                        <a href='{_appUrl}' target='_blank' style='display:inline-block;padding:14px 35px;color:#ffffff;font-size:15px;font-weight:bold;text-decoration:none;border-radius:8px;'>
                                            Open BoneVisQA →
                                        </a>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    <tr>
                        <td style='background:#f8f9fa;padding:20px 40px;border-top:1px solid #eee;'>
                            <p style='margin:0;font-size:12px;color:#aaa;text-align:center;'>
                                This email was sent automatically by BoneVisQA — Radiology Education Platform.<br>
                                Please do not reply directly to this email.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";

        try
        {
            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                Timeout = 15000
            };

            var message = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
            _logger.LogInformation("[SendAssignmentUpdateEmailAsync] SUCCESS: sent to {ToEmail}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SendAssignmentUpdateEmailAsync] ERROR sending to {ToEmail}: {Message}", toEmail, ex.Message);
            return false;
        }
    }

    // ── Retake Request Email ─────────────────────────────────────────────────────

    public async Task<bool> SendRetakeRequestEmailAsync(
        string toEmail,
        string studentName,
        string quizTitle,
        string className,
        string lecturerName)
    {
        _logger.LogInformation("[SendRetakeRequestEmailAsync] Sending to {ToEmail}", toEmail);

        if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
        {
            _logger.LogError("[SendRetakeRequestEmailAsync] FAIL: SMTP credentials not configured.");
            return false;
        }

        var subject = $"[BoneVisQA] Retake Request — {studentName}";

        var body = $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px; }}
        .container {{ max-width: 600px; margin: 0 auto; background: #ffffff; border-radius: 12px; overflow: hidden;
                     box-shadow: 0 2px 8px rgba(0,0,0,0.1); }}
        .header {{ background: linear-gradient(135deg, #00478d, #006a68); color: white; padding: 24px 32px; }}
        .header h1 {{ margin: 0; font-size: 20px; }}
        .content {{ padding: 24px 32px; color: #333; }}
        .content p {{ margin: 0 0 14px; line-height: 1.6; }}
        .highlight {{ background: #fff3cd; border-left: 4px solid #ffc107;
                     padding: 12px 16px; border-radius: 6px; margin: 16px 0; }}
        .highlight strong {{ color: #856404; }}
        .action-btn {{ display: inline-block; background: linear-gradient(135deg, #00478d, #006a68);
                       color: white !important; padding: 12px 28px; border-radius: 8px;
                       text-decoration: none; font-weight: bold; margin-top: 8px; }}
        .footer {{ padding: 16px 32px; background: #f9f9f9; color: #888; font-size: 12px;
                   text-align: center; border-top: 1px solid #eee; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Quiz Retake Request</h1>
        </div>
        <div class='content'>
            <p>Dear <strong>{lecturerName}</strong>,</p>
            <p>Student <strong>{studentName}</strong> has requested a retake for a quiz.</p>

            <div class='highlight'>
                <p><strong>Quiz:</strong> {quizTitle}</p>
                <p><strong>Class:</strong> {className}</p>
            </div>

            <p>Please log in to BoneVisQA and enable <strong>Retake</strong> for this student
               from <strong>Quizzes → Results</strong>.</p>
        </div>
        <div class='footer'>
            <p>This is an automated email from BoneVisQA — please do not reply to this message.</p>
        </div>
    </div>
</body>
</html>";

        try
        {
            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                Timeout = 15000
            };

            var message = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
            _logger.LogInformation("[SendRetakeRequestEmailAsync] SUCCESS: sent to {ToEmail}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SendRetakeRequestEmailAsync] ERROR sending to {ToEmail}: {Message}", toEmail, ex.Message);
            return false;
        }
    }

    // ── Essay Submission Notification ──────────────────────────────────────────────

    public async Task<bool> SendEssaySubmittedNotificationAsync(
        string toEmail,
        string lecturerName,
        string studentName,
        string quizTitle,
        string className,
        string attemptDetailUrl)
    {
        _logger.LogInformation("[SendEssaySubmittedNotificationAsync] Sending to {ToEmail}", toEmail);

        if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
        {
            _logger.LogError("[SendEssaySubmittedNotificationAsync] FAIL: SMTP credentials not configured.");
            return false;
        }

        var subject = $"[BoneVisQA] Essay Submission Pending — {quizTitle}";

        var body = $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{subject}</title>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 20px; }}
        .container {{ max-width: 600px; margin: 0 auto; background: #ffffff; border-radius: 12px; overflow: hidden;
                     box-shadow: 0 2px 8px rgba(0,0,0,0.1); }}
        .header {{ background: linear-gradient(135deg, #667eea, #764ba2); color: white; padding: 24px 32px; }}
        .header h1 {{ margin: 0; font-size: 20px; }}
        .content {{ padding: 24px 32px; color: #333; }}
        .content p {{ margin: 0 0 14px; line-height: 1.6; }}
        .highlight {{ background: #fff3cd; border-left: 4px solid #ffc107;
                     padding: 12px 16px; border-radius: 6px; margin: 16px 0; }}
        .highlight strong {{ color: #856404; }}
        .action-btn {{ display: inline-block; background: linear-gradient(135deg, #667eea, #764ba2);
                       color: white !important; padding: 12px 28px; border-radius: 8px;
                       text-decoration: none; font-weight: bold; margin-top: 8px; }}
        .footer {{ padding: 16px 32px; background: #f9f9f9; color: #888; font-size: 12px;
                   text-align: center; border-top: 1px solid #eee; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>📝 Essay Submission Requires Grading</h1>
        </div>
        <div class='content'>
            <p>Dear <strong>{lecturerName}</strong>,</p>
            <p>Student <strong>{studentName}</strong> has submitted an essay answer for the quiz <strong>{quizTitle}</strong> in class <strong>{className}</strong>.</p>
            <p>The essay requires your review and grading.</p>

            <div class='highlight'>
                <p><strong>Quiz:</strong> {quizTitle}</p>
                <p><strong>Class:</strong> {className}</p>
                <p><strong>Student:</strong> {studentName}</p>
            </div>

            <p>Please log in to BoneVisQA to grade the essay submission:</p>
            <p>
                <a href='{attemptDetailUrl}' class='action-btn'>Grade Essay Submission</a>
            </p>
        </div>
        <div class='footer'>
            <p>This is an automated email from BoneVisQA — please do not reply to this message.</p>
        </div>
    </div>
</body>
</html>";

        try
        {
            using var client = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_smtpUsername, _smtpPassword),
                Timeout = 15000
            };

            var message = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            await client.SendMailAsync(message);
            _logger.LogInformation("[SendEssaySubmittedNotificationAsync] SUCCESS: sent to {ToEmail}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SendEssaySubmittedNotificationAsync] ERROR sending to {ToEmail}: {Message}", toEmail, ex.Message);
            return false;
        }
    }
}
