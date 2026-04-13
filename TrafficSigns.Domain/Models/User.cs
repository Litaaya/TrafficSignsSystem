namespace TrafficSigns.Domain.Models
{
    public class User : BaseEntity
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? PendingEmail { get; set; }
        public string? Email { get; set; }
        public string? PendingPhone { get; set; }
        public string? Phone { get; set; }

        //public DateTime? LastLoginAt { get; set; }
        //public DateTime? LastActiveAt { get; set; }

        public virtual ICollection<AccountUser> AccountUsers { get; set; } = new List<AccountUser>();
    }
}
