using Marten;
using Marten.Linq.MatchesSql;
using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Events;
using TrafficSigns.Domain.Models;
using FluentValidation;
using FluentValidation.Results;

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
            throw new KeyNotFoundException($"Invalid Traffic Sign with Id {request.Id}");
        }

        if (!await permissionService.CanManageTrafficSignsAsync(sign.AccountId))
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        if (!sign.IsDeleted)
        {
            var failure = new ValidationFailure(nameof(request.Id), "Traffic Sign is already active");
            throw new ValidationException(new[] { failure });
        }

        var duplicateSign = await session.Query<TrafficSign>()
            .Where(s => s.IsDeleted == false
                     && s.Id != sign.Id
                     && s.AccountId == sign.AccountId
                     && s.Code == sign.Code
                     && s.MatchesSql(@"ST_DWithin(
                                       ST_GeomFromGeoJSON((data -> 'location')::text), 
                                       ST_SetSRID(ST_MakePoint(?, ?), 4326), 
                                       0.00003
                                )", sign.Location.X, sign.Location.Y))
            .FirstOrDefaultAsync(cancellationToken);

        if (duplicateSign != null)
        {
            var failure = new ValidationFailure(nameof(sign.Code), $"Cannot reactivate. An active sign with code '{duplicateSign.Code}' already exists at this location");
            throw new ValidationException(new[] { failure });
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