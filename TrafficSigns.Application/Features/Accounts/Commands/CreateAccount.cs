using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;
using MediatR;

namespace TrafficSigns.Application.Features.Commands;

public record CreateAccountCommand(
    string Name,
    string? Desc,
    string? Email,
    string? Phone,
    bool System = false
) : IRequest<Guid>;

public class CreateAccountHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IPermissionService permissionService) : IRequestHandler<CreateAccountCommand, Guid>
{
    public async Task<Guid> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        if (!permissionService.IsAdmin())
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        string actor = currentUser.GetUsername() ?? "Unknown";
        var actorId = currentUser.GetUserId();
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Desc = request.Desc?.Trim(),
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            System = request.System,
            Inactive = false,
            CreatedDt = DateTime.UtcNow,
            UpdatedDt = DateTime.UtcNow
        };

        account.AddMetadataLog("update_history", $"Created by {actor}({actorId}) at {timestamp}");

        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);

        return account.Id;
    }
}

