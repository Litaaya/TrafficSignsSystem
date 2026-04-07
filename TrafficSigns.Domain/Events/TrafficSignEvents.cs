using NetTopologySuite.Geometries;

namespace TrafficSigns.Domain.Events;

public record TrafficSignCreated(
    Guid Id,
    string Code,
    string Name,
    Point Location,
    long RoadSegmentId,
    bool IsForwardDirection,
    Guid AccountId,
    Dictionary<string, object>? Metadata
);

public record TrafficSignInactivated(Guid Id);

public record TrafficSignReactivated(Guid Id);

public record TrafficSignUpdated(
    Guid Id,
    string Code,
    string Name,
    Point Location,
    long RoadSegmentId,
    bool IsForwardDirection,
    Dictionary<string, object> Metadata
);