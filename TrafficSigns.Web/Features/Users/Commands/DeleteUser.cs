using MediatR;
using Microsoft.EntityFrameworkCore;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Application.Common.Interfaces;

namespace TrafficSigns.Web.Features.Users.Commands;

public record DeleteUserCommand(Guid UserId) : IRequest<bool>;

public class DeleteUserHandler(
    AppDbContext db,
    IKeycloakAdminService keycloakService,
    ICurrentUserService currentUser,
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
            .FirstOrDefaultAsync(u => u.Id == request.UserId && !u.Inactive, cancellationToken);

        if (user == null)
        {
            throw new Exception("User not found or already inactivated.");
        }

        var ownerAccountIds = user.AccountUsers
            .Where(au => !au.Inactive && au.Role == "Owner")
            .Select(au => au.AccountId)
            .ToList();

        if (ownerAccountIds.Count > 0)
        {
            var accountsWithLastOwner = await db.AccountUsers
                .Where(au => ownerAccountIds.Contains(au.AccountId) && !au.Inactive && au.Role == "Owner")
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

        string actor = currentUser.GetUsername() ?? "Unknown";
        var actorId = currentUser.GetUserId();
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        user.Inactive = true;
        user.UpdatedDt = DateTime.UtcNow;
        user.AddMetadataLog("update_history", $"Deactivated by {actor}({actorId}) at {timestamp}");

        foreach (var link in user.AccountUsers.Where(au => !au.Inactive))
        {
            link.Inactive = true;
            link.UpdatedDt = DateTime.UtcNow;
            link.AddMetadataLog("update_history", $"Removed due to User deactivation by {actor}({actorId}) at {timestamp}");
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public static class DeleteUserEndpoint
{
    public static void MapDeleteUser(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/users/{userId:guid}", async (Guid userId, IMediator mediator) =>
        {
            try
            {
                var success = await mediator.Send(new DeleteUserCommand(userId));
                return success
                    ? Results.Ok(new { Message = "User deactivated successfully" })
                    : Results.NotFound();
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Message = ex.Message });
            }
        })
        .WithTags("Users")
        .RequireAuthorization();
    }
}