using MediatR;
using Microsoft.EntityFrameworkCore;
using TrafficSigns.Application.Common.Interfaces;

namespace TrafficSigns.Application.Features.Users.Commands;

public record DeleteUserCommand(Guid UserId) : IRequest<bool>;

public class DeleteUserHandler(
    IApplicationDbContext db,
    IKeycloakAdminService keycloakService,
    IPermissionService permissionService) : IRequestHandler<DeleteUserCommand, bool>
{
    public async Task<bool> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        if (!await permissionService.CanManageGlobalUsersAsync())
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        var user = await db.Users
            .Include(u => u.AccountUsers)
            .FirstOrDefaultAsync(u => u.Id == request.UserId && !u.IsDeleted, cancellationToken);

        if (user == null)
        {
            throw new Exception("User not found or already inactivated.");
        }

        var ownerAccountIds = user.AccountUsers
            .Where(au => !au.IsDeleted && au.Role == "Owner")
            .Select(au => au.AccountId)
            .ToList();

        if (ownerAccountIds.Count > 0)
        {
            var accountsWithLastOwner = await db.AccountUsers
                .Where(au => ownerAccountIds.Contains(au.AccountId) && !au.IsDeleted && au.Role == "Owner")
                .GroupBy(au => au.AccountId)
                .Select(g => new { AccountId = g.Key, OwnerCount = g.Count() })
                .Where(x => x.OwnerCount <= 1)
                .Select(x => x.AccountId)
                .ToListAsync(cancellationToken);

            if (accountsWithLastOwner.Count > 0)
            {
                var ids = string.Join(", ", accountsWithLastOwner);
                throw new Exception($"User cannot be deleted because it is the sole owner of the: {ids} accounts. Please specify a new owner first.");
            }
        }

        await keycloakService.UpdateUserStatusAsync(user.Id, false);

        user.IsDeleted = true;
        user.UpdatedDt = DateTime.UtcNow;

        foreach (var link in user.AccountUsers.Where(au => !au.IsDeleted))
        {
            link.IsDeleted = true;
            link.UpdatedDt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}