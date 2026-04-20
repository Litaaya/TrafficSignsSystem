using Marten;
using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Events;
using TrafficSigns.Domain.Models;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using FluentValidation;
using FluentValidation.Results;

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
    ICurrentUserService currentUser,
    IPermissionService permissionService) : IRequestHandler<UpdateTrafficSignCommand, bool>
{
    public async Task<bool> Handle(UpdateTrafficSignCommand request, CancellationToken cancellationToken)
    {
        var sign = await session.LoadAsync<TrafficSign>(request.Id, cancellationToken);
        if (sign == null)
        {
            throw new KeyNotFoundException($"Traffic sign with ID {request.Id} was not found");
        }

        if (!await permissionService.CanManageTrafficSignsAsync(sign.AccountId))
        {
            throw new UnauthorizedAccessException("Access denied");
        }

        if (sign.IsDeleted)
        {
            var failure = new ValidationFailure(nameof(request.Id), "Traffic sign is inactivated and cannot be updated");
            throw new ValidationException(new[] { failure });
        }

        string actor = currentUser.GetUsername() ?? "Unknown";
        var actorId = currentUser.GetUserId();

        var location = new Point(request.Longitude, request.Latitude) { SRID = 4326 };

        var @event = new TrafficSignUpdated(
            request.Id,
            request.Code.Trim().ToUpper(),
            request.Name.Trim(),
            location,
            request.Metadata ?? new Dictionary<string, object>()
        );

        session.SetHeader("user-id", actorId?.ToString() ?? Guid.Empty.ToString());
        session.SetHeader("user-name", actor);

        session.Events.Append(request.Id, @event);
        await session.SaveChangesAsync(cancellationToken);

        return true;
    }
}