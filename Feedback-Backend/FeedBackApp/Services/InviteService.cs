using FeedBackApp.Exceptions;
using FeedBackApp.Helpers;
using FeedBackApp.Interfaces;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Models;
using FeedBackApp.Models.DTOs;

namespace FeedBackApp.Services
{
    public class InviteService : IInviteService
    {
        private readonly IRepository<Survey>       _surveyRepo;
        private readonly IRepository<SurveyInvite> _inviteRepo;
        private readonly IEmailService             _email;
        private readonly IConfiguration            _config;

        public InviteService(
            IRepository<Survey>       surveyRepo,
            IRepository<SurveyInvite> inviteRepo,
            IEmailService             email,
            IConfiguration            config)
        {
            _surveyRepo = surveyRepo;
            _inviteRepo = inviteRepo;
            _email      = email;
            _config     = config;
        }

        public async Task SendInvitesAsync(int surveyId, SendInvitesDto dto, int userId, string role)
        {
            var survey = await _surveyRepo.GetByIdAsync(surveyId)
                ?? throw new NotFoundException("Survey not found.");

            if (!RoleHelper.IsAdmin(role) && survey.CreatedBy != userId)
                throw new ForbiddenException("You do not own this survey.");

            if (!survey.IsInviteOnly)
                throw new BadRequestException("Enable 'Invite Only' on this survey before adding invites.");

            if (dto.Emails == null || !dto.Emails.Any())
                throw new BadRequestException("Email list cannot be empty.");

            if (dto.Emails.Count > 100)
                throw new BadRequestException("Cannot send more than 100 invites at once.");

            var existingCount = (await _inviteRepo.FindAsync(i => i.SurveyId == surveyId)).Count();
            if (existingCount + dto.Emails.Count > 500)
                throw new BadRequestException($"Survey invite limit is 500. Currently at {existingCount}.");

            var baseUrl   = _config["AppBaseUrl"] ?? "http://localhost:4200";
            var surveyUrl = $"{baseUrl}/survey/{survey.PublicToken}/respond";
            var errors    = new List<string>();

            foreach (var rawEmail in dto.Emails.Distinct())
            {
                var email = rawEmail.Trim().ToLower();

                if (string.IsNullOrWhiteSpace(email) ||
                    !System.Net.Mail.MailAddress.TryCreate(email, out _))
                {
                    errors.Add($"'{rawEmail}' is not a valid email.");
                    continue;
                }

                var alreadyInvited = await _inviteRepo.AnyAsync(i => i.SurveyId == surveyId && i.Email == email);
                if (alreadyInvited) continue;

                await _inviteRepo.AddAsync(new SurveyInvite { SurveyId = surveyId, Email = email });
                await _inviteRepo.SaveChangesAsync();

                try { await _email.SendInviteEmailAsync(email, survey.Title, surveyUrl); }
                catch { errors.Add($"Failed to send email to '{email}'. Invite saved, resend manually."); }
            }

            if (errors.Any())
                throw new BadRequestException($"Completed with issues: {string.Join(" | ", errors)}");
        }

        public async Task<List<SurveyInviteDto>> GetInvitesAsync(int surveyId, int userId, string role)
        {
            var survey = await _surveyRepo.GetByIdAsync(surveyId)
                ?? throw new NotFoundException("Survey not found.");

            if (!RoleHelper.IsAdmin(role) && survey.CreatedBy != userId)
                throw new ForbiddenException("You do not own this survey.");

            var invites = await _inviteRepo.FindAsync(i => i.SurveyId == surveyId);
            return invites.Select(i => new SurveyInviteDto
            {
                Id     = i.Id,
                Email  = i.Email,
                IsUsed = i.IsUsed,
                SentAt = i.SentAt
            }).ToList();
        }
    }
}
