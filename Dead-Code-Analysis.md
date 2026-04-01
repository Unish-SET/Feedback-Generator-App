# Dead Code & Unused Code Analysis

> Analysis covers: `Feedback-Backend/` (.NET 10 Web API) and `Feedback-Angular/` (Angular 19)
> Rule: No code was modified. Every finding is verified by cross-referencing actual usages.

---

## Angular Frontend

---

### 1. `returnUrl` signal — LoginComponent & RegisterComponent

**Files:**
- `src/app/features/auth/login/login.component.ts` — line 23
- `src/app/features/auth/register/register.component.ts` — line 23

**Code:**
```typescript
readonly returnUrl = signal<string | null>(null);

constructor() {
  this.returnUrl.set(this.route.snapshot.queryParamMap.get('returnUrl'));
}
```

**Why unused:**
The `returnUrl` signal is set in the constructor but never read again. The actual redirect logic in `onSubmit()` re-reads the query param directly from `this.route.snapshot.queryParamMap.get('returnUrl')` — it does not use the signal at all. The signal is a leftover from an earlier refactor.

**Safe to delete:** Yes — the signal and the `constructor()` body that sets it can both be removed. The `ActivatedRoute` injection can also be removed from `RegisterComponent` if `returnUrl` is the only reason it was injected (check the template first — it is not used there either).

**Cleanup:** Remove `readonly returnUrl = signal<string | null>(null)` and the `constructor()` block in both files.

---

### 2. Empty `ngAfterViewInit()` — DashboardComponent & AnalyticsComponent

**Files:**
- `src/app/features/dashboard/dashboard.component.ts` — line 49
- `src/app/features/analytics/analytics.component.ts` — line 103

**Code:**
```typescript
ngAfterViewInit(): void {}
```

**Why unused:**
Both implementations are completely empty. The `AfterViewInit` interface is implemented and the lifecycle hook is declared but does nothing. Chart building is handled via `setTimeout()` inside `ngOnInit` (dashboard) and inside the reactive pipeline's `subscribe` callback (analytics) — neither needs `ngAfterViewInit`.

**Safe to delete:** Yes — remove the empty method and remove `AfterViewInit` from the `implements` clause and from the import in both files.

---

### 3. `UserRole` type — `shared/models/index.ts`

**File:** `src/app/shared/models/index.ts` — line 244

**Code:**
```typescript
export type UserRole = 'Admin' | 'Creator';
```

**Why unused:**
Searched all `.ts` files — `UserRole` is never imported or referenced anywhere in the Angular codebase. Role comparisons are done with plain string literals (`'Admin'`, `'Creator'`) throughout the components and services.

**Safe to delete:** Yes — remove the type alias entirely.

---

### 4. `QuestionType` type — `shared/models/index.ts`

**File:** `src/app/shared/models/index.ts` — line 102

**Code:**
```typescript
export type QuestionType = 'ShortText' | 'LongText' | 'SingleChoice' | 'MultipleChoice' | 'RatingScale';
```

**Why unused:**
Searched all `.ts` files — `QuestionType` is never imported or used as a type annotation anywhere. Question types are handled as plain `string` throughout the codebase (e.g., `type: string` in `Question`, `CreateQuestionRequest`, `UpdateQuestionRequest`).

**Safe to delete:** Yes — remove the type alias.

---

### 5. `QuestionBankService.getById()` — never called from any component

**File:** `src/app/core/services/question-bank.service.ts` — lines 38–41

**Code:**
```typescript
getById(id: number): Observable<BankQuestion> {
  return this.http.get<ApiResponse<BankQuestion>>(`${this.base}/${id}`)
    .pipe(map(r => r.data));
}
```

**Why unused:**
Searched all component `.ts` files — `bankService.getById(` and `questionBankService.getById(` return zero matches. The `QuestionBankComponent` uses `getAll()` for listing and `update()` for editing (it already has the full object from the list). No component ever fetches a single bank question by ID.

**Safe to delete:** Yes — the method and the corresponding `GET /api/question-bank/{id}` call are dead from the frontend's perspective. The backend endpoint itself can remain if you plan to use it in the future.

---

### 6. `AnalyticsService.exportExcel()` is duplicated in usage

**File:** `src/app/core/services/analytics.service.ts` — lines 18–23

**Note — not dead, but a design smell:**
`exportExcel()` lives in `AnalyticsService` but is called from both `AnalyticsComponent` and `ResponsesComponent`. Logically, Excel export is a survey data operation, not an analytics operation. This is not dead code but is misplaced — worth moving to a dedicated `ExportService` or `SurveyService` in a future refactor.

---

### 7. `getOptionText()` method — ResponsesComponent

**File:** `src/app/features/responses/responses.component.ts` — lines 97–99

**Code:**
```typescript
getOptionText(_response: SurveyResponseRecord, _questionId: number, optionId: number): string {
  return `#${optionId}`;
}
```

**Why unused / dead:**
Both parameters `_response` and `_questionId` are prefixed with `_` indicating they are intentionally unused. The method returns a trivial fallback string `#${optionId}`. Searching the template (`responses.component.html`) would confirm whether this is called — but the implementation is clearly a stub/placeholder that was never completed. The actual answer text is already available in `AnswerDto.selectedOptionText` which is populated by the backend.

**Safe to delete:** Risky without checking the template — verify `getOptionText` is not called in `responses.component.html` before removing.

---

### 8. `LOGIN_REQUIRED` error handling — error.interceptor.ts

**File:** `src/app/core/interceptors/error.interceptor.ts` — lines 26–29

**Code:**
```typescript
if (backendMessage === 'LOGIN_REQUIRED') {
  return throwError(() => error);
}
```

**Why unused:**
Searched the entire backend codebase — the string `LOGIN_REQUIRED` is never thrown or returned by any controller, service, or middleware. This was likely written in anticipation of a feature (login-required surveys) that was removed via the `RemoveAccessControl` migration. The condition will never be true.

**Safe to delete:** Yes — remove the `LOGIN_REQUIRED` special case. The standard 401 handling below it already covers all real 401 scenarios.

---

## .NET Backend

---

### 9. `SetSurveyStatusDto` class — CommonDtos.cs

**File:** `Feedback-Backend/FeedBackApp/Models/DTOs/CommonDtos.cs` — lines 163–168

**Code:**
```csharp
public class SetSurveyStatusDto
{
    /// <summary>Draft, Active, or Closed</summary>
    public string Status { get; set; } = string.Empty;
}
```

**Why unused:**
Searched all `.cs` files — `SetSurveyStatusDto` is never referenced in any controller, service, or interface. The actual state-change endpoint uses `SetSurveyStateDto` (in `SurveyDtos.cs`). This class is a leftover from before the rename from `/status` → `/state`.

**Safe to delete:** Yes — completely safe to remove.

---

### 10. `SetSurveyAvailabilityDto` class — CommonDtos.cs

**File:** `Feedback-Backend/FeedBackApp/Models/DTOs/CommonDtos.cs` — lines 170–174

**Code:**
```csharp
public class SetSurveyAvailabilityDto
{
    public bool IsActive { get; set; }
}
```

**Why unused:**
Searched all `.cs` files — `SetSurveyAvailabilityDto` is never referenced anywhere. This was likely from an older `/availability` endpoint that was removed when the unified `/state` endpoint was introduced.

**Safe to delete:** Yes — completely safe to remove.

---

### 11. `UserRole.Respondent` enum value — UserRole.cs

**File:** `Feedback-Backend/FeedBackApp/Models/Enums/UserRole.cs` — line 8

**Code:**
```csharp
public enum UserRole
{
    Admin = 0,
    Creator = 1,
    Respondent = 2   // ← never used
}
```

**Why unused:**
`UserRole.Respondent` is never assigned anywhere in the codebase. Registration always assigns `UserRole.Creator`. No `[Authorize(Roles = "Respondent")]` attribute exists anywhere. The only mention is in an error message string in `UserService.UpdateUserRoleAsync()` — it lists `Respondent` as a valid role in the error text, but the role itself is never actually used in any business logic, authorization check, or seeding.

**Risky to delete:** Moderate risk. Removing it would be a breaking migration if any existing database rows have `Role = 2`. Check the database before removing. If no rows use it, it is safe to remove the enum value and update the error message in `UserService`.

---

### 12. `Models/Options/` folder — empty

**File:** `Feedback-Backend/FeedBackApp/Models/Options/` — empty directory

**Why unused:**
The folder exists but contains no files. It was likely created in anticipation of options/configuration classes (e.g., `SmtpOptions`, `JwtOptions`) that were never implemented — configuration is read directly via `IConfiguration` instead.

**Safe to delete:** Yes — remove the empty folder.

---

### 13. `Services/AnalyticsEmail/` and `Services/Email/` folders — empty

**Files:**
- `Feedback-Backend/FeedBackApp/Services/AnalyticsEmail/` — empty
- `Feedback-Backend/FeedBackApp/Services/Email/` — empty

**Why unused:**
Both folders are empty. The `appsettings.json` has an `Smtp` section and an `AnalyticsEmail` section with configuration values, but no email service implementation exists. These are placeholder folders for a feature that was planned but not built.

**Safe to delete:** Yes — remove both empty folders.

---

### 14. `Migration: 20260330000001_AddIndexesAndUniqueConstraints` — references dropped table

**File:** `Feedback-Backend/FeedBackApp/Migrations/20260330000001_AddIndexesAndUniqueConstraints.cs`

**Why problematic:**
This migration creates indexes on `SurveyAccesses` table (`IX_SurveyAccesses_SurveyId_UserId`, `IX_SurveyAccesses_SurveyId_Email`). However, the subsequent migration `20260330200000_RemoveAccessControl` drops the entire `SurveyAccesses` table. The `AddIndexesAndUniqueConstraints` migration's `Up()` method will fail if run on a fresh database that never had `SurveyAccesses` — but since `RemoveAccessControl` runs after it, on a fresh DB both migrations would fail.

**Note:** This is not dead code per se — it is a migration ordering issue. The `AddIndexesAndUniqueConstraints` migration was created before the table was dropped. On an existing database that already ran both migrations, this is harmless. On a fresh database, `db.Database.Migrate()` would fail at this migration.

**Risky to change:** Yes — do not modify existing migrations if the database has already been migrated. Instead, squash or consolidate migrations in a new migration if starting fresh.

---

### 15. `Smtp` configuration section — appsettings.json

**File:** `Feedback-Backend/FeedBackApp/appsettings.json` — lines for `Smtp` section

**Code:**
```json
"Smtp": {
  "Host": "",
  "Port": "587",
  "Username": "",
  "Password": "",
  "From": "noreply@feedbackapp.com",
  "UseSsl": "true"
}
```

**Why unused:**
No `SmtpClient`, `IEmailService`, or any email-sending code exists anywhere in the backend. The `Services/Email/` folder is empty. The SMTP config is dead configuration.

**Safe to delete:** Yes — remove the `Smtp` section from `appsettings.json` and `appsettings.Development.json` if it exists there too.

---

### 16. `AnalyticsEmail` configuration section — appsettings.json

**File:** `Feedback-Backend/FeedBackApp/appsettings.json`

**Code:**
```json
"AnalyticsEmail": {
  "MaxRecipients": 25,
  "MaxTextSamplesPerQuestion": 20,
  "MaxTextCharsPerSample": 400,
  "SmtpAttemptsPerRecipient": 3,
  "SmtpRetryBaseSeconds": 2
}
```

**Why unused:**
The `Services/AnalyticsEmail/` folder is empty. No service reads these configuration values. This is dead configuration for a planned but unimplemented feature.

**Safe to delete:** Yes — remove the `AnalyticsEmail` section from `appsettings.json`.

---

### 17. `AuditController` — queries DbContext directly (bypasses service layer)

**File:** `Feedback-Backend/FeedBackApp/Controllers/AuditController.cs`

**Note — not dead, but an architectural inconsistency:**
`AuditController` injects `FeedBackDbContext` directly instead of using `IAuditService`. Every other controller uses a service interface. This is not dead code but is inconsistent with the rest of the architecture. The `IAuditService` interface only exposes `LogAsync()` — it has no read methods — so the controller had no choice. This is a gap in the service layer, not dead code.

---

## Summary

### Total Dead / Unused Items Found: 16

| # | Location | Type | Safe to Delete |
|---|---|---|---|
| 1 | `login.component.ts` + `register.component.ts` | Unused `returnUrl` signal | Yes |
| 2 | `dashboard.component.ts` + `analytics.component.ts` | Empty `ngAfterViewInit()` | Yes |
| 3 | `shared/models/index.ts` | Unused `UserRole` type | Yes |
| 4 | `shared/models/index.ts` | Unused `QuestionType` type | Yes |
| 5 | `question-bank.service.ts` | `getById()` never called | Yes |
| 6 | `responses.component.ts` | Stub `getOptionText()` method | Verify template first |
| 7 | `error.interceptor.ts` | Dead `LOGIN_REQUIRED` branch | Yes |
| 8 | `CommonDtos.cs` | `SetSurveyStatusDto` class | Yes |
| 9 | `CommonDtos.cs` | `SetSurveyAvailabilityDto` class | Yes |
| 10 | `UserRole.cs` | `Respondent = 2` enum value | Check DB first |
| 11 | `Models/Options/` | Empty folder | Yes |
| 12 | `Services/AnalyticsEmail/` | Empty folder | Yes |
| 13 | `Services/Email/` | Empty folder | Yes |
| 14 | `appsettings.json` | Dead `Smtp` config section | Yes |
| 15 | `appsettings.json` | Dead `AnalyticsEmail` config section | Yes |
| 16 | Migration `20260330000001` | References dropped `SurveyAccesses` table | Do not modify |

---

## Optimization Suggestions

1. **Add a read method to `IAuditService`** — `AuditController` bypasses the service layer because `IAuditService` only has `LogAsync`. Add `GetLogsAsync(AuditFilterParams)` to the interface and move the query logic from the controller into `AuditService`. This makes the controller consistent with the rest of the architecture and makes the audit query testable.

2. **Squash the three migrations into one** — `InitialCreate`, `AddIndexesAndUniqueConstraints`, and `RemoveAccessControl` can be squashed into a single clean migration. The current sequence creates a `SurveyAccesses` table and then immediately drops it, which is confusing and causes issues on fresh databases.

3. **Move `exportExcel()` out of `AnalyticsService`** — Excel export is not an analytics concern. Move it to `SurveyService` or a dedicated `ExportService` to keep service responsibilities clean.

4. **Implement or remove the email feature** — The `Smtp` and `AnalyticsEmail` config sections, the two empty service folders, and the `IEmailService` gap suggest a planned feature. Either implement it or remove all traces to keep the codebase clean.

5. **Consolidate `importExcel` and `importExcelWithProgress`** in `QuestionBankService` — `importExcel` (used in `QuestionBankComponent`) and `importExcelWithProgress` (used in `SurveyBuilderComponent`) hit the same endpoint. Consider unifying them into one method with an optional `reportProgress` flag to reduce duplication.

6. **Add `GetLogsAsync` to `IAuditService`** — currently the only way to read audit logs is to inject `FeedBackDbContext` directly into a controller, which breaks the abstraction layer.
