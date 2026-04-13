using MediatR;
using TrafficSigns.Application.Common.Interfaces;

namespace TrafficSigns.Application.Features.Accounts.Commands;

public record ReactivateAccountCommand(Guid AccountId) : IRequest<Guid>;

public class ReactivateAccountHandler(
    IApplicationDbContext db,
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

        if (!account.IsDeleted)
            throw new Exception("Account is already active.");

        string actor = currentUser.GetUsername() ?? "Unknown";
        var actorId = currentUser.GetUserId();
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        account.IsDeleted = false;
        account.UpdatedDt = DateTime.UtcNow;
        account.AddMetadataLog("update_history", $"Reactivated by {actor}({actorId}) at {timestamp}");

        await db.SaveChangesAsync(cancellationToken);

        return account.Id;
    }
}

