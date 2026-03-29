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

    public async Task<bool> SendRoleAssignedEmailAsync(string toEmail, string fullName, string roleName)
    {
        _logger.LogInformation("[SendRoleAssignedEmailAsync] Attempting to send role assignment email to {ToEmail} for {FullName} with role {Role}", toEmail, fullName, roleName);

        if (string.IsNullOrEmpty(_smtpUsername) || string.IsNullOrEmpty(_smtpPassword))
        {
            _logger.LogError("[SendRoleAssignedEmailAsync] FAIL: Email config missing. Username: {Username}, Password empty: {IsEmpty}",
                _smtpUsername ?? "NULL", string.IsNullOrEmpty(_smtpPassword));
            return false;
        }

        _logger.LogInformation("[SendRoleAssignedEmailAsync] SMTP config OK - connecting to {Host}:{Port}", _smtpHost, _smtpPort);

        string roleDisplayName = roleName;
        string roleDescription = "";

        switch (roleName.ToLower())
        {
            case "student":
                roleDisplayName = "Sinh viên";
                roleDescription = "Bạn có thể truy cập và tham gia các bài kiểm tra, xem ca lâm sàng và đặt câu hỏi.";
                break;
            case "lecturer":
                roleDisplayName = "Giảng viên";
                roleDescription = "Bạn có thể quản lý lớp học, tạo bài kiểm tra, và theo dõi tiến độ học tập của sinh viên.";
                break;
            case "expert":
                roleDisplayName = "Chuyên gia";
                roleDescription = "Bạn có thể xem xét và phản hồi các câu hỏi của sinh viên về ca lâm sàng.";
                break;
            case "admin":
                roleDisplayName = "Quản trị viên";
                roleDescription = "Bạn có toàn quyền quản lý hệ thống, người dùng và nội dung.";
                break;
            default:
                roleDescription = $"Vai trò của bạn hiện là: {roleName}.";
                break;
        }

        try
        {
            var subject = $"BoneVisQA - Tài khoản của bạn đã được phê duyệt!";
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
        .success-box {{ background-color: #d4edda; border: 1px solid #c3e6cb; padding: 20px; border-radius: 10px; margin: 20px 0; }}
        .role-badge {{ display: inline-block; background-color: #3498db; color: white; padding: 10px 25px; border-radius: 20px; font-size: 18px; font-weight: bold; margin: 15px 0; }}
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
            <h2>Xin chúc mừng, {fullName}!</h2>
            <p>Tài khoản của bạn đã được phê duyệt và kích hoạt bởi quản trị viên.</p>
            
            <div class='success-box' style='text-align: center;'>
                <p><strong>Vai trò của bạn:</strong></p>
                <div class='role-badge'>{roleDisplayName}</div>
            </div>
            
            <div class='features'>
                <p><strong>Quyền hạn của bạn:</strong></p>
                <p>{roleDescription}</p>
            </div>
            
            <p>Bạn có thể đăng nhập ngay bây giờ để bắt đầu sử dụng hệ thống.</p>
            
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

            _logger.LogInformation("[SendRoleAssignedEmailAsync] SMTP client ready, sending email to {ToEmail}...", toEmail);

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
            _logger.LogInformation("[SendRoleAssignedEmailAsync] SUCCESS: Role assignment email sent to {ToEmail}", toEmail);
            return true;
        }
        catch (SmtpException smtpEx)
        {
            _logger.LogError(smtpEx, "[SendRoleAssignedEmailAsync] SMTP ERROR sending to {ToEmail}: {Code} - {Message}",
                toEmail, smtpEx.StatusCode, smtpEx.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SendRoleAssignedEmailAsync] GENERAL ERROR sending role assignment email to {ToEmail}: {Message}", toEmail, ex.Message);
            return false;
        }
    }
}
