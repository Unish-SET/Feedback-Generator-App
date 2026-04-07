using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Models;
using FeedBackApp.Models.DTOs;
using FeedBackApp.Models.Enums;
using System.Security.Cryptography;
using System.Text;

namespace FeedBackApp.Services
{
    public class OtpService : IOtpService
    {
        private readonly IRepository<SurveyOtp>    _otpRepo;
        private readonly IRepository<Survey>       _surveyRepo;
        private readonly IRepository<SurveyInvite> _inviteRepo;
        private readonly IEmailService             _email;
        private readonly IConfiguration            _config;

        public OtpService(
            IRepository<SurveyOtp>    otpRepo,
            IRepository<Survey>       surveyRepo,
            IRepository<SurveyInvite> inviteRepo,
            IEmailService             email,
            IConfiguration            config)
        {
            _otpRepo    = otpRepo;
            _surveyRepo = surveyRepo;
            _inviteRepo = inviteRepo;
            _email      = email;
            _config     = config;
        }

        public async Task SendOtpAsync(SendOtpDto dto)
        {
            var emailLower = dto.Email.Trim().ToLower();

            if (!System.Net.Mail.MailAddress.TryCreate(emailLower, out _))
                throw new BadRequestException("Invalid email address.");

            var survey = await _surveyRepo.FirstOrDefaultAsync(s => s.PublicToken == dto.SurveyPublicToken)
                ?? throw new NotFoundException("Survey not found.");

            if (survey.State != SurveyState.Active)
                throw new BadRequestException("This survey is not currently active.");

            if (!survey.IsInviteOnly)
                throw new BadRequestException("This survey does not require OTP.");

            var invited = await _inviteRepo.AnyAsync(i => i.SurveyId == survey.Id && i.Email == emailLower);
            if (!invited)
                throw new ForbiddenException("This email is not invited to this survey.");

            // Rate limit — max 3 OTP requests per email per 10 minutes
            var windowStart = DateTime.UtcNow.AddMinutes(-10);
            var recentCount = (await _otpRepo.FindAsync(o =>
                o.SurveyId == survey.Id &&
                o.Email    == emailLower &&
                o.CreatedAt >= windowStart)).Count();

            if (recentCount >= 3)
                throw new BadRequestException("Too many OTP requests. Please wait 10 minutes before trying again.");

            // Invalidate old unused OTPs
            var old = await _otpRepo.FindAsync(o => o.SurveyId == survey.Id && o.Email == emailLower && !o.IsUsed);
            foreach (var o in old) _otpRepo.Remove(o);

            var expiryMin = int.Parse(_config["OtpSettings:ExpiryMinutes"] ?? "10");
            var code      = new Random().Next(100000, 999999).ToString();

            await _otpRepo.AddAsync(new SurveyOtp
            {
                SurveyId  = survey.Id,
                Email     = emailLower,
                Code      = code,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMin)
            });
            await _otpRepo.SaveChangesAsync();
            await _email.SendOtpEmailAsync(dto.Email, code, survey.Title);
        }

        public async Task<OtpVerifiedDto> VerifyOtpAsync(VerifyOtpDto dto)
        {
            var emailLower = dto.Email.Trim().ToLower();

            if (!System.Net.Mail.MailAddress.TryCreate(emailLower, out _))
                throw new BadRequestException("Invalid email address.");

            if (string.IsNullOrWhiteSpace(dto.Code) || dto.Code.Length != 6 || !dto.Code.All(char.IsDigit))
                throw new BadRequestException("OTP must be a 6-digit number.");

            var survey = await _surveyRepo.FirstOrDefaultAsync(s => s.PublicToken == dto.SurveyPublicToken)
                ?? throw new NotFoundException("Survey not found.");

            // Brute-force guard — expired-but-unused OTPs in last 15 min = failed attempts
            var windowStart   = DateTime.UtcNow.AddMinutes(-15);
            var failedAttempts = (await _otpRepo.FindAsync(o =>
                o.SurveyId  == survey.Id &&
                o.Email     == emailLower &&
                !o.IsUsed   &&
                o.CreatedAt >= windowStart &&
                o.ExpiresAt  < DateTime.UtcNow)).Count();

            if (failedAttempts >= 5)
                throw new BadRequestException("Too many failed attempts. Please request a new OTP.");

            var otp = await _otpRepo.FirstOrDefaultAsync(o =>
                o.SurveyId == survey.Id &&
                o.Email    == emailLower &&
                o.Code     == dto.Code  &&
                !o.IsUsed);

            if (otp == null)
                throw new BadRequestException("Invalid OTP code.");

            if (otp.ExpiresAt < DateTime.UtcNow)
                throw new BadRequestException("OTP has expired. Please request a new one.");

            otp.IsUsed = true;
            _otpRepo.Update(otp);
            await _otpRepo.SaveChangesAsync();

            var sessionMin = int.Parse(_config["OtpSettings:SessionExpiryMinutes"] ?? "60");
            var expiresAt  = DateTime.UtcNow.AddMinutes(sessionMin);
            var secret     = _config["OtpSettings:SessionSecret"]!;
            var payload    = $"{emailLower}|{survey.Id}|{expiresAt:O}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var sig   = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{payload}|{sig}"));

            return new OtpVerifiedDto { SessionToken = token, ExpiresAt = expiresAt };
        }
    }
}
