using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Interfaces;
using FeedBackApp.Models.DTOs;
using FeedBackApp.Models.Enums;
using FeedBackApp.Models;

namespace FeedBackApp.Services
{
    public class UserService : IUserService
    {
        private readonly IRepository<User> _userRepo;

        public UserService(IRepository<User> userRepo)
        {
            _userRepo = userRepo;
        }

        public async Task<List<UserResponseDto>> GetAllUsersAsync()
        {
            var users = await _userRepo.GetAllAsync();
            return users.Select(u => MapToDto(u)).ToList();
        }

        public async Task<UserResponseDto> GetUserByIdAsync(int id)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user == null)
                throw new NotFoundException($"User with ID {id} not found.");

            return MapToDto(user);
        }

        public async Task<UserResponseDto> UpdateUserRoleAsync(int id, UpdateUserRoleDto dto)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user == null)
                throw new NotFoundException($"User with ID {id} not found.");

            if (!Enum.TryParse<UserRole>(dto.Role, true, out var role))
                throw new BadRequestException($"Invalid role '{dto.Role}'. Valid roles: Admin, Creator, Respondent.");

            user.Role = role;
            _userRepo.Update(user);
            await _userRepo.SaveChangesAsync();

            return MapToDto(user);
        }

        public async Task DeleteUserAsync(int id)
        {
            var user = await _userRepo.GetByIdAsync(id);
            if (user == null)
                throw new NotFoundException($"User with ID {id} not found.");

            _userRepo.Remove(user);
            await _userRepo.SaveChangesAsync();
        }

        private static UserResponseDto MapToDto(User user)
        {
            return new UserResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt,
                SurveyCount = user.Surveys?.Count ?? 0,
                ResponseCount = user.Responses?.Count ?? 0
            };
        }
    }
}
