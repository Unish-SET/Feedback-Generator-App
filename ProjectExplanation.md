# 📘 Project Explanation — FeedBack Generator App

## Overview

The FeedBack Generator App is a **multi-layered ASP.NET Core Web API** designed to manage the entire lifecycle of surveys and feedback collection. It follows industry-standard patterns including **Repository Pattern**, **Dependency Injection**, **DTO Mapping**, and **Global Exception Handling**.

---

## 🏗️ Architecture

The application follows a **Layered Architecture** pattern:

```
┌──────────────────────────────────────────┐
│           Controllers (API Layer)         │  ← Receives HTTP requests
├──────────────────────────────────────────┤
│           Services (Business Layer)       │  ← Contains business logic
├──────────────────────────────────────────┤
│         Repository (Data Access Layer)    │  ← Abstracts database operations
├──────────────────────────────────────────┤
│        EF Core DbContext + SQL Server     │  ← Actual database
└──────────────────────────────────────────┘
```

**Data flows one direction:** Controller → Service → Repository → Database.  
**Responses flow back:** Database → Repository → Service → Controller → HTTP Response.

---

## 📂 Layer-by-Layer Breakdown

### 1. Controllers (API Layer)
**Location:** `/Controllers/`

Controllers are the entry point for all HTTP requests. They do **not** contain any business logic. Their only job is to:
- Accept the HTTP request and validate the input (via `[ApiController]` and Data Annotations)
- Call the appropriate Service method
- Return the HTTP response (`200 OK`, `201 Created`, `404 NotFound`, etc.)

| Controller | Responsibility |
|-----------|---------------|
| `AuthController` | Register, Login, Token Refresh, Logout, User Management |
| `SurveyController` | CRUD operations for Surveys and Questions |
| `SurveyResponseController` | Submit, Pause, Resume survey responses |
| `RecipientController` | Add, List, Delete, Bulk Import recipients |
| `DistributionController` | Create survey distributions (Email/Link/QR) |
| `NotificationController` | Get notifications, Mark as read, Unread count |
| `AnalyticsController` | Survey analytics + CSV/Excel export |
| `TemplateController` | CRUD for reusable survey templates |

### 2. Services (Business Logic Layer)
**Location:** `/Services/`

Services contain all the core business logic. They are injected into Controllers via **Dependency Injection**.

Key responsibilities:
- **AuthService**: Password hashing (BCrypt), JWT token generation, refresh token rotation
- **SurveyService**: Survey creation, question cloning, versioning, expiry management
- **SurveyResponseService**: Answer submission, pause/resume state management
- **RecipientService**: Duplicate email detection, bulk import with batch inserts
- **NotificationService**: Server-side paginated notifications with unread counts
- **AnalyticsService**: Aggregation of responses into completion rates, answer distributions, average ratings
- **ExportService**: Converts analytics data into CSV (CsvHelper) and Excel (ClosedXML) byte arrays
- **DistributionService**: Creates survey distribution records
- **TemplateService**: Manages reusable survey blueprints

### 3. Repository (Data Access Layer)
**Location:** `/Repositories/Repository.cs`

The application uses a **Generic Repository Pattern** that provides common CRUD operations for all entities:

| Method | Description |
|--------|-------------|
| `GetByIdAsync(int id)` | Fetch a single entity by primary key |
| `GetAllAsync()` | Fetch all entities (with `AsNoTracking` for performance) |
| `FindAsync(Expression<Func<T, bool>>)` | Query entities by a filter condition |
| `AddAsync(T entity)` | Insert a single entity |
| `AddRangeAsync(IEnumerable<T>)` | Batch insert multiple entities in one transaction |
| `UpdateAsync(T entity)` | Update an existing entity |
| `DeleteAsync(T entity)` | Delete an entity |
| `Query()` | Returns `IQueryable<T>` for custom LINQ queries |

### 4. DTOs (Data Transfer Objects)
**Location:** `/DTOs/`

DTOs ensure that **database models are never exposed directly** to the API consumer. They act as a contract between the frontend and backend.

- **Input DTOs** (`CreateSurveyDto`, `RegisterDto`, etc.) — Define what the API accepts
- **Output DTOs** (`SurveyResponseDto`, `UserResponseDto`, etc.) — Define what the API returns
- **Pagination DTOs** (`PaginationParams`, `PagedResult<T>`) — Standardized pagination

**AutoMapper** (configured in `/Helpers/AutoMapperProfile.cs`) automatically maps between Models ↔ DTOs.

### 5. Models (Database Entities)
**Location:** `/Models/`

These are the C# classes that map directly to SQL Server tables via Entity Framework Core.

### 6. DbContext
**Location:** `/Contexts/FeedbackDbContext.cs`

Configures all entity relationships, foreign keys, cascade behaviors, and unique indexes using **Fluent API**.

---

## 🗄️ Database Schema

```
┌─────────┐     ┌──────────┐     ┌──────────┐
│  Users   │────▶│  Surveys │────▶│ Questions│
└─────────┘     └──────────┘     └──────────┘
     │               │                 │
     │               ▼                 ▼
     │        ┌──────────────┐   ┌──────────┐
     │        │SurveyResponses│──▶│ Answers  │
     │        └──────────────┘   └──────────┘
     │               │
     ▼               ▼
┌──────────┐  ┌────────────────┐
│Recipients│  │SurveyDistributions│
└──────────┘  └────────────────┘
     
┌──────────────┐  ┌───────────────┐  ┌──────────────┐
│ Notifications │  │ RefreshTokens │  │SurveyTemplates│
└──────────────┘  └───────────────┘  └──────────────┘
```

### Key Relationships:
- **User → Surveys**: One-to-Many (a user creates many surveys)
- **Survey → Questions**: One-to-Many with Cascade Delete
- **Survey → SurveyResponses**: One-to-Many with Cascade Delete
- **SurveyResponse → Answers**: One-to-Many with Cascade Delete
- **Answer → Question**: Many-to-One (NoAction on delete to prevent cycles)
- **User → RefreshTokens**: One-to-Many with Cascade Delete
- **User → Notifications**: One-to-Many with Cascade Delete

---

## 🔐 Security Model

### Authentication
- **JWT Bearer Tokens** with HMAC-SHA256 signing
- Access tokens expire in **15 minutes** (configurable)
- Refresh tokens expire in **7 days** (configurable)
- **Token rotation**: Each refresh generates a new pair and revokes the old token
- **Algorithm check**: Guards against `alg:none` JWT attacks

### Authorization (Role-Based Access Control)

| Role | Permissions |
|------|------------|
| **Admin** | Full access: create, read, update, delete everything |
| **Staff** | Create surveys, manage recipients, view analytics |
| **Viewer** | Read-only access to surveys, analytics, templates |
| **Respondent** | Submit and manage their own survey responses |

### Additional Security Features
- **BCrypt password hashing** with salt
- **Rate limiting** (fixed window) to prevent abuse
- **Global exception shielding** — internal errors never leak to the client
- **Forwarded headers** for correct IP detection behind proxies
- **CORS policy** configured for cross-origin requests

---

## ⚙️ Middleware Pipeline

The request pipeline is configured in `Program.cs` in this exact order:

```
1. ExceptionHandlingMiddleware    ← Catches ALL unhandled errors globally
2. HTTPS Redirection
3. CORS
4. Forwarded Headers              ← Reads real IP from X-Forwarded-For
5. Rate Limiter                   ← Limits requests per IP
6. Authentication                 ← Validates JWT tokens
7. Authorization                  ← Checks role-based access
8. Controllers                    ← Routes to the correct endpoint
```

---

## 🧱 Exception Handling

The `ExceptionHandlingMiddleware` catches **12 exception types** and maps them to appropriate HTTP status codes:

| Exception | HTTP Status |
|-----------|-------------|
| `ApiException` (custom) | Varies (400/401/404/409) |
| `DbUpdateConcurrencyException` | 409 Conflict |
| `DbUpdateException` | 400 Bad Request |
| `ArgumentNullException` | 400 Bad Request |
| `ArgumentException` | 400 Bad Request |
| `InvalidOperationException` | 400 Bad Request |
| `FormatException` | 400 Bad Request |
| `UnauthorizedAccessException` | 403 Forbidden |
| `KeyNotFoundException` | 404 Not Found |
| `TimeoutException` | 408 Request Timeout |
| `NotImplementedException` | 501 Not Implemented |
| `Exception` (fallback) | 500 Internal Server Error |

All error responses follow a consistent JSON format:
```json
{
  "statusCode": 400,
  "message": "A user with this email already exists.",
  "details": "A user with this email already exists."
}
```

---

## 📄 Performance Optimizations

1. **Server-Side Pagination** — `Skip/Take` at the database level, not in memory
2. **AsNoTracking** — Read-only queries skip EF Core change tracking
3. **Batch Inserts** — `AddRangeAsync` for bulk operations (recipients, questions, answers)
4. **IQueryable Composition** — Queries are built as expression trees and translated to SQL
