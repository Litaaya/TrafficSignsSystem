using Marten;
using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Events;
using TrafficSigns.Domain.Models;
using FluentValidation;
using FluentValidation.Results;

namespace TrafficSigns.Application.Features.TrafficSigns.Commands;

public record DeleteTrafficSignCommand(Guid Id) : IRequest<bool>;

public class DeleteTrafficSignHandler(
    IDocumentSession session,
    ICurrentUserService currentUser,
    IPermissionService permissionService) : IRequestHandler<DeleteTrafficSignCommand, bool>
{
    public async Task<bool> Handle(DeleteTrafficSignCommand request, CancellationToken cancellationToken)
    {
        var sign = await session.LoadAsync<TrafficSign>(request.Id, cancellationToken);

        if (sign == null)
        {
            throw new KeyNotFoundException($"Invalid TrafficSign {request.Id}");
        }

        if (!await permissionService.CanManageTrafficSignsAsync(sign.AccountId))
        {
            throw new UnauthorizedAccessException("Access denied");
        }

        if (sign.IsDeleted)
        {
            var failure = new ValidationFailure(nameof(request.Id), "TrafficSign has already been inactivated before");
            throw new ValidationException(new[] { failure });
        }

        string actor = currentUser.GetUsername() ?? "Unknown";
        var actorId = currentUser.GetUserId();

        var @event = new TrafficSignInactivated(request.Id);

        session.SetHeader("user-id", actorId?.ToString() ?? Guid.Empty.ToString());
        session.SetHeader("user-name", actor);

        session.Events.Append(request.Id, @event);
        await session.SaveChangesAsync(cancellationToken);

        return true;
    }
}
