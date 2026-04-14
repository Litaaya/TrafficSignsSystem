using NetTopologySuite.Geometries;
using TrafficSigns.Domain.Events;

namespace TrafficSigns.Domain.Models;

public class TrafficSign
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Point Location { get; set; } = null!;
    public bool IsDeleted { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public Guid AccountId { get; set; }
    public long RoadSegmentId { get; set; }
    public bool IsForwardDirection { get; set; }

    public void Apply(TrafficSignCreated @event)
    {
        Id = @event.Id;
        Code = @event.Code;
        Name = @event.Name;
        Location = @event.Location;
        AccountId = @event.AccountId;
        RoadSegmentId = @event.RoadSegmentId;
        IsForwardDirection = @event.IsForwardDirection;
        IsDeleted = false;
        Metadata = @event.Metadata ?? new Dictionary<string, object>();
    }

    public void Apply(TrafficSignUpdated @event)
    {
        Code = @event.Code;
        Name = @event.Name;
        Location = @event.Location;
        RoadSegmentId = @event.RoadSegmentId;
        IsForwardDirection = @event.IsForwardDirection;
        Metadata = @event.Metadata;
    }

    public void Apply(TrafficSignInactivated @event) => IsDeleted = true;
    public void Apply(TrafficSignReactivated @event) => IsDeleted = false;
}