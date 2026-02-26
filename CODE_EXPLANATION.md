# Feedback Generator App — Line-by-Line Code Explanation

This document explains **every line of code** in the project, file by file.

---

## Table of Contents
1. [Program.cs](#1-programcs)
2. [appsettings.json](#2-appsettingsjson)
3. [Models](#3-models)
4. [DTOs](#4-dtos)
5. [Contexts — FeedbackDbContext.cs](#5-contexts--feedbackdbcontextcs)
6. [Interfaces — IRepository.cs](#6-interfaces--irepositorycs)
7. [Interfaces — Service Interfaces](#7-interfaces--service-interfaces)
8. [Repositories — Repository.cs](#8-repositories--repositorycs)
9. [Helpers — JwtHelper.cs](#9-helpers--jwthelpercs)
10. [Helpers — AutoMapperProfile.cs](#10-helpers--automapperprofilecs)
11. [Middlewares — ExceptionHandlingMiddleware.cs](#11-middlewares--exceptionhandlingmiddlewarecs)
12. [Services](#12-services)
13. [Controllers](#13-controllers)

---

## 1. Program.cs

This is the **entry point** of the application. It configures all services and middleware.

```csharp
using System.Text;                                          // For Encoding.UTF8 (used in JWT key)
using Microsoft.AspNetCore.Authentication.JwtBearer;        // JWT authentication middleware
using Microsoft.EntityFrameworkCore;                        // Entity Framework Core for database
using Microsoft.IdentityModel.Tokens;                       // Token validation parameters
using FeedBackGeneratorApp.Contexts;                        // Our DbContext
using FeedBackGeneratorApp.Helpers;                         // JwtHelper, AutoMapperProfile
using FeedBackGeneratorApp.Interfaces;                      // All our interfaces
using FeedBackGeneratorApp.Repositories;                    // Generic Repository
using FeedBackGeneratorApp.Services;                        // All our service classes

var builder = WebApplication.CreateBuilder(args);            // Creates the web app builder with default config

// ═══════════════════ DATABASE ═══════════════════
builder.Services.AddDbContext<FeedbackDbContext>(options =>  // Registers DbContext in DI container
    options.UseSqlServer(                                    // Tells EF Core to use SQL Server
        builder.Configuration.GetConnectionString("DefaultConnection"))); // Reads connection string from appsettings.json

// ═══════════════════ REPOSITORY ═══════════════════
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
// Registers generic repository: whenever IRepository<X> is needed, create Repository<X>
// AddScoped = new instance per HTTP request

// ═══════════════════ SERVICES ═══════════════════
builder.Services.AddScoped<IAuthService, AuthService>();                   // Auth → register, login
builder.Services.AddScoped<ISurveyService, SurveyService>();               // Survey CRUD
builder.Services.AddScoped<ISurveyResponseService, SurveyResponseService>(); // Response submit/pause/resume
builder.Services.AddScoped<IRecipientService, RecipientService>();          // Recipient management
builder.Services.AddScoped<IDistributionService, DistributionService>();    // Link/QR generation
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();          // Survey analytics
builder.Services.AddScoped<INotificationService, NotificationService>();    // In-app notifications
builder.Services.AddScoped<ITemplateService, TemplateService>();            // Survey templates
// Each line says: "When someone asks for IXService, give them XService"

// ═══════════════════ HELPERS ═══════════════════
builder.Services.AddScoped<JwtHelper>();                    // JWT token generator utility

// ═══════════════════ AUTOMAPPER ═══════════════════
builder.Services.AddAutoMapper(typeof(AutoMapperProfile));  // Registers AutoMapper with our mapping profile
                                                            // AutoMapper converts Models ↔ DTOs automatically

// ═══════════════════ JWT AUTHENTICATION ═══════════════════
var jwtSettings = builder.Configuration.GetSection("JwtSettings"); // Read JWT config from appsettings.json

builder.Services.AddAuthentication(options =>                // Configure authentication
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme; // Use JWT as default auth
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;    // Use JWT for challenges (401)
})
.AddJwtBearer(options =>                                     // Configure JWT token validation rules
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,                               // Check token was issued by us
        ValidateAudience = true,                             // Check token is meant for our app
        ValidateLifetime = true,                             // Check token hasn't expired
        ValidateIssuerSigningKey = true,                     // Verify the signature
        ValidIssuer = jwtSettings["Issuer"],                 // Expected issuer: "FeedbackGeneratorApp"
        ValidAudience = jwtSettings["Audience"],             // Expected audience: "FeedbackGeneratorAppUsers"
        IssuerSigningKey = new SymmetricSecurityKey(          // The secret key to verify signatures
            Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!)) // Convert string key to bytes
    };
});

builder.Services.AddAuthorization();                         // Enable [Authorize] attribute on controllers

// ═══════════════════ CONTROLLERS ═══════════════════
builder.Services.AddControllers();                           // Register MVC controllers (our API controllers)

// ═══════════════════ SWAGGER ═══════════════════
builder.Services.AddEndpointsApiExplorer();                  // Required for Swagger to discover endpoints
builder.Services.AddSwaggerGen();                            // Adds Swagger documentation generator

// ═══════════════════ CORS ═══════════════════
builder.Services.AddCors(options =>                          // Cross-Origin Resource Sharing
{
    options.AddPolicy("AllowAll", policy =>                   // Create a policy named "AllowAll"
    {
        policy.AllowAnyOrigin()                               // Allow requests from any domain
              .AllowAnyMethod()                               // Allow GET, POST, PUT, DELETE, etc.
              .AllowAnyHeader();                               // Allow any HTTP headers
    });
});

var app = builder.Build();                                   // Build the application

// ═══════════════════ MIDDLEWARE PIPELINE ═══════════════════
// Order matters! Each request flows through these in order:

if (app.Environment.IsDevelopment())                         // Only in development mode:
{
    app.UseSwagger();                                        // Enable Swagger JSON endpoint
    app.UseSwaggerUI();                                      // Enable Swagger web UI at /swagger
}

app.UseHttpsRedirection();                                   // Redirect HTTP → HTTPS
app.UseCors("AllowAll");                                     // Apply CORS policy
app.UseAuthentication();                                     // Check JWT tokens (WHO are you?)
app.UseAuthorization();                                      // Check permissions (WHAT can you do?)
app.MapControllers();                                        // Map controller routes to endpoints

app.Run();                                                   // Start the application
```

---

## 2. appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=FeedbackGeneratorDb;Trusted_Connection=true;TrustServerCertificate=true;"
    // Server=.              → Connect to local SQL Server (default instance)
    // Database=...          → Database name (auto-created by EF migrations)
    // Trusted_Connection    → Use Windows Authentication (no username/password needed)
    // TrustServerCertificate → Skip SSL certificate validation (for local dev)
  },
  "JwtSettings": {
    "SecretKey": "YourSuperSecretKeyThatIsAtLeast32CharactersLong!",
    // Secret key used to sign JWT tokens. Must be 32+ characters.
    // In production, store this in environment variables, not here!
    "Issuer": "FeedbackGeneratorApp",        // Who issued the token
    "Audience": "FeedbackGeneratorAppUsers",  // Who the token is for
    "ExpirationHours": "24"                   // Token expires after 24 hours
  }
}
```

---

## 3. Models

### User.cs
```csharp
using System.ComponentModel.DataAnnotations;    // For [Key], [Required], [MaxLength] attributes

namespace FeedBackGeneratorApp.Models            // Namespace = folder path in the project
{
    public class User                            // This class maps to the "Users" table in SQL
    {
        public int Id { get; set; }              // Primary Key (EF Core infers this based on name 'Id')

        [Required]                               // This column cannot be NULL in the database
        [MaxLength(100)]                         // Maximum 100 characters
        public string FullName { get; set; } = string.Empty;  // Default value = empty string

        [Required]
        [MaxLength(150)]
        [EmailAddress]                           // Validates email format (e.g., user@example.com)
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;  // Stores hashed password, NOT plain text

        [MaxLength(20)]
        public string Role { get; set; } = "Respondent";  // Default role when registering
        // Possible values: "Admin", "Staff", "Viewer", "Respondent"

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;  // Auto-set to current UTC time

        // ═══ Navigation Properties ═══
        // These tell EF Core about relationships between tables
        // They don't create columns — they create FOREIGN KEY relationships

        public ICollection<Survey> Surveys { get; set; } = new List<Survey>();
        // One User can have MANY Surveys (one-to-many)

        public ICollection<SurveyResponse> SurveyResponses { get; set; } = new List<SurveyResponse>();
        // One User can submit MANY SurveyResponses

        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
        // One User can have MANY Notifications

        public ICollection<Recipient> Recipients { get; set; } = new List<Recipient>();
        // One User can create MANY Recipients
    }
}
```

### Survey.cs
```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;  // For [ForeignKey] attribute

namespace FeedBackGeneratorApp.Models
{
    public class Survey                          // Maps to "Surveys" table
    {
        public int Id { get; set; }

        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;       // Survey title

        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty; // Survey description

        public int CreatedByUserId { get; set; }                 // FK to Users table

        public bool IsActive { get; set; } = true;               // Can disable survey

        public int Version { get; set; } = 1;                    // Auto-incremented on update

        public string? BrandingConfig { get; set; }              // JSON for custom theme/logo
        // "?" means nullable — this field is optional

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // ═══ Navigation Properties ═══

        [ForeignKey("CreatedByUserId")]                          // Links CreatedByUserId → User.Id
        public User CreatedByUser { get; set; } = null!;         // The user who created this survey
        // "null!" tells compiler "I know this won't be null at runtime"

        public ICollection<Question> Questions { get; set; } = new List<Question>();
        public ICollection<SurveyDistribution> SurveyDistributions { get; set; } = new List<SurveyDistribution>();
        public ICollection<SurveyResponse> SurveyResponses { get; set; } = new List<SurveyResponse>();
    }
}
```

### Question.cs
```csharp
namespace FeedBackGeneratorApp.Models
{
    public class Question                        // Maps to "Questions" table
    {
        public int Id { get; set; }

        public int SurveyId { get; set; }        // FK → which survey this question belongs to

        [MaxLength(500)]
        public string Text { get; set; } = string.Empty;  // The actual question text

        [MaxLength(30)]
        public string QuestionType { get; set; } = "OpenText";
        // "MultipleChoice" → pick from options
        // "OpenText"        → free text answer
        // "Rating"          → numeric 1-5
        // "YesNo"           → yes or no

        public string? Options { get; set; }     // JSON array like: ["Option A", "Option B", "Option C"]
        // Only used for MultipleChoice type. Null for other types.

        public bool IsRequired { get; set; } = false;  // Must the respondent answer this?

        public int OrderIndex { get; set; } = 0;        // Display order (1st, 2nd, 3rd question)

        [ForeignKey("SurveyId")]
        public Survey Survey { get; set; } = null!;      // Navigation to parent Survey

        public ICollection<Answer> Answers { get; set; } = new List<Answer>();  // All answers to this question
    }
}
```

### SurveyResponse.cs
```csharp
namespace FeedBackGeneratorApp.Models
{
    public class SurveyResponse                  // Maps to "SurveyResponses" table
    {                                            // One row = one person's complete submission
        public int Id { get; set; }

        public int SurveyId { get; set; }        // Which survey was answered

        public int? RespondentUserId { get; set; } // WHO answered (nullable = anonymous allowed)
        // "int?" = nullable integer. If null, response was anonymous.

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;  // When they started

        public DateTime? CompletedAt { get; set; }   // When they finished (null if paused)

        public bool IsComplete { get; set; } = false; // false = paused, true = completed

        [ForeignKey("SurveyId")]
        public Survey Survey { get; set; } = null!;

        [ForeignKey("RespondentUserId")]
        public User? RespondentUser { get; set; }    // Nullable because anonymous

        public ICollection<Answer> Answers { get; set; } = new List<Answer>();
    }
}
```

### Answer.cs
```csharp
namespace FeedBackGeneratorApp.Models
{
    public class Answer                          // Maps to "Answers" table
    {                                            // One row = one answer to one question
        public int Id { get; set; }

        public int SurveyResponseId { get; set; }  // Which response this answer belongs to

        public int QuestionId { get; set; }          // Which question was answered

        [MaxLength(2000)]
        public string AnswerText { get; set; } = string.Empty;  // The actual answer
        // For Rating: "5", For YesNo: "Yes", For MC: "Option A", For OpenText: "any text..."

        [ForeignKey("SurveyResponseId")]
        public SurveyResponse SurveyResponse { get; set; } = null!;

        [ForeignKey("QuestionId")]
        public Question Question { get; set; } = null!;
    }
}
```

### SurveyDistribution.cs
```csharp
namespace FeedBackGeneratorApp.Models
{
    public class SurveyDistribution              // Maps to "SurveyDistributions" table
    {                                            // Tracks how a survey is shared
        public int Id { get; set; }

        public int SurveyId { get; set; }

        [MaxLength(20)]
        public string DistributionType { get; set; } = "Link";  // "Link", "QRCode", or "Email"

        [MaxLength(500)]
        public string DistributionValue { get; set; } = string.Empty;
        // The generated URL: "/survey/respond/1?token=abc-123-..."

        public DateTime? ScheduledAt { get; set; }   // Schedule for later distribution
        public DateTime? SentAt { get; set; }         // When it was actually sent
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("SurveyId")]
        public Survey Survey { get; set; } = null!;
    }
}
```

### Recipient.cs, SurveyTemplate.cs, Notification.cs
```csharp
// Recipient — People who receive surveys
// Fields: Id, Name, Email, GroupName (for segmentation), CreatedByUserId

// SurveyTemplate — Reusable survey blueprints
// Fields: Id, Name, Description, TemplateData (JSON of survey structure)

// Notification — In-app alerts
// Fields: Id, UserId, Message, IsRead, CreatedAt
```

---

## 4. DTOs

### UserDtos.cs
```csharp
namespace FeedBackGeneratorApp.DTOs
{
    public class RegisterDto                     // What the client sends to register
    {
        [Required] [MaxLength(100)]
        public string FullName { get; set; }     // Client provides name

        [Required] [EmailAddress] [MaxLength(150)]
        public string Email { get; set; }        // Client provides email

        [Required] [MinLength(6)]
        public string Password { get; set; }     // Client provides PLAIN password
        // (We hash it in AuthService before saving)

        [MaxLength(20)]
        public string Role { get; set; } = "Respondent";  // Optional, defaults to Respondent
    }

    public class LoginDto                        // What the client sends to login
    {
        [Required] [EmailAddress]
        public string Email { get; set; }
        [Required]
        public string Password { get; set; }
    }

    public class UserResponseDto                 // What we send BACK to the client
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
        public DateTime CreatedAt { get; set; }
        // NOTE: No PasswordHash! We never expose it.
    }

    public class AuthResponseDto                 // Login/Register response
    {
        public string Token { get; set; }        // JWT token for authentication
        public UserResponseDto User { get; set; } // User details
    }
}
```

---

## 5. Contexts — FeedbackDbContext.cs

```csharp
using Microsoft.EntityFrameworkCore;             // EF Core library
using FeedBackGeneratorApp.Models;               // Our model classes

namespace FeedBackGeneratorApp.Contexts
{
    public class FeedbackDbContext : DbContext    // Inherits from EF Core's DbContext
    {
        // Constructor: receives connection options from Program.cs DI
        public FeedbackDbContext(DbContextOptions<FeedbackDbContext> options)
            : base(options) { }                  // Pass options to parent class

        // ═══ DbSets = Database Tables ═══
        // Each DbSet<T> maps to one table in SQL Server
        public DbSet<User> Users { get; set; }                       // → dbo.Users table
        public DbSet<Survey> Surveys { get; set; }                   // → dbo.Surveys table
        public DbSet<Question> Questions { get; set; }               // → dbo.Questions table
        public DbSet<SurveyDistribution> SurveyDistributions { get; set; }
        public DbSet<Recipient> Recipients { get; set; }
        public DbSet<SurveyResponse> SurveyResponses { get; set; }
        public DbSet<Answer> Answers { get; set; }
        public DbSet<SurveyTemplate> SurveyTemplates { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        // ═══ Fluent API — Relationship Configuration ═══
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User: email must be unique (no two users with same email)
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Email).IsUnique();   // Creates unique index on Email column
            });

            // Survey → User: one user creates many surveys
            modelBuilder.Entity<Survey>(entity =>
            {
                entity.HasOne(s => s.CreatedByUser)         // Survey has ONE User
                      .WithMany(u => u.Surveys)             // User has MANY Surveys
                      .HasForeignKey(s => s.CreatedByUserId) // FK column
                      .OnDelete(DeleteBehavior.Restrict);    // Can't delete user if they have surveys
            });

            // Question → Survey: deleting survey deletes its questions
            modelBuilder.Entity<Question>(entity =>
            {
                entity.HasOne(q => q.Survey)
                      .WithMany(s => s.Questions)
                      .HasForeignKey(q => q.SurveyId)
                      .OnDelete(DeleteBehavior.Cascade);     // Delete survey → delete all questions
            });

            // Answer → Question: NoAction to avoid multiple cascade paths
            modelBuilder.Entity<Answer>(entity =>
            {
                entity.HasOne(a => a.SurveyResponse)
                      .WithMany(sr => sr.Answers)
                      .HasForeignKey(a => a.SurveyResponseId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.Question)
                      .WithMany(q => q.Answers)
                      .HasForeignKey(a => a.QuestionId)
                      .OnDelete(DeleteBehavior.NoAction);     // Prevents circular cascade error
            });

            // SurveyResponse → User: set null if user is deleted
            modelBuilder.Entity<SurveyResponse>(entity =>
            {
                entity.HasOne(sr => sr.RespondentUser)
                      .WithMany(u => u.SurveyResponses)
                      .HasForeignKey(sr => sr.RespondentUserId)
                      .OnDelete(DeleteBehavior.SetNull);      // Delete user → set FK to NULL
            });

            // ... similar configs for other entities
        }
    }
}
```

---

## 6. Interfaces — IRepository.cs

```csharp
using System.Linq.Expressions;                  // For Expression<Func<T, bool>> (lambda queries)

namespace FeedBackGeneratorApp.Interfaces
{
    public interface IRepository<T> where T : class  // Generic interface. T = any class (User, Survey, etc.)
    // "where T : class" means T must be a reference type (not int, bool, etc.)
    {
        Task<IEnumerable<T>> GetAllAsync();           // Get ALL records from the table
        // Task = async operation, IEnumerable = collection of items

        Task<T?> GetByIdAsync(int id);                // Get ONE record by its ID (null if not found)

        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        // Find records matching a condition. Example usage:
        // FindAsync(u => u.Email == "test@test.com")  → finds users with that email
        // Expression<Func<T, bool>> = a lambda that takes T and returns true/false

        Task<T> AddAsync(T entity);                   // INSERT a new record
        Task<T> UpdateAsync(T entity);                // UPDATE an existing record
        Task DeleteAsync(T entity);                   // DELETE a record
        Task<bool> ExistsAsync(int id);               // Check if record with ID exists
    }
}
```

---

## 7. Interfaces — Service Interfaces

```csharp
// IAuthService.cs
public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterDto dto);    // Create account → returns token
    Task<AuthResponseDto> LoginAsync(LoginDto dto);           // Login → returns token
    Task<UserResponseDto?> GetUserByIdAsync(int id);          // Get user profile
    Task<IEnumerable<UserResponseDto>> GetAllUsersAsync();    // List all users
}

// ISurveyService.cs
public interface ISurveyService
{
    Task<SurveyResponseDto> CreateSurveyAsync(CreateSurveyDto dto, int userId);  // Create with questions
    Task<SurveyResponseDto?> GetSurveyByIdAsync(int id);                          // Get one survey
    Task<IEnumerable<SurveyResponseDto>> GetAllSurveysAsync();                    // Get all surveys
    Task<IEnumerable<SurveyResponseDto>> GetSurveysByUserAsync(int userId);       // My surveys
    Task<SurveyResponseDto?> UpdateSurveyAsync(int id, UpdateSurveyDto dto);      // Update + version++
    Task<bool> DeleteSurveyAsync(int id);                                          // Delete survey
    Task<QuestionResponseDto> AddQuestionAsync(int surveyId, CreateQuestionDto dto); // Add question
    Task<bool> DeleteQuestionAsync(int questionId);                                 // Remove question
}

// ISurveyResponseService.cs → Submit, GetById, GetBySurvey, Pause, Resume
// IRecipientService.cs      → Add, GetAll, GetByGroup, Delete, Import
// IDistributionService.cs   → Create, GetBySurvey, Delete
// IAnalyticsService.cs      → GetSurveyAnalytics
// INotificationService.cs   → Create, GetUserNotifications, MarkRead, UnreadCount
// ITemplateService.cs       → Create, GetById, GetAll, Delete
```

---

## 8. Repositories — Repository.cs

```csharp
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using FeedBackGeneratorApp.Contexts;
using FeedBackGeneratorApp.Interfaces;

namespace FeedBackGeneratorApp.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    // Implements IRepository<T> for ANY entity type
    {
        private readonly FeedbackDbContext _context;  // Database context
        private readonly DbSet<T> _dbSet;             // The specific table for type T

        public Repository(FeedbackDbContext context)  // DI injects the DbContext
        {
            _context = context;
            _dbSet = context.Set<T>();                // Get the DbSet for type T
            // If T is User → _dbSet = context.Users
            // If T is Survey → _dbSet = context.Surveys
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.ToListAsync();        // SELECT * FROM [Table]
        }

        public async Task<T?> GetByIdAsync(int id)
        {
            return await _dbSet.FindAsync(id);        // SELECT * FROM [Table] WHERE Id = @id
        }

        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(predicate).ToListAsync();
            // SELECT * FROM [Table] WHERE [condition]
            // Example: FindAsync(u => u.Email == "test@test.com")
            // Becomes: SELECT * FROM Users WHERE Email = 'test@test.com'
        }

        public async Task<T> AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);            // Stage the INSERT
            await _context.SaveChangesAsync();        // Execute the INSERT in database
            return entity;                             // Return with auto-generated Id
        }

        public async Task<T> UpdateAsync(T entity)
        {
            _dbSet.Update(entity);                    // Stage the UPDATE
            await _context.SaveChangesAsync();        // Execute the UPDATE in database
            return entity;
        }

        public async Task DeleteAsync(T entity)
        {
            _dbSet.Remove(entity);                    // Stage the DELETE
            await _context.SaveChangesAsync();        // Execute the DELETE in database
        }

        public async Task<bool> ExistsAsync(int id)
        {
            var entity = await _dbSet.FindAsync(id);  // Try to find by ID
            return entity != null;                     // true if found, false if not
        }
    }
}
```

---

## 9. Helpers — JwtHelper.cs

```csharp
using System.IdentityModel.Tokens.Jwt;           // For JwtSecurityToken
using System.Security.Claims;                     // For Claims (data inside the token)
using System.Text;                                // For Encoding
using Microsoft.IdentityModel.Tokens;             // For SymmetricSecurityKey

namespace FeedBackGeneratorApp.Helpers
{
    public class JwtHelper
    {
        private readonly IConfiguration _configuration;  // Access to appsettings.json

        public JwtHelper(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(int userId, string email, string role)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings"); // Read JWT config

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));     // Convert secret to bytes
            var credentials = new SigningCredentials(
                key, SecurityAlgorithms.HmacSha256);                    // Sign with HMAC-SHA256

            var claims = new[]                    // Claims = data embedded IN the token
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()), // User's ID
                new Claim(ClaimTypes.Email, email),                       // User's email
                new Claim(ClaimTypes.Role, role),                          // User's role
                new Claim(JwtRegisteredClaimNames.Jti,
                    Guid.NewGuid().ToString())                            // Unique token ID
            };
            // Controllers read these claims:
            // User.FindFirst(ClaimTypes.NameIdentifier) → gets userId

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],         // Token issuer
                audience: jwtSettings["Audience"],     // Token audience
                claims: claims,                         // Embedded data
                expires: DateTime.UtcNow.AddHours(      // Expiration time
                    double.Parse(jwtSettings["ExpirationHours"]!)),
                signingCredentials: credentials         // Signature
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
            // Converts token object to string like: "eyJhbGciOi..."
        }
    }
}
```

---

## 10. Helpers — AutoMapperProfile.cs

```csharp
using AutoMapper;

namespace FeedBackGeneratorApp.Helpers
{
    public class AutoMapperProfile : Profile     // Inherits AutoMapper's Profile class
    {
        public AutoMapperProfile()
        {
            // Each CreateMap tells AutoMapper how to convert between two types

            CreateMap<UserModel, UserResponseDto>();
            // User → UserResponseDto: copies Id, FullName, Email, Role, CreatedAt automatically
            // (matching property names are copied, PasswordHash is ignored because it's not in the DTO)

            CreateMap<RegisterDto, UserModel>()
                .ForMember(dest => dest.PasswordHash, opt => opt.Ignore());
            // RegisterDto → User: copies FullName, Email, Role
            // PasswordHash is IGNORED because we set it manually after hashing

            CreateMap<SurveyModel, SurveyResponseDto>()
                .ForMember(dest => dest.CreatedByUserName,
                    opt => opt.MapFrom(src => src.CreatedByUser.FullName));
            // Survey → SurveyResponseDto: auto-copies all matching fields
            // PLUS maps CreatedByUser.FullName → CreatedByUserName (custom mapping)

            // ... similar mappings for all other entity ↔ DTO pairs
        }
    }
}
```

---

## 11. Middlewares — ExceptionHandlingMiddleware.cs

**Location:** `Middlewares/ExceptionHandlingMiddleware.cs`

This middleware intercepts **all** exceptions thrown anywhere in the application (like in Services) and translates them into appropriate HTTP status codes, returning a structured JSON error payload. This eliminates the need for `try/catch` blocks in controllers.

```csharp
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context); // Moves to next middleware / controller
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var code = HttpStatusCode.InternalServerError; // 500

        // Map known exceptions to specific HTTP status codes
        if (ex is InvalidOperationException || ex is ArgumentException) code = HttpStatusCode.BadRequest; // 400
        else if (ex is UnauthorizedAccessException) code = HttpStatusCode.Unauthorized; // 401
        else if (ex is KeyNotFoundException) code = HttpStatusCode.NotFound; // 404

        // Return a clean JSON response
    }
}
```

---

## 12. Services

### AuthService.cs
```csharp
public class AuthService : IAuthService
{
    // Dependencies injected via constructor
    private readonly IRepository<User> _userRepo;  // Database operations for Users
    private readonly IMapper _mapper;               // AutoMapper for conversions
    private readonly JwtHelper _jwtHelper;           // JWT token generator

    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
    {
        // 1. Check if email already exists
        var existingUsers = await _userRepo.FindAsync(u => u.Email == dto.Email);
        if (existingUsers.Any())
            throw new InvalidOperationException("A user with this email already exists.");

        // 2. Convert DTO to User model
        var user = _mapper.Map<User>(dto);            // RegisterDto → User (auto-copies fields)

        // 3. Hash the password with BCrypt
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        // "password123" → "$2a$11$K3Fg7Rm..." (irreversible hash with random salt)

        // 4. Save to database
        await _userRepo.AddAsync(user);               // INSERT INTO Users ...

        // 5. Generate JWT token
        var token = _jwtHelper.GenerateToken(user.Id, user.Email, user.Role);

            // Auto-generate shareable link
            var distribution = new SurveyDistribution
            {
                SurveyId = survey.Id,
                DistributionType = "Link",
                DistributionValue = $"/survey/respond/{survey.Id}?token={Guid.NewGuid()}",
                SentAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            await _distributionRepo.AddAsync(distribution);

            return await GetSurveyByIdAsync(survey.Id) ?? throw new Exception("Failed to create survey.");
        return new AuthResponseDto
        {
            Token = token,
            User = _mapper.Map<UserResponseDto>(user)  // User → UserResponseDto
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        // 1. Find user by email
        var users = await _userRepo.FindAsync(u => u.Email == dto.Email);
        var user = users.FirstOrDefault();             // Get first match or null

        // 2. Verify password
        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");
        // BCrypt.Verify compares plain password with stored hash

        // 3. Generate token and return
        var token = _jwtHelper.GenerateToken(user.Id, user.Email, user.Role);
        return new AuthResponseDto { Token = token, User = _mapper.Map<UserResponseDto>(user) };
    }
}
```

### SurveyService.cs (key methods)
```csharp
public async Task<SurveyResponseDto> CreateSurveyAsync(CreateSurveyDto dto, int userId)
{
    var survey = _mapper.Map<Survey>(dto);             // DTO → Survey model
    survey.CreatedByUserId = userId;                   // Set who created it

    await _surveyRepo.AddAsync(survey);                // Save survey to DB

    if (dto.Questions != null)                          // If questions were provided
    {
        foreach (var qDto in dto.Questions)             // Loop through each question
        {
            var question = _mapper.Map<Question>(qDto); // DTO → Question model
            question.SurveyId = survey.Id;              // Link to the survey
            await _questionRepo.AddAsync(question);     // Save question to DB
        }
    }

    return await GetSurveyByIdAsync(survey.Id);         // Return complete survey with questions
}

public async Task<SurveyResponseDto?> UpdateSurveyAsync(int id, UpdateSurveyDto dto)
{
    var survey = await _surveyRepo.GetByIdAsync(id);
    if (survey == null) return null;

    // Only update fields that were provided (null = not sent by client)
    if (dto.Title != null) survey.Title = dto.Title;
    if (dto.Description != null) survey.Description = dto.Description;
    if (dto.IsActive.HasValue) survey.IsActive = dto.IsActive.Value;

    survey.Version++;                                   // Increment version on every update
    survey.UpdatedAt = DateTime.UtcNow;

    await _surveyRepo.UpdateAsync(survey);
    return await GetSurveyByIdAsync(id);
}
```

### AnalyticsService.cs (key method)
```csharp
public async Task<SurveyAnalyticsDto?> GetSurveyAnalyticsAsync(int surveyId)
{
    // Calculate completion rate
    var completionRate = totalResponses > 0
        ? Math.Round((double)completedResponses / totalResponses * 100, 2)
        : 0;
    // Example: 45 completed / 50 total = 90.00%

    foreach (var question in survey.Questions)
    {
        switch (question.QuestionType)
        {
            case "MultipleChoice":
            case "YesNo":
                // Group answers and count each option
                // { "Yes": 30, "No": 15 } or { "Option A": 10, "Option B": 20 }
                questionAnalytics.AnswerDistribution = answers
                    .GroupBy(a => a.AnswerText)
                    .ToDictionary(g => g.Key, g => g.Count());
                break;

            case "Rating":
                // Calculate average: (5+4+3+5+4) / 5 = 4.2
                questionAnalytics.AverageRating = numericAnswers.Average();
                break;

            case "OpenText":
                // Collect all text responses into a list
                questionAnalytics.OpenTextResponses = answers
                    .Select(a => a.AnswerText).ToList();
                break;
        }
    }
}
```

---

## 12. Controllers

### AuthController.cs
```csharp
[Route("api/[controller]")]                      // Base URL: /api/Auth
[ApiController]                                   // Enables auto model validation
public class AuthController : ControllerBase      // Base class for API controllers
{
    private readonly IAuthService _authService;   // Injected via constructor

    [HttpPost("register")]                        // POST /api/Auth/register
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterDto dto)
    // [FromBody] = read from request JSON body
    // ActionResult<T> = can return data (200) or error (400, 401)
    {
        try
        {
            var result = await _authService.RegisterAsync(dto);  // Call service
            return Ok(result);                                    // 200 OK with data
        }
        catch (InvalidOperationException ex)                      // Email already exists
        {
            return BadRequest(new { message = ex.Message });      // 400 Bad Request
        }
    }

    [HttpPost("login")]                           // POST /api/Auth/login
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
    {
        try
        {
            var result = await _authService.LoginAsync(dto);
            return Ok(result);                                    // 200 + token
        }
        catch (UnauthorizedAccessException ex)                    // Wrong password
        {
            return Unauthorized(new { message = ex.Message });    // 401 Unauthorized
        }
    }
}
```

### SurveyController.cs (key parts)
```csharp
[Authorize]                                       // ALL endpoints need JWT token
public class SurveyController : ControllerBase
{
    [HttpPost]                                    // POST /api/Survey
    public async Task<ActionResult<SurveyResponseDto>> CreateSurvey([FromBody] CreateSurveyDto dto)
    {
        // Extract userId from JWT token claims
        var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        // User.FindFirst(ClaimTypes.NameIdentifier) reads the claim we set in JwtHelper

        var result = await _surveyService.CreateSurveyAsync(dto, userId);
        return CreatedAtAction(nameof(GetSurvey), new { id = result.Id }, result);
        // Returns 201 Created with Location header pointing to GET endpoint
    }

    [HttpGet]
    [AllowAnonymous]                              // Override [Authorize] — no token needed
    public async Task<ActionResult<IEnumerable<SurveyResponseDto>>> GetAllSurveys()
    {
        var surveys = await _surveyService.GetAllSurveysAsync();
        return Ok(surveys);                       // 200 OK with list of surveys
    }

    [HttpDelete("{id}")]                          // DELETE /api/Survey/5
    public async Task<ActionResult> DeleteSurvey(int id)  // {id} from URL → int id parameter
    {
        var deleted = await _surveyService.DeleteSurveyAsync(id);
        if (!deleted) return NotFound();          // 404 if survey doesn't exist
        return NoContent();                       // 204 No Content (success, no body)
    }
}
```

---

## Summary of Request Flow

```
1. Client sends: POST /api/Auth/register { "fullName": "John", "email": "john@test.com", "password": "123456" }
        │
2. AuthController.Register() receives the request
        │ Validates DTO (checks [Required] fields)
        │
3. AuthService.RegisterAsync() runs business logic
        │ Checks if email exists
        │ Hashes password with BCrypt
        │ Maps DTO → User model
        │
4. Repository<User>.AddAsync() saves to database
        │ EF Core generates: INSERT INTO Users (FullName, Email, PasswordHash, ...) VALUES (...)
        │
5. JwtHelper.GenerateToken() creates JWT token
        │
6. Response flows back: { "token": "eyJ...", "user": { "id": 1, "fullName": "John", ... } }
```
