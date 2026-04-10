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
            if (user != null && !user.Inactive)
            {
                user.Inactive = true;
                user.UpdatedDt = DateTime.UtcNow;
                user.AddMetadataLog("sync_event", "User deactivated: Not found in Keycloak");
                await db.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        var json = kcUser.Value;
        bool isNew = false;

        if (user == null)
        {
            user = new User
            {
                Id = request.UserId,
                CreatedDt = DateTime.UtcNow
            };
            db.Users.Add(user);
            isNew = true;
        }

        bool hasChanged = isNew;

        string? kcUsername = json.TryGetProperty("username", out var un) ? un.GetString() : null;
        if (user.Username != kcUsername && kcUsername != null)
        {
            user.Username = kcUsername;
            hasChanged = true;
        }

        string? kcEmail = json.TryGetProperty("email", out var em) ? em.GetString() : null;
        if (user.Email != kcEmail)
        {
            user.Email = kcEmail;
            hasChanged = true;
        }

        string? kcFirstName = json.TryGetProperty("firstName", out var fn) ? fn.GetString() : null;
        if (user.FirstName != kcFirstName)
        {
            user.FirstName = kcFirstName;
            hasChanged = true;
        }

        string? kcLastName = json.TryGetProperty("lastName", out var ln) ? ln.GetString() : null;
        if (user.LastName != kcLastName)
        {
            user.LastName = kcLastName;
            hasChanged = true;
        }

        if (json.TryGetProperty("enabled", out var enabledProp))
        {
            bool shouldBeInactive = !enabledProp.GetBoolean();
            if (user.Inactive != shouldBeInactive)
            {
                user.Inactive = shouldBeInactive;
                hasChanged = true;
            }
        }

        if (hasChanged)
        {
            user.UpdatedDt = DateTime.UtcNow;
            user.AddMetadataLog("sync_source", "Keycloak_Background_Poll");
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}