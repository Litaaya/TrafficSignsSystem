using Marten;
using Marten.Linq.MatchesSql;
using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Events;
using TrafficSigns.Domain.Models;

namespace TrafficSigns.Application.Features.TrafficSigns.Commands;

public record ReactivateTrafficSignCommand(Guid Id) : IRequest<bool>;

public class ReactivateTrafficSignHandler(
    IDocumentSession session,
    ICurrentUserService currentUser,
    IPermissionService permissionService) : IRequestHandler<ReactivateTrafficSignCommand, bool>
{
    public async Task<bool> Handle(ReactivateTrafficSignCommand request, CancellationToken cancellationToken)
    {
        var sign = await session.LoadAsync<TrafficSign>(request.Id, cancellationToken);

        if (sign == null)
        {
            throw new Exception($"Invalid Traffic Sign with Id {request.Id}");
        }

        if (!await permissionService.CanManageTrafficSignsAsync(sign.AccountId))
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        if (!sign.Inactive)
        {
            throw new Exception("Traffic Sign is already active.");
        }

        var duplicateSign = await session.Query<TrafficSign>()
            .Where(s => s.Inactive == false
                     && s.Id != sign.Id
                     && s.AccountId == sign.AccountId
                     && s.RoadSegmentId == sign.RoadSegmentId
                     && s.Code == sign.Code
                     && s.IsForwardDirection == sign.IsForwardDirection
                     && s.MatchesSql(@"ST_DWithin(
                                       ST_GeomFromGeoJSON((data -> 'location')::text), 
                                       ST_SetSRID(ST_MakePoint(?, ?), 4326), 
                                       0.00003
                                )", sign.Location.X, sign.Location.Y))
            .FirstOrDefaultAsync(cancellationToken);

        if (duplicateSign != null)
        {
            throw new Exception($"Cannot reactivate. An active sign with code '{duplicateSign.Code}' already exists at this location.");
        }

        string actor = currentUser.GetUsername() ?? "Unknown";
        var actorId = currentUser.GetUserId();

        var @event = new TrafficSignReactivated(request.Id);

        session.SetHeader("user-id", actorId?.ToString() ?? Guid.Empty.ToString());
        session.SetHeader("user-name", actor);

        session.Events.Append(request.Id, @event);
        await session.SaveChangesAsync(cancellationToken);

        return true;
    }
}