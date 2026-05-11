namespace TrafficSigns.Application.Common.Interfaces;

public interface IAuditActorProvider
{
    Guid? ActorId { get; set; }
    string? ActorName { get; set; }
    string? OverrideAction { get; set; }
}