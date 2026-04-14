using MediatR;
using Microsoft.EntityFrameworkCore;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;

namespace TrafficSigns.Application.Features.Users.Commands;

public record SyncUserFromKeycloakCommand(Guid UserId, string? ActorId = null) : IRequest;

public class SyncUserFromKeycloakHandler(
    IApplicationDbContext db,
    IKeycloakAdminService keycloakService) : IRequestHandler<SyncUserFromKeycloakCommand>
{
    public async Task Handle(SyncUserFromKeycloakCommand request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(request.ActorId) && Guid.TryParse(request.ActorId, out var actorId))
        {
            var adminUser = await db.Users.IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == actorId, cancellationToken);
            if (adminUser != null) adminUser.LastActiveDt = DateTime.UtcNow;
        }

        var kcUser = await keycloakService.GetUserByIdAsync(request.UserId);
        var user = await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (kcUser == null)
        {
            if (user != null && !user.IsDeleted)
            {
                user.IsDeleted = true;
                user.UpdatedDt = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        var json = kcUser.Value;
        bool isNew = (user == null);

        if (isNew)
        {
            user = new User { Id = request.UserId, CreatedDt = DateTime.UtcNow };
            db.Users.Add(user);
        }

        user!.Username = json.TryGetProperty("username", out var un) ? (un.GetString() ?? string.Empty) : user.Username;
        user.Email = json.TryGetProperty("email", out var em) ? em.GetString() : user.Email;
        user.FirstName = json.TryGetProperty("firstName", out var fn) ? fn.GetString() : user.FirstName;
        user.LastName = json.TryGetProperty("lastName", out var ln) ? ln.GetString() : user.LastName;

        if (json.TryGetProperty("enabled", out var en))
            user.IsDeleted = !en.GetBoolean();

        if (json.TryGetProperty("attributes", out var attrs) &&
            attrs.TryGetProperty("phone", out var ph) && ph.GetArrayLength() > 0)
        {
            user.Phone = ph[0].GetString();
        }

        user.UpdatedDt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}