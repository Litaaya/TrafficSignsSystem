using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using FluentValidation.Results;

namespace TrafficSigns.Application.Features.AccountUsers.Commands;

public record RemoveUserFromAccountCommand(Guid AccountId, Guid UserId) : IRequest<bool>;

public class RemoveUserFromAccountHandler(
    IApplicationDbContext db,
    IPermissionService permissionService) : IRequestHandler<RemoveUserFromAccountCommand, bool>
{
    public async Task<bool> Handle(RemoveUserFromAccountCommand request, CancellationToken cancellationToken)
    {
        if (!await permissionService.CanRemoveUserAsync(request.AccountId, request.UserId))
        {
            throw new UnauthorizedAccessException("Access denied or cannot remove the last owner");
        }

        var accountUser = await db.AccountUsers
            .FirstOrDefaultAsync(au => au.AccountId == request.AccountId
                                    && au.UserId == request.UserId
                                    && !au.IsDeleted, cancellationToken);

        if (accountUser == null)
        {
            throw new KeyNotFoundException("User association not found or already inactive");
        }

        if (accountUser.Role == "Owner")
        {
            var otherOwnersExist = await db.AccountUsers.AnyAsync(au =>
                au.AccountId == request.AccountId &&
                au.UserId != request.UserId &&
                au.Role == "Owner" &&
                !au.IsDeleted, cancellationToken);

            if (!otherOwnersExist)
            {
                var failure = new ValidationFailure(nameof(request.UserId), "Cannot remove the last owner of the account. Please assign another owner first");
                throw new ValidationException(new[] { failure });
            }
        }

        accountUser.IsDeleted = true;
        accountUser.UpdatedDt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}