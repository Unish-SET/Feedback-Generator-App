# 📋 FeedBack Generator App

A production-ready **Survey & Feedback Management System** built with **ASP.NET Core (.NET 10)** and **Entity Framework Core**. It provides a complete REST API for creating surveys, collecting responses, managing recipients, generating analytics, and exporting reports.

---

## ✨ Features

| Feature | Description |
|---------|-------------|
| 🔐 **JWT Authentication** | Secure login/register with Access + Refresh Token rotation |
| 👥 **Role-Based Authorization** | 4 roles: Admin, Staff, Viewer, Respondent |
| 📝 **Survey Management** | Create, update, delete, and version surveys with questions |
| 📊 **Real-Time Analytics** | Auto-calculated completion rates, answer distributions, and average ratings |
| 📨 **Survey Distribution** | Distribute surveys via Email, Link, or QR Code |
| 👤 **Recipient Management** | Manage contacts individually or bulk import |
| 🔔 **Notifications** | In-app notification system with read/unread tracking |
| 📁 **Export** | Export analytics to CSV and Excel (`.xlsx`) |
| 📄 **Templates** | Save and reuse survey blueprints |
| ⏸️ **Pause & Resume** | Respondents can pause and resume survey responses |
| 🛡️ **Rate Limiting** | IP-based rate limiting to prevent API abuse |
| 🌐 **Forwarded Headers** | Correct IP detection behind load balancers/proxies |
| 🧱 **Global Exception Handling** | Centralized error handling with 12+ exception types |
| 📄 **Server-Side Pagination** | Efficient pagination with search, sort, and filter |
| ⚡ **Batch Operations** | Bulk insert support for recipients and survey questions |

---

## 🛠️ Tech Stack

| Layer | Technology |
|-------|------------|
| **Framework** | ASP.NET Core (.NET 10) |
| **ORM** | Entity Framework Core 10 |
| **Database** | SQL Server |
| **Authentication** | JWT Bearer Tokens |
| **Mapping** | AutoMapper |
| **Excel Export** | ClosedXML |
| **CSV Export** | CsvHelper |
| **Password Hashing** | BCrypt.Net |
| **API Docs** | Swagger / Swashbuckle |

---

## 📂 Project Structure

```
FeedBackGeneratorApp/
│
├── Controllers/              # API endpoints (8 controllers)
│   ├── AuthController.cs
│   ├── SurveyController.cs
│   ├── SurveyResponseController.cs
│   ├── RecipientController.cs
│   ├── DistributionController.cs
│   ├── NotificationController.cs
│   ├── AnalyticsController.cs
│   └── TemplateController.cs
│
├── Models/                   # Database entities (10 models)
│   ├── User.cs
│   ├── Survey.cs
│   ├── Question.cs
│   ├── Answer.cs
│   ├── SurveyResponse.cs
│   ├── SurveyDistribution.cs
│   ├── Recipient.cs
│   ├── Notification.cs
│   ├── SurveyTemplate.cs
│   └── RefreshToken.cs
│
├── DTOs/                     # Data Transfer Objects (9 files)
├── Services/                 # Business logic layer (9 services)
├── Interfaces/               # Service contracts (10 interfaces)
├── Repositories/             # Generic Repository pattern
├── Contexts/                 # EF Core DbContext
├── Helpers/                  # JWT + AutoMapper configuration
├── Exceptions/               # Global exception handling middleware
└── Program.cs                # Application entry point & DI configuration
```

---

## 🚀 Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [SQL Server](https://www.microsoft.com/en-us/sql-server) (LocalDB or full instance)
- Visual Studio 2022+ or VS Code

### Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/your-username/FeedBackGeneratorApp.git
   cd FeedBackGeneratorApp
   ```

2. **Update the connection string** in `appsettings.json`:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Server=.;Database=FeedbackGeneratorDb;Trusted_Connection=true;TrustServerCertificate=true;"
   }
   ```

3. **Apply database migrations**
   ```bash
   dotnet ef database update
   ```

4. **Run the application**
   ```bash
   dotnet run
   ```

5. **Open Swagger UI** at `https://localhost:{port}/swagger`

---

## 🔑 Authentication Flow

1. **Register** → `POST /api/auth/register` → Returns Access Token + Refresh Token
2. **Login** → `POST /api/auth/login` → Returns Access Token + Refresh Token
3. **Use Token** → Add `Authorization: Bearer {token}` header to all protected requests
4. **Refresh** → `POST /api/auth/refresh` → Exchange expired access token for a new pair
5. **Logout** → `POST /api/auth/revoke` → Revokes the refresh token

---

## 📖 API Documentation

Refer to the following documentation files for more details:

- **[ProjectExplanation.md](ProjectExplanation.md)** — Detailed architecture and design explanation
- **[ApiTesting.md](ApiTesting.md)** — Step-by-step Postman/Swagger testing guide

---

## 📜 License

This project is for educational/training purposes.
