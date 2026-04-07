using MediatR;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Application.Common.Interfaces;

namespace TrafficSigns.Web.Features.Accounts.Commands;

public record ReactivateAccountCommand(Guid AccountId) : IRequest<Guid>;

public class ReactivateAccountHandler(
    AppDbContext db,
    ICurrentUserService currentUser,
    IPermissionService permissionService) : IRequestHandler<ReactivateAccountCommand, Guid>
{
    public async Task<Guid> Handle(ReactivateAccountCommand request, CancellationToken cancellationToken)
    {
        if (!permissionService.IsAdmin())
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        var account = await db.Accounts.FindAsync([request.AccountId], cancellationToken);

        if (account == null)
            throw new Exception("Invalid Account.");

        if (!account.Inactive)
            throw new Exception("Account is already active.");

        string actor = currentUser.GetUsername() ?? "Unknown";
        var actorId = currentUser.GetUserId();
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        account.Inactive = false;
        account.UpdatedDt = DateTime.UtcNow;
        account.AddMetadataLog("update_history", $"Reactivated by {actor}({actorId}) at {timestamp}");

        await db.SaveChangesAsync(cancellationToken);

        return account.Id;
    }
}

public static class ReactivateAccountEndpoint
{
    public static void MapReactivateAccount(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/accounts/{id:guid}/reactivate", async (Guid id, IMediator mediator) =>
        {
            try
            {
                var command = new ReactivateAccountCommand(id);
                var accountId = await mediator.Send(command);

                return Results.Ok(new
                {
                    Message = "Account reactivated successfully",
                    Id = accountId
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
        .WithTags("Accounts")
        .RequireAuthorization();
    }
}