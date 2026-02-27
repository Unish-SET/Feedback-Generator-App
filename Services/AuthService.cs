using AutoMapper;
using FeedBackGeneratorApp.DTOs;
using FeedBackGeneratorApp.Exceptions;
using FeedBackGeneratorApp.Helpers;
using FeedBackGeneratorApp.Interfaces;
using FeedBackGeneratorApp.Models;

namespace FeedBackGeneratorApp.Services
{
    public class AuthService : IAuthService
    {
        private readonly IRepository<User> _userRepo;
        private readonly IMapper _mapper;
        private readonly JwtHelper _jwtHelper;

        public AuthService(IRepository<User> userRepo, IMapper mapper, JwtHelper jwtHelper)
        {
            _userRepo = userRepo;
            _mapper = mapper;
            _jwtHelper = jwtHelper;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
        {
            var existingUsers = await _userRepo.FindAsync(u => u.Email == dto.Email);
            if (existingUsers.Any())
                throw new ConflictException("A user with this email already exists.");

            var user = _mapper.Map<User>(dto);
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
            user.CreatedAt = DateTime.UtcNow;

            await _userRepo.AddAsync(user);

            var token = _jwtHelper.GenerateToken(user.Id, user.Email, user.Role);

            return new AuthResponseDto
            {
                Token = token,
                User = _mapper.Map<UserResponseDto>(user)
            };
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            var users = await _userRepo.FindAsync(u => u.Email == dto.Email);
            var user = users.FirstOrDefault();

            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                throw new UnauthorizedException("Invalid email or password.");

            var token = _jwtHelper.GenerateToken(user.Id, user.Email, user.Role);

            return new AuthResponseDto
            {
                Token = token,
                User = _mapper.Map<UserResponseDto>(user)
            };
        }

        public async Task<UserResponseDto?> GetUserByIdAsync(int id)
        {
            var user = await _userRepo.GetByIdAsync(id);
            return user == null ? null : _mapper.Map<UserResponseDto>(user);
        }

        public async Task<PagedResult<UserResponseDto>> GetAllUsersAsync(PaginationParams paginationParams)
        {
            var allUsers = await _userRepo.GetAllAsync();
            var query = allUsers.AsQueryable();

            // Filter by search term
            if (!string.IsNullOrWhiteSpace(paginationParams.SearchTerm))
            {
                var search = paginationParams.SearchTerm.ToLower();
                query = query.Where(u => u.FullName.ToLower().Contains(search)
                    || u.Email.ToLower().Contains(search)
                    || u.Role.ToLower().Contains(search));
            }

            // Sort
            query = paginationParams.SortBy?.ToLower() switch
            {
                "name" => paginationParams.SortDescending ? query.OrderByDescending(u => u.FullName) : query.OrderBy(u => u.FullName),
                "email" => paginationParams.SortDescending ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
                "role" => paginationParams.SortDescending ? query.OrderByDescending(u => u.Role) : query.OrderBy(u => u.Role),
                _ => query.OrderByDescending(u => u.CreatedAt)
            };

            var totalCount = query.Count();

            var users = query
                .Skip((paginationParams.PageNumber - 1) * paginationParams.PageSize)
                .Take(paginationParams.PageSize)
                .ToList();

            return new PagedResult<UserResponseDto>
            {
                Items = _mapper.Map<List<UserResponseDto>>(users),
                TotalCount = totalCount,
                PageNumber = paginationParams.PageNumber,
                PageSize = paginationParams.PageSize
            };
        }
    }
}
