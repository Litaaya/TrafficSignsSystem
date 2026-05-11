using TrafficSigns.Application.Common.Interfaces;

namespace TrafficSigns.Infrastructure.Services;

public class AuditActorProvider : IAuditActorProvider
{
    public Guid? ActorId { get; set; }
    public string? ActorName { get; set; }
    public string? OverrideAction { get; set; }
}