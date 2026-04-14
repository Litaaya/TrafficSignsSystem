using System.ComponentModel.DataAnnotations.Schema;

namespace TrafficSigns.Domain.Models
{
    public class AuditLog
    {
        public Guid Id { get; set; }
        public string EntityName { get; set; } = null!;
        public Guid EntityId { get; set; }
        public string Action { get; set; } = null!;

        public Guid? UserId { get; set; }
        public string? UserName { get; set; }
        public DateTime Timestamp { get; set; }
        
        [Column(TypeName = "jsonb")]
        public string? OldValues { get; set; }
        [Column(TypeName = "jsonb")]
        public string? NewValues { get; set; }

        public string? ChangedColumns { get; set; }

        public string? RelationalId { get; set; }
    }
}
