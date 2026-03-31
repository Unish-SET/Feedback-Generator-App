using FeedBackApp.Context;
using FeedBackApp.Exceptions.Middleware;
using FeedBackApp.Interfaces;
using FeedBackApp.Interfaces.RepositoryInterface;
using FeedBackApp.Repository;
using FeedBackApp.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ── RATE LIMITING ─────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    // Return a consistent JSON body on 429
    options.OnRejected = async (ctx, _) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsJsonAsync(new
        {
            success    = false,
            statusCode = 429,
            message    = "Too many requests. Please slow down and try again shortly."
        });
    };

    // ── Auth: 10 attempts / 1 min per IP ──────────────────────────────────────
    // Protects login and register from brute-force and credential stuffing.
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit          = 10,
                Window               = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0
            }));

    // ── Survey submit: 5 submissions / 1 min per IP ───────────────────────────
    // Prevents survey spam from anonymous or scripted respondents.
    options.AddPolicy("survey-submit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit          = 5,
                Window               = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0
            }));

    // ── Global: 200 requests / 1 min per IP ──────────────────────────────────
    // General backstop — applied to all endpoints via UseRateLimiter().
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit          = 200,
                Window               = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0
            }));
});

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
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

// ── CORS ───────────────────────────────────────────────────────────────────────
// SEC-01 FIX: AllowAnyOrigin was used for both dev and production.
// In production the allowed origin must be explicitly set via CorsOrigins config.
// Development keeps AllowAnyOrigin for convenience.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Dev: allow any origin so the Angular dev server (any port) can connect
            policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
        }
        else
        {
            // Production: only allow the configured frontend origin.
            // Set CorsOrigins in appsettings.Production.json or as an environment variable.
            var allowedOrigin = builder.Configuration["CorsOrigins"]
                ?? throw new InvalidOperationException(
                    "CorsOrigins is not configured. Set it in appsettings.Production.json " +
                    "or as the CORSORIGINS environment variable.");
            policy.WithOrigins(allowedOrigin)
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
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
builder.Services.AddScoped<IQuestionService,     QuestionService>();
builder.Services.AddScoped<IQuestionBankService, QuestionBankService>();
builder.Services.AddScoped<IResponseService,  ResponseService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IUserService,      UserService>();
builder.Services.AddScoped<IExcelService,     ExcelService>();
builder.Services.AddScoped<IAuditService,     AuditService>();
builder.Services.AddScoped<IAdminSurveyService, AdminSurveyService>();
builder.Services.AddScoped<IQuestionImportService, QuestionImportService>();

// ── OTP SERVICE (in-memory cache — no DB table needed) ────────────────────────
builder.Services.AddMemoryCache();

// ── JWT AUTHENTICATION ─────────────────────────────────────────────────────────
string jwtKey = builder.Configuration["JwtSettings:Key"]
    ?? throw new InvalidOperationException("JWT Key not found in configuration.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidIssuer              = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience            = builder.Configuration["JwtSettings:Audience"],
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        });

builder.Services.AddAuthorization();

// ── HEALTH CHECKS ──────────────────────────────────────────────────────────────
// PROD-03: Required by Kubernetes, Azure App Service, and load balancers.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<FeedBackDbContext>();

// ── BUILD ──────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── DATABASE SEED ──────────────────────────────────────────────────────────────
try
{
    using var scope  = app.Services.CreateScope();
    var db           = scope.ServiceProvider.GetRequiredService<FeedBackDbContext>();
    var logger       = scope.ServiceProvider.GetRequiredService<ILogger<FeedBackDbContext>>();
    var cfg          = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    // PROD-02 FIX: EnsureCreated creates tables from the current model snapshot but never
    // applies EF migrations — any schema change added via Add-Migration would be silently
    // skipped in production. Migrate() runs all pending migrations and is idempotent.
    db.Database.Migrate();

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
else
{
    // PROD-01 FIX: enforce HTTPS and add HSTS header in production
    app.UseHsts();
}

// Redirect HTTP → HTTPS in all environments (no-op in dev if only HTTPS is configured)
app.UseHttpsRedirection();

app.UseCors();
app.UseRateLimiter();

// RequestLoggingMiddleware first — sets CorrelationId used by GlobalExceptionMiddleware
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseRouting();

// GlobalExceptionMiddleware after routing so route values are populated in error logs
app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// PROD-03: Health check endpoint — used by orchestrators and load balancers
app.MapHealthChecks("/health");

app.Run();
