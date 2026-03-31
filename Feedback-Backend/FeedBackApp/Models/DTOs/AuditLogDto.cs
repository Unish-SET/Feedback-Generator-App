namespace FeedBackApp.Models.DTOs
{
    
    public class AuditLogDto
    {
        public Guid Id { get; set; }
        public int? UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string EntityName { get; set; } = string.Empty;
        public string? EntityId { get; set; }
        public string? Changes { get; set; }
        public string? IPAddress { get; set; }
        public string? CorrelationId { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
