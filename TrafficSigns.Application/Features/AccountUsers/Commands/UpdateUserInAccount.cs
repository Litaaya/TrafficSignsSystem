using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace TrafficSigns.Application.Features.AccountUsers.Commands;

public record UpdateUserInAccountRequest(string Role);

public record UpdateUserInAccountCommand(
    Guid AccountId,
    Guid UserId,
    string Role
) : IRequest<bool>;

public class UpdateUserInAccountHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IPermissionService permissionService) : IRequestHandler<UpdateUserInAccountCommand, bool>
{
    private readonly string[] _allowedRoles = ["Viewer", "Member", "Owner"];

    public async Task<bool> Handle(UpdateUserInAccountCommand request, CancellationToken cancellationToken)
    {
        if (!await permissionService.CanManageAccountUsersAsync(request.AccountId))
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        if (!_allowedRoles.Contains(request.Role))
        {
            throw new Exception($"Invalid role. Allowed roles are: {string.Join(", ", _allowedRoles)}");
        }

        var accountUser = await db.AccountUsers
            .FirstOrDefaultAsync(au => au.AccountId == request.AccountId
                                    && au.UserId == request.UserId
                                    && !au.Inactive, cancellationToken);

        if (accountUser == null)
        {
            throw new Exception("User association not found or is inactive.");
        }

        if (accountUser.Role == "Owner" && request.Role != "Owner")
        {
            var otherOwnersExist = await db.AccountUsers
                .AnyAsync(au => au.AccountId == request.AccountId
                                && au.UserId != request.UserId
                                && au.Role == "Owner"
                                && !au.Inactive, cancellationToken);

            if (!otherOwnersExist)
            {
                throw new Exception("Cannot change the role of the last owner in this account.");
            }
        }

        string actor = currentUser.GetUsername() ?? "Unknown";
        var actorId = currentUser.GetUserId();
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        accountUser.Role = request.Role;
        accountUser.UpdatedDt = DateTime.UtcNow;
        accountUser.AddMetadataLog("update_history", $"Role updated to {request.Role} by {actor}({actorId}) at {timestamp}");

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

