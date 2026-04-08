using MediatR;
using Microsoft.EntityFrameworkCore;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;

namespace TrafficSigns.Application.Features.Users.Commands;

public record SyncUserFromKeycloakCommand(Guid UserId) : IRequest;

public class SyncUserFromKeycloakHandler(
    IApplicationDbContext db,
    IKeycloakAdminService keycloakService) : IRequestHandler<SyncUserFromKeycloakCommand>
{
    public async Task Handle(SyncUserFromKeycloakCommand request, CancellationToken cancellationToken)
    {
        var kcUser = await keycloakService.GetUserByIdAsync(request.UserId);

        var user = await db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (kcUser == null)
        {
            if (user != null)
            {
                user.Inactive = true;
                user.UpdatedDt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        var json = kcUser.Value;

        if (user == null)
        {
            user = new User
            {
                Id = request.UserId,
                CreatedDt = DateTime.UtcNow
            };
            db.Users.Add(user);
        }

        user.Username = json.TryGetProperty("username", out var un) ? un.GetString() ?? "" : user.Username;
        user.Email = json.TryGetProperty("email", out var em) ? em.GetString() : user.Email;
        user.FirstName = json.TryGetProperty("firstName", out var fn) ? fn.GetString() : user.FirstName;
        user.LastName = json.TryGetProperty("lastName", out var ln) ? ln.GetString() : user.LastName;

        if (json.TryGetProperty("enabled", out var enabledProp))
        {
            user.Inactive = !enabledProp.GetBoolean();
        }

        user.UpdatedDt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }
}