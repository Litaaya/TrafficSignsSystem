namespace TrafficSigns.Domain.Models
{
    public class Account : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? Desc { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public bool System { get; set; }
        public virtual ICollection<AccountUser> AccountUsers { get; set; } = new List<AccountUser>();
    }
}
