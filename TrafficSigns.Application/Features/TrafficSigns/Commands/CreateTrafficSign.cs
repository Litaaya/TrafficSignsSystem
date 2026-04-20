using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Events;
using TrafficSigns.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Marten;
using Marten.Linq.MatchesSql;
using NetTopologySuite.Geometries;
using FluentValidation;
using FluentValidation.Results;

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
            throw new UnauthorizedAccessException("Access denied");

        var accountExists = await EntityFrameworkQueryableExtensions
            .AnyAsync(db.Accounts, a => a.Id == request.AccountId, cancellationToken);

        if (!accountExists)
            throw new KeyNotFoundException($"Account {request.AccountId} does not exist");

        var location = new Point(request.Longitude, request.Latitude) { SRID = 4326 };

        var query = session.Query<TrafficSign>()
            .Where(s => s.IsDeleted == false
                     && s.AccountId == request.AccountId
                     && s.Code == request.Code.Trim()
                     && s.MatchesSql(@"ST_DWithin(
                                    ST_GeomFromGeoJSON((data -> 'location')::text), 
                                    ST_SetSRID(ST_MakePoint(?, ?), 4326), 
                                    0.00003
                                )", request.Longitude, request.Latitude));

        var existingSign = await QueryableExtensions.FirstOrDefaultAsync(query, cancellationToken);

        if (existingSign != null)
        {
            var failure = new ValidationFailure(nameof(request.Code), $"This position already has an active sign with code '{existingSign.Code}'");
            throw new ValidationException(new[] { failure });
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