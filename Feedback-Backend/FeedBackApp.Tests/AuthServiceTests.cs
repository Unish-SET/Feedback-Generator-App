using Moq;
using FeedBackApp.Exceptions;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Interfaces;
using FeedBackApp.Models.DTOs;
using FeedBackApp.Models.Enums;
using FeedBackApp.Models;
using FeedBackApp.Services;
using Microsoft.Extensions.Configuration;

namespace FeedBackApp.Tests
{
    [TestFixture]
    public class AuthServiceTests
    {
        private Mock<IRepository<User>> _userRepoMock;
        private Mock<IConfiguration> _configMock;
        private Mock<IConfigurationSection> _jwtSectionMock;
        private AuthService _authService;

        [SetUp]
        public void Setup()
        {
            _userRepoMock = new Mock<IRepository<User>>();
            _configMock = new Mock<IConfiguration>();
            _jwtSectionMock = new Mock<IConfigurationSection>();

            _jwtSectionMock.Setup(s => s["Key"]).Returns("YourSuperSecretKeyThatIsAtLeast32CharactersLong!@#$");
            _jwtSectionMock.Setup(s => s["Issuer"]).Returns("FeedBackApp");
            _jwtSectionMock.Setup(s => s["Audience"]).Returns("FeedBackAppUsers");
            _jwtSectionMock.Setup(s => s["ExpiryMinutes"]).Returns("60");
            _configMock.Setup(c => c.GetSection("JwtSettings")).Returns(_jwtSectionMock.Object);

            _authService = new AuthService(_userRepoMock.Object, _configMock.Object);
        }

        [Test]
        public async Task RegisterAsync_ValidDto_ReturnsAuthResponse()
        {
            var dto = new RegisterDto
            {
                Username = "testuser",
                Email = "test@example.com",
                Password = "password123"
            };

            _userRepoMock.Setup(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
                .ReturnsAsync(false);

            var result = await _authService.RegisterAsync(dto);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Token, Is.Not.Empty);
            _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Once);
            _userRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Test]
        public void RegisterAsync_DuplicateUsername_ThrowsConflictException()
        {
            var dto = new RegisterDto
            {
                Username = "existing",
                Email = "new@test.com",
                Password = "pass123"
            };

            _userRepoMock.SetupSequence(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
                .ReturnsAsync(true);

            var ex = Assert.ThrowsAsync<ConflictException>(() => _authService.RegisterAsync(dto));
            Assert.That(ex.Message, Is.EqualTo("Username already exists."));
        }

        [Test]
        public void RegisterAsync_DuplicateEmail_ThrowsConflictException()
        {
            var dto = new RegisterDto
            {
                Username = "newuser",
                Email = "existing@test.com",
                Password = "pass123"
            };

            _userRepoMock.SetupSequence(r => r.AnyAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
                .ReturnsAsync(false)
                .ReturnsAsync(true);

            var ex = Assert.ThrowsAsync<ConflictException>(() => _authService.RegisterAsync(dto));
            Assert.That(ex.Message, Is.EqualTo("Email already exists."));
        }

        [Test]
        public void LoginAsync_InvalidUsername_ThrowsBadRequestException()
        {
            var dto = new LoginDto { Username = "nonexistent", Password = "password123" };

            _userRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
                .ReturnsAsync((User?)null);

            var ex = Assert.ThrowsAsync<BadRequestException>(() => _authService.LoginAsync(dto));
            Assert.That(ex.Message, Is.EqualTo("Invalid username or password."));
        }

        [Test]
        public void LoginAsync_WrongPassword_ThrowsBadRequestException()
        {
            var dto = new LoginDto { Username = "testuser", Password = "wrongpassword" };

            using var hmac = new System.Security.Cryptography.HMACSHA512();
            var user = new User
            {
                Id = 1,
                Username = "testuser",
                Email = "test@test.com",
                PasswordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes("correctpassword")),
                PasswordSalt = hmac.Key,
                Role = UserRole.Creator
            };

            _userRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
                .ReturnsAsync(user);

            Assert.ThrowsAsync<BadRequestException>(() => _authService.LoginAsync(dto));
        }

        [Test]
        public async Task LoginAsync_ValidCredentials_ReturnsAuthResponse()
        {
            using var hmac = new System.Security.Cryptography.HMACSHA512();
            var password = "password123";
            var user = new User
            {
                Id = 1,
                Username = "testuser",
                Email = "test@test.com",
                PasswordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password)),
                PasswordSalt = hmac.Key,
                Role = UserRole.Creator
            };

            var dto = new LoginDto { Username = "testuser", Password = password };
            _userRepoMock.Setup(r => r.FirstOrDefaultAsync(It.IsAny<System.Linq.Expressions.Expression<Func<User, bool>>>()))
                .ReturnsAsync(user);

            var result = await _authService.LoginAsync(dto);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Token, Is.Not.Empty);
        }
    }
}