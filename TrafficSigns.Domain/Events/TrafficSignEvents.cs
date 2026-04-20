using NetTopologySuite.Geometries;

namespace TrafficSigns.Domain.Events;

public record TrafficSignCreated(
    Guid Id,
    string Code,
    string Name,
    Point Location,
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
    Dictionary<string, object> Metadata
);