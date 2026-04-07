namespace TrafficSigns.Domain.Models
{
    public class AccountUser : BaseEntity
    {
        public Guid AccountId { get; set; }
        public Guid UserId { get; set; }
        public string Role { get; set; } = "Viewer";

        public virtual Account Account { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}
