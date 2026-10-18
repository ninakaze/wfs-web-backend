using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Server_WFS.Application.Interfaces;
using Server_WFS.Domain.Constants;
using Server_WFS.Domain.Exceptions;
using Server_WFS.Infrastructure.Configurations;

namespace Server_WFS.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
    {
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    public async Task SendOtpEmailAsync(
        string toEmail, 
        string toName, 
        string otpCode, 
        string otpType, 
        CancellationToken ct = default)
    {
        string subject = otpType switch
        {
            OtpTypes.Registration => "Xác thực tài khoản của bạn - Mã OTP",
            OtpTypes.ForgotPassword => "Yêu cầu đặt lại mật khẩu - Mã OTP",
            _ => "Mã xác thực OTP của bạn"
        };

        string actionTitle = otpType switch
        {
            OtpTypes.Registration => "Xác Thực Đăng Ký Tài Khoản",
            OtpTypes.ForgotPassword => "Đặt Lại Mật Khẩu",
            _ => "Xác Thực Bảo Mật"
        };

        string actionDescription = otpType switch
        {
            OtpTypes.Registration => "Cảm ơn bạn đã đăng ký tài khoản. Vui lòng nhập mã OTP dưới đây để hoàn tất quá trình kích hoạt tài khoản của bạn:",
            OtpTypes.ForgotPassword => "Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. Nhập mã OTP dưới đây để tiếp tục:",
            _ => "Vui lòng nhập mã OTP dưới đây để tiếp tục thao tác:"
        };

        string htmlBody = $@"
<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{subject}</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
            background-color: #f4f6f9;
            margin: 0;
            padding: 20px;
            color: #333333;
        }}
        .container {{
            max-width: 600px;
            margin: 0 auto;
            background-color: #ffffff;
            border-radius: 12px;
            overflow: hidden;
            box-shadow: 0 4px 15px rgba(0, 0, 0, 0.08);
            border: 1px solid #e1e7ec;
        }}
        .header {{
            background: linear-gradient(135deg, #4f46e5 0%, #3b82f6 100%);
            padding: 30px 20px;
            text-align: center;
            color: #ffffff;
        }}
        .header h1 {{
            margin: 0;
            font-size: 24px;
            font-weight: 700;
            letter-spacing: 0.5px;
        }}
        .content {{
            padding: 35px 30px;
        }}
        .greeting {{
            font-size: 16px;
            margin-bottom: 20px;
            color: #4b5563;
        }}
        .otp-box {{
            background-color: #f8fafc;
            border: 2px dashed #6366f1;
            border-radius: 10px;
            padding: 20px;
            text-align: center;
            margin: 30px 0;
        }}
        .otp-code {{
            font-size: 38px;
            font-weight: 800;
            color: #4f46e5;
            letter-spacing: 8px;
            font-family: 'Courier New', Courier, monospace;
            display: inline-block;
        }}
        .expiry-notice {{
            font-size: 13px;
            color: #ef4444;
            margin-top: 10px;
            font-weight: 600;
        }}
        .security-note {{
            background-color: #fef2f2;
            border-left: 4px solid #ef4444;
            padding: 12px 16px;
            margin-top: 25px;
            border-radius: 0 6px 6px 0;
            font-size: 13px;
            color: #991b1b;
        }}
        .footer {{
            background-color: #f9fafb;
            padding: 20px;
            text-align: center;
            font-size: 12px;
            color: #9ca3af;
            border-top: 1px solid #f3f4f6;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>{actionTitle}</h1>
        </div>
        <div class=""content"">
            <div class=""greeting"">
                Xin chào <strong>{toName}</strong>,
            </div>
            <p>{actionDescription}</p>
            
            <div class=""otp-box"">
                <div class=""otp-code"">{otpCode}</div>
                <div class=""expiry-notice"">Mã có hiệu lực trong vòng 5 phút</div>
            </div>

            <div class=""security-note"">
                <strong>Lưu ý bảo mật:</strong> Tuyệt đối không chia sẻ mã này cho bất kỳ ai, kể cả nhân viên hỗ trợ hệ thống.
            </div>
        </div>
        <div class=""footer"">
            Email này được gửi tự động từ hệ thống WFS Backend. Vui lòng không trả lời email này.
        </div>
    </div>
</body>
</html>";

        // Check if mock mode is on or password not set
        if (_emailSettings.UseDevelopmentMock || string.IsNullOrWhiteSpace(_emailSettings.Password))
        {
            _logger.LogWarning(@"
╔══════════════════════════════════════════════════════════════════════╗
║                    [DEV MOCK EMAIL OTP NOTIFICATION]                ║
╠══════════════════════════════════════════════════════════════════════╣
  Người nhận : {ToName} <{ToEmail}>
  Loại OTP   : {OtpType}
  MÃ OTP     : >>> {OtpCode} <<<
  Hiệu lực   : 5 phút
  (Để gửi email thật: cập nhật Email:Password trong appsettings.json)
╚══════════════════════════════════════════════════════════════════════╝",
                toName, toEmail, otpType, otpCode);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.SenderEmail));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlBody
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.SmtpPort, SecureSocketOptions.StartTls, ct);
            await client.AuthenticateAsync(_emailSettings.Username, _emailSettings.Password, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("Đã gửi email OTP thành công tới: {Email}", toEmail);
        }
        catch (Exception ex)
        {
            var isDevelopment = string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);

            _logger.LogError(ex, "Lỗi gửi email OTP qua SMTP tới {Email}: {Msg}", toEmail, ex.Message);

            if (isDevelopment)
            {
                _logger.LogWarning(@"
╔══════════════════════════════════════════════════════════════════════╗
║              [SMTP FALLBACK EMAIL OTP NOTIFICATION]                  ║
╠══════════════════════════════════════════════════════════════════════╣
  Người nhận : {ToName} <{ToEmail}>
  Loại OTP   : {OtpType}
  MÃ OTP     : >>> {OtpCode} <<<
  Hiệu lực   : 5 phút
  Lỗi SMTP   : {ErrorMsg}
╚══════════════════════════════════════════════════════════════════════╝",
                    toName, toEmail, otpType, otpCode, ex.Message);
            }
            else
            {
                throw new ServiceUnavailableException("Không thể gửi email OTP qua máy chủ thư. Vui lòng thử lại sau hoặc liên hệ hỗ trợ kỹ thuật.");
            }
        }
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string toName, CancellationToken ct = default)
    {
        _logger.LogInformation("Gửi email chào mừng thành viên mới: {Email}", toEmail);
        await Task.CompletedTask;
    }
}
