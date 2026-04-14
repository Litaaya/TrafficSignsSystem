using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Events;
using TrafficSigns.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Marten;
using Marten.Linq.MatchesSql;
using NetTopologySuite.Geometries;

namespace TrafficSigns.Application.Features.TrafficSigns.Commands;

public record CreateTrafficSignCommand(
    string Code,
    string Name,
    double Latitude,
    double Longitude,
    Guid AccountId,
    long RoadSegmentId,
    bool IsForwardDirection,
    Dictionary<string, object>? Metadata = null
) : IRequest<Guid>;

public class CreateTrafficSignHandler(
    IDocumentSession session,
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IPermissionService permissionService) : IRequestHandler<CreateTrafficSignCommand, Guid>
{
    public async Task<Guid> Handle(CreateTrafficSignCommand request, CancellationToken cancellationToken)
    {
        if (!await permissionService.CanManageTrafficSignsAsync(request.AccountId))
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        var accountExists = await EntityFrameworkQueryableExtensions
            .AnyAsync(db.Accounts, a => a.Id == request.AccountId, cancellationToken);

        if (!accountExists)
        {
            throw new Exception($"Account {request.AccountId} does not exist.");
        }

        var roadQuery = db.Database.SqlQueryRaw<int>("SELECT 1 FROM traffic_signs_map WHERE segment_id = {0} LIMIT 1", request.RoadSegmentId);
        var roadExists = await EntityFrameworkQueryableExtensions
            .AnyAsync(roadQuery, cancellationToken);

        if (!roadExists)
        {
            throw new Exception($"Road Segment ID {request.RoadSegmentId} is invalid.");
        }

        var location = new Point(request.Longitude, request.Latitude) { SRID = 4326 };

        var query = session.Query<TrafficSign>()
            .Where(s => s.IsDeleted == false
                     && s.AccountId == request.AccountId
                     && s.Code == request.Code.Trim()
                     && s.IsForwardDirection == request.IsForwardDirection
                     && s.RoadSegmentId == request.RoadSegmentId
                     && s.MatchesSql(@"ST_DWithin(
                                    ST_GeomFromGeoJSON((data -> 'location')::text), 
                                    ST_SetSRID(ST_MakePoint(?, ?), 4326), 
                                    0.00003
                                )", request.Longitude, request.Latitude));

        var existingSign = await QueryableExtensions.FirstOrDefaultAsync(query, cancellationToken);

        if (existingSign != null)
        {
            throw new Exception($"This position already has an active sign with code '{existingSign.Code}'.");
        }

        string actor = currentUser.GetUsername() ?? "Unknown";
        var actorId = currentUser.GetUserId();

        var signId = Guid.NewGuid();
        var metadata = request.Metadata ?? new Dictionary<string, object>();

        var @event = new TrafficSignCreated(
            signId,
            request.Code.Trim().ToUpper(),
            request.Name.Trim(),
            location,
            request.RoadSegmentId,
            request.IsForwardDirection,
            request.AccountId,
            metadata
        );

        session.SetHeader("user-id", actorId?.ToString() ?? Guid.Empty.ToString());
        session.SetHeader("user-name", actor);

        session.Events.StartStream<TrafficSign>(signId, @event);
        await session.SaveChangesAsync(cancellationToken);

        return signId;
    }
}