using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace TrafficSigns.Application.Features.AccountUsers.Commands;

public record RemoveUserFromAccountCommand(Guid AccountId, Guid UserId) : IRequest<bool>;

public class RemoveUserFromAccountHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUserService,
    IPermissionService permissionService) : IRequestHandler<RemoveUserFromAccountCommand, bool>
{
    public async Task<bool> Handle(RemoveUserFromAccountCommand request, CancellationToken cancellationToken)
    {
        if (!await permissionService.CanRemoveUserAsync(request.AccountId, request.UserId))
        {
            throw new UnauthorizedAccessException("Access denied or cannot remove the last owner.");
        }

        var accountUser = await db.AccountUsers
            .FirstOrDefaultAsync(au => au.AccountId == request.AccountId
                                    && au.UserId == request.UserId
                                    && !au.Inactive, cancellationToken);

        if (accountUser == null)
        {
            throw new Exception("User association not found or already inactive.");
        }

        if (accountUser.Role == "Owner")
        {
            var otherOwnersExist = await db.AccountUsers.AnyAsync(au =>
                au.AccountId == request.AccountId &&
                au.UserId != request.UserId &&
                au.Role == "Owner" &&
                !au.Inactive, cancellationToken);

            if (!otherOwnersExist)
            {
                throw new Exception("Cannot remove the last owner of the account.");
            }
        }

        string actor = currentUserService.GetUsername() ?? "Unknown";
        var actorId = currentUserService.GetUserId();
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        accountUser.Inactive = true;
        accountUser.UpdatedDt = DateTime.UtcNow;
        accountUser.AddMetadataLog("update_history", $"Removed by {actor}({actorId}) at {timestamp}");

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}