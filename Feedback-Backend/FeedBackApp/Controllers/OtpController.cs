using FeedBackApp.Interfaces;
using FeedBackApp.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FeedBackApp.Controllers
{
    [Route("api/otp")]
    [ApiController]
    [AllowAnonymous]
    public class OtpController : ControllerBase
    {
        private readonly IOtpService _otpService;
        public OtpController(IOtpService otpService) => _otpService = otpService;

        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] SendOtpDto dto)
        {
            await _otpService.SendOtpAsync(dto);
            return Ok(new { success = true, message = "OTP sent to your email." });
        }

        [HttpPost("verify")]
        public async Task<IActionResult> Verify([FromBody] VerifyOtpDto dto)
        {
            var result = await _otpService.VerifyOtpAsync(dto);
            return Ok(new { success = true, data = result });
        }
    }
}
