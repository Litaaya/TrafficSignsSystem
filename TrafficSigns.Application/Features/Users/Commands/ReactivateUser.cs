using MediatR;
using TrafficSigns.Application.Common.Interfaces;

namespace TrafficSigns.Application.Features.Users.Commands;

public record ReactivateUserRequest(string NewPassword);

public record ReactivateUserCommand(
    Guid UserId,
    string NewPassword
) : IRequest<Guid>;

public class ReactivateUserHandler(
    IKeycloakAdminService keycloakService,
    IApplicationDbContext db,
    IPermissionService permissionService) : IRequestHandler<ReactivateUserCommand, Guid>
{
    public async Task<Guid> Handle(ReactivateUserCommand request, CancellationToken cancellationToken)
    {
        if (!await permissionService.CanManageGlobalUsersAsync())
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        var existingUser = await db.Users.FindAsync([request.UserId], cancellationToken);

        if (existingUser == null)
        {
            throw new Exception("User not found.");
        }

        if (!existingUser.IsDeleted)
        {
            throw new Exception("User is already active.");
        }

        await keycloakService.UpdateUserStatusAsync(existingUser.Id, true);
        await keycloakService.ResetPasswordAsync(existingUser.Id, request.NewPassword);

        existingUser.IsDeleted = false;
        existingUser.UpdatedDt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return existingUser.Id;
    }
}
