namespace FeedBackApp.Models.DTOs
{
    public class PaginatedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPrevious => PageNumber > 1;
        public bool HasNext => PageNumber < TotalPages;
    }

    public class PaginationParams
    {
        private const int MaxPageSize = 50;
        private int _pageSize = 10;

        public int PageNumber { get; set; } = 1;

        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
        }
    }

    // ── Survey Filters ────────────────────────────────────────────────────────
    public class SurveyFilterParams : PaginationParams
    {
        /// <summary>Filter by status: Draft, Active, Closed</summary>
        public string? Status { get; set; }

        /// <summary>Case-insensitive title search</summary>
        public string? Search { get; set; }

        /// <summary>Admin only — filter by creator user ID</summary>
        public int? CreatedBy { get; set; }

        /// <summary>Filter surveys whose StartDate is on or after this value (UTC)</summary>
        public DateTime? StartDateFrom { get; set; }

        /// <summary>Filter surveys whose StartDate is on or before this value (UTC)</summary>
        public DateTime? StartDateTo { get; set; }

        /// <summary>Sort field: createdAt (default), title, responseCount</summary>
        public string? SortBy { get; set; }

        /// <summary>Sort direction: asc, desc (default)</summary>
        public string? SortDir { get; set; }
    }

    // ── Response Filters ──────────────────────────────────────────────────────
    public class ResponseFilterParams : PaginationParams
    {
        /// <summary>Filter responses submitted on or after this date (UTC). Alias: FromDate.</summary>
        public DateTime? SubmittedFrom { get; set; }

        /// <summary>Filter responses submitted on or before this date (UTC). Alias: ToDate.</summary>
        public DateTime? SubmittedTo { get; set; }

        /// <summary>Alias for SubmittedFrom — accepted as ?fromDate= in query string.</summary>
        public DateTime? FromDate
        {
            get => SubmittedFrom;
            set => SubmittedFrom = value;
        }

        /// <summary>Alias for SubmittedTo — accepted as ?toDate= in query string.</summary>
        public DateTime? ToDate
        {
            get => SubmittedTo;
            set => SubmittedTo = value;
        }

        /// <summary>Filter by respondent user ID (null = anonymous only when set to 0)</summary>
        public int? UserId { get; set; }
    }

    // ── Analytics Filters ─────────────────────────────────────────────────────
    public class AnalyticsFilterParams
    {
        /// <summary>Scope date-wise counts and total responses from this date (UTC)</summary>
        public DateTime? FromDate { get; set; }

        /// <summary>Scope date-wise counts and total responses to this date (UTC)</summary>
        public DateTime? ToDate { get; set; }
    }

    // ── User Filters ──────────────────────────────────────────────────────────
    public class UserFilterParams : PaginationParams
    {
        /// <summary>Filter by role: Admin, Creator</summary>
        public string? Role { get; set; }

        /// <summary>Case-insensitive search on username or email</summary>
        public string? Search { get; set; }

        /// <summary>Filter users created on or after this date (UTC)</summary>
        public DateTime? FromDate { get; set; }

        /// <summary>Filter users created on or before this date (UTC)</summary>
        public DateTime? ToDate { get; set; }
    }

    // ── Admin Survey Filters ──────────────────────────────────────────────────
    public class AdminSurveyFilterParams : PaginationParams
    {
        /// <summary>Search by survey title (case-insensitive)</summary>
        public string? Search { get; set; }

        /// <summary>true = deleted only, false = active only, null = all</summary>
        public bool? IsDeleted { get; set; }

        /// <summary>Filter by CreatedAt >= fromDate (UTC)</summary>
        public DateTime? FromDate { get; set; }

        /// <summary>Filter by CreatedAt <= toDate (UTC)</summary>
        public DateTime? ToDate { get; set; }
    }

    // ── Admin Survey DTOs ─────────────────────────────────────────────────────
    public class AdminSurveyListDto
    {
        public int      Id            { get; set; }
        public string   Title         { get; set; } = string.Empty;
        public string   Status        { get; set; } = string.Empty;
        public string   CreatedBy     { get; set; } = string.Empty;  // username
        public string   CreatorEmail  { get; set; } = string.Empty;
        public DateTime CreatedAt     { get; set; }
        public bool     IsDeleted     { get; set; }
        public int      ResponseCount { get; set; }
    }

    public class AdminStatsDto
    {
        public int TotalSurveys    { get; set; }
        public int ActiveSurveys   { get; set; }
        public int DeletedSurveys  { get; set; }
        public int TotalResponses  { get; set; }
    }

    // ── Admin Survey Detail (with versions) ───────────────────────────────────
    public class AdminSurveyDetailDto
    {
        public int      Id             { get; set; }
        public string   Title          { get; set; } = string.Empty;
        public string   Description    { get; set; } = string.Empty;
        public string   Status         { get; set; } = string.Empty;
        public string   CreatedBy      { get; set; } = string.Empty;
        public string   CreatorEmail   { get; set; } = string.Empty;
        public DateTime CreatedAt      { get; set; }
        public DateTime UpdatedAt      { get; set; }
        public bool     IsDeleted      { get; set; }
        public bool     AllowAnonymous { get; set; }
        public DateTime? StartDate     { get; set; }
        public DateTime? EndDate       { get; set; }
        public int      TotalResponses { get; set; }
        public int      QuestionCount  { get; set; }
    }

    // ── Audit Filters ─────────────────────────────────────────────────────────
    public class AuditFilterParams : PaginationParams
    {
        public string?   Search   { get; set; }
        public string?   Action   { get; set; }
        public string?   Entity   { get; set; }
        public int?      UserId   { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate   { get; set; }
    }

    public class ApiErrorResponse
    {
        public int StatusCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
