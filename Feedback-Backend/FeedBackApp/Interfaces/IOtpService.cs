using FeedBackApp.Models.DTOs;

namespace FeedBackApp.Interfaces
{
    public interface IOtpService
    {
        Task SendOtpAsync(SendOtpDto dto);
        Task<OtpVerifiedDto> VerifyOtpAsync(VerifyOtpDto dto);
    }
}
