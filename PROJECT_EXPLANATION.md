# Feedback Generator App — Project Explanation

## 1. What is This Project?

The Feedback Generator App is a **.NET 10 Web API** that allows organizations to create feedback surveys, distribute them to people, collect responses, and analyze the results. Think of it as a backend for apps like **Google Forms** or **SurveyMonkey**.

It is built using **Clean Architecture** with the **Repository + Service pattern**, meaning the code is organized into clear, separated layers.

---

## 2. Architecture Overview

```
Client (Swagger/Frontend/Mobile)
        │
        ▼
  ┌─────────────┐
  │ Controllers  │   ← Receives HTTP requests, returns responses
  └──────┬──────┘
         │
  ┌──────▼──────┐
  │  Services    │   ← Contains business logic (validation, processing)
  └──────┬──────┘
         │
  ┌──────▼──────┐
  │ Repository   │   ← Handles database operations (CRUD)
  └──────┬──────┘
         │
  ┌──────▼──────┐
  │  DbContext   │   ← Entity Framework Core (ORM)
  └──────┬──────┘
         │
  ┌──────▼──────┐
  │  MS SQL DB   │   ← Actual database
  └─────────────┘
```

**Data flows top-down:** Controller → Service → Repository → Database

**Each layer only talks to the one directly below it.** Controllers never touch the database directly. Services never handle HTTP requests.

---

## 3. Folder Structure Explained

```
FeedBackGeneratorApp/
│
├── Models/          → Database table definitions (entities)
├── DTOs/            → Data shapes for API requests/responses
├── Contexts/        → Database connection and configuration
├── Interfaces/      → Contracts (what methods must exist)
├── Repositories/    → Database operations (generic CRUD)
├── Services/        → Business logic
├── Middlewares/     → Global Exception Handling middleware
├── Controllers/     → API endpoints (HTTP routes)
├── Helpers/         → Utility classes (JWT, AutoMapper)
├── Program.cs       → App startup and configuration
└── appsettings.json → Settings (DB connection, JWT keys)
```

---

## 4. Layer-by-Layer Explanation

### 4.1 Models (Database Entities)

**Location:** `Models/`

Models represent the **database tables**. Each class = one table. Each property = one column.

| Model | Purpose | Key Fields |
|-------|---------|------------|
| `User` | Registered users | FullName, Email, PasswordHash, Role |
| `Survey` | Feedback forms | Title, Description, Version, IsActive |
| `Question` | Questions inside surveys | Text, QuestionType, Options, OrderIndex |
| `SurveyResponse` | A respondent's submission | SurveyId, IsComplete, StartedAt, CompletedAt |
| `Answer` | Individual answer to a question | QuestionId, AnswerText |
| `SurveyDistribution` | Link/QR/Email for sharing | DistributionType, DistributionValue |
| `Recipient` | People to send surveys to | Name, Email, GroupName |
| `SurveyTemplate` | Reusable survey blueprints | Name, TemplateData (JSON) |
| `Notification` | In-app alerts | Message, IsRead |

**Relationships:**
- A **User** creates many **Surveys**
- A **Survey** has many **Questions**
- A **Survey** has many **SurveyResponses**
- A **SurveyResponse** has many **Answers**
- Each **Answer** belongs to one **Question**

---

### 4.2 DTOs (Data Transfer Objects)

**Location:** `DTOs/`

DTOs control **what data goes in and out** of the API. They prevent exposing sensitive fields (like `PasswordHash`) and allow you to accept only the fields needed.

**Example:**
- When registering, the client sends `RegisterDto` (has `Password`)
- The API returns `UserResponseDto` (has NO password, only safe fields)
- `PasswordHash` is never exposed to the client

| File | Contains |
|------|----------|
| `UserDtos.cs` | RegisterDto, LoginDto, UserResponseDto, AuthResponseDto |
| `SurveyDtos.cs` | CreateSurveyDto, UpdateSurveyDto, SurveyResponseDto, CreateQuestionDto, QuestionResponseDto |
| `SurveyResponseDtos.cs` | SubmitResponseDto, SubmitAnswerDto, SurveyResponseDetailDto, AnswerResponseDto |
| `RecipientDtos.cs` | CreateRecipientDto, RecipientResponseDto |
| `DistributionDtos.cs` | CreateDistributionDto, DistributionResponseDto |
| `TemplateDtos.cs` | CreateTemplateDto, TemplateResponseDto |
| `NotificationDtos.cs` | NotificationResponseDto |
| `AnalyticsDtos.cs` | SurveyAnalyticsDto, QuestionAnalyticsDto |
| `PaginationDtos.cs` | PaginationParams, PagedResult<T> |

---

### 4.3 Contexts (Database Context)

**Location:** `Contexts/FeedbackDbContext.cs`

This is the **bridge between C# code and the SQL database**. It uses **Entity Framework Core** (an ORM) to:
- Define which models map to which tables (`DbSet<User> Users`)
- Configure relationships between tables (foreign keys, cascade delete)
- Handle all database read/write operations

**Key configurations:**
- User email is unique (index)
- Deleting a Survey cascades to delete its Questions and Responses
- Deleting a User does NOT delete their Surveys (restrict)

---

### 4.4 Interfaces (Contracts)

**Location:** `Interfaces/`

Interfaces define **what methods a class must implement** — without saying how. This enables:
- **Dependency Injection** — swap implementations without changing code
- **Testability** — mock interfaces in unit tests
- **Loose coupling** — layers depend on abstractions, not concrete classes

**`IRepository<T>`** — Generic interface for ALL database operations:
```
GetAllAsync()    → Get all records
GetByIdAsync()   → Get one by ID
FindAsync()      → Search with a condition
AddAsync()       → Insert new record
UpdateAsync()    → Update existing record
DeleteAsync()    → Remove a record
ExistsAsync()    → Check if record exists
```

**Service Interfaces** — Define business operations:
- `IAuthService` — Register, Login, GetUser
- `ISurveyService` — CRUD surveys + questions
- `ISurveyResponseService` — Submit, Pause, Resume responses
- `IRecipientService` — CRUD recipients + bulk import
- `IDistributionService` — Generate links/QR codes
- `IAnalyticsService` — Survey statistics
- `INotificationService` — Create, list, mark-read notifications
- `ITemplateService` — CRUD survey templates

---

### 4.5 Repositories (Data Access)

**Location:** `Repositories/Repository.cs`

A single **generic repository** that works with ANY model type. Instead of writing separate code for User CRUD, Survey CRUD, Question CRUD, etc., one class handles them all.

**How it works:**
```csharp
// The same Repository class works for:
IRepository<User>       → handles User table
IRepository<Survey>     → handles Survey table
IRepository<Question>   → handles Question table
// ... and all other models
```

This is registered in `Program.cs` as:
```csharp
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
```

---

### 4.6 Services (Business Logic)

**Location:** `Services/`

Services contain the **core business rules**. They sit between Controllers and Repositories.

| Service | Key Logic |
|---------|-----------|
| `AuthService` | Hashes passwords with BCrypt, generates JWT tokens, validates login |
| `SurveyService` | Creates surveys with questions, auto-increments version on update |
| `SurveyResponseService` | Saves responses, supports pause/resume, triggers notification to survey creator |
| `RecipientService` | Manages recipients, supports bulk import |
| `DistributionService` | Auto-generates unique links/QR data with GUIDs |
| `AnalyticsService` | Calculates completion rate, answer distributions, average ratings per question type |
| `NotificationService` | Creates and manages in-app notifications |
| `TemplateService` | CRUD for reusable survey templates |

**Example flow — Submitting a response:**
1. `SurveyResponseService.SubmitResponseAsync()` is called
2. Creates a `SurveyResponse` record in the database
3. Loops through answers and saves each `Answer` record
4. Finds the survey's creator
5. Sends them a notification: *"New response received for survey: [title]"*
6. Returns the complete response with question texts

---

### 4.7 Controllers (API Endpoints)

**Location:** `Controllers/`

Controllers are the **entry point** for all HTTP requests. They:
- Define the URL routes (e.g., `POST /api/Survey`)
- Validate incoming data
- Call the appropriate service method
- Return HTTP status codes (200 OK, 404 Not Found, etc.)

| Controller | Routes | Auth Required? |
|------------|--------|---------------|
| `AuthController` | `/api/Auth/*` | No (public) |
| `SurveyController` | `/api/Survey/*` | Yes (except GET) |
| `SurveyResponseController` | `/api/SurveyResponse/*` | Submit is public; others need auth |
| `RecipientController` | `/api/Recipient/*` | Yes |
| `DistributionController` | `/api/Distribution/*` | Yes |
| `AnalyticsController` | `/api/Analytics/*` | Yes |
| `NotificationController` | `/api/Notification/*` | Yes |
| `TemplateController` | `/api/Template/*` | Yes (except GET) |

---

### 4.8 Helpers (Utilities)

**Location:** `Helpers/`

| File | Purpose |
|------|---------|
| `JwtHelper.cs` | Generates JWT tokens with user claims (ID, email, role). Token expires after 24 hours (configurable). |
| `AutoMapperProfile.cs` | Defines how Models map to DTOs and vice versa. Eliminates manual property copying. |

**AutoMapper Example:**
Instead of writing:
```csharp
var dto = new UserResponseDto {
    Id = user.Id,
    FullName = user.FullName,
    Email = user.Email,
    Role = user.Role,
    CreatedAt = user.CreatedAt
};
```
AutoMapper does it automatically:
```csharp
var dto = _mapper.Map<UserResponseDto>(user);
```

---

### 4.9 Program.cs (App Configuration)

**Location:** `Program.cs`

This is the **startup file** that configures everything:

1. **Database** — Connects Entity Framework to MS SQL Server
2. **Dependency Injection** — Registers Repository and all Services so they can be injected into constructors
3. **JWT Authentication** — Configures token validation (issuer, audience, signing key)
4. **CORS** — Allows cross-origin requests from any frontend
5. **Swagger** — Enables API documentation UI at `/swagger`
6. **Middleware Pipeline** — Sets up the request processing order: HTTPS → CORS → Auth → Controllers

---

### 4.10 appsettings.json (Configuration)

**Location:** `appsettings.json`

Contains:
- **ConnectionStrings.DefaultConnection** — SQL Server database connection
- **JwtSettings.SecretKey** — Secret key for signing JWT tokens (min 32 characters)
- **JwtSettings.Issuer/Audience** — Token issuer and audience for validation
- **JwtSettings.ExpirationHours** — Token validity period

---

## 5. Key Design Patterns Used

| Pattern | Where | Why |
|---------|-------|-----|
| **Repository Pattern** | `IRepository<T>` + `Repository<T>` | Abstracts database access, single point for CRUD |
| **Service Pattern** | All service classes | Separates business logic from controllers |
| **Dependency Injection** | `Program.cs` registrations | Loose coupling, testability |
| **DTO Pattern** | `DTOs/` folder | Controls data exposure, input validation |
| **Generic Repository** | `Repository<T>` | One implementation for all entities |

---

## 6. Security Features

- **Password Hashing** — BCrypt with salt (never stores plain passwords)
- **JWT Tokens** — Stateless authentication with expiration
- **Role-Based Access** — Admin, Staff, Viewer, Respondent roles
- **[Authorize] Attribute** — Protects endpoints from unauthenticated access
- **Input Validation** — Data annotations on DTOs ([Required], [MaxLength], [EmailAddress])

---

## 7. NuGet Packages Used

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.EntityFrameworkCore.SqlServer` | 10.x | SQL Server database provider |
| `Microsoft.EntityFrameworkCore.Tools` | 10.x | EF migrations CLI |
| `Microsoft.EntityFrameworkCore.Design` | 10.x | Design-time EF services |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.x | JWT authentication middleware |
| `AutoMapper.Extensions.Microsoft.DependencyInjection` | 12.x | Object-to-object mapping |
| `BCrypt.Net-Next` | 4.x | Password hashing |
| `Swashbuckle.AspNetCore` | 10.x | Swagger UI for API testing |
