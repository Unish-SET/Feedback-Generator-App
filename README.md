# Feedback Generator App

A .NET 10 Web API for collecting, managing, and analyzing feedback through surveys. Built with Clean Architecture (Repository + Service pattern), Entity Framework Core, JWT Authentication, and MS SQL Server.

## Features

- **User Authentication** — Register/Login with JWT tokens and BCrypt password hashing
- **Role-Based Access** — Admin, Staff, Viewer, Respondent roles
- **Survey Builder** — Create surveys with multiple question types (Multiple Choice, Open Text, Rating, Yes/No)
- **Auto-Generated Links** — Shareable links are automatically created and returned when a survey is published
- **Survey Versioning** — Auto-increments version on updates with history tracking
- **Survey Distribution** — Generate shareable links and QR codes for surveys
- **Response Collection** — Submit responses (supports anonymous), pause and resume partially completed surveys
- **Real-Time Notifications** — In-app notifications when feedback is received
- **Analytics Dashboard** — Per-question-type aggregation (answer distribution, average ratings, open text collection)
- **Recipient Management** — Import/manage respondents, segment into groups
- **Survey Templates** — Pre-designed templates for common feedback scenarios
- **Global Exception Handling** — Centralized middleware mapping domain exceptions to standard HTTP error codes
- **Pagination, Sorting, & Filtering** — Unified search and pagination wrapper (`PagedResult<T>`) across all collection endpoints

## Tech Stack

| Technology | Purpose |
|------------|---------|
| .NET 10 | Framework |
| ASP.NET Core Web API | Backend |
| Entity Framework Core | ORM |
| MS SQL Server | Database |
| JWT Bearer | Authentication |
| BCrypt.Net | Password Hashing |
| AutoMapper | Object Mapping |
| Swagger / Swashbuckle | API Documentation |

## Project Structure

```text
FeedBackGeneratorApp/
├── Models/          → Domain entities (User, Survey, Question, Answer, etc.)
├── DTOs/            → Request/Response data transfer objects (includes Pagination params)
├── Contexts/        → EF Core DbContext
├── Interfaces/      → IRepository<T> + Service interfaces
├── Repositories/    → Generic Repository<T> implementation
├── Services/        → Business logic layer
├── Middlewares/     → Global Exception Handling middleware
├── Controllers/     → API endpoints
├── Helpers/         → JwtHelper, AutoMapperProfile
├── Program.cs       → DI, Auth, CORS, Swagger configuration
└── appsettings.json → Connection string & JWT settings
```

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (Express or Developer edition)
- [EF Core CLI Tools](https://learn.microsoft.com/en-us/ef/core/cli/dotnet)

### Setup

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd FeedBackGeneratorApp
   ```

2. **Update the connection string** in `appsettings.json`
   ```json
   "ConnectionStrings": {
       "DefaultConnection": "Server=.\\SQLEXPRESS;Database=FeedbackGeneratorDb;Trusted_Connection=true;TrustServerCertificate=true;"
   }
   ```

3. **Install EF Core tools** (if not already installed)
   ```bash
   dotnet tool install --global dotnet-ef
   ```

4. **Run database migrations**
   ```bash
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```

5. **Run the application**
   ```bash
   dotnet run
   ```

6. **Open Swagger UI** at `https://localhost:5001/swagger`

### Running in Visual Studio

1. Open `FeedBackGeneratorApp.sln`
2. Open **Package Manager Console** and run:
   ```powershell
   Add-Migration InitialCreate
   Update-Database
   ```
3. Press **F5** to run

## API Endpoints

### Pagination & Filtering Note
All endpoints that return a list of items (`GET` collections) support the following query parameters:
- `pageNumber` (int) - Default: 1
- `pageSize` (int) - Default: 10, Max: 50
- `searchTerm` (string) - Filters results by name, email, title, etc. based on the endpoint
- `sortBy` (string) - The field to sort by
- `sortDescending` (bool) - Default: false

### Authentication
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/Auth/register` | Register a new user |
| POST | `/api/Auth/login` | Login and get JWT token |
| GET | `/api/Auth/users` | Get all users (Paged) |
| GET | `/api/Auth/users/{id}` | Get user by ID |

### Surveys
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/Survey` | Create a new survey (returns shareable link) |
| GET | `/api/Survey` | Get all surveys (Paged) |
| GET | `/api/Survey/{id}` | Get survey by ID |
| GET | `/api/Survey/my-surveys` | Get current user's surveys (Paged) |
| PUT | `/api/Survey/{id}` | Update a survey |
| DELETE | `/api/Survey/{id}` | Delete a survey |
| POST | `/api/Survey/{surveyId}/questions` | Add question to survey |
| DELETE | `/api/Survey/questions/{questionId}` | Delete a question |

### Survey Responses
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/SurveyResponse` | Submit a response |
| GET | `/api/SurveyResponse/{id}` | Get response by ID |
| GET | `/api/SurveyResponse/survey/{surveyId}` | Get all responses for a survey (Paged) |
| PUT | `/api/SurveyResponse/{id}/pause` | Pause a response |
| PUT | `/api/SurveyResponse/{id}/resume` | Resume a paused response |

### Distribution
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/Distribution` | Create distribution (Link/QR/Email) |
| GET | `/api/Distribution/survey/{surveyId}` | Get distributions for a survey |
| DELETE | `/api/Distribution/{id}` | Delete a distribution |

### Recipients
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/Recipient` | Add a recipient |
| GET | `/api/Recipient` | Get all recipients (Paged) |
| GET | `/api/Recipient/group/{groupName}` | Get recipients by group (Paged) |
| DELETE | `/api/Recipient/{id}` | Delete a recipient |
| POST | `/api/Recipient/import` | Bulk import recipients |

### Analytics
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/Analytics/survey/{surveyId}` | Get survey analytics |

### Notifications
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/Notification` | Get my notifications (Paged) |
| GET | `/api/Notification/unread-count` | Get unread count |
| PUT | `/api/Notification/{id}/mark-read` | Mark notification as read |

### Templates
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/Template` | Create a template |
| GET | `/api/Template` | Get all templates |
| GET | `/api/Template/{id}` | Get template by ID |
| DELETE | `/api/Template/{id}` | Delete a template |

## Workflow

```text
Register → Login → Create Survey (Link auto-generated) → Add Questions → Share Link → Collect Responses → View Analytics
```

## License

This project is licensed under the MIT License.
