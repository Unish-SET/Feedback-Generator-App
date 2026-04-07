using FeedBackApp.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace FeedBackApp.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        public EmailService(IConfiguration config) => _config = config;

        private async Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            var smtp = _config.GetSection("SmtpSettings");
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(smtp["FromName"], smtp["FromEmail"] ?? smtp["Username"]!));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            await client.ConnectAsync(smtp["Host"], int.Parse(smtp["Port"]!), SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtp["Username"], smtp["Password"]);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        public Task SendOtpEmailAsync(string toEmail, string otpCode, string surveyTitle) =>
            SendAsync(toEmail, $"Your OTP for \"{surveyTitle}\"", $"""
                <div style="font-family:sans-serif;max-width:480px;margin:auto;padding:32px">
                  <h2 style="color:#1e293b">Survey Access Code</h2>
                  <p>You have been invited to fill: <strong>{surveyTitle}</strong></p>
                  <div style="font-size:36px;font-weight:700;letter-spacing:12px;color:#6366f1;
                              background:#f1f5f9;padding:20px;border-radius:10px;text-align:center;margin:24px 0">
                    {otpCode}
                  </div>
                  <p style="color:#64748b;font-size:13px">This code expires in 10 minutes. Do not share it.</p>
                </div>
            """);

        public Task SendInviteEmailAsync(string toEmail, string surveyTitle, string surveyUrl) =>
            SendAsync(toEmail, $"You're invited: \"{surveyTitle}\"", $"""
                <div style="font-family:sans-serif;max-width:480px;margin:auto;padding:32px">
                  <h2 style="color:#1e293b">You have been invited!</h2>
                  <p>You are invited to participate in the survey: <strong>{surveyTitle}</strong></p>
                  <a href="{surveyUrl}" style="display:inline-block;margin-top:20px;padding:12px 28px;
                     background:#6366f1;color:#fff;border-radius:8px;text-decoration:none;font-weight:600">
                    Open Survey
                  </a>
                  <p style="color:#64748b;font-size:12px;margin-top:24px">
                    If the button doesn't work, copy this link: {surveyUrl}
                  </p>
                </div>
            """);

        public Task SendAnalyticsReportAsync(string toEmail, string htmlBody, string surveyTitle) =>
            SendAsync(toEmail, $"Analytics Report: \"{surveyTitle}\"", htmlBody);
    }
}
