# Full-Stack Code Review Report

> Reviewed: FeedBackApp .NET 10 Backend + Angular 19 Frontend
> Reviewer role: Senior Full-Stack Architect

---

## 1. ERRORS & RUNTIME RISKS

---

### [BACKEND] [CRITICAL] `GetUserId()` returns 0 on parse failure — silent privilege escalation risk

**File:** All controllers that use `GetUserId()`
```csharp
private int GetUserId() =>
    int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
```
**Problem:** If the claim is missing or malformed, this returns `0` instead of throwing. A user with ID `0` does not exist, but the value `0` is passed to service methods. In `SurveyService.GetAllAsync`, a Creator with `userId = 0` would get an empty list (harmless). But in `DeleteAsync` or `UpdateAsync`, the access check `survey.CreatedBy != userId` would compare against `0` — which would never match a real survey, so it would throw `ForbiddenException`. This is safe by accident, not by design. If a survey were ever seeded with `CreatedBy = 0`, it would be accessible to any broken token.

**Fix:** Return `Unauthorized` immediately if the claim is missing:
```csharp
private int GetUserId()
{
    var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
    if (string.IsNullOrEmpty(raw) || !int.TryParse(raw, out var id))
        throw new UnAuthorizedException("User identity could not be resolved.");
    return id;
}
```

---

### [BACKEND] [HIGH] `AuthController.Register` returns HTTP 200 instead of 201

**File:** `Controllers/AuthController.cs` — line 22
```csharp
return Ok(new { success = true, data = result });
```
**Problem:** A successful resource creation should return `201 Created`, not `200 OK`. This is inconsistent with `SurveyController.Create` which correctly returns `CreatedAtAction`.

**Fix:**
```csharp
return StatusCode(201, new { success = true, data = result });
```

---

### [BACKEND] [HIGH] `AnalyticsService` loads ALL answers into memory — N+1 risk on large surveys

**File:** `Services/AnalyticsService.cs` — lines 55–65
```csharp
var questions = await _questionRepo.GetQueryable()
    .Include(q => q.Options)
    .Include(q => q.Answers)
        .ThenInclude(a => a.SelectedOption)
    .Where(q => q.SurveyId == surveyId)
    .ToListAsync();
```
**Problem:** This loads every `Answer` row for every question into memory. For a survey with 1000 responses and 20 questions, that's 20,000 Answer rows loaded into RAM. The in-memory LINQ then iterates them multiple times per question (`.Count(a => ...)` called twice per option in SingleChoice).

**Fix:** Push the aggregation to SQL using a projection query instead of loading raw entities. At minimum, avoid calling `.Count(a => a.SelectedOptionId == o.Id)` twice — compute it once:
```csharp
// In SingleChoice case — compute counts once, not twice per option
var countsByOption = question.Answers
    .Where(a => a.SelectedOptionId.HasValue)
    .GroupBy(a => a.SelectedOptionId!.Value)
    .ToDictionary(g => g.Key, g => g.Count());
var totalSingle = countsByOption.Values.Sum();
qa.OptionDistributions = question.Options.OrderBy(o => o.Order).Select(o => {
    var count = countsByOption.GetValueOrDefault(o.Id, 0);
    return new OptionDistributionDto {
        OptionId = o.Id, OptionText = o.Text, Count = count,
        Percentage = totalSingle > 0 ? Math.Round((double)count / totalSingle * 100, 2) : 0
    };
}).ToList();
```

---

### [BACKEND] [HIGH] `QuestionService.UpdateQuestionAsync` — options added to wrong context state

**File:** `Services/QuestionService.cs` — lines 80–92
```csharp
_optionRepo.RemoveRange(question.Options);

if (questionType == QuestionType.SingleChoice || questionType == QuestionType.MultipleChoice)
{
    foreach (var optionDto in dto.Options)
    {
        var option = new QuestionOption
        {
            QuestionId = question.Id,
            ...
        };
        await _optionRepo.AddAsync(option);
    }
}

_questionRepo.Update(question);
await _questionRepo.SaveChangesAsync();
```
**Problem:** `_optionRepo.RemoveRange(question.Options)` marks the existing options for deletion. Then new options are added via `_optionRepo.AddAsync`. Then `_questionRepo.Update(question)` is called — but `question.Options` still holds the old (now-deleted) collection reference. EF Core's change tracker may attempt to re-insert the old options or produce a conflict. The correct pattern is to clear the navigation collection and let EF track the new ones.

**Fix:** After removing, clear the collection and add new options directly to it:
```csharp
_optionRepo.RemoveRange(question.Options);
question.Options.Clear();

if (questionType == QuestionType.SingleChoice || questionType == QuestionType.MultipleChoice)
{
    foreach (var optionDto in dto.Options)
        question.Options.Add(new QuestionOption { Text = optionDto.Text, Order = optionDto.Order });
}

_questionRepo.Update(question);
await _questionRepo.SaveChangesAsync();
```

---

### [BACKEND] [MEDIUM] `SurveyService.CreateAsync` — two `SaveChangesAsync` calls in one request

**File:** `Services/SurveyService.cs` — lines 42–65
```csharp
await _surveyRepo.AddAsync(survey);
await _surveyRepo.SaveChangesAsync();  // first save — gets the ID

if (dto.State?.Equals("Active", ...) == true)
{
    await ValidateCanActivate(survey.Id);
    survey.State = SurveyState.Active;
    _surveyRepo.Update(survey);
    await _surveyRepo.SaveChangesAsync();  // second save
}
```
**Problem:** The first save is needed to get the survey ID for `ValidateCanActivate`. But if the second save fails (e.g., DB timeout), the survey is left in `Inactive` state even though the caller requested `Active`. This is not atomic. Also, `ValidateCanActivate` checks for questions — a brand-new survey will never have questions, so passing `State = "Active"` on creation will always throw `BadRequestException`. This makes the `State` field in `CreateSurveyDto` effectively useless.

**Fix:** Remove the `State` field from `CreateSurveyDto` entirely, or document clearly that it cannot be `Active` on creation. The current code will always throw for `Active` on a new survey.

---

### [BACKEND] [MEDIUM] `UnAuthorizedException` is defined but never thrown or used

**File:** `Exceptions/AppExceptions.cs` — line 26
```csharp
public class UnAuthorizedException : AppException
{
    public UnAuthorizedException(string message = "Unauthorized") : base(message, 401) { }
}
```
**Problem:** This class exists but is never thrown anywhere in the codebase. The 401 case is handled by ASP.NET Core's built-in JWT middleware, not by this exception. It's dead code that could mislead developers into thinking it's in use.

**Fix:** Either use it in `GetUserId()` (see issue #1 above) or remove it.

---

### [BACKEND] [MEDIUM] `ApiErrorResponse` DTO is defined but never used

**File:** `Models/DTOs/CommonDtos.cs` — bottom of file
```csharp
public class ApiErrorResponse
{
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
```
**Problem:** Error responses are returned as anonymous objects in `GlobalExceptionMiddleware`. This DTO is never instantiated or referenced anywhere.

**Fix:** Either use it in `GlobalExceptionMiddleware` for a typed response, or remove it.

---

### [ANGULAR] [HIGH] `public-survey.component.ts` — `survey()!` non-null assertion without guard

**File:** `features/public-survey/public-survey.component.ts` — lines 120, 130
```typescript
nextQuestion(): void {
    const q = this.survey()!.questions[this.currentQuestion()];
    ...
}

submit(): void {
    const s = this.survey()!;
    ...
}
```
**Problem:** If `survey()` is `null` (e.g., the user navigates back after an error, or the signal hasn't been set), the `!` assertion will cause a runtime `TypeError: Cannot read properties of null`. There is no guard before accessing `.questions`.

**Fix:**
```typescript
nextQuestion(): void {
    const s = this.survey();
    if (!s) return;
    const q = s.questions[this.currentQuestion()];
    ...
}
```

---

### [ANGULAR] [HIGH] `error.interceptor.ts` — 401 triggers `auth.logout()` on ALL 401s including public survey errors

**File:** `core/interceptors/error.interceptor.ts` — lines 26–29
```typescript
} else if (error.status === 401) {
    userMessage = 'Session expired. Please log in again.';
    auth.logout();
}
```
**Problem:** The public survey endpoint (`GET /survey/{token}`) can return 401 if the survey requires login. When an anonymous user hits this, the interceptor calls `auth.logout()` — which calls `router.navigate(['/auth/login'])`. But the user was never logged in. This causes a redirect loop: the public survey page redirects to login, login redirects back to the survey, which hits 401 again.

**Fix:** Only call `auth.logout()` if the user is currently authenticated:
```typescript
} else if (error.status === 401) {
    if (auth.isAuthenticated()) {
        userMessage = 'Session expired. Please log in again.';
        auth.logout();
    } else {
        userMessage = 'Authentication required.';
    }
}
```

---

### [ANGULAR] [MEDIUM] `responses.component.ts` — `surveyService.getById()` has no error handler

**File:** `features/responses/responses.component.ts` — lines 65–68
```typescript
this.surveyService.getById(Number(this.id())).subscribe({
    next: s => this.survey.set(s)
});
```
**Problem:** No `error` handler. If the survey doesn't exist or the user lacks access, the error is swallowed silently. The `survey` signal stays `null`, and the template renders with no context. The global error interceptor will show a toast, but the component has no way to show a proper error state or redirect.

**Fix:**
```typescript
this.surveyService.getById(Number(this.id())).subscribe({
    next: s => this.survey.set(s),
    error: () => this.router.navigate(['/surveys'])
});
```

---

### [ANGULAR] [MEDIUM] `analytics.component.ts` — same missing error handler on `getById`

**File:** `features/analytics/analytics.component.ts` — lines 68–71
Same issue as above. No error handler on `surveyService.getById()`.

---

### [ANGULAR] [MEDIUM] `analytics.component.ts` — `document.querySelectorAll` breaks OnPush

**File:** `features/analytics/analytics.component.ts` — lines 107–125
```typescript
const pieCanvases = document.querySelectorAll('canvas.pie-chart');
pieCanvases.forEach((canvas) => { ... });
```
**Problem:** Direct DOM querying with `document.querySelectorAll` bypasses Angular's change detection entirely. Under `ChangeDetectionStrategy.OnPush`, the canvases may not be in the DOM yet when `buildCharts()` runs (called via `setTimeout(..., 100)`). The 100ms timeout is a fragile workaround — on slow devices or under load, the canvases may not be rendered yet.

**Fix:** Use `@ViewChildren('pieChart')` with a `QueryList<ElementRef>` to get references to the canvases in a lifecycle-safe way.


---

## 2. DEBUGGING ISSUES

---

### [BACKEND] [MEDIUM] Fire-and-forget audit calls lose exceptions silently

**File:** All services — e.g. `SurveyService.cs`, `QuestionService.cs`
```csharp
_ = _audit.LogAsync("Create", "Survey", survey.Id.ToString(), userId);
```
**Problem:** The `_` discard means any exception from `LogAsync` is completely swallowed. `AuditService` already has an internal try/catch that logs a warning, so this is safe — but if `AuditService` itself has a bug (e.g., wrong method signature call), the failure is invisible.

**Status:** Acceptable as-is since `AuditService` has its own catch. No change needed, but worth noting.

---

### [BACKEND] [MEDIUM] `ExportController` has manual try/catch duplicating `GlobalExceptionMiddleware`

**File:** `Controllers/ExportController.cs` — lines 42–58
```csharp
catch (NotFoundException ex)   { return NotFound(...); }
catch (ForbiddenException ex)  { return StatusCode(403, ...); }
catch (BadRequestException ex) { return BadRequest(...); }
catch (Exception ex)           { return StatusCode(500, ...); }
```
**Problem:** `GlobalExceptionMiddleware` already handles all `AppException` subclasses and returns the correct status codes. This manual try/catch in `ExportController` is redundant and creates two code paths for the same error handling. If the global middleware's error format changes, this controller won't be updated.

**Fix:** Remove the try/catch entirely. Let `GlobalExceptionMiddleware` handle it uniformly like every other controller.

---

### [BACKEND] [LOW] `AdminSurveyController` has partial manual try/catch — inconsistent with other controllers

**File:** `Controllers/AdminSurveyController.cs` — `GetDetail`, `SetState`, `Restore`, `SoftDelete`
Same issue as above. Some actions have try/catch, others don't. `GetAll` and `GetStats` have no try/catch and rely on the global middleware. The inconsistency makes the error handling hard to reason about.

**Fix:** Remove all manual try/catch blocks from `AdminSurveyController` and rely on `GlobalExceptionMiddleware`.

---

### [BACKEND] [LOW] No logging in `SurveyService`, `ResponseService`, `QuestionService`

**Problem:** These are the most critical services in the app, but they have zero `ILogger` usage. If a survey submission fails or a question update silently misbehaves, there is no server-side trace beyond the audit log.

**Fix:** Inject `ILogger<T>` into at least `ResponseService` and log key events:
```csharp
_logger.LogInformation("Response submitted for survey {SurveyId} by user {UserId}", survey.Id, userId);
_logger.LogWarning("Duplicate response attempt for survey {SurveyId} by user {UserId}", survey.Id, userId);
```

---

### [ANGULAR] [MEDIUM] `admin.component.ts` — `confirmDelete` has no error handler on `userService.delete()`

**File:** `features/admin/admin.component.ts` — lines 195–203
```typescript
this.userService.delete(user.id).subscribe({
    next: () => {
        this.enrichedUsers.update(list => list.filter(u => u.id !== user.id));
        this.userTotal.update(t => t - 1);
        this.toast.success(`${user.username} has been deleted.`);
    }
    // no error handler
});
```
**Problem:** If the delete fails (e.g., user has active surveys — the backend returns 400), the global interceptor shows a toast, but the local list is not updated. The user disappears from the list optimistically... wait, no — the `next` block only runs on success. But there's no `error` handler to reset any loading state. The `actionUserId` signal is never reset on error, leaving the action button permanently disabled.

**Fix:**
```typescript
this.userService.delete(user.id).subscribe({
    next: () => { ... },
    error: () => { /* no spinner to reset here, but good practice */ }
});
```

---

## 3. WRONG LOGIC

---

### [BACKEND] [HIGH] `SurveyService.SetStateAsync` — allows `Closed → Active` transition

**File:** `Services/SurveyService.cs` — lines 130–135
```csharp
case SurveyState.Active when survey.State == SurveyState.Inactive:
case SurveyState.Active when survey.State == SurveyState.Closed:
    await ValidateCanActivate(surveyId);
    survey.State = SurveyState.Active;
```
**Problem:** The code comment in `SurveyController` says "Closed surveys cannot be reopened", but the `SetStateAsync` switch case explicitly allows `Closed → Active`. This is a direct contradiction between the documented behavior and the implementation. The `AdminSurveyService.SetSurveyStatusAsync` also allows reopening closed surveys. You need to decide which is correct and make both consistent.

**Fix:** If closed surveys should not be reopened by Creators (only by Admins), add a guard:
```csharp
case SurveyState.Active when survey.State == SurveyState.Closed:
    if (!RoleHelper.IsAdmin(role))
        throw new BadRequestException("Closed surveys cannot be reopened. Contact an admin.");
    goto case SurveyState.Active; // or duplicate the logic
```

---

### [BACKEND] [HIGH] `ResponseService.SubmitAsync` — answers for questions not in the survey are silently dropped

**File:** `Services/ResponseService.cs` — lines 72–82
```csharp
Answers = dto.Answers
    .Where(a => survey.Questions.Any(q => q.Id == a.QuestionId))
    .Select(a => new Answer { ... }).ToList()
```
**Problem:** If a client submits answers for question IDs that don't belong to this survey, those answers are silently filtered out. The response is saved with fewer answers than submitted, with no error or warning. This could mask a client bug or a malicious attempt to probe question IDs.

**Fix:** Throw a `BadRequestException` if any submitted `QuestionId` doesn't belong to the survey:
```csharp
var surveyQuestionIds = survey.Questions.Select(q => q.Id).ToHashSet();
var invalidIds = dto.Answers.Select(a => a.QuestionId).Where(id => !surveyQuestionIds.Contains(id)).ToList();
if (invalidIds.Any())
    throw new BadRequestException($"Question IDs do not belong to this survey: {string.Join(", ", invalidIds)}");
```

---

### [BACKEND] [MEDIUM] `SurveyService.GetAllAsync` — `_userRepo` is injected but never used

**File:** `Services/SurveyService.cs` — constructor
```csharp
private readonly IRepository<User> _userRepo;
```
**Problem:** `_userRepo` is injected in the constructor but never referenced in any method. This is dead dependency injection — it adds overhead and confusion.

**Fix:** Remove `IRepository<User> userRepo` from the constructor and the field.

---

### [BACKEND] [MEDIUM] `PaginationParams` default `PageSize` is 10, but `SurveyService` overrides to 20

**File:** `Models/DTOs/CommonDtos.cs` — `PaginationParams._pageSize = 10`
**File:** `Services/SurveyService.cs` — `var pageSize = filter.PageSize <= 0 ? 20 : ...`

**Problem:** `PaginationParams` defaults to `PageSize = 10`. But `SurveyService.GetAllAsync` overrides this with `20` when `PageSize <= 0`. Since `PageSize` defaults to `10` (not `0`), the `<= 0` check never triggers — the service always uses whatever `PaginationParams` provides. The `20` fallback is dead code. This is confusing.

**Fix:** Either set `PaginationParams._pageSize = 20` to match the service's intent, or remove the redundant override in the service.

---

### [BACKEND] [MEDIUM] `AdminSurveyService.GetSurveyDetailAsync` — loads all Questions and Responses into memory

**File:** `Services/AdminSurveyService.cs` — lines 65–72
```csharp
var survey = await _db.Surveys
    .IgnoreQueryFilters()
    .Include(s => s.Creator)
    .Include(s => s.Questions)
    .Include(s => s.Responses)
    .FirstOrDefaultAsync(s => s.Id == surveyId);
```
**Problem:** For a survey with 500 responses and 30 questions, this loads all 530 rows into memory just to count them. The DTO only uses `.Count` on both collections.

**Fix:** Use a projection to avoid loading the full collections:
```csharp
var survey = await _db.Surveys
    .IgnoreQueryFilters()
    .Select(s => new AdminSurveyDetailDto {
        ...
        TotalResponses = s.Responses.Count(),
        QuestionCount  = s.Questions.Count()
    })
    .FirstOrDefaultAsync(s => s.Id == surveyId);
```

---

### [ANGULAR] [HIGH] `public-survey.component.ts` — `formValues` signal is declared but never used

**File:** `features/public-survey/public-survey.component.ts` — line 35
```typescript
readonly formValues = toSignal(this.responseForm.valueChanges, { initialValue: {} as Record<string, unknown> });
```
**Problem:** `formValues` is created but never referenced in the component class or template. It creates an unnecessary subscription to `valueChanges` for the entire lifetime of the component.

**Fix:** Remove this line entirely.

---

### [ANGULAR] [MEDIUM] `admin.component.ts` — `calculatedSurveyCount` is always equal to `surveyCount`

**File:** `features/admin/admin.component.ts` — line 148
```typescript
this.enrichedUsers.set(result.items.map(u => ({ ...u, calculatedSurveyCount: u.surveyCount })));
```
**Problem:** `EnrichedUser` adds `calculatedSurveyCount` which is always set to `u.surveyCount`. There is no calculation — it's a direct copy. The `EnrichedUser` interface and the extra field add complexity with zero benefit.

**Fix:** Remove `EnrichedUser` interface and `calculatedSurveyCount`. Use `AppUser` directly.

---

### [ANGULAR] [MEDIUM] `responses.component.ts` — `getOptionText()` is a stub that returns `#${optionId}`

**File:** `features/responses/responses.component.ts` — lines 97–99
```typescript
getOptionText(_response: SurveyResponseRecord, _questionId: number, optionId: number): string {
    return `#${optionId}`;
}
```
**Problem:** This method is called in the template for MultipleChoice answers, but it only returns the raw option ID prefixed with `#`. The actual option text is already available in `AnswerDto.selectedOptionText` for SingleChoice. For MultipleChoice, the backend returns `selectedOptionIds` as a list of integers — the option text is not included in the response DTO.

**Fix (backend):** Add option texts to `AnswerDto` for MultipleChoice:
```csharp
// In ResponseService, for MultipleChoice answers, resolve option texts server-side
SelectedOptionTexts = a.SelectedOptionIds != null
    ? ResolveMultipleChoiceTexts(a.SelectedOptionIds, a.Question)
    : null
```
**Fix (frontend):** Until the backend is updated, display the IDs with a note, or fetch question options separately.

---

## 4. PERFORMANCE & BEST PRACTICES

---

### [BACKEND] [HIGH] `ExcelService.ExportExcelAsync` — loads ALL responses into memory

**File:** `Services/ExcelService.cs` — lines 30–42
```csharp
var responses = await _responseRepo.GetQueryable()
    .Include(r => r.User)
    .Include(r => r.Answers)
        .ThenInclude(a => a.SelectedOption)
    .Where(r => r.SurveyId == surveyId)
    .OrderBy(r => r.SubmittedAt)
    .ToListAsync();
```
**Problem:** For a survey with 10,000 responses, this loads all rows into RAM before writing the Excel file. This will cause memory pressure and potential OOM on large surveys.

**Fix:** Process responses in batches using `Skip/Take`, or use `IAsyncEnumerable` with streaming. At minimum, add a cap:
```csharp
// Temporary safety cap until streaming is implemented
const int MaxExportRows = 5000;
var responses = await _responseRepo.GetQueryable()
    ...
    .Take(MaxExportRows)
    .ToListAsync();
```

---

### [BACKEND] [MEDIUM] `QuestionBankService.GetAllAsync` — `Include(b => b.Options)` on list query

**File:** `Services/QuestionBankService.cs` — line 100
```csharp
var query = _bankRepo.GetQueryable()
    .Include(b => b.Options)
    .Where(b => !b.IsDeleted)
```
**Problem:** The list endpoint loads all options for every bank question in the page. For a page of 20 questions each with 5 options, that's 100 extra rows. The list DTO includes options, so this is intentional — but it should use a projection instead of `Include` to avoid loading the full entity graph.

---

### [ANGULAR] [MEDIUM] `dashboard.component.ts` — fetches up to 100 surveys on every load

**File:** `features/dashboard/dashboard.component.ts` — line 68
```typescript
this.surveyService.getAll({ pageNumber: 1, pageSize: 100 }).subscribe({
```
**Problem:** The dashboard fetches 100 surveys on every load to compute stats (active count, inactive count, etc.) client-side. This is wasteful — the backend already has `AdminSurveyService.GetStatsAsync()` which returns these counts in a single SQL query. For Creators, there's no equivalent stats endpoint, but computing counts from 100 surveys client-side is fragile (if a Creator has more than 100 surveys, the counts will be wrong).

**Fix:** Add a `GET /api/survey/stats` endpoint for Creators, or reuse the admin stats endpoint with role-based filtering.

---

### [ANGULAR] [MEDIUM] `responses.component.ts` — `AnalyticsService` injected but only used for Excel export

**File:** `features/responses/responses.component.ts` — line 22
```typescript
private readonly analyticsService = inject(AnalyticsService);
```
**Problem:** `AnalyticsService` is injected only to call `exportExcel()`. This is a misplaced concern — Excel export is not an analytics operation. The `exportExcel` method should be on a dedicated export service or on `SurveyService`.

---

## 5. SECURITY

---

### [BACKEND] [HIGH] `QuestionImportController` — MIME type `application/octet-stream` is too permissive

**File:** `Controllers/QuestionImportController.cs` — line 38
```csharp
"application/octet-stream"  // some browsers send this for Excel
```
**Problem:** `application/octet-stream` is the generic binary MIME type. Accepting it means any binary file (executables, PDFs, ZIPs) passes the MIME check. The extension check provides some protection, but a malicious file named `evil.xlsx` with non-Excel content would pass both checks and be fed to `ClosedXML`.

**Fix:** Remove `application/octet-stream` from the allowed list. Modern browsers send the correct MIME type for Excel files. If compatibility is needed, validate the file's magic bytes instead:
```csharp
// XLSX files start with PK (ZIP header): 0x50 0x4B
var buffer = new byte[4];
await file.OpenReadStream().ReadAsync(buffer, 0, 4);
if (buffer[0] != 0x50 || buffer[1] != 0x4B)
    return BadRequest(new { success = false, message = "File is not a valid Excel file." });
```

---

### [BACKEND] [MEDIUM] `AuditController` — queries `_db.Users` without `IgnoreQueryFilters`

**File:** `Controllers/AuditController.cs` — lines 30–36
```csharp
var query = _db.AuditLogs
    .GroupJoin(
        _db.Users,
        log  => log.UserId,
        user => (int?)user.Id,
        ...
```
**Problem:** `_db.Users` has a global query filter for `IsDeleted`. Audit logs for deleted users will show `Username = "System"` instead of the actual username, because the deleted user is filtered out of the join. This makes audit logs misleading — you can't tell who performed an action if they were later deleted.

**Fix:**
```csharp
_db.Users.IgnoreQueryFilters(),
```

---

### [BACKEND] [MEDIUM] `appsettings.json` — JWT key placeholder is committed

**File:** `Feedback-Backend/FeedBackApp/appsettings.json`
```json
"Key": "REPLACE_ME_WITH_A_STRONG_SECRET_KEY_MIN_32_CHARS"
```
**Problem:** The placeholder key is committed to source control. If a developer forgets to replace it and deploys, the app runs with a known, weak key. Any token signed with this key can be forged by anyone who reads the repo.

**Fix:** Add a startup validation that throws if the key matches the placeholder:
```csharp
if (jwtKey.StartsWith("REPLACE_ME"))
    throw new InvalidOperationException("JWT key has not been configured. Set JwtSettings:Key.");
```

---

### [ANGULAR] [MEDIUM] `auth.guard.ts` — `adminGuard` redirects to `/dashboard` instead of showing 403

**File:** `core/guards/auth.guard.ts` — lines 18–25
```typescript
export const adminGuard: CanActivateFn = () => {
    if (auth.isAdmin()) return true;
    router.navigate(['/dashboard']);
    return false;
};
```
**Problem:** A Creator who manually navigates to `/admin` is silently redirected to `/dashboard` with no explanation. This is confusing UX — the user doesn't know why they were redirected.

**Fix:** Show a toast before redirecting:
```typescript
// inject ToastService and call:
toast.warning('You do not have permission to access the admin panel.');
router.navigate(['/dashboard']);
```

---

### [ANGULAR] [LOW] `public-survey.component.ts` — localStorage key uses `userId` which could be `'anon'` for all anonymous users

**File:** `features/public-survey/public-survey.component.ts` — lines 44–50
```typescript
private submissionKey(publicToken: string): string {
    const userId = this.authService.user()?.userId ?? 'anon';
    return `survey_submitted_${userId}_${publicToken}`;
}
```
**Problem:** All anonymous users share the key `survey_submitted_anon_${token}`. If User A submits anonymously on a shared device, User B on the same device will see `alreadySubmitted = true` even though they haven't submitted. This is a UX bug on shared devices.

**Status:** This is a known limitation of localStorage-based deduplication. It's acceptable for anonymous surveys but worth documenting.

---

## 6. CLEAN CODE IMPROVEMENTS

---

### [BACKEND] `GetUserId()` / `GetUserRole()` duplicated in 8 controllers

**Problem:** Every controller that needs the current user's ID and role has identical private helper methods:
```csharp
private int    GetUserId()   => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
private string GetUserRole() => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
```
This is copy-pasted in `SurveyController`, `QuestionController`, `AnalyticsController`, `ExportController`, `AdminSurveyController`, `QuestionBankController`, `QuestionImportController`, `ResponseController`.

**Fix:** Create a base controller class:
```csharp
public abstract class ApiControllerBase : ControllerBase
{
    protected int GetUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(raw) || !int.TryParse(raw, out var id))
            throw new UnAuthorizedException();
        return id;
    }
    protected string GetUserRole() => User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
}
```
All controllers inherit from `ApiControllerBase` instead of `ControllerBase`.

---

### [BACKEND] `SurveyService` and `QuestionService` both have `ValidateSurveyAccess` / `GetSurveyWithAccessCheck` — near-duplicate logic

**Problem:** Both services load a survey and check ownership. The logic is nearly identical but slightly different (one uses `GetByIdAsync`, the other uses `GetQueryable().Include(s => s.Creator)`). This duplication means a bug fix in one won't automatically fix the other.

**Fix:** Extract a shared `SurveyAccessValidator` helper or move the access check into a dedicated service method.

---

### [ANGULAR] `downloadBlob()` is duplicated in `analytics.component.ts` and `responses.component.ts`

**Problem:**
```typescript
private downloadBlob(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url; a.download = filename; a.click();
    URL.revokeObjectURL(url);
}
```
This exact method exists in both components.

**Fix:** Move it to a shared utility service or a standalone utility function in a `utils.ts` file.

---

### [ANGULAR] `admin.component.ts` is a 400+ line god component

**Problem:** `AdminComponent` manages three completely separate tabs (Users, Surveys, Audit) with their own state, pagination, search, and API calls — all in one component. This makes it hard to test, maintain, and reason about.

**Fix:** Split into three components:
- `AdminUsersComponent`
- `AdminSurveysComponent`
- `AdminAuditComponent`

Each loaded lazily under the `/admin` route as child routes.

---

## Summary

| Category | Count | Severity |
|---|---|---|
| Runtime Errors / Crashes | 3 | Critical/High |
| Wrong HTTP Status Codes | 1 | High |
| Logic Bugs | 6 | High/Medium |
| Performance Issues | 4 | High/Medium |
| Security Issues | 4 | High/Medium |
| Silent Failures / Missing Error Handlers | 4 | Medium |
| Dead Code / Unused | 3 | Low |
| Clean Code / Duplication | 4 | Low/Medium |

**Top 3 to fix immediately:**
1. `GetUserId()` returning `0` on failure — fix with proper `UnAuthorizedException`
2. `error.interceptor.ts` calling `auth.logout()` on all 401s — causes redirect loop for anonymous users
3. `public-survey.component.ts` non-null assertion `survey()!` — runtime crash risk
