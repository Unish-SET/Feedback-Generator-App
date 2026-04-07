namespace FeedBackApp.Interfaces
{
    public interface IEmailService
    {
        Task SendOtpEmailAsync(string toEmail, string otpCode, string surveyTitle);
        Task SendInviteEmailAsync(string toEmail, string surveyTitle, string surveyUrl);
        Task SendAnalyticsReportAsync(string toEmail, string htmlBody, string surveyTitle);
    }
}
