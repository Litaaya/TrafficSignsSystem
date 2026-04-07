using MediatR;
using Microsoft.EntityFrameworkCore;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Domain.Models;
using TrafficSigns.Application.Common.Interfaces;

namespace TrafficSigns.Web.Features.AccountUsers.Commands;

public record AssignUserToAccountCommand(
    Guid AccountId,
    Guid UserId,
    string Role = "Viewer"
) : IRequest<Guid>;

public class AssignUserToAccountHandler(
    AppDbContext db,
    ICurrentUserService currentUser,
    IPermissionService permissionService) : IRequestHandler<AssignUserToAccountCommand, Guid>
{
    private readonly string[] _allowedRoles = ["Viewer", "Member", "Owner"];

    public async Task<Guid> Handle(AssignUserToAccountCommand request, CancellationToken cancellationToken)
    {
        if (!await permissionService.CanManageAccountUsersAsync(request.AccountId))
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        if (!_allowedRoles.Contains(request.Role))
        {
            throw new Exception($"Invalid role. Allowed roles are: {string.Join(", ", _allowedRoles)}");
        }

        if (!await db.Users.AnyAsync(u => u.Id == request.UserId, cancellationToken))
        {
            throw new Exception("User not found.");
        }

        if (!await db.Accounts.AnyAsync(a => a.Id == request.AccountId, cancellationToken))
        {
            throw new Exception("Account not found.");
        }

        string actor = currentUser.GetUsername() ?? "Unknown";
        var actorId = currentUser.GetUserId();
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        var hasActiveOwner = await db.AccountUsers.AnyAsync(au =>
            au.AccountId == request.AccountId &&
            au.Role == "Owner" &&
            !au.Inactive, cancellationToken);
        var effectiveRole = !hasActiveOwner ? "Owner" : request.Role;

        var existingLink = await db.AccountUsers
            .FirstOrDefaultAsync(au => au.AccountId == request.AccountId
                                    && au.UserId == request.UserId, cancellationToken);

        if (existingLink != null)
        {
            if (!existingLink.Inactive)
            {
                throw new Exception("User is already assigned to this account and is currently active.");
            }

            existingLink.Inactive = false;
            existingLink.Role = effectiveRole;
            existingLink.UpdatedDt = DateTime.UtcNow;
            existingLink.AddMetadataLog("update_history", $"Re-activated with role {effectiveRole} by {actor}({actorId}) at {timestamp}");

            await db.SaveChangesAsync(cancellationToken);
            return existingLink.Id;
        }

        var accountUser = new AccountUser
        {
            Id = Guid.NewGuid(),
            AccountId = request.AccountId,
            UserId = request.UserId,
            Role = effectiveRole,
            Inactive = false,
            CreatedDt = DateTime.UtcNow,
            UpdatedDt = DateTime.UtcNow
        };

        accountUser.AddMetadataLog("update_history", $"Assigned as {request.Role} by {actor}({actorId}) at {timestamp}");

        db.AccountUsers.Add(accountUser);
        await db.SaveChangesAsync(cancellationToken);

        return accountUser.Id;
    }
}

public static class AssignUserToAccountEndpoint
{
    public static void MapAssignUserToAccount(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/accounts/assign-user", async (AssignUserToAccountCommand command, IMediator mediator) =>
        {
            try
            {
                var accountUserId = await mediator.Send(command);
                return Results.Ok(new
                {
                    Message = "User has been assigned successfully",
                    Id = accountUserId
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
        .WithTags("AccountUsers")
        .RequireAuthorization();
    }
}