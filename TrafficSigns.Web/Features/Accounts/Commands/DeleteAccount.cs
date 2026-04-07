using MediatR;
using Microsoft.EntityFrameworkCore;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Application.Common.Interfaces;

namespace TrafficSigns.Web.Features.Accounts.Commands;

public record DeleteAccountCommand(Guid AccountId) : IRequest<bool>;

public class DeleteAccountHandler(
    AppDbContext db,
    ICurrentUserService currentUser,
    IPermissionService permissionService) : IRequestHandler<DeleteAccountCommand, bool>
{
    public async Task<bool> Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        if (!await permissionService.CanManageAccountAsync(request.AccountId))
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        var account = await db.Accounts
            .Include(a => a.AccountUsers)
            .FirstOrDefaultAsync(a => a.Id == request.AccountId && !a.Inactive, cancellationToken);

        if (account == null) throw new Exception("Invalid Account or Account already inactive");

        if (account.System && !permissionService.IsAdmin())
            throw new Exception("Cannot delete a system account without administrator privileges.");

        string actor = currentUser.GetUsername() ?? "Unknown";
        var actorId = currentUser.GetUserId();
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        string logEntry = $"Deactivated by {actor}({actorId}) at {timestamp}";

        account.Inactive = true;
        account.UpdatedDt = DateTime.UtcNow;
        account.AddMetadataLog("update_history", logEntry);

        var activeLinks = account.AccountUsers.Where(au => !au.Inactive).ToList();
        foreach (var link in activeLinks)
        {
            link.Inactive = true;
            link.UpdatedDt = DateTime.UtcNow;
            link.AddMetadataLog("update_history", $"Deactivated due to Account deactivation by {actor} at {timestamp}");
        }

        await db.SaveChangesAsync(cancellationToken);

        return true;
    }
}

public static class DeleteAccountEndpoint
{
    public static void MapDeleteAccount(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/accounts/{accountId:guid}", async (Guid accountId, IMediator mediator) =>
        {
            try
            {
                var command = new DeleteAccountCommand(accountId);
                var success = await mediator.Send(command);
                return success ? Results.Ok(new { Message = "Account deleted successfully" }) : Results.NotFound();
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
        .WithTags("Accounts")
        .RequireAuthorization();
    }
}