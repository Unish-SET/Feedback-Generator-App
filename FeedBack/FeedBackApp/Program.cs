using FeedBackApp.Context;
using FeedBackApp.Exceptions.Middleware;
using FeedBackApp.Interfaces;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Repository;
using FeedBackApp.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// ── CONTROLLERS ───────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddEndpointsApiExplorer();

// ── SWAGGER + JWT ──────────────────────────────────────────────────────────────
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "FeedBack App API",
        Version     = "v1",
        Description = "Survey/Feedback Management System — Admin, Creator roles"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter: Bearer {your JWT token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── DATABASE ───────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<FeedBackDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── CORS ───────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// ── GENERIC REPOSITORY ─────────────────────────────────────────────────────────
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// ── AUDIT FACTORY (independent DB connection for audit writes) ─────────────────
builder.Services.AddSingleton<IAuditDbContextFactory>(
    new AuditDbContextFactory(
        builder.Configuration.GetConnectionString("DefaultConnection")!));

// ── APPLICATION SERVICES ──────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService,      AuthService>();
builder.Services.AddScoped<ISurveyService,    SurveyService>();
builder.Services.AddScoped<IQuestionService,  QuestionService>();
builder.Services.AddScoped<IResponseService,  ResponseService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IExportService,    ExportService>();
builder.Services.AddScoped<IUserService,      UserService>();
builder.Services.AddScoped<IExcelService,     ExcelService>();
builder.Services.AddScoped<IAuditService,     AuditService>();

// ── JWT AUTHENTICATION ─────────────────────────────────────────────────────────
string jwtKey = builder.Configuration["JwtSettings:Key"]
    ?? throw new InvalidOperationException("JWT Key not found in configuration.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = false,
            ValidateAudience         = false,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        });

builder.Services.AddAuthorization();

// ── BUILD ──────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── DATABASE SEED ──────────────────────────────────────────────────────────────
try
{
    using var scope  = app.Services.CreateScope();
    var db           = scope.ServiceProvider.GetRequiredService<FeedBackDbContext>();
    var logger       = scope.ServiceProvider.GetRequiredService<ILogger<FeedBackDbContext>>();
    var cfg          = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    db.Database.EnsureCreated();

    if (!db.Users.Any(u => u.Role == FeedBackApp.Models.Enums.UserRole.Admin))
    {
        var username = cfg["AdminSeed:Username"] ?? "admin";
        var email    = cfg["AdminSeed:Email"]    ?? "admin@feedbackapp.com";
        var password = cfg["AdminSeed:Password"] ?? "Admin@123";

        using var hmac = new HMACSHA512();

        db.Users.Add(new FeedBackApp.Models.User
        {
            Username     = username,
            Email        = email,
            PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password)),
            PasswordSalt = hmac.Key,
            Role         = FeedBackApp.Models.Enums.UserRole.Admin,
            CreatedAt    = DateTime.UtcNow
        });
        db.SaveChanges();
        logger.LogInformation("Admin user '{Username}' seeded successfully.", username);
    }
}
catch (Exception ex)
{
    var startupLog = app.Services.GetRequiredService<ILogger<Program>>();
    startupLog.LogWarning(ex, "Database setup encountered an issue. App will continue.");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

// RequestLoggingMiddleware first — sets CorrelationId used by GlobalExceptionMiddleware
app.UseMiddleware<RequestLoggingMiddleware>();

// GlobalExceptionMiddleware catches all unhandled exceptions from the pipeline below
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
