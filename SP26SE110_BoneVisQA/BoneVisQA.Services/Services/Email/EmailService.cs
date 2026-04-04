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

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;

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
        _smtpPassword = smtpPassword ?? "";
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
            var subject = "Đặt lại mật khẩu - BoneVisQA";
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
            <h2>Yêu cầu đặt lại mật khẩu</h2>
            <p>Xin chào,</p>
            <p>Chúng tôi đã nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn.</p>
            <p>Vui lòng nhấn vào nút bên dưới để đặt lại mật khẩu:</p>
            <p style='text-align: center;'>
                <a href='{resetLink}' class='button'>Đặt lại mật khẩu</a>
            </p>
            <p>Hoặc sao chép và dán đường link sau vào trình duyệt:</p>
            <p style='word-break: break-all; color: #3498db;'>{resetLink}</p>
            <div class='warning'>
                <strong>Lưu ý:</strong> Link đặt lại mật khẩu sẽ hết hạn sau 1 giờ. Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này.
            </div>
            <p>Trân trọng,<br>Đội ngũ BoneVisQA</p>
        </div>
        <div class='footer'>
            <p>Email này được gửi tự động từ hệ thống BoneVisQA.</p>
            <p>Không trả lời email này.</p>
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
            var subject = "Chào mừng đến với BoneVisQA";
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
            <h2>Chào mừng {fullName}!</h2>
            <p>Xin chào {fullName},</p>
            <p>Cảm ơn bạn đã đăng ký tài khoản tại <strong>BoneVisQA</strong>.</p>
            <div class='success'>
                <p><strong>Tài khoản của bạn đang chờ duyệt.</strong></p>
                <p>Vui lòng đợi admin xác nhận để có thể đăng nhập và sử dụng hệ thống.</p>
            </div>
            <p>Nếu bạn có bất kỳ câu hỏi nào, vui lòng liên hệ với chúng tôi.</p>
            <p>Trân trọng,<br>Đội ngũ BoneVisQA</p>
        </div>
        <div class='footer'>
            <p>Email này được gửi tự động từ hệ thống BoneVisQA.</p>
            <p>Không trả lời email này.</p>
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
            <h2>Xin chào, {fullName}!</h2>
            <p>Chúc mừng! Thông tin <strong>sinh viên y khoa</strong> của bạn đã được <strong>phê duyệt</strong>.</p>
            <div class='status-box'>
                <p>✓ Xác minh sinh viên y khoa thành công</p>
                <p>Tài khoản của bạn đã được kích hoạt.</p>
            </div>
            <p>Bây giờ bạn có thể đăng nhập vào BoneVisQA và truy cập các tài liệu y khoa.</p>
            <p>Nếu bạn có câu hỏi nào, vui lòng liên hệ với chúng tôi.</p>
            <p>Trân trọng,<br>Đội ngũ BoneVisQA</p>
        </div>
        <div class='footer'>
            <p>Email này được gửi tự động từ hệ thống BoneVisQA.</p>
            <p>Không trả lời email này.</p>
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
            ? "Thông tin xác minh không hợp lệ hoặc không đầy đủ."
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
            <h2>Xin chào, {fullName}!</h2>
            <p>Thông tin <strong>sinh viên y khoa</strong> của bạn đã bị <strong>từ chối</strong>.</p>
            <div class='status-box'>
                <p><strong>Lý do:</strong></p>
                <p>{reasonText}</p>
            </div>
            <p>Vui lòng liên hệ với quản trị viên để được hỗ trợ hoặc đăng ký lại với thông tin chính xác.</p>
            <p>Trân trọng,<br>Đội ngũ BoneVisQA</p>
        </div>
        <div class='footer'>
            <p>Email này được gửi tự động từ hệ thống BoneVisQA.</p>
            <p>Không trả lời email này.</p>
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
                roleDescription = "Bạn có thể truy cập thư viện ca bệnh, làm bài kiểm tra và đặt câu hỏi về các ca lâm sàng.";
                break;
            case "lecturer":
                roleDisplayName = "Giảng viên";
                roleDescription = "Bạn có thể quản lý lớp học, tạo bài kiểm tra và theo dõi tiến độ học tập của sinh viên.";
                break;
            case "expert":
                roleDisplayName = "Chuyên gia";
                roleDescription = "Bạn có thể xem xét và trả lời câu hỏi của sinh viên về các ca lâm sàng.";
                break;
            case "admin":
                roleDisplayName = "Quản trị viên";
                roleDescription = "Bạn có toàn quyền truy cập hệ thống quản trị, quản lý người dùng và nội dung.";
                break;
            default:
                roleDisplayName = roleName;
                roleDescription = $"Vai trò của bạn là: {roleName}.";
                break;
        }

        try
        {
            var subject = "BoneVisQA - Tài khoản đã được kích hoạt!";
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
            <h2>Xin chào, {fullName}!</h2>
            <p>Chúc mừng! Tài khoản của bạn đã được <strong>kích hoạt</strong>.</p>
            <div class='status-box'>
                <p>✓ Xác minh sinh viên y khoa thành công</p>
                <p>✓ Tài khoản đã được kích hoạt</p>
            </div>
            <p>Vai trò của bạn: <span class='role-badge'>{roleDisplayName}</span></p>
            <div class='features'>
                <h3>Tính năng của bạn:</h3>
                <p>{roleDescription}</p>
            </div>
            <p>Bây giờ bạn có thể đăng nhập vào BoneVisQA và bắt đầu sử dụng.</p>
            <p>Nếu bạn có câu hỏi nào, vui lòng liên hệ với chúng tôi.</p>
            <p>Trân trọng,<br>Đội ngũ BoneVisQA</p>
        </div>
        <div class='footer'>
            <p>Email này được gửi tự động từ hệ thống BoneVisQA.</p>
            <p>Không trả lời email này.</p>
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
}
