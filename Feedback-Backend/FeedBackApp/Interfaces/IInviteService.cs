using FeedBackApp.Models.DTOs;

namespace FeedBackApp.Interfaces
{
    public interface IInviteService
    {
        Task SendInvitesAsync(int surveyId, SendInvitesDto dto, int userId, string role);
        Task<List<SurveyInviteDto>> GetInvitesAsync(int surveyId, int userId, string role);
    }
}
