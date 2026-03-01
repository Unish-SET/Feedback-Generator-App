# 📮 Postman API Documentation — FeedBack Generator App

## 🔧 Environment Setup in Postman

### Step 1: Create a Postman Environment
1. Open Postman → Click **Environments** (left sidebar) → **Create Environment**
2. Name it `FeedbackApp`
3. Add these variables:

| Variable | Initial Value | Description |
|----------|--------------|-------------|
| `base_url` | `https://localhost:7001` | Your API base URL |
| `access_token` | *(leave empty)* | Filled automatically after login |
| `refresh_token` | *(leave empty)* | Filled automatically after login |

4. Click **Save** and select `FeedbackApp` as your active environment.

### Step 2: Auto-Save Token After Login/Register
In the **Tests** tab of your Login and Register requests, paste this script:
```javascript
var jsonData = pm.response.json();
pm.environment.set("access_token", jsonData.token);
pm.environment.set("refresh_token", jsonData.refreshToken);
```
This automatically saves the token so you don't have to copy-paste it every time!

### Step 3: Set Default Authorization
1. Create a **Collection** called `FeedbackApp API`
2. Go to the **Authorization** tab of the collection
3. Set **Type** = `Bearer Token`
4. Set **Token** = `{{access_token}}`
5. All requests inside this collection will automatically inherit the token.

---

## 📁 Collection Structure

Create these folders inside your Postman Collection:

```
📂 FeedbackApp API
├── 📁 Auth
├── 📁 Surveys
├── 📁 Survey Responses
├── 📁 Recipients
├── 📁 Distributions
├── 📁 Notifications
├── 📁 Analytics & Export
└── 📁 Templates
```

---

## 📁 Auth

### 1. Register
| Field | Value |
|-------|-------|
| Method | `POST` |
| URL | `{{base_url}}/api/auth/register` |
| Body | `raw` → `JSON` |

```json
{
  "fullName": "John Doe",
  "email": "john@example.com",
  "password": "Password123!"
}
```

**Tests tab** (auto-save tokens):
```javascript
var jsonData = pm.response.json();
pm.environment.set("access_token", jsonData.token);
pm.environment.set("refresh_token", jsonData.refreshToken);
```

---

### 2. Login
| Field | Value |
|-------|-------|
| Method | `POST` |
| URL | `{{base_url}}/api/auth/login` |
| Body | `raw` → `JSON` |

```json
{
  "email": "john@example.com",
  "password": "Password123!"
}
```

**Tests tab** (same as Register):
```javascript
var jsonData = pm.response.json();
pm.environment.set("access_token", jsonData.token);
pm.environment.set("refresh_token", jsonData.refreshToken);
```

---

### 3. Refresh Token
| Field | Value |
|-------|-------|
| Method | `POST` |
| URL | `{{base_url}}/api/auth/refresh` |
| Body | `raw` → `JSON` |

```json
{
  "refreshToken": "{{refresh_token}}"
}
```

---

### 4. Revoke Token (Logout)
| Field | Value |
|-------|-------|
| Method | `POST` |
| URL | `{{base_url}}/api/auth/revoke` |
| Auth | Bearer Token → `{{access_token}}` |
| Body | `raw` → `JSON` |

```json
{
  "refreshToken": "{{refresh_token}}"
}
```
**Expected:** `204 No Content`

---

### 5. Get All Users (Admin Only)
| Field | Value |
|-------|-------|
| Method | `GET` |
| URL | `{{base_url}}/api/auth/users` |
| Auth | Bearer Token → `{{access_token}}` |
| Params | `pageNumber=1`, `pageSize=10` |

---

### 6. Get User by ID (Admin Only)
| Field | Value |
|-------|-------|
| Method | `GET` |
| URL | `{{base_url}}/api/auth/users/1` |
| Auth | Bearer Token → `{{access_token}}` |

---

## 📁 Surveys

### 7. Create Survey
| Field | Value |
|-------|-------|
| Method | `POST` |
| URL | `{{base_url}}/api/survey` |
| Auth | Bearer Token → `{{access_token}}` |
| Body | `raw` → `JSON` |

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
      "text": "Would you recommend us?",
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

**Tests tab** (save survey ID):
```javascript
var jsonData = pm.response.json();
pm.environment.set("survey_id", jsonData.id);
```

---

### 8. Create Survey by Cloning
| Field | Value |
|-------|-------|
| Method | `POST` |
| URL | `{{base_url}}/api/survey` |
| Body | `raw` → `JSON` |

```json
{
  "title": "Q2 Satisfaction Survey",
  "description": "Cloned from Q1.",
  "copyQuestionsFromSurveyId": {{survey_id}}
}
```

---

### 9. Get All Surveys (No Auth Required)
| Field | Value |
|-------|-------|
| Method | `GET` |
| URL | `{{base_url}}/api/survey` |
| Params | `pageNumber=1`, `pageSize=10`, `searchTerm=satisfaction` |

---

### 10. Get Survey by ID (No Auth Required)
| Field | Value |
|-------|-------|
| Method | `GET` |
| URL | `{{base_url}}/api/survey/{{survey_id}}` |

---

### 11. Get My Surveys
| Field | Value |
|-------|-------|
| Method | `GET` |
| URL | `{{base_url}}/api/survey/my-surveys` |
| Params | `pageNumber=1`, `pageSize=10` |

---

### 12. Update Survey
| Field | Value |
|-------|-------|
| Method | `PUT` |
| URL | `{{base_url}}/api/survey/{{survey_id}}` |
| Body | `raw` → `JSON` |

```json
{
  "title": "Updated Survey Title",
  "description": "New description",
  "isActive": true
}
```

---

### 13. Delete Survey (Admin Only)
| Field | Value |
|-------|-------|
| Method | `DELETE` |
| URL | `{{base_url}}/api/survey/{{survey_id}}` |

**Expected:** `204 No Content`

---

### 14. Add Question to Survey
| Field | Value |
|-------|-------|
| Method | `POST` |
| URL | `{{base_url}}/api/survey/{{survey_id}}/questions` |
| Body | `raw` → `JSON` |

```json
{
  "text": "How did you hear about us?",
  "questionType": "MultipleChoice",
  "options": "[\"Social Media\", \"Friend\", \"Ad\", \"Other\"]",
  "isRequired": false,
  "orderIndex": 5
}
```

---

### 15. Delete Question (Admin Only)
| Field | Value |
|-------|-------|
| Method | `DELETE` |
| URL | `{{base_url}}/api/survey/questions/1` |

---

## 📁 Survey Responses

### 16. Submit Response (No Auth Required)
| Field | Value |
|-------|-------|
| Method | `POST` |
| URL | `{{base_url}}/api/surveyresponse` |
| Body | `raw` → `JSON` |

```json
{
  "surveyId": {{survey_id}},
  "answers": [
    { "questionId": 1, "answerText": "5" },
    { "questionId": 2, "answerText": "Yes" },
    { "questionId": 3, "answerText": "Speed" },
    { "questionId": 4, "answerText": "Great service!" }
  ]
}
```

**Tests tab** (save response ID):
```javascript
var jsonData = pm.response.json();
pm.environment.set("response_id", jsonData.id);
```

---

### 17. Get Response by ID
| Field | Value |
|-------|-------|
| Method | `GET` |
| URL | `{{base_url}}/api/surveyresponse/{{response_id}}` |

---

### 18. Get All Responses for a Survey
| Field | Value |
|-------|-------|
| Method | `GET` |
| URL | `{{base_url}}/api/surveyresponse/survey/{{survey_id}}` |
| Params | `pageNumber=1`, `pageSize=10` |

---

### 19. Pause Response
| Field | Value |
|-------|-------|
| Method | `PUT` |
| URL | `{{base_url}}/api/surveyresponse/{{response_id}}/pause` |

---

### 20. Resume Response
| Field | Value |
|-------|-------|
| Method | `PUT` |
| URL | `{{base_url}}/api/surveyresponse/{{response_id}}/resume` |
| Body | `raw` → `JSON` |

```json
[
  { "questionId": 3, "answerText": "Design" },
  { "questionId": 4, "answerText": "Updated feedback." }
]
```

---

## 📁 Recipients

### 21. Add Recipient
| Field | Value |
|-------|-------|
| Method | `POST` |
| URL | `{{base_url}}/api/recipient` |
| Body | `raw` → `JSON` |

```json
{
  "name": "Jane Smith",
  "email": "jane@example.com",
  "groupName": "Customers"
}
```

---

### 22. Bulk Import Recipients
| Field | Value |
|-------|-------|
| Method | `POST` |
| URL | `{{base_url}}/api/recipient/import` |
| Body | `raw` → `JSON` |

```json
[
  { "name": "Alice", "email": "alice@test.com", "groupName": "Customers" },
  { "name": "Bob", "email": "bob@test.com", "groupName": "Partners" },
  { "name": "Charlie", "email": "charlie@test.com", "groupName": "Customers" }
]
```

---

### 23. Get All Recipients
| Field | Value |
|-------|-------|
| Method | `GET` |
| URL | `{{base_url}}/api/recipient` |
| Params | `pageNumber=1`, `pageSize=10` |

---

### 24. Filter by Group
| Field | Value |
|-------|-------|
| Method | `GET` |
| URL | `{{base_url}}/api/recipient/group/Customers` |
| Params | `pageNumber=1`, `pageSize=10` |

---

### 25. Delete Recipient
| Field | Value |
|-------|-------|
| Method | `DELETE` |
| URL | `{{base_url}}/api/recipient/1` |

---

## 📁 Distributions

### 26. Create Distribution
| Field | Value |
|-------|-------|
| Method | `POST` |
| URL | `{{base_url}}/api/distribution` |
| Body | `raw` → `JSON` |

```json
{
  "surveyId": {{survey_id}},
  "distributionType": "Email",
  "distributionValue": "jane@example.com",
  "scheduledAt": "2026-03-05T09:00:00Z"
}
```

---

### 27. Get Distributions for Survey
| Field | Value |
|-------|-------|
| Method | `GET` |
| URL | `{{base_url}}/api/distribution/survey/{{survey_id}}` |

---

### 28. Delete Distribution (Admin Only)
| Field | Value |
|-------|-------|
| Method | `DELETE` |
| URL | `{{base_url}}/api/distribution/1` |

---

## 📁 Notifications

### 29. Get My Notifications
| Field | Value |
|-------|-------|
| Method | `GET` |
| URL | `{{base_url}}/api/notification` |
| Params | `pageNumber=1`, `pageSize=10` |

---

### 30. Get Unread Count
| Field | Value |
|-------|-------|
| Method | `GET` |
| URL | `{{base_url}}/api/notification/unread-count` |

---

### 31. Mark as Read
| Field | Value |
|-------|-------|
| Method | `PUT` |
| URL | `{{base_url}}/api/notification/1/mark-read` |

---

## 📁 Analytics & Export

### 32. Get Survey Analytics
| Field | Value |
|-------|-------|
| Method | `GET` |
| URL | `{{base_url}}/api/analytics/survey/{{survey_id}}` |

---

### 33. Export to CSV
| Field | Value |
|-------|-------|
| Method | `GET` |
| URL | `{{base_url}}/api/analytics/survey/{{survey_id}}/export/csv` |

> Click **Send and Download** in Postman to save the `.csv` file.

---

### 34. Export to Excel
| Field | Value |
|-------|-------|
| Method | `GET` |
| URL | `{{base_url}}/api/analytics/survey/{{survey_id}}/export/excel` |

> Click **Send and Download** in Postman to save the `.xlsx` file.

---

## 📁 Templates

### 35. Create Template
| Field | Value |
|-------|-------|
| Method | `POST` |
| URL | `{{base_url}}/api/template` |
| Body | `raw` → `JSON` |

```json
{
  "name": "Employee Feedback Template",
  "description": "Quarterly employee review",
  "templateData": "{\"questions\": [{\"text\": \"Rate your team?\", \"type\": \"Rating\"}]}"
}
```

---

### 36. Get All Templates
| Field | Value |
|-------|-------|
| Method | `GET` |
| URL | `{{base_url}}/api/template` |

---

### 37. Get Template by ID
| Field | Value |
|-------|-------|
| Method | `GET` |
| URL | `{{base_url}}/api/template/1` |

---

### 38. Delete Template (Admin Only)
| Field | Value |
|-------|-------|
| Method | `DELETE` |
| URL | `{{base_url}}/api/template/1` |

---

## 🧪 Recommended Testing Order

Follow this exact order to test the full flow without errors:

1. **Register** (Request #1) — creates a user and saves the token
2. **Create Survey** (Request #7) — creates a survey with questions
3. **Submit Response** (Request #16) — submits answers to the survey
4. **Get Analytics** (Request #32) — view aggregated results
5. **Export to Excel** (Request #34) — download the report
6. **Bulk Import Recipients** (Request #22) — add contacts
7. **Create Distribution** (Request #26) — distribute the survey
8. **Check Notifications** (Request #29) — see if notifications were generated

---

## ❌ Common Errors

| Error | Cause | Fix |
|-------|-------|-----|
| `401 Unauthorized` | Missing or expired token | Login again, copy the new token |
| `403 Forbidden` | Your role doesn't have access | Use an Admin account |
| `404 Not Found` | Wrong ID in the URL | Check the ID exists in the database |
| `400 Bad Request` | Invalid JSON body | Check required fields and data types |
| `409 Conflict` | Duplicate email on register | Use a different email address |
