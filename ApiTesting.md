# 🧪 API Testing Guide — FeedBack Generator App

This guide walks you through testing **every API endpoint** using **Postman** or **Swagger UI**.

---

## 🔧 Setup

### Base URL
```
https://localhost:{port}/api
```
> Replace `{port}` with the port number shown when you run the app (e.g., `7001`).

### How to Authenticate
Most endpoints require a JWT token. After logging in, add this header to every request:
```
Authorization: Bearer {your_access_token}
```

**In Swagger:** Click the 🔓 **Authorize** button at the top and enter:
```
Bearer eyJhbGciOiJIUzI1NiIs...
```

---

## 1. 🔐 Authentication Endpoints

### 1.1 Register a New User
```
POST /api/auth/register
```
**Body (JSON):**
```json
{
  "fullName": "John Doe",
  "email": "john@example.com",
  "password": "Password123!"
}
```
**Expected Response (200):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2g=...",
  "tokenExpiresAt": "2026-03-02T02:30:00Z",
  "user": {
    "id": 1,
    "fullName": "John Doe",
    "email": "john@example.com",
    "role": "Respondent",
    "createdAt": "2026-03-02T02:15:00Z"
  }
}
```

### 1.2 Login
```
POST /api/auth/login
```
**Body (JSON):**
```json
{
  "email": "john@example.com",
  "password": "Password123!"
}
```
**Expected Response (200):** Same structure as Register.

> ⚠️ **Save the `token` value!** You need it for all following requests.

### 1.3 Refresh Token
```
POST /api/auth/refresh
```
**Body (JSON):**
```json
{
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2g=..."
}
```
**Expected Response (200):** New token + new refresh token pair.

### 1.4 Revoke Token (Logout)
```
POST /api/auth/revoke
```
**Headers:** `Authorization: Bearer {token}`  
**Body (JSON):**
```json
{
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2g=..."
}
```
**Expected Response:** `204 No Content`

### 1.5 Get All Users (Admin Only)
```
GET /api/auth/users?pageNumber=1&pageSize=10
```
**Headers:** `Authorization: Bearer {admin_token}`  
**Expected Response (200):** Paginated list of users.

### 1.6 Get User by ID (Admin Only)
```
GET /api/auth/users/1
```
**Expected Response (200):** Single user object.

---

## 2. 📝 Survey Endpoints

### 2.1 Create Survey (Admin/Staff)
```
POST /api/survey
```
**Headers:** `Authorization: Bearer {token}`  
**Body (JSON):**
```json
{
  "title": "Customer Satisfaction Survey",
  "description": "Help us improve our services.",
  "expiresAt": "2026-12-31T23:59:59Z",
  "questions": [
    {
      "text": "How satisfied are you with our service?",
      "questionType": "Rating",
      "isRequired": true,
      "orderIndex": 1
    },
    {
      "text": "Would you recommend us to a friend?",
      "questionType": "YesNo",
      "isRequired": true,
      "orderIndex": 2
    },
    {
      "text": "What is your favorite feature?",
      "questionType": "MultipleChoice",
      "options": "[\"Speed\", \"Design\", \"Support\", \"Pricing\"]",
      "isRequired": false,
      "orderIndex": 3
    },
    {
      "text": "Any additional feedback?",
      "questionType": "OpenText",
      "isRequired": false,
      "orderIndex": 4
    }
  ]
}
```
**Expected Response (201):** Survey object with generated ID.

### 2.2 Create Survey by Cloning Questions from Another Survey
```
POST /api/survey
```
**Body (JSON):**
```json
{
  "title": "Q2 Satisfaction Survey",
  "description": "Same questions, new quarter.",
  "copyQuestionsFromSurveyId": 1
}
```

### 2.3 Get All Surveys (Public)
```
GET /api/survey?pageNumber=1&pageSize=10&searchTerm=satisfaction&sortBy=title&sortDescending=false
```
**No authentication required.**

### 2.4 Get Survey by ID (Public)
```
GET /api/survey/1
```

### 2.5 Get My Surveys (Authenticated)
```
GET /api/survey/my-surveys?pageNumber=1&pageSize=10
```

### 2.6 Update Survey (Admin/Staff)
```
PUT /api/survey/1
```
**Body (JSON):**
```json
{
  "title": "Updated Survey Title",
  "isActive": false
}
```

### 2.7 Delete Survey (Admin Only)
```
DELETE /api/survey/1
```
**Expected Response:** `204 No Content`

### 2.8 Add Question to Survey (Admin/Staff)
```
POST /api/survey/1/questions
```
**Body (JSON):**
```json
{
  "text": "How did you hear about us?",
  "questionType": "MultipleChoice",
  "options": "[\"Social Media\", \"Friend\", \"Ad\", \"Other\"]",
  "isRequired": false,
  "orderIndex": 5
}
```

### 2.9 Delete Question (Admin Only)
```
DELETE /api/survey/questions/1
```

---

## 3. 📨 Survey Response Endpoints

### 3.1 Submit a Response (Public)
```
POST /api/surveyresponse
```
**Body (JSON):**
```json
{
  "surveyId": 1,
  "answers": [
    { "questionId": 1, "answerText": "5" },
    { "questionId": 2, "answerText": "Yes" },
    { "questionId": 3, "answerText": "Speed" },
    { "questionId": 4, "answerText": "Great service overall!" }
  ]
}
```

### 3.2 Get Response by ID (Admin/Staff/Viewer)
```
GET /api/surveyresponse/1
```

### 3.3 Get All Responses for a Survey (Admin/Staff/Viewer)
```
GET /api/surveyresponse/survey/1?pageNumber=1&pageSize=10
```

### 3.4 Pause a Response (Admin/Staff)
```
PUT /api/surveyresponse/1/pause
```

### 3.5 Resume a Response with Additional Answers (Admin/Staff)
```
PUT /api/surveyresponse/1/resume
```
**Body (JSON):**
```json
[
  { "questionId": 3, "answerText": "Design" },
  { "questionId": 4, "answerText": "Nothing to add." }
]
```

---

## 4. 👤 Recipient Endpoints

### 4.1 Add a Recipient (Admin/Staff)
```
POST /api/recipient
```
**Body (JSON):**
```json
{
  "name": "Jane Smith",
  "email": "jane@example.com",
  "groupName": "Customers"
}
```

### 4.2 Bulk Import Recipients (Admin/Staff)
```
POST /api/recipient/import
```
**Body (JSON):**
```json
[
  { "name": "Alice", "email": "alice@example.com", "groupName": "Customers" },
  { "name": "Bob", "email": "bob@example.com", "groupName": "Partners" },
  { "name": "Charlie", "email": "charlie@example.com", "groupName": "Customers" }
]
```

### 4.3 Get All Recipients (Admin/Staff/Viewer)
```
GET /api/recipient?pageNumber=1&pageSize=10
```

### 4.4 Filter Recipients by Group (Admin/Staff/Viewer)
```
GET /api/recipient/group/Customers?pageNumber=1&pageSize=10
```

### 4.5 Delete Recipient (Admin/Staff)
```
DELETE /api/recipient/1
```

---

## 5. 📤 Distribution Endpoints

### 5.1 Create Distribution (Admin/Staff)
```
POST /api/distribution
```
**Body (JSON):**
```json
{
  "surveyId": 1,
  "distributionType": "Email",
  "distributionValue": "jane@example.com",
  "scheduledAt": "2026-03-05T09:00:00Z"
}
```

### 5.2 Get Distributions for a Survey (Admin/Staff/Viewer)
```
GET /api/distribution/survey/1
```

### 5.3 Delete Distribution (Admin Only)
```
DELETE /api/distribution/1
```

---

## 6. 🔔 Notification Endpoints

### 6.1 Get My Notifications
```
GET /api/notification?pageNumber=1&pageSize=10
```

### 6.2 Get Unread Count
```
GET /api/notification/unread-count
```
**Expected Response:**
```json
{ "count": 3 }
```

### 6.3 Mark Notification as Read
```
PUT /api/notification/1/mark-read
```
**Expected Response:**
```json
{ "message": "Notification marked as read." }
```

---

## 7. 📊 Analytics & Export Endpoints

### 7.1 Get Survey Analytics (Admin/Staff/Viewer)
```
GET /api/analytics/survey/1
```
**Expected Response:**
```json
{
  "surveyId": 1,
  "surveyTitle": "Customer Satisfaction Survey",
  "totalResponses": 50,
  "completedResponses": 45,
  "incompleteResponses": 5,
  "completionRate": 90.0,
  "questionAnalytics": [
    {
      "questionId": 1,
      "questionText": "How satisfied are you?",
      "questionType": "Rating",
      "totalAnswers": 45,
      "averageRating": 4.2,
      "answerDistribution": { "5": 20, "4": 15, "3": 8, "2": 2 }
    }
  ]
}
```

### 7.2 Export to CSV
```
GET /api/analytics/survey/1/export/csv
```
**Response:** Downloads a `.csv` file.

### 7.3 Export to Excel
```
GET /api/analytics/survey/1/export/excel
```
**Response:** Downloads a `.xlsx` file.

---

## 8. 📄 Template Endpoints

### 8.1 Create Template (Admin/Staff)
```
POST /api/template
```
**Body (JSON):**
```json
{
  "name": "Employee Feedback Template",
  "description": "Standard template for quarterly employee reviews",
  "templateData": "{\"questions\": [{\"text\": \"Rate your team?\", \"type\": \"Rating\"}]}"
}
```

### 8.2 Get All Templates (Admin/Staff/Viewer)
```
GET /api/template
```

### 8.3 Get Template by ID (Admin/Staff/Viewer)
```
GET /api/template/1
```

### 8.4 Delete Template (Admin Only)
```
DELETE /api/template/1
```

---

## 🧪 Testing Pagination Parameters

All paginated endpoints support these query parameters:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `pageNumber` | 1 | Page number to fetch |
| `pageSize` | 10 | Number of items per page |
| `searchTerm` | (empty) | Search/filter keyword |
| `sortBy` | (varies) | Column to sort by (e.g., `name`, `email`, `title`) |
| `sortDescending` | false | Sort direction |

**Example:**
```
GET /api/auth/users?pageNumber=2&pageSize=5&searchTerm=john&sortBy=name&sortDescending=true
```

---

## ❌ Error Response Format

All errors return this consistent JSON structure:

```json
{
  "statusCode": 404,
  "message": "The requested resource was not found.",
  "details": "The requested resource was not found."
}
```

| Status Code | Meaning |
|-------------|---------|
| 400 | Bad Request — invalid input data |
| 401 | Unauthorized — missing or invalid JWT |
| 403 | Forbidden — insufficient role permissions |
| 404 | Not Found — resource doesn't exist |
| 408 | Request Timeout — query took too long |
| 409 | Conflict — duplicate or concurrency error |
| 500 | Internal Server Error — unexpected failure |
