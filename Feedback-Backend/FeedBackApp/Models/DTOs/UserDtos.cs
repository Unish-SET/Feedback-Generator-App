namespace FeedBackApp.Models.DTOs
{
    public class UserResponseDto
    {
        public int      Id            { get; set; }
        public string   Username      { get; set; } = string.Empty;
        public string   Email         { get; set; } = string.Empty;
        public string   Role          { get; set; } = string.Empty;
        public bool     IsActive      { get; set; }
        public bool     IsDeleted     { get; set; }
        public DateTime CreatedAt     { get; set; }
        public int      SurveyCount   { get; set; }
        public int      ResponseCount { get; set; }
    }

    public class UpdateUserRoleDto
    {
        public string Role { get; set; } = string.Empty;
    }

    public class UpdateUserStatusDto
    {
        /// <summary>true = activate, false = deactivate</summary>
        public bool IsActive { get; set; }
    }
}
