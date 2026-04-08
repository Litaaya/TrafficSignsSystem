using MediatR;
using TrafficSigns.Application.Common.Interfaces;

namespace TrafficSigns.Application.Features.Accounts.Commands;

public record UpdateAccountCommand(
    Guid Id,
    string Name,
    string? Desc,
    string? Email,
    string? Phone,
    bool System = false
) : IRequest<bool>;

public class UpdateAccountHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IPermissionService permissionService) : IRequestHandler<UpdateAccountCommand, bool>
{
    public async Task<bool> Handle(UpdateAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.FindAsync([request.Id], cancellationToken);
        if (account == null) return false;

        bool isUpdatingSystemField = request.System != account.System;

        if (!await permissionService.CanUpdateAccountAsync(request.Id, isUpdatingSystemField))
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        string actor = currentUser.GetUsername() ?? "Unknown";
        var actorId = currentUser.GetUserId();
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        account.Name = request.Name.Trim();
        account.Desc = request.Desc?.Trim();
        account.Email = request.Email?.Trim();
        account.Phone = request.Phone?.Trim();
        account.System = request.System;
        account.UpdatedDt = DateTime.UtcNow;

        account.AddMetadataLog("update_history", $"Updated by {actor}({actorId}) at {timestamp}");

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

