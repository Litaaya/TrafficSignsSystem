using MediatR;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Application.Common.Interfaces;

namespace TrafficSigns.Web.Features.Users.Commands;

public record ReactivateUserRequest(string NewPassword);

public record ReactivateUserCommand(
    Guid UserId,
    string NewPassword
) : IRequest<Guid>;

public class ReactivateUserHandler(
    IKeycloakAdminService keycloakService,
    AppDbContext db,
    ICurrentUserService currentUser,
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

        if (!existingUser.Inactive)
        {
            throw new Exception("User is already active.");
        }

        string actor = currentUser.GetUsername() ?? "Unknown";
        var actorId = currentUser.GetUserId();
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        await keycloakService.UpdateUserStatusAsync(existingUser.Id, true);
        await keycloakService.ResetPasswordAsync(existingUser.Id, request.NewPassword);

        existingUser.Inactive = false;
        existingUser.UpdatedDt = DateTime.UtcNow;
        existingUser.AddMetadataLog("update_history", $"Reactivated by {actor}({actorId}) at {timestamp}");

        await db.SaveChangesAsync(cancellationToken);
        return existingUser.Id;
    }
}

public static class ReactivateUserEndpoint
{
    public static void MapReactivateUser(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/users/{id:guid}/reactivate", async (Guid id, ReactivateUserRequest body, IMediator mediator) =>
        {
            try
            {
                var command = new ReactivateUserCommand(id, body.NewPassword);
                var userId = await mediator.Send(command);

                return Results.Ok(new
                {
                    Message = "User reactivated successfully",
                    Id = userId
                });
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