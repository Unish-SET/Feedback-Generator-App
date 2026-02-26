# Feedback Generator App — API Documentation

## Base URL
```
https://localhost:5001/api
```

---

## Authentication

All protected endpoints require a JWT token in the `Authorization` header:
```
Authorization: Bearer <your_jwt_token>
```

## Pagination & Filtering

All endpoints returning lists support the following query string parameters:
- `pageNumber` (int) - Default: 1
- `pageSize` (int) - Default: 10, Max: 50
- `searchTerm` (string) - Filters results by relevant text (name, title, etc.)
- `sortBy` (string) - Field to sort by
- `sortDescending` (bool) - Default: false

Response format for paged endpoints:
```json
{
  "items": [],
  "totalCount": 0,
  "pageNumber": 1,
  "pageSize": 10,
  "totalPages": 1,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

---

## 1. Auth Endpoints

### POST `/api/Auth/register`
Register a new user.

**Request Body:**
```json
{
  "fullName": "John Doe",
  "email": "john@example.com",
  "password": "password123",
  "role": "Admin"
}
```
> Roles: `Admin`, `Staff`, `Viewer`, `Respondent`

**Response (200):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "user": {
    "id": 1,
    "fullName": "John Doe",
    "email": "john@example.com",
    "role": "Admin",
    "createdAt": "2026-02-27T00:00:00Z"
  }
}
```

---

### POST `/api/Auth/login`
Login and receive JWT token.

**Request Body:**
```json
{
  "email": "john@example.com",
  "password": "password123"
}
```

**Response (200):** Same as register response.

---

### GET `/api/Auth/users`
Get all registered users. Supports **Pagination**.

**Response (200):**
```json
[
  {
    "id": 1,
    "fullName": "John Doe",
    "email": "john@example.com",
    "role": "Admin",
    "createdAt": "2026-02-27T00:00:00Z"
  }
]
```

---

### GET `/api/Auth/users/{id}`
Get a specific user by ID.

---

## 2. Survey Endpoints 🔒

### POST `/api/Survey` 🔒
Create a new survey with questions.

**Request Body:**
```json
{
  "title": "Customer Satisfaction Survey",
  "description": "Please rate our services",
  "brandingConfig": "{\"theme\": \"blue\", \"logo\": \"url\"}",
  "questions": [
    {
      "text": "How would you rate our service?",
      "questionType": "Rating",
      "isRequired": true,
      "orderIndex": 1
    },
    {
      "text": "Which feature do you use most?",
      "questionType": "MultipleChoice",
      "options": "[\"Dashboard\", \"Reports\", \"Settings\", \"Support\"]",
      "isRequired": true,
      "orderIndex": 2
    },
    {
      "text": "Any suggestions for improvement?",
      "questionType": "OpenText",
      "isRequired": false,
      "orderIndex": 3
    },
    {
      "text": "Would you recommend us?",
      "questionType": "YesNo",
      "isRequired": true,
      "orderIndex": 4
    }
  ]
}
```
> Question Types: `MultipleChoice`, `OpenText`, `Rating`, `YesNo`

**Response (201):**
```json
{
  "id": 1,
  "title": "Customer Satisfaction Survey",
  "description": "Please rate our services",
  "createdByUserId": 1,
  "createdByUserName": "John Doe",
  "isActive": true,
  "version": 1,
  "brandingConfig": "{\"theme\": \"blue\"}",
  "createdAt": "2026-02-27T00:00:00Z",
  "updatedAt": "2026-02-27T00:00:00Z",
  "questions": [
    {
      "id": 1,
      "surveyId": 1,
      "text": "How would you rate our service?",
      "questionType": "Rating",
      "options": null,
      "isRequired": true,
      "orderIndex": 1
    }
  ]
}
```

---

### GET `/api/Survey`
Get all surveys. Supports **Pagination**. *(No auth required)*

---

### GET `/api/Survey/{id}`
Get survey by ID with all questions. *(No auth required)*

---

### GET `/api/Survey/my-surveys` 🔒
Get surveys created by the logged-in user. Supports **Pagination**.

---

### PUT `/api/Survey/{id}` 🔒
Update a survey. Auto-increments version number.

**Request Body:**
```json
{
  "title": "Updated Survey Title",
  "description": "Updated description",
  "isActive": false,
  "brandingConfig": "{\"theme\": \"dark\"}"
}
```
> All fields are optional — only send what you want to update.

---

### DELETE `/api/Survey/{id}` 🔒
Delete a survey and all its questions/responses.

**Response:** `204 No Content`

---

### POST `/api/Survey/{surveyId}/questions` 🔒
Add a question to an existing survey.

**Request Body:**
```json
{
  "text": "New question text?",
  "questionType": "Rating",
  "isRequired": true,
  "orderIndex": 5
}
```

---

### DELETE `/api/Survey/questions/{questionId}` 🔒
Delete a question from a survey.

---

## 3. Survey Response Endpoints

### POST `/api/SurveyResponse`
Submit a survey response. *(Works without auth for anonymous responses)*

**Request Body:**
```json
{
  "surveyId": 1,
  "answers": [
    {
      "questionId": 1,
      "answerText": "5"
    },
    {
      "questionId": 2,
      "answerText": "Dashboard"
    },
    {
      "questionId": 3,
      "answerText": "Great service, keep it up!"
    },
    {
      "questionId": 4,
      "answerText": "Yes"
    }
  ]
}
```

**Response (200):**
```json
{
  "id": 1,
  "surveyId": 1,
  "surveyTitle": "Customer Satisfaction Survey",
  "respondentUserId": null,
  "startedAt": "2026-02-27T00:00:00Z",
  "completedAt": "2026-02-27T00:00:00Z",
  "isComplete": true,
  "answers": [
    {
      "id": 1,
      "questionId": 1,
      "questionText": "How would you rate our service?",
      "answerText": "5"
    }
  ]
}
```

---

### GET `/api/SurveyResponse/{id}` 🔒
Get a specific response with all answers.

---

### GET `/api/SurveyResponse/survey/{surveyId}` 🔒
Get all responses for a survey. Supports **Pagination**.

---

### PUT `/api/SurveyResponse/{id}/pause` 🔒
Pause a partially completed response. Sets `isComplete = false`.

---

### PUT `/api/SurveyResponse/{id}/resume` 🔒
Resume a paused response with additional answers.

**Request Body:**
```json
[
  {
    "questionId": 3,
    "answerText": "Completing remaining answer"
  }
]
```

---

## 4. Distribution Endpoints 🔒

### POST `/api/Distribution` 🔒
Generate a distribution link, QR code, or email for a survey.

**Request Body:**
```json
{
  "surveyId": 1,
  "distributionType": "Link",
  "scheduledAt": null
}
```
> Distribution Types: `Link`, `QRCode`, `Email`

**Response (200):**
```json
{
  "id": 1,
  "surveyId": 1,
  "surveyTitle": "Customer Satisfaction Survey",
  "distributionType": "Link",
  "distributionValue": "/survey/respond/1?token=a3b8d1b6-...",
  "scheduledAt": null,
  "sentAt": "2026-02-27T00:00:00Z",
  "createdAt": "2026-02-27T00:00:00Z"
}
```

---

### GET `/api/Distribution/survey/{surveyId}` 🔒
Get all distributions for a survey.

---

### DELETE `/api/Distribution/{id}` 🔒
Delete a distribution.

---

## 5. Recipient Endpoints 🔒

### POST `/api/Recipient` 🔒
Add a single recipient.

**Request Body:**
```json
{
  "name": "Jane Smith",
  "email": "jane@example.com",
  "groupName": "Customers"
}
```

---

### GET `/api/Recipient` 🔒
Get all recipients for the logged-in user. Supports **Pagination**.

---

### GET `/api/Recipient/group/{groupName}` 🔒
Get recipients filtered by group name. Supports **Pagination**.

---

### POST `/api/Recipient/import` 🔒
Bulk import multiple recipients.

**Request Body:**
```json
[
  { "name": "Alice", "email": "alice@example.com", "groupName": "Team A" },
  { "name": "Bob", "email": "bob@example.com", "groupName": "Team A" },
  { "name": "Charlie", "email": "charlie@example.com", "groupName": "Team B" }
]
```

---

### DELETE `/api/Recipient/{id}` 🔒
Delete a recipient.

---

## 6. Analytics Endpoints 🔒

### GET `/api/Analytics/survey/{surveyId}` 🔒
Get analytics for a survey.

**Response (200):**
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
      "questionText": "How would you rate our service?",
      "questionType": "Rating",
      "totalAnswers": 45,
      "answerDistribution": { "5": 20, "4": 15, "3": 7, "2": 2, "1": 1 },
      "averageRating": 4.13,
      "openTextResponses": []
    },
    {
      "questionId": 2,
      "questionText": "Which feature do you use most?",
      "questionType": "MultipleChoice",
      "totalAnswers": 45,
      "answerDistribution": { "Dashboard": 18, "Reports": 15, "Settings": 7, "Support": 5 },
      "averageRating": null,
      "openTextResponses": []
    },
    {
      "questionId": 3,
      "questionText": "Any suggestions for improvement?",
      "questionType": "OpenText",
      "totalAnswers": 30,
      "answerDistribution": {},
      "averageRating": null,
      "openTextResponses": ["Great service!", "Need better UI", "More features please"]
    }
  ]
}
```

---

## 7. Notification Endpoints 🔒

### GET `/api/Notification` 🔒
Get all notifications for the logged-in user. Supports **Pagination**.

**Response (200):**
```json
[
  {
    "id": 1,
    "message": "New response received for survey: Customer Satisfaction Survey",
    "isRead": false,
    "createdAt": "2026-02-27T00:00:00Z"
  }
]
```

---

### GET `/api/Notification/unread-count` 🔒
Get count of unread notifications.

**Response:** `{ "count": 3 }`

---

### PUT `/api/Notification/{id}/mark-read` 🔒
Mark a notification as read.

**Response:** `{ "message": "Notification marked as read." }`

---

## 8. Template Endpoints

### POST `/api/Template` 🔒
Create a survey template.

**Request Body:**
```json
{
  "name": "Employee Satisfaction",
  "description": "Standard employee feedback template",
  "templateData": "{\"questions\": [{\"text\": \"Rate your work environment\", \"type\": \"Rating\"}, {\"text\": \"Suggestions?\", \"type\": \"OpenText\"}]}"
}
```

---

### GET `/api/Template`
Get all templates. *(No auth required)*

---

### GET `/api/Template/{id}`
Get template by ID. *(No auth required)*

---

### DELETE `/api/Template/{id}` 🔒
Delete a template.

---

## Error Responses

| Status Code | Meaning |
|-------------|---------|
| `200` | Success |
| `201` | Created |
| `204` | Deleted (No Content) |
| `400` | Bad Request (validation error) |
| `401` | Unauthorized (missing/invalid token) |
| `404` | Not Found (via Global Exception Handler) |
| `500` | Internal Server Error (via Global Exception Handler) |

**Error format:**
```json
{
  "message": "Invalid email or password."
}
```

---

## Legend
- 🔒 = Requires JWT token in Authorization header
- All dates are in **UTC** format
- `options` field in questions uses **JSON string** format: `"[\"Option1\", \"Option2\"]"`
- `brandingConfig` and `templateData` use **JSON string** format for flexibility
