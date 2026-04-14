using Marten;
using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Events;
using TrafficSigns.Domain.Models;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace TrafficSigns.Application.Features.TrafficSigns.Commands;

public record UpdateTrafficSignCommand(
    Guid Id,
    string Code,
    string Name,
    double Latitude,
    double Longitude,
    long RoadSegmentId,
    bool IsForwardDirection,
    Dictionary<string, object>? Metadata = null
) : IRequest<bool>;

public class UpdateTrafficSignHandler(
    IDocumentSession session,
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IPermissionService permissionService) : IRequestHandler<UpdateTrafficSignCommand, bool>
{
    public async Task<bool> Handle(UpdateTrafficSignCommand request, CancellationToken cancellationToken)
    {
        var sign = await session.LoadAsync<TrafficSign>(request.Id, cancellationToken);
        if (sign == null)
        {
            throw new Exception($"Traffic sign with ID {request.Id} was not found.");
        }

        if (!await permissionService.CanManageTrafficSignsAsync(sign.AccountId))
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        if (sign.Inactive)
        {
            throw new Exception("Traffic sign is inactivated and cannot be updated.");
        }

        var roadQuery = db.Database.SqlQueryRaw<int>("SELECT 1 FROM traffic_signs_map WHERE segment_id = {0} LIMIT 1", request.RoadSegmentId);
        var roadExists = await EntityFrameworkQueryableExtensions.AnyAsync(roadQuery, cancellationToken);

        if (!roadExists)
        {
            throw new Exception($"Road Segment ID {request.RoadSegmentId} is invalid.");
        }

        string actor = currentUser.GetUsername() ?? "Unknown";
        var actorId = currentUser.GetUserId();

        var location = new Point(request.Longitude, request.Latitude) { SRID = 4326 };

        var @event = new TrafficSignUpdated(
            request.Id,
            request.Code.Trim().ToUpper(),
            request.Name.Trim(),
            location,
            request.RoadSegmentId,
            request.IsForwardDirection,
            request.Metadata ?? new Dictionary<string, object>()
        );

        session.SetHeader("user-id", actorId?.ToString() ?? Guid.Empty.ToString());
        session.SetHeader("user-name", actor);

        session.Events.Append(request.Id, @event);
        await session.SaveChangesAsync(cancellationToken);

        return true;
    }
}