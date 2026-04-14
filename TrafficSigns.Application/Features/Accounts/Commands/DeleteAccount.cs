using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace TrafficSigns.Application.Features.Accounts.Commands;

public record DeleteAccountCommand(Guid AccountId) : IRequest<bool>;

public class DeleteAccountHandler(
    IApplicationDbContext db,
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
            .FirstOrDefaultAsync(a => a.Id == request.AccountId && !a.IsDeleted, cancellationToken);

        if (account == null) throw new Exception("Invalid Account or Account already inactive");

        if (account.System && !permissionService.IsAdmin())
            throw new Exception("Cannot delete a system account without administrator privileges.");

        account.IsDeleted = true;
        account.UpdatedDt = DateTime.UtcNow;

        var activeLinks = account.AccountUsers.Where(au => !au.IsDeleted).ToList();
        foreach (var link in activeLinks)
        {
            link.IsDeleted = true;
            link.UpdatedDt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);

        return true;
    }
}

