# Project Backend Documentation

> **Project:** FeedBackApp — Survey & Feedback Management System
> **Framework:** ASP.NET Core (.NET 10) Web API
> **Database:** SQL Server (Entity Framework Core)
> **Authentication:** JWT Bearer Tokens

---

## 1. Project Overview

### What This Backend Does

FeedBackApp is a full-stack survey and feedback management system. The backend is a RESTful Web API built with ASP.NET Core that handles everything from user authentication to survey creation, response collection, analytics, and audit logging.

### Main Purpose

The API allows two types of authenticated users (Admin and Creator) to build and manage surveys, and allows anyone (anonymous or authenticated) to submit responses via a public survey link.

### Core Features

- User registration and login with JWT authentication
- Survey lifecycle management (Inactive → Active → Closed)
- Question management with 5 question types (Single Choice, Multiple Choice, Rating Scale, Short Text, Long Text)
- Public survey submission via unique token (anonymous or authenticated)
- Response collection and retrieval
- Analytics per survey (option distributions, average ratings, text responses, date-wise counts)
- Question Bank — reusable question templates with SHA256 deduplication
- Excel export of survey responses (formatted .xlsx with two sheets)
- Excel import of questions from a template file
- Admin panel — manage all users and surveys, view audit logs
- Audit logging — every important action is recorded with user, IP, and correlation ID
- Rate limiting — brute-force protection on auth endpoints, spam protection on survey submit
- Global exception handling with structured JSON error responses
- Request/response logging with correlation IDs for distributed tracing

### How the Backend Interacts with the Angular Frontend

The Angular frontend (running on a separate dev server) communicates with this API over HTTP/HTTPS. In development, CORS is open to any origin. In production, only the configured `CorsOrigins` value is allowed.

The Angular app:
1. Calls `POST /api/auth/login` → receives a JWT token
2. Stores the token and sends it as `Authorization: Bearer <token>` on every subsequent request
3. Uses the `publicToken` (a GUID) to load and submit public surveys without authentication


---

## 2. Solution Structure

```
FeedbackApp.sln
│
├── FeedBackApp/                        ← Main Web API project
│   ├── Controllers/                    ← HTTP endpoints (request/response handling)
│   ├── Services/                       ← Business logic layer
│   ├── Interfaces/                     ← Contracts (interfaces) for services and repositories
│   │   └── RepositoryInterface/        ← Generic IRepository<T> interface
│   ├── Repository/                     ← Generic repository implementation
│   ├── Context/                        ← EF Core DbContext and AuditDbContextFactory
│   ├── Models/                         ← Entity classes (database tables)
│   │   ├── DTOs/                       ← Data Transfer Objects (request/response shapes)
│   │   └── Enums/                      ← Enumerations (UserRole, SurveyState, QuestionType)
│   ├── Exceptions/                     ← Custom exception classes
│   │   └── Middleware/                 ← GlobalExceptionMiddleware, RequestLoggingMiddleware
│   ├── Helpers/                        ← Utility classes (RoleHelper, DbExceptionHelper)
│   ├── Migrations/                     ← EF Core database migration files
│   ├── Properties/                     ← launchSettings.json
│   ├── appsettings.json                ← App configuration (connection string, JWT, CORS)
│   └── Program.cs                      ← App entry point, DI registrations, middleware pipeline
│
└── FeedBackApp.Tests/                  ← Unit test project (xUnit)
    ├── AuthServiceTests.cs
    ├── SurveyServiceTests.cs
    ├── ResponseServiceTests.cs
    └── ...
```

### Folder Responsibilities

| Folder | Responsibility |
|---|---|
| `Controllers/` | Receives HTTP requests, extracts data, calls services, returns HTTP responses |
| `Services/` | Contains all business logic — validation, rules, orchestration |
| `Interfaces/` | Defines contracts so services and controllers depend on abstractions, not concrete classes |
| `Repository/` | Generic data access layer — wraps EF Core DbSet operations |
| `Context/` | EF Core DbContext — defines tables, relationships, query filters |
| `Models/` | Entity classes that map directly to database tables |
| `Models/DTOs/` | Shapes for incoming requests and outgoing responses — never expose raw entities |
| `Models/Enums/` | Strongly-typed enumerations used across the app |
| `Exceptions/` | Custom exception types with HTTP status codes |
| `Exceptions/Middleware/` | Global error handler and request logger injected into the pipeline |
| `Helpers/` | Small static utility methods (role checks, DB error detection) |
| `Migrations/` | Auto-generated EF Core migration files that version the database schema |


---

## 3. Database Layer

### DbContext

`FeedBackDbContext` is the bridge between your C# code and SQL Server. It inherits from EF Core's `DbContext` and does three things:

1. Holds a `DbSet<T>` for each table
2. Configures relationships, indexes, and query filters in `OnModelCreating`
3. Receives the SQL Server connection string via Dependency Injection

```csharp
// Context/FeedBackDbContext.cs
public class FeedBackDbContext : DbContext
{
    public FeedBackDbContext(DbContextOptions<FeedBackDbContext> options) : base(options) { }

    public DbSet<User>               Users               => Set<User>();
    public DbSet<Survey>             Surveys             => Set<Survey>();
    public DbSet<Question>           Questions           => Set<Question>();
    public DbSet<QuestionOption>     QuestionOptions     => Set<QuestionOption>();
    public DbSet<SurveyResponse>     SurveyResponses     => Set<SurveyResponse>();
    public DbSet<Answer>             Answers             => Set<Answer>();
    public DbSet<AuditLog>           AuditLogs           => Set<AuditLog>();
    public DbSet<BankQuestion>       BankQuestions       => Set<BankQuestion>();
    public DbSet<BankQuestionOption> BankQuestionOptions => Set<BankQuestionOption>();
    ...
}
```

The connection string is configured in `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=FeedBackAppDb;Trusted_Connection=true;"
}
```

And registered in `Program.cs`:

```csharp
builder.Services.AddDbContext<FeedBackDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
```

### Global Query Filters

Two entities have automatic soft-delete filters applied so deleted records are never returned unless explicitly bypassed:

```csharp
// Surveys with IsDeleted=true are automatically excluded from all queries
entity.HasQueryFilter(s => !s.IsDeleted);

// Questions automatically filter out questions belonging to deleted surveys
entity.HasQueryFilter(q => !q.Survey.IsDeleted);
```

To bypass these filters (e.g., in admin views), use `.IgnoreQueryFilters()`.

### Table Relationships

```
User ──────────────────────────────────────────────────────────────────────────
  │  (one User creates many Surveys)
  ├──► Survey (CreatedBy FK → User.Id)  [Restrict on delete — can't delete user with surveys]
  │      │  (one Survey has many Questions)
  │      ├──► Question (SurveyId FK)    [Cascade delete — delete survey = delete questions]
  │      │       │  (one Question has many QuestionOptions)
  │      │       └──► QuestionOption    [Cascade delete]
  │      │
  │      └──► SurveyResponse (SurveyId FK)  [Cascade delete]
  │               │  (one Response has many Answers)
  │               └──► Answer (ResponseId FK)  [Cascade delete]
  │                       ├── QuestionId FK → Question  [NoAction — no cascade]
  │                       └── SelectedOptionId FK → QuestionOption  [NoAction]
  │
  └──► SurveyResponse (UserId FK — nullable, null = anonymous)
```

**Navigation Properties** let you traverse relationships in code:

```csharp
// From a Survey, access all its questions:
survey.Questions  // ICollection<Question>

// From a Question, access its options:
question.Options  // ICollection<QuestionOption>

// From a SurveyResponse, access all answers:
response.Answers  // ICollection<Answer>

// From a User, access all surveys they created:
user.Surveys  // ICollection<Survey>
```

EF Core translates these into SQL JOINs when you use `.Include()`.


---

## 4. Models / Entities

### User

Represents a registered user of the system.

```csharp
public class User
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Username { get; set; }

    [Required, MaxLength(200)]
    public string Email { get; set; }

    [Required]
    public byte[] PasswordHash { get; set; }  // HMACSHA512 hash of the password

    [Required]
    public byte[] PasswordSalt { get; set; }  // HMACSHA512 key used to hash

    [Required]
    public UserRole Role { get; set; }        // Admin=0, Creator=1, Respondent=2

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;  // soft delete
    public DateTime CreatedAt { get; set; }

    // Navigation
    public ICollection<Survey> Surveys { get; set; }
    public ICollection<SurveyResponse> Responses { get; set; }
}
```

Passwords are never stored as plain text. HMACSHA512 is used — the salt is the HMAC key, and the hash is the computed hash of the password bytes.

---

### Survey

The core entity. A survey has a lifecycle: Inactive → Active → Closed.

```csharp
public class Survey
{
    [Key] public int Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public Guid PublicToken { get; set; }     // unique GUID used in the public URL
    public SurveyState State { get; set; }    // Inactive=0, Active=1, Closed=2
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool AllowAnonymous { get; set; }
    public int CreatedBy { get; set; }        // FK → User.Id
    public bool IsDeleted { get; set; }       // soft delete
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User Creator { get; set; }
    public ICollection<Question> Questions { get; set; }
    public ICollection<SurveyResponse> Responses { get; set; }
}
```

---

### Question

A question belonging to a survey. Supports 5 types via the `QuestionType` enum.

```csharp
public class Question
{
    [Key] public int Id { get; set; }
    public int SurveyId { get; set; }         // FK → Survey.Id
    public string Text { get; set; }
    public QuestionType Type { get; set; }    // SingleChoice, MultipleChoice, RatingScale, ShortText, LongText
    public bool IsRequired { get; set; }
    public int Order { get; set; }            // display order

    // Navigation
    public Survey Survey { get; set; }
    public ICollection<QuestionOption> Options { get; set; }
    public ICollection<Answer> Answers { get; set; }
}
```

---

### QuestionOption

An answer choice for SingleChoice or MultipleChoice questions.

```csharp
public class QuestionOption
{
    [Key] public int Id { get; set; }
    public int QuestionId { get; set; }       // FK → Question.Id
    public string Text { get; set; }
    public int Order { get; set; }
}
```

---

### SurveyResponse

One submission of a survey by one respondent.

```csharp
public class SurveyResponse
{
    [Key] public int Id { get; set; }
    public int SurveyId { get; set; }         // FK → Survey.Id
    public int? UserId { get; set; }          // null = anonymous respondent
    public DateTime SubmittedAt { get; set; }

    // Navigation
    public Survey Survey { get; set; }
    public User? User { get; set; }
    public ICollection<Answer> Answers { get; set; }
}
```

A unique index on `(SurveyId, UserId)` where `UserId IS NOT NULL` prevents authenticated users from submitting twice.

---

### Answer

One answer to one question within a response. Stores different value types depending on question type.

```csharp
public class Answer
{
    [Key] public int Id { get; set; }
    public int ResponseId { get; set; }           // FK → SurveyResponse.Id
    public int QuestionId { get; set; }           // FK → Question.Id
    public int? SelectedOptionId { get; set; }    // for SingleChoice
    public string? TextValue { get; set; }        // for ShortText / LongText
    public int? RatingValue { get; set; }         // for RatingScale (1–5)
    public string? SelectedOptionIds { get; set; } // for MultipleChoice — comma-separated IDs e.g. "3,7,12"
}
```

---

### AuditLog

Records every important action in the system for compliance and debugging.

```csharp
public class AuditLog
{
    [Key] public Guid Id { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; }        // e.g. "Login", "Create", "Delete"
    public string EntityName { get; set; }    // e.g. "Survey", "User"
    public string? EntityId { get; set; }     // e.g. "42"
    public string? Changes { get; set; }      // JSON with OldValues/NewValues
    public DateTime Timestamp { get; set; }
    public string? IPAddress { get; set; }
    public string? CorrelationId { get; set; }
}
```

---

### BankQuestion / BankQuestionOption

Reusable question templates stored in a question bank. Not tied to any survey. Used as a library of pre-written questions that can be cloned into surveys.

```csharp
public class BankQuestion
{
    [Key] public int Id { get; set; }
    public int CreatedBy { get; set; }        // FK → User.Id (owner)
    public string Text { get; set; }
    public QuestionType Type { get; set; }
    public bool IsRequired { get; set; }
    public string? Tag { get; set; }          // category label e.g. "NPS", "Satisfaction"
    public string? Hash { get; set; }         // SHA256 hash for deduplication
    public bool IsDeleted { get; set; }
    public ICollection<BankQuestionOption> Options { get; set; }
}
```


---

## 5. DTOs (Data Transfer Objects)

### Why DTOs Are Used

DTOs are plain C# classes that define exactly what data goes in (request) and what comes out (response). They exist for three reasons:

1. **Security** — Entity models contain sensitive fields like `PasswordHash`, `PasswordSalt`, `IsDeleted`. DTOs expose only what the client needs.
2. **Decoupling** — The API contract (DTO shape) can change independently from the database schema (entity shape).
3. **Validation** — DTOs carry `[Required]`, `[MaxLength]`, `[Range]` annotations that ASP.NET Core validates automatically before the controller method even runs.

### Example: Register Flow

The client sends a `RegisterDto`. The service creates a `User` entity. The response returns an `AuthResponseDto` — never the raw `User`.

```csharp
// What the client sends:
public class RegisterDto
{
    [Required, MaxLength(100)]  public string Username { get; set; }
    [Required, EmailAddress]    public string Email    { get; set; }
    [Required, MinLength(6)]    public string Password { get; set; }
}

// What the client receives back:
public class AuthResponseDto
{
    public string Token    { get; set; }  // JWT token
    public int    UserId   { get; set; }
    public string Username { get; set; }
    public string Email    { get; set; }
    public string Role     { get; set; }
    // Notice: NO PasswordHash, NO PasswordSalt, NO IsDeleted
}
```

### Mapping Between DTO and Entity

Mapping is done manually inside the service layer. There is no AutoMapper — the code is explicit and easy to trace.

```csharp
// In AuthService.RegisterAsync():
var user = new User
{
    Username     = dto.Username,       // from RegisterDto
    Email        = dto.Email,
    PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dto.Password)),
    PasswordSalt = hmac.Key,
    Role         = UserRole.Creator,   // hardcoded — never trust client role
    CreatedAt    = DateTime.UtcNow
};
// ... save to DB ...

return new AuthResponseDto
{
    Token    = GenerateToken(user),
    UserId   = user.Id,
    Username = user.Username,
    Email    = user.Email,
    Role     = user.Role.ToString()
};
```

### Pagination DTO

All list endpoints return a `PaginatedResult<T>` wrapper:

```csharp
public class PaginatedResult<T>
{
    public List<T> Items      { get; set; }   // the page of data
    public int PageNumber     { get; set; }
    public int PageSize       { get; set; }
    public int TotalCount     { get; set; }
    public int TotalPages     => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPrevious   => PageNumber > 1;
    public bool HasNext       => PageNumber < TotalPages;
}
```

### Key DTOs Summary

| DTO | Direction | Purpose |
|---|---|---|
| `RegisterDto` | Request | New user registration |
| `LoginDto` | Request | User login |
| `AuthResponseDto` | Response | JWT token + user identity |
| `CreateSurveyDto` | Request | Create a new survey |
| `UpdateSurveyDto` | Request | Edit survey metadata |
| `SurveyResponseDto` | Response | Full survey detail |
| `SurveyListDto` | Response | Survey in a paginated list |
| `CreateQuestionDto` | Request | Add a question to a survey |
| `QuestionResponseDto` | Response | Question with its options |
| `SubmitResponseDto` | Request | Submit answers to a survey |
| `ResponseListDto` | Response | A submitted response with all answers |
| `SurveyAnalyticsDto` | Response | Analytics data for a survey |
| `AuditLogDto` | Response | Audit log entry |
| `PaginatedResult<T>` | Response | Generic paginated wrapper |


---

## 6. Repository Pattern

### Why Repository Pattern Is Used

Without a repository, every service would write raw EF Core queries directly. This creates two problems:
- Hard to test (you'd need a real database)
- Business logic gets mixed with data access code

The repository wraps all EF Core operations behind a clean interface. Services only call the interface — they don't know or care whether the data comes from SQL Server, an in-memory store, or a mock.

### The Interface

```csharp
// Interfaces/RepositoryInterface/IRepository.cs
public interface IRepository<T> where T : class
{
    Task<T?>              GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<T?>              FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
    Task<bool>            AnyAsync(Expression<Func<T, bool>> predicate);
    Task<int>             CountAsync(Expression<Func<T, bool>> predicate);
    IQueryable<T>         GetQueryable();   // for complex queries with Include/OrderBy/Skip/Take
    Task                  AddAsync(T entity);
    Task                  AddRangeAsync(IEnumerable<T> entities);
    void                  Update(T entity);
    void                  Remove(T entity);
    void                  RemoveRange(IEnumerable<T> entities);
    Task                  SaveChangesAsync();
}
```

### The Implementation

```csharp
// Repository/Repository.cs
public class Repository<T> : IRepository<T> where T : class
{
    private readonly FeedBackDbContext _context;
    private readonly DbSet<T> _dbSet;

    public Repository(FeedBackDbContext context)
    {
        _context = context;
        _dbSet   = context.Set<T>();  // gets the DbSet for type T
    }

    public async Task<T?> GetByIdAsync(int id)
        => await _dbSet.FindAsync(id);

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        => await _dbSet.Where(predicate).ToListAsync();

    public IQueryable<T> GetQueryable()
        => _dbSet.AsQueryable();  // lets callers chain .Include(), .Where(), .Skip(), .Take()

    public async Task AddAsync(T entity)
        => await _dbSet.AddAsync(entity);

    public void Update(T entity)
        => _dbSet.Update(entity);

    public void Remove(T entity)
        => _dbSet.Remove(entity);

    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}
```

### How It Is Registered

The generic repository is registered once in `Program.cs` and works for every entity type:

```csharp
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
```

This means `IRepository<Survey>`, `IRepository<User>`, `IRepository<Question>` etc. are all automatically available via DI.

### Usage Example in a Service

```csharp
// In SurveyService — injected via constructor
private readonly IRepository<Survey> _surveyRepo;

// Find a survey by ID
var survey = await _surveyRepo.GetByIdAsync(surveyId);

// Complex query with Include and filtering
var survey = await _surveyRepo.GetQueryable()
    .Include(s => s.Creator)
    .FirstOrDefaultAsync(s => s.Id == surveyId && !s.IsDeleted);

// Check if a username already exists
bool exists = await _userRepo.AnyAsync(u => u.Username == dto.Username);

// Add and save
await _surveyRepo.AddAsync(survey);
await _surveyRepo.SaveChangesAsync();
```


---

## 7. Unit of Work Pattern

### What Unit of Work Does

This project uses a simplified Unit of Work approach. There is no explicit `IUnitOfWork` class — instead, the `SaveChangesAsync()` method on the repository acts as the commit point.

All repositories for a given HTTP request share the **same `FeedBackDbContext` instance** (because it is registered as `Scoped`). This means:

- Multiple repository operations within one service method all participate in the same EF Core change tracker
- Calling `SaveChangesAsync()` on any one repository commits all pending changes from all repositories in that request

### Why This Matters

```csharp
// In QuestionService.UpdateQuestionAsync():

// Step 1: Remove old options (uses _optionRepo)
_optionRepo.RemoveRange(question.Options);

// Step 2: Add new options (uses _optionRepo)
foreach (var optionDto in dto.Options)
    await _optionRepo.AddAsync(new QuestionOption { ... });

// Step 3: Update the question itself (uses _questionRepo)
_questionRepo.Update(question);

// Step 4: ONE SaveChangesAsync commits ALL of the above atomically
await _questionRepo.SaveChangesAsync();
```

If `SaveChangesAsync()` throws, none of the changes are persisted. This is the Unit of Work guarantee — all or nothing.

### The Audit Exception

The `AuditService` deliberately uses its own **independent** `FeedBackDbContext` via `AuditDbContextFactory`. This means:

- Audit writes never share a transaction with the business operation
- If the main operation fails and rolls back, the audit log is still written
- If the audit write fails, it never crashes the main request

```csharp
// AuditDbContextFactory creates a brand-new context with its own connection
public class AuditDbContextFactory : IAuditDbContextFactory
{
    private readonly string _connectionString;

    public FeedBackDbContext Create()
    {
        var options = new DbContextOptionsBuilder<FeedBackDbContext>()
            .UseSqlServer(_connectionString)
            .Options;
        return new FeedBackDbContext(options);
    }
}

// In AuditService.LogAsync():
await using var db = _dbFactory.Create();  // independent context
db.AuditLogs.Add(log);
await db.SaveChangesAsync();               // its own transaction
```


---

## 8. Services Layer

Services contain all business logic. Controllers are kept thin — they only extract data from the HTTP request and call the appropriate service method.

### Why Services Are Separated from Controllers

- Controllers should not contain `if` statements about business rules
- Services can be unit tested without spinning up an HTTP server
- The same service method can be called from multiple controllers if needed

### AuthService

Handles registration and login. Generates JWT tokens.

Key logic:
- Registration always assigns `UserRole.Creator` — the role field in the DTO is intentionally ignored to prevent privilege escalation
- Passwords are hashed with `HMACSHA512` — the key becomes the salt
- Login returns the same generic error message for wrong password, deleted account, and inactive account — prevents user enumeration attacks

```csharp
// Password hashing on register:
using var hmac = new HMACSHA512();
user.PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dto.Password));
user.PasswordSalt = hmac.Key;

// Password verification on login:
using var hmac = new HMACSHA512(user.PasswordSalt);
var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dto.Password));
if (!computedHash.SequenceEqual(user.PasswordHash))
    throw new BadRequestException("Invalid username or password.");
```

---

### SurveyService

Manages the full survey lifecycle.

Key logic:
- Creators can only access their own surveys; Admins can access all
- A survey can only be edited when `State == Inactive`
- A survey can only be published (set to Active) if it has at least one question
- Soft delete sets `IsDeleted = true` — the global query filter hides it automatically
- `CloneQuestionsAsync` copies all questions and options from one survey to another

```csharp
// State transition guard:
if (survey.State != SurveyState.Inactive)
    throw new BadRequestException("Only Inactive surveys can be edited.");

// Publish guard:
var hasQuestions = await _questionRepo.GetQueryable()
    .AnyAsync(q => q.SurveyId == surveyId && !string.IsNullOrWhiteSpace(q.Text));
if (!hasQuestions)
    throw new BadRequestException("Cannot publish a survey with no questions.");
```

---

### QuestionService

Manages questions within a survey.

Key logic:
- Questions can only be added/updated/deleted when the survey is `Inactive`
- After saving a question, it is automatically saved to the Question Bank (deduplication via SHA256 hash)
- Options are replaced atomically on update — old options are removed, new ones are added

---

### ResponseService

Handles survey submission and response retrieval.

Key logic:
- Validates survey is `Active` and within date range
- Checks `AllowAnonymous` — if false, user must be authenticated
- Prevents duplicate submissions for authenticated users (checked in code AND enforced by a unique DB index)
- Handles race conditions: if two requests arrive simultaneously, the DB unique constraint catches the second one

```csharp
// Duplicate check:
var duplicate = await _responseRepo.AnyAsync(
    r => r.SurveyId == survey.Id && r.UserId == userId.Value);
if (duplicate)
    throw new ConflictException("You have already submitted a response for this survey.");

// Race condition fallback:
catch (DbUpdateException ex) when (DbExceptionHelper.IsUniqueConstraintViolation(ex))
{
    throw new ConflictException("You have already submitted a response for this survey.");
}
```

---

### AnalyticsService

Computes per-question analytics for a survey.

Key logic:
- Rating questions → calculates average rating
- Single choice → counts and percentages per option
- Multiple choice → parses comma-separated `SelectedOptionIds`, counts per option
- Text questions → returns list of all text responses
- Date-wise counts → groups responses by date for a timeline chart
- Optimized: fetches `SubmittedAt` values in one query, derives both total count and date grouping from the in-memory list

---

### UserService

Admin-only user management.

Key logic:
- Soft delete prevents deleting users who have active surveys
- Status change (activate/deactivate) is blocked for already-deleted users
- `GetSurveysByUserAsync` uses `IgnoreQueryFilters()` to include soft-deleted surveys in the admin view

---

### AuditService

Writes audit log entries using an independent DB connection.

Key logic:
- Uses `IAuditDbContextFactory` to create a fresh `DbContext` — never shares a transaction with the caller
- Wrapped in try/catch — a failed audit write never crashes the main request
- Called with fire-and-forget (`_ = _audit.LogAsync(...)`) so it doesn't block the response

---

### ExcelService

Exports survey responses as a formatted `.xlsx` file using the `ClosedXML` library.

Produces two sheets:
1. **Responses** — one row per response, one column per question, with frozen header row and alternating row colors
2. **Summary** — survey metadata (title, total responses, date range, export timestamp)

---

### QuestionBankService

Manages the reusable question library.

Key logic:
- `AutoSaveQuestionsAsync` — called automatically when a question is created/updated. Computes a SHA256 hash of `(text + type + options)` and only saves if the hash doesn't already exist for that user
- `CloneIntoSurveyAsync` — copies bank questions into a draft survey, preserving order, in a single `SaveChangesAsync`

---

### QuestionImportService

Imports questions from an Excel file.

Key logic:
- Parses the Excel using `ClosedXML`, validates each row, and builds `Question` and/or `BankQuestion` entities
- Supports type aliases: `MCQ`, `Text`, `Rating`, `Single`, etc.
- Validates: question text required, type must be recognized, choice questions need at least 2 options
- All valid rows are saved in a single `SaveChangesAsync` — partial success is reported (success count + error list)

---

### AdminSurveyService

Admin-only survey management that bypasses soft-delete filters.

Key logic:
- Uses `IgnoreQueryFilters()` everywhere to see deleted surveys
- `GetStatsAsync` returns total/active/deleted survey counts and total response count in one grouped query
- `RestoreSurveyAsync` undoes a soft delete


---

## 9. Controllers

Controllers are the entry point for HTTP requests. Each controller:
1. Declares a route prefix with `[Route]`
2. Declares authorization requirements with `[Authorize]`
3. Extracts data from the request (route params, query string, body)
4. Calls the appropriate service method
5. Returns a consistent JSON response

All success responses follow this shape:
```json
{ "success": true, "data": { ... } }
```

All error responses follow this shape:
```json
{ "success": false, "statusCode": 404, "message": "Survey not found.", "traceId": "..." }
```

### AuthController — `/api/auth`

```
POST /api/auth/register   → Register a new Creator account
POST /api/auth/login      → Login (all roles)
```

Both endpoints are `[AllowAnonymous]` and protected by the `"auth"` rate limit policy (10 requests/minute per IP).

---

### SurveyController — `/api/survey`

Requires `[Authorize(Roles = "Admin,Creator")]`.

```
GET    /api/survey              → Get paginated list of surveys (filtered by role)
GET    /api/survey/{id}         → Get survey by ID
POST   /api/survey              → Create a new survey
PUT    /api/survey/{id}         → Update survey metadata
PATCH  /api/survey/{id}/state   → Change survey state (Inactive/Active/Closed)
DELETE /api/survey/{id}         → Soft-delete a survey
POST   /api/survey/{sourceId}/clone-questions/{targetId} → Clone questions between surveys
```

The controller extracts `userId` and `role` from JWT claims and passes them to the service for access control:

```csharp
private int    GetUserId()   => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
private string GetUserRole() => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
```

---

### QuestionController — `/api/surveys/{surveyId}/questions`

Requires `[Authorize(Roles = "Admin,Creator")]`. Questions are nested under their survey.

```
GET    /api/surveys/{surveyId}/questions              → Get all questions for a survey
POST   /api/surveys/{surveyId}/questions              → Add a question
PUT    /api/surveys/{surveyId}/questions/{questionId} → Update a question
DELETE /api/surveys/{surveyId}/questions/{questionId} → Delete a question
```

---

### ResponseController — `/api/surveys`

Mixed authorization — submit is public, get is protected.

```
POST /api/surveys/{publicToken}/responses   → Submit a response (anonymous or authenticated)
GET  /api/surveys/{surveyId}/responses      → Get all responses (Admin/Creator only)
```

The submit endpoint uses the survey's `publicToken` (a GUID) — not the integer ID — so the URL is not guessable. It is also protected by the `"survey-submit"` rate limit (5 submissions/minute per IP).

---

### AnalyticsController — `/api/surveys/{surveyId}/analytics`

Requires `[Authorize(Roles = "Admin,Creator")]`.

```
GET /api/surveys/{surveyId}/analytics → Get analytics for a survey
```

Supports optional query params: `?fromDate=2025-01-01&toDate=2025-12-31`

---

### UserController — `/api/user`

Requires `[Authorize(Roles = "Admin")]` — all endpoints are admin-only.

```
GET    /api/user              → List all users (paginated, searchable)
GET    /api/user/{id}         → Get user detail
GET    /api/user/{id}/surveys → Get all surveys by this user
PATCH  /api/user/{id}/role    → Change user role
PATCH  /api/user/{id}/status  → Activate or deactivate user
DELETE /api/user/{id}         → Soft-delete user
```

---

### AdminSurveyController — `/api/admin/surveys`

Requires `[Authorize(Roles = "Admin")]`.

```
GET    /api/admin/surveys           → All surveys (including deleted), paginated
GET    /api/admin/surveys/stats     → Dashboard stats (totals)
GET    /api/admin/surveys/{id}      → Full survey detail
PATCH  /api/admin/surveys/{id}/state   → Force any state change
PATCH  /api/admin/surveys/{id}/restore → Undo soft-delete
DELETE /api/admin/surveys/{id}         → Soft-delete with audit log
```

---

### AuditController — `/api/audit`

Requires `[Authorize(Roles = "Admin")]`. Queries the `AuditLogs` table directly (no service layer needed here).

```
GET /api/audit       → Paginated audit log with filters (search, action, entity, userId, date range)
GET /api/audit/meta  → Distinct actions and entity names for filter dropdowns
```

---

### ExportController — `/api/surveys/{surveyId}/export`

Requires `[Authorize(Roles = "Admin,Creator")]`.

```
GET /api/surveys/{surveyId}/export/excel → Download responses as .xlsx file
```

Returns a binary file response with content type `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`.

---

### QuestionBankController — `/api/question-bank`

Requires `[Authorize(Roles = "Admin,Creator")]`.

```
GET    /api/question-bank                    → List bank questions (paginated, filterable)
GET    /api/question-bank/{id}               → Get one bank question
POST   /api/question-bank                    → Create a bank question
PUT    /api/question-bank/{id}               → Update a bank question
DELETE /api/question-bank/{id}               → Soft-delete a bank question
POST   /api/question-bank/clone-into-survey  → Clone bank questions into a draft survey
```

---

### QuestionImportController — `/api/questions`

Requires `[Authorize(Roles = "Admin,Creator")]`.

```
POST /api/questions/import-excel     → Upload Excel file to import questions
GET  /api/questions/import-template  → Download the Excel template
```

File upload is validated for extension (`.xlsx`, `.xls`) and MIME type. Max file size is 10 MB.

---

### PublicSurveyController — `/survey`

No authentication required.

```
GET /survey/{publicToken} → Load a survey for public display (validates state and date range)
```


---

## 10. Authentication & Token System

### JWT Authentication

JWT (JSON Web Token) is a compact, self-contained token. The server generates it on login and the client sends it back on every request in the `Authorization` header.

```
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### Token Generation

```csharp
// In AuthService.GenerateToken():
private string GenerateToken(User user)
{
    var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),  // user ID
        new Claim(ClaimTypes.Name,           user.Username),
        new Claim(ClaimTypes.Email,          user.Email),
        new Claim(ClaimTypes.Role,           user.Role.ToString()) // "Admin" or "Creator"
    };

    var token = new JwtSecurityToken(
        issuer:             jwtSettings["Issuer"],    // "FeedBackApp"
        audience:           jwtSettings["Audience"],  // "FeedBackAppUsers"
        claims:             claims,
        expires:            DateTime.UtcNow.AddMinutes(expiryMinutes),  // default 1440 min = 24h
        signingCredentials: credentials
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

The token is signed with `HMACSHA256` using the secret key from `appsettings.json`. Anyone can decode the payload (it's Base64), but they cannot forge a valid signature without the secret key.

### Token Validation

Configured in `Program.cs`:

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidIssuer              = "FeedBackApp",
            ValidAudience            = "FeedBackAppUsers",
            ValidateLifetime         = true,          // rejects expired tokens
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        });
```

On every request, ASP.NET Core automatically:
1. Reads the `Authorization: Bearer ...` header
2. Validates the signature, issuer, audience, and expiry
3. Populates `HttpContext.User` with the claims from the token

### Claims in Controllers

Controllers read claims from `HttpContext.User`:

```csharp
// Get the logged-in user's ID from the token:
int userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

// Get the role:
string role = User.FindFirstValue(ClaimTypes.Role);  // "Admin" or "Creator"
```

### Role-Based Authorization

```csharp
[Authorize(Roles = "Admin")]          // only Admins
[Authorize(Roles = "Admin,Creator")]  // Admins and Creators
[AllowAnonymous]                      // no token required
```


---

## 11. Exception Handling

### Custom Exceptions

All domain exceptions inherit from `AppException`, which carries an HTTP status code:

```csharp
// Exceptions/AppExceptions.cs
public class AppException : Exception
{
    public int StatusCode { get; }
    public AppException(string message, int statusCode) : base(message)
        => StatusCode = statusCode;
}

public class NotFoundException    : AppException { public NotFoundException(string msg)    : base(msg, 404) {} }
public class ConflictException    : AppException { public ConflictException(string msg)    : base(msg, 409) {} }
public class BadRequestException  : AppException { public BadRequestException(string msg)  : base(msg, 400) {} }
public class ForbiddenException   : AppException { public ForbiddenException(string msg)   : base(msg, 403) {} }
public class UnAuthorizedException: AppException { public UnAuthorizedException(string msg): base(msg, 401) {} }
```

Services throw these exceptions instead of returning error codes. Example:

```csharp
var survey = await _surveyRepo.GetByIdAsync(surveyId);
if (survey == null)
    throw new NotFoundException($"Survey with ID {surveyId} not found.");

if (survey.CreatedBy != userId)
    throw new ForbiddenException("You do not have access to this survey.");
```

### Global Exception Middleware

`GlobalExceptionMiddleware` wraps the entire request pipeline. Any unhandled exception is caught here:

```csharp
public async Task InvokeAsync(HttpContext context)
{
    try
    {
        await _next(context);  // run the rest of the pipeline
    }
    catch (Exception ex)
    {
        await HandleExceptionAsync(context, ex);
    }
}

private async Task HandleExceptionAsync(HttpContext context, Exception ex)
{
    // AppException subclasses carry their own status code
    var statusCode = ex is AppException appEx ? appEx.StatusCode : 500;
    var message    = ex is AppException ? ex.Message : "An unexpected error occurred.";

    // Log the error
    _logger.LogError(ex, "Exception | Status:{StatusCode} | ...", statusCode, ...);

    // Write the exception to the audit log (using independent DB connection)
    await using var db = _auditFactory.Create();
    db.AuditLogs.Add(new AuditLog { Action = $"Exception:{ex.GetType().Name}", ... });
    await db.SaveChangesAsync();

    // Return structured JSON error response
    context.Response.StatusCode = statusCode;
    await context.Response.WriteAsJsonAsync(new
    {
        success    = false,
        statusCode,
        message,
        traceId    = context.TraceIdentifier
    });
}
```

### Error Response Examples

```json
// 404 Not Found
{ "success": false, "statusCode": 404, "message": "Survey with ID 99 not found.", "traceId": "..." }

// 400 Bad Request
{ "success": false, "statusCode": 400, "message": "Only Inactive surveys can be edited.", "traceId": "..." }

// 409 Conflict
{ "success": false, "statusCode": 409, "message": "You have already submitted a response for this survey.", "traceId": "..." }

// 500 Internal Server Error
{ "success": false, "statusCode": 500, "message": "An unexpected error occurred.", "traceId": "..." }
```


---

## 12. Middleware

Middleware is code that runs on every HTTP request, in a defined order, before and after the controller handles it. Think of it as a pipeline of layers.

### Request Pipeline Order

```
HTTP Request
    │
    ▼
UseHttpsRedirection       → Redirect HTTP to HTTPS
    │
    ▼
UseCors                   → Add CORS headers (allow Angular frontend)
    │
    ▼
UseRateLimiter            → Enforce rate limits (429 if exceeded)
    │
    ▼
RequestLoggingMiddleware  → Log request, assign CorrelationId
    │
    ▼
UseRouting                → Match URL to controller action
    │
    ▼
GlobalExceptionMiddleware → Catch any unhandled exceptions
    │
    ▼
UseAuthentication         → Validate JWT token, populate HttpContext.User
    │
    ▼
UseAuthorization          → Check [Authorize] attributes
    │
    ▼
MapControllers            → Execute the controller action
    │
    ▼
HTTP Response
```

### RequestLoggingMiddleware

Runs before everything else (except HTTPS redirect and CORS). It:
1. Reads or generates a `X-Correlation-ID` header — a unique ID for this request
2. Stores it in `HttpContext.Items["CorrelationId"]` so other middleware and services can use it
3. Logs the incoming request (method, path, IP)
4. After the response, logs the status code and duration

```csharp
// Generates or propagates correlation ID:
var correlationId =
    context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
    ?? Guid.NewGuid().ToString("N");

context.Items["CorrelationId"]               = correlationId;
context.Response.Headers["X-Correlation-ID"] = correlationId;

// Logs request:
_logger.LogInformation("[REQ ] {Method} {Path} from {IP} CorrelationId={CorrelationId}", ...);

// After response:
_logger.Log(level, "[RESP] {Method} {Path} -> {Status} in {Ms}ms ...", ...);
```

Skips logging for `/health`, `/favicon.ico`, and `/swagger/index.html` to reduce noise.

### GlobalExceptionMiddleware

Catches any exception that bubbles up from controllers or services. Returns a structured JSON error response. Also writes the exception to the audit log. (See Section 11 for full details.)

### Rate Limiting Middleware

Three policies are configured:

| Policy | Limit | Applied To |
|---|---|---|
| `"auth"` | 10 requests/min per IP | `POST /api/auth/login` and `POST /api/auth/register` |
| `"survey-submit"` | 5 requests/min per IP | `POST /api/surveys/{token}/responses` |
| Global | 200 requests/min per IP | All endpoints |

When a limit is exceeded, the API returns HTTP 429 with:
```json
{ "success": false, "statusCode": 429, "message": "Too many requests. Please slow down and try again shortly." }
```

### Authentication Middleware

`UseAuthentication()` reads the `Authorization: Bearer ...` header, validates the JWT, and populates `HttpContext.User` with the claims. If the token is invalid or missing, `HttpContext.User` is unauthenticated — the request is not rejected here, only when `[Authorize]` is checked.

### Authorization Middleware

`UseAuthorization()` checks `[Authorize]` attributes on controllers and actions. If the user is not authenticated or doesn't have the required role, it returns HTTP 401 or 403.


---

## 13. Program.cs — Step by Step

`Program.cs` is the application entry point. It does two things: registers services into the DI container (`builder.Services`), then configures the middleware pipeline (`app.Use...`).

```csharp
var builder = WebApplication.CreateBuilder(args);
```
Creates the app builder. Loads configuration from `appsettings.json`, environment variables, and command-line args.

---

### Service Registrations (builder.Services)

```csharp
// Rate limiting — 3 policies: auth, survey-submit, global
builder.Services.AddRateLimiter(options => { ... });
```
Registers the rate limiter with fixed-window policies per IP address.

```csharp
// Controllers + JSON camelCase serialization
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
```
Registers MVC controllers. `CamelCase` means C# property `SurveyId` becomes `surveyId` in JSON.

```csharp
// Swagger UI with JWT support
builder.Services.AddSwaggerGen(c => { ... });
```
Registers Swagger/OpenAPI documentation. Adds a "Bearer" security definition so you can test authenticated endpoints directly in the Swagger UI.

```csharp
// SQL Server database
builder.Services.AddDbContext<FeedBackDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
```
Registers `FeedBackDbContext` as a scoped service. One instance per HTTP request.

```csharp
// CORS — open in dev, restricted in production
builder.Services.AddCors(options => { ... });
```
In development: allows any origin (Angular dev server on any port).
In production: only allows the URL configured in `CorsOrigins`.

```csharp
// Generic repository — one registration covers all entity types
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
```
`IRepository<Survey>`, `IRepository<User>`, etc. are all resolved automatically.

```csharp
// Audit factory — Singleton because it only holds a connection string
builder.Services.AddSingleton<IAuditDbContextFactory>(
    new AuditDbContextFactory(connectionString));
```
Singleton lifetime — safe because it creates new `DbContext` instances on demand.

```csharp
// Application services — all Scoped (one per request)
builder.Services.AddScoped<IAuthService,      AuthService>();
builder.Services.AddScoped<ISurveyService,    SurveyService>();
builder.Services.AddScoped<IQuestionService,  QuestionService>();
// ... etc.
```

```csharp
// In-memory cache for OTP (if used)
builder.Services.AddMemoryCache();
```

```csharp
// JWT authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidIssuer              = "FeedBackApp",
        ValidAudience            = "FeedBackAppUsers",
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    });
builder.Services.AddAuthorization();
```

```csharp
// Health check endpoint (for Kubernetes / load balancers)
builder.Services.AddHealthChecks().AddDbContextCheck<FeedBackDbContext>();
```

---

### App Build and Database Seed

```csharp
var app = builder.Build();
```
Builds the app and finalizes the DI container.

```csharp
db.Database.Migrate();
```
Runs all pending EF Core migrations on startup. Safe to run multiple times (idempotent).

```csharp
// Seed admin user if none exists
if (!db.Users.Any(u => u.Role == UserRole.Admin))
{
    db.Users.Add(new User { Username = "admin", Role = UserRole.Admin, ... });
    db.SaveChanges();
}
```
Creates the first admin account on first run. Credentials come from `appsettings.json` (`AdminSeed` section).

---

### Middleware Pipeline (app.Use...)

```csharp
app.UseHsts();                              // HSTS header in production
app.UseHttpsRedirection();                  // HTTP → HTTPS redirect
app.UseCors();                              // CORS headers
app.UseRateLimiter();                       // Rate limiting
app.UseMiddleware<RequestLoggingMiddleware>(); // Request logging + CorrelationId
app.UseRouting();                           // Route matching
app.UseMiddleware<GlobalExceptionMiddleware>(); // Global error handler
app.UseAuthentication();                    // JWT validation
app.UseAuthorization();                     // Role/policy checks
app.MapControllers();                       // Execute controller actions
app.MapHealthChecks("/health");             // Health check endpoint
```


---

## 14. Pagination

### Why Pagination Is Used

Without pagination, a query like "get all surveys" could return thousands of rows in one response. This is slow, wastes bandwidth, and can crash the browser. Pagination returns a small "page" of results at a time.

### PaginationParams Base Class

All filter parameter classes inherit from `PaginationParams`:

```csharp
public class PaginationParams
{
    private const int MaxPageSize = 50;
    private int _pageSize = 10;

    public int PageNumber { get; set; } = 1;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value;  // cap at 50
    }
}
```

The `PageSize` setter automatically caps the value at 50 — clients cannot request more than 50 items per page.

### How Skip/Take Works

```csharp
// In SurveyService.GetAllAsync():
var surveys = await query
    .Skip((pageNumber - 1) * pageSize)  // skip previous pages
    .Take(pageSize)                      // take only this page
    .ToListAsync();
```

Example: `pageNumber=3, pageSize=10` → skip 20, take 10 → rows 21–30.

EF Core translates this to SQL:
```sql
SELECT ... FROM Surveys ORDER BY CreatedAt DESC
OFFSET 20 ROWS FETCH NEXT 10 ROWS ONLY
```

### Query Parameters

Clients pass pagination via query string:

```
GET /api/survey?pageNumber=2&pageSize=20&search=customer&status=Active&sortBy=title&sortDir=asc
```

### Pagination Response Structure

```json
{
  "success": true,
  "data": {
    "items": [ ... ],
    "pageNumber": 2,
    "pageSize": 20,
    "totalCount": 87,
    "totalPages": 5,
    "hasPrevious": true,
    "hasNext": true
  }
}
```

The Angular frontend uses `totalPages`, `hasPrevious`, and `hasNext` to render pagination controls.


---

## 15. SQL & Database Operations

### Tables Created

EF Core creates the following tables from the entity models:

| Table | Entity | Notes |
|---|---|---|
| `Users` | `User` | Unique indexes on `Username` and `Email` |
| `Surveys` | `Survey` | Unique index on `PublicToken`; global query filter on `IsDeleted` |
| `Questions` | `Question` | Cascade delete from Survey |
| `QuestionOptions` | `QuestionOption` | Cascade delete from Question |
| `SurveyResponses` | `SurveyResponse` | Unique index on `(SurveyId, UserId)` where `UserId IS NOT NULL` |
| `Answers` | `Answer` | NoAction delete on Question and QuestionOption FKs |
| `AuditLogs` | `AuditLog` | GUID primary key; no FK to Users (intentional — logs survive user deletion) |
| `BankQuestions` | `BankQuestion` | Soft delete; SHA256 hash for deduplication |
| `BankQuestionOptions` | `BankQuestionOption` | Cascade delete from BankQuestion |

### Relationships in SQL

```sql
-- Survey.CreatedBy → Users.Id (RESTRICT — cannot delete user with surveys)
ALTER TABLE Surveys ADD CONSTRAINT FK_Surveys_Users_CreatedBy
    FOREIGN KEY (CreatedBy) REFERENCES Users(Id) ON DELETE RESTRICT;

-- Questions.SurveyId → Surveys.Id (CASCADE — delete survey = delete questions)
ALTER TABLE Questions ADD CONSTRAINT FK_Questions_Surveys_SurveyId
    FOREIGN KEY (SurveyId) REFERENCES Surveys(Id) ON DELETE CASCADE;

-- Answers.QuestionId → Questions.Id (NO ACTION — prevents cascade cycles)
ALTER TABLE Answers ADD CONSTRAINT FK_Answers_Questions_QuestionId
    FOREIGN KEY (QuestionId) REFERENCES Questions(Id) ON DELETE NO ACTION;
```

### Queries Generated by EF Core

**Simple lookup:**
```csharp
await _surveyRepo.GetByIdAsync(id);
// → SELECT TOP(1) * FROM Surveys WHERE Id = @id
```

**Filtered paginated query:**
```csharp
query.Where(s => s.Title.Contains(filter.Search))
     .OrderByDescending(s => s.CreatedAt)
     .Skip(20).Take(10)
// → SELECT ... FROM Surveys WHERE Title LIKE '%customer%'
//   ORDER BY CreatedAt DESC OFFSET 20 ROWS FETCH NEXT 10 ROWS ONLY
```

**Eager loading with Include:**
```csharp
_surveyRepo.GetQueryable()
    .Include(s => s.Questions)
        .ThenInclude(q => q.Options)
// → SELECT s.*, q.*, o.* FROM Surveys s
//   LEFT JOIN Questions q ON q.SurveyId = s.Id
//   LEFT JOIN QuestionOptions o ON o.QuestionId = q.Id
```

### Migrations

EF Core migrations version the database schema. Each migration is a C# file with `Up()` (apply) and `Down()` (rollback) methods.

```
Migrations/
├── 20260330120046_InitialCreate.cs          ← Creates all tables
├── 20260330000001_AddIndexesAndUniqueConstraints.cs  ← Adds indexes
├── 20260330200000_RemoveAccessControl.cs    ← Schema cleanup
└── FeedBackDbContextModelSnapshot.cs        ← Current schema snapshot
```

On startup, `db.Database.Migrate()` applies any pending migrations automatically.

To add a new migration manually:
```bash
dotnet ef migrations add MigrationName --project FeedBackApp
dotnet ef database update
```


---

## 16. HTTP Concepts Used

### HTTP Methods

| Method | Purpose | Example in This Project |
|---|---|---|
| `GET` | Retrieve data (no side effects) | `GET /api/survey` — list surveys |
| `POST` | Create a new resource | `POST /api/survey` — create survey |
| `PUT` | Replace an entire resource | `PUT /api/survey/{id}` — update all survey fields |
| `PATCH` | Partially update a resource | `PATCH /api/survey/{id}/state` — change only the state |
| `DELETE` | Delete a resource | `DELETE /api/survey/{id}` — soft-delete survey |

### HTTP Status Codes Used

| Code | Meaning | When Used |
|---|---|---|
| `200 OK` | Success | GET, PUT, PATCH, DELETE responses |
| `201 Created` | Resource created | POST responses (survey, question, response) |
| `400 Bad Request` | Invalid input | Validation errors, business rule violations |
| `401 Unauthorized` | Not authenticated | Missing or invalid JWT token |
| `403 Forbidden` | Not authorized | Authenticated but wrong role or not the owner |
| `404 Not Found` | Resource doesn't exist | Survey/user/question not found |
| `409 Conflict` | Duplicate resource | Username taken, duplicate survey response |
| `429 Too Many Requests` | Rate limit exceeded | Too many login attempts or survey submissions |
| `500 Internal Server Error` | Unexpected error | Unhandled exceptions |

### Route Patterns

```csharp
[Route("api/[controller]")]          // → /api/survey, /api/user
[Route("api/surveys/{surveyId}/questions")]  // nested resource
[Route("survey")]                    // public route (no "api/" prefix)
```

`[controller]` is replaced by the controller class name minus "Controller" (e.g., `SurveyController` → `survey`).

### Request/Response Flow Example

```
Client sends:
POST /api/survey
Authorization: Bearer eyJ...
Content-Type: application/json
Body: { "title": "Customer Feedback", "allowAnonymous": true }

Server responds:
HTTP 201 Created
Body: {
  "success": true,
  "data": {
    "id": 42,
    "title": "Customer Feedback",
    "publicToken": "a1b2c3d4-...",
    "state": "Inactive",
    ...
  }
}
```


---

## 17. OOP Concepts Used

### Encapsulation

Properties and internal logic are hidden behind controlled access. The `PaginationParams` class encapsulates the max page size rule:

```csharp
public class PaginationParams
{
    private const int MaxPageSize = 50;
    private int _pageSize = 10;

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value;  // enforced internally
    }
}
```

Clients cannot bypass the 50-item cap — it's enforced by the setter.

### Abstraction

Services and repositories are accessed through interfaces, not concrete classes. Controllers and services only know about the interface contract:

```csharp
// Controller only knows about the interface:
public SurveyController(ISurveyService surveyService)
    => _surveyService = surveyService;

// It doesn't know or care that SurveyService exists
```

This means you can swap `SurveyService` for a mock in tests without changing the controller.

### Inheritance

All custom exceptions inherit from `AppException`:

```csharp
public class AppException : Exception
{
    public int StatusCode { get; }
    public AppException(string message, int statusCode) : base(message)
        => StatusCode = statusCode;
}

public class NotFoundException   : AppException { ... : base(msg, 404) }
public class BadRequestException : AppException { ... : base(msg, 400) }
public class ForbiddenException  : AppException { ... : base(msg, 403) }
```

`GlobalExceptionMiddleware` checks `ex is AppException` — one check handles all domain exceptions.

All filter parameter classes inherit from `PaginationParams`:

```csharp
public class SurveyFilterParams  : PaginationParams { ... }
public class UserFilterParams    : PaginationParams { ... }
public class ResponseFilterParams: PaginationParams { ... }
```

### Polymorphism

The generic repository `Repository<T>` works for any entity type. The same code handles `Survey`, `User`, `Question`, etc.:

```csharp
public class Repository<T> : IRepository<T> where T : class
{
    private readonly DbSet<T> _dbSet;
    // Same implementation works for every entity
}
```

`GlobalExceptionMiddleware` uses polymorphism to handle all exception types:

```csharp
var statusCode = ex is AppException appEx ? appEx.StatusCode : 500;
// NotFoundException, BadRequestException, ForbiddenException all handled by one line
```

### Method Overriding

`FeedBackDbContext` overrides `OnModelCreating` from the base `DbContext` class to configure relationships and indexes:

```csharp
public class FeedBackDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);  // call base first
        // then add custom configuration...
    }
}
```

### Interfaces

Every service and the repository have a corresponding interface:

```csharp
public interface ISurveyService
{
    Task<SurveyResponseDto> CreateAsync(CreateSurveyDto dto, int userId);
    Task<SurveyResponseDto> UpdateAsync(int id, UpdateSurveyDto dto, int userId, string role);
    // ...
}

public class SurveyService : ISurveyService { ... }
```

This enables unit testing with mocks and follows the Dependency Inversion Principle.

### Dependency Injection

All dependencies are injected via constructors — nothing is `new`-ed up manually inside classes:

```csharp
public class SurveyService : ISurveyService
{
    private readonly IRepository<Survey>   _surveyRepo;
    private readonly IRepository<Question> _questionRepo;
    private readonly IAuditService         _audit;

    // ASP.NET Core DI container provides these automatically
    public SurveyService(
        IRepository<Survey>   surveyRepo,
        IRepository<Question> questionRepo,
        IAuditService         audit)
    {
        _surveyRepo   = surveyRepo;
        _questionRepo = questionRepo;
        _audit        = audit;
    }
}
```

The DI container resolves the entire dependency graph automatically when a request arrives.


---

## 18. Coding Standards

### Naming Conventions

| Element | Convention | Example |
|---|---|---|
| Classes | PascalCase | `SurveyService`, `AuthController` |
| Interfaces | `I` prefix + PascalCase | `ISurveyService`, `IRepository<T>` |
| Methods | PascalCase | `GetAllAsync`, `CreateAsync` |
| Properties | PascalCase | `PublicToken`, `IsDeleted` |
| Private fields | `_camelCase` | `_surveyRepo`, `_audit` |
| Local variables | camelCase | `pageNumber`, `totalCount` |
| Constants | PascalCase or UPPER_CASE | `MaxPageSize` |
| Enums | PascalCase | `SurveyState.Active`, `UserRole.Admin` |
| DTOs | Suffix with `Dto` | `CreateSurveyDto`, `AuthResponseDto` |
| Controllers | Suffix with `Controller` | `SurveyController` |
| Services | Suffix with `Service` | `SurveyService` |

### Folder Organization

Code is organized by technical layer (Controllers, Services, Models) rather than by feature. This is a standard approach for small-to-medium APIs.

### Async/Await Usage

All I/O operations (database queries, file reads) are async. This prevents threads from blocking while waiting for the database:

```csharp
// Always async for DB operations:
public async Task<SurveyResponseDto> CreateAsync(CreateSurveyDto dto, int userId)
{
    await _surveyRepo.AddAsync(survey);
    await _surveyRepo.SaveChangesAsync();
    return MapToResponseDto(survey);
}
```

Fire-and-forget is used for audit logging so it doesn't add latency to the response:

```csharp
_ = _audit.LogAsync("Create", "Survey", survey.Id.ToString(), userId);
// The _ discard means we don't await it — it runs in the background
```

### Dependency Injection

All services are registered as `Scoped` (one instance per HTTP request). The `AuditDbContextFactory` is `Singleton` (one instance for the app lifetime) because it only holds a connection string.

### Clean Architecture Principles

- Controllers are thin — no business logic, only HTTP concerns
- Services contain all business rules
- Repositories handle all data access
- DTOs decouple the API contract from the database schema
- Interfaces decouple implementations from their consumers
- Custom exceptions carry semantic meaning (not just error strings)
- Global middleware handles cross-cutting concerns (logging, error handling, rate limiting)

### Security Practices

- Passwords hashed with HMACSHA512 — never stored as plain text
- JWT secret key loaded from configuration — never hardcoded
- Role is always assigned server-side — never trusted from client input
- Same error message for all login failures — prevents user enumeration
- Soft delete instead of hard delete — data is preserved for audit purposes
- Rate limiting on sensitive endpoints — prevents brute force and spam
- CORS restricted in production — only the configured frontend origin is allowed


---

## 19. API Endpoints Summary

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/api/auth/register` | None | Register a new Creator account |
| POST | `/api/auth/login` | None | Login (all roles) |
| GET | `/api/survey` | Admin, Creator | List surveys (paginated, filtered) |
| GET | `/api/survey/{id}` | Admin, Creator | Get survey by ID |
| POST | `/api/survey` | Admin, Creator | Create a new survey |
| PUT | `/api/survey/{id}` | Admin, Creator | Update survey metadata |
| PATCH | `/api/survey/{id}/state` | Admin, Creator | Change survey state |
| DELETE | `/api/survey/{id}` | Admin, Creator | Soft-delete a survey |
| POST | `/api/survey/{sourceId}/clone-questions/{targetId}` | Admin, Creator | Clone questions between surveys |
| GET | `/api/surveys/{surveyId}/questions` | Admin, Creator | Get all questions for a survey |
| POST | `/api/surveys/{surveyId}/questions` | Admin, Creator | Add a question to a survey |
| PUT | `/api/surveys/{surveyId}/questions/{questionId}` | Admin, Creator | Update a question |
| DELETE | `/api/surveys/{surveyId}/questions/{questionId}` | Admin, Creator | Delete a question |
| GET | `/survey/{publicToken}` | None | Load public survey for display |
| POST | `/api/surveys/{publicToken}/responses` | None (optional) | Submit a survey response |
| GET | `/api/surveys/{surveyId}/responses` | Admin, Creator | Get all responses for a survey |
| GET | `/api/surveys/{surveyId}/analytics` | Admin, Creator | Get analytics for a survey |
| GET | `/api/surveys/{surveyId}/export/excel` | Admin, Creator | Export responses as .xlsx |
| GET | `/api/question-bank` | Admin, Creator | List bank questions |
| GET | `/api/question-bank/{id}` | Admin, Creator | Get one bank question |
| POST | `/api/question-bank` | Admin, Creator | Create a bank question |
| PUT | `/api/question-bank/{id}` | Admin, Creator | Update a bank question |
| DELETE | `/api/question-bank/{id}` | Admin, Creator | Soft-delete a bank question |
| POST | `/api/question-bank/clone-into-survey` | Admin, Creator | Clone bank questions into a survey |
| POST | `/api/questions/import-excel` | Admin, Creator | Import questions from Excel file |
| GET | `/api/questions/import-template` | Admin, Creator | Download Excel import template |
| GET | `/api/user` | Admin | List all users |
| GET | `/api/user/{id}` | Admin | Get user detail |
| GET | `/api/user/{id}/surveys` | Admin | Get surveys by user |
| PATCH | `/api/user/{id}/role` | Admin | Change user role |
| PATCH | `/api/user/{id}/status` | Admin | Activate/deactivate user |
| DELETE | `/api/user/{id}` | Admin | Soft-delete user |
| GET | `/api/admin/surveys` | Admin | All surveys (including deleted) |
| GET | `/api/admin/surveys/stats` | Admin | Dashboard statistics |
| GET | `/api/admin/surveys/{id}` | Admin | Full survey detail |
| PATCH | `/api/admin/surveys/{id}/state` | Admin | Force survey state change |
| PATCH | `/api/admin/surveys/{id}/restore` | Admin | Restore soft-deleted survey |
| DELETE | `/api/admin/surveys/{id}` | Admin | Soft-delete survey (admin) |
| GET | `/api/audit` | Admin | Paginated audit log |
| GET | `/api/audit/meta` | Admin | Distinct actions/entities for filters |
| GET | `/health` | None | Health check (DB connectivity) |


---

## 20. Complete Request Flow

This section traces a real request end-to-end: a Creator submitting a new survey response via the public link.

### Scenario: `POST /api/surveys/a1b2c3d4-e5f6-7890-abcd-ef1234567890/responses`

**Request body:**
```json
{
  "answers": [
    { "questionId": 5, "ratingValue": 4 },
    { "questionId": 6, "textValue": "Great product overall!" },
    { "questionId": 7, "selectedOptionId": 12 }
  ]
}
```

---

### Step 1: Angular Frontend

The Angular app builds the HTTP request and sends it to the API:

```typescript
// Angular service call
this.http.post(`/api/surveys/${publicToken}/responses`, { answers: [...] })
```

No `Authorization` header is sent (anonymous submission).

---

### Step 2: Middleware Pipeline

1. `UseHttpsRedirection` — request is already HTTPS, no redirect needed
2. `UseCors` — adds `Access-Control-Allow-Origin` header to the response
3. `UseRateLimiter` — checks the `"survey-submit"` policy (5/min per IP). Passes.
4. `RequestLoggingMiddleware` — generates `CorrelationId = "abc123"`, logs:
   ```
   [REQ ] POST /api/surveys/a1b2c3d4.../responses from 192.168.1.1 CorrelationId=abc123
   ```
5. `UseRouting` — matches the URL to `ResponseController.Submit()`
6. `GlobalExceptionMiddleware` — wraps the rest in try/catch
7. `UseAuthentication` — no `Authorization` header, so `HttpContext.User` is anonymous
8. `UseAuthorization` — endpoint is `[AllowAnonymous]`, so no check needed

---

### Step 3: Controller

```csharp
// ResponseController.Submit()
[HttpPost("{publicToken}/responses")]
[AllowAnonymous]
[EnableRateLimiting("survey-submit")]
public async Task<IActionResult> Submit(Guid publicToken, [FromBody] SubmitResponseDto dto)
{
    int? userId = null;  // anonymous — User.Identity.IsAuthenticated is false

    var result = await _responseService.SubmitAsync(publicToken, dto, userId);
    return StatusCode(201, new { success = true, data = result });
}
```

The controller:
- Parses `publicToken` from the URL
- Deserializes the JSON body into `SubmitResponseDto`
- Calls `_responseService.SubmitAsync()`
- Returns HTTP 201 with the result

---

### Step 4: Service (Business Logic)

```csharp
// ResponseService.SubmitAsync()
public async Task<ResponseListDto> SubmitAsync(Guid publicToken, SubmitResponseDto dto, int? userId)
{
    // 1. Find the survey by public token
    var survey = await _surveyRepo.GetQueryable()
        .Include(s => s.Questions).ThenInclude(q => q.Options)
        .FirstOrDefaultAsync(s => s.PublicToken == publicToken);

    if (survey == null) throw new NotFoundException("Survey not found.");

    // 2. Check state is Active
    if (survey.State != SurveyState.Active)
        throw new BadRequestException("This survey is not currently accepting responses.");

    // 3. Check date range
    // 4. Check AllowAnonymous (survey.AllowAnonymous = true, userId = null → OK)
    // 5. Skip duplicate check (userId is null)
    // 6. Validate required questions are answered

    // 7. Build and save the response
    var response = new SurveyResponse
    {
        SurveyId    = survey.Id,
        UserId      = null,  // anonymous
        SubmittedAt = DateTime.UtcNow,
        Answers     = dto.Answers.Select(a => new Answer { ... }).ToList()
    };

    await _responseRepo.AddAsync(response);
    await _responseRepo.SaveChangesAsync();

    return await MapToResponseListDto(response.Id);
}
```

---

### Step 5: Repository

```csharp
// Repository<SurveyResponse>.AddAsync()
public async Task AddAsync(SurveyResponse entity)
    => await _dbSet.AddAsync(entity);  // marks entity as Added in EF change tracker

// Repository<SurveyResponse>.SaveChangesAsync()
public async Task SaveChangesAsync()
    => await _context.SaveChangesAsync();  // generates and executes INSERT SQL
```

EF Core generates:
```sql
INSERT INTO SurveyResponses (SurveyId, UserId, SubmittedAt) VALUES (42, NULL, '2026-03-31 10:00:00')
INSERT INTO Answers (ResponseId, QuestionId, RatingValue) VALUES (101, 5, 4)
INSERT INTO Answers (ResponseId, QuestionId, TextValue) VALUES (101, 6, 'Great product overall!')
INSERT INTO Answers (ResponseId, QuestionId, SelectedOptionId) VALUES (101, 7, 12)
```

---

### Step 6: DbContext → SQL Server

EF Core sends the SQL to SQL Server via the connection string. SQL Server executes the INSERT statements and returns the generated IDs.

---

### Step 7: Response Back to Angular

The service maps the saved entity to a `ResponseListDto` and returns it. The controller wraps it:

```json
HTTP 201 Created
{
  "success": true,
  "data": {
    "id": 101,
    "surveyId": 42,
    "userId": null,
    "username": null,
    "submittedAt": "2026-03-31T10:00:00Z",
    "answers": [
      { "questionId": 5, "questionText": "Rate your experience", "questionType": "RatingScale", "ratingValue": 4 },
      { "questionId": 6, "questionText": "Any comments?", "questionType": "ShortText", "textValue": "Great product overall!" },
      { "questionId": 7, "questionText": "How did you hear about us?", "questionType": "SingleChoice", "selectedOptionId": 12, "selectedOptionText": "Social Media" }
    ]
  }
}
```

`RequestLoggingMiddleware` logs the response:
```
[RESP] POST /api/surveys/a1b2c3d4.../responses -> 201 in 45ms CorrelationId=abc123
```

The Angular app receives the 201 response and shows a success message to the user.

---

*End of Backend Documentation*
