using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace TrafficSigns.Application.Features.Users.Commands;

public record CreateUserCommand(
    string Username,
    string Password,
    string Email,
    string Phone,
    string? FirstName = null,
    string? LastName = null
) : IRequest<Guid>;

public class CreateUserHandler(
    IKeycloakAdminService keycloakService,
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IPermissionService permissionService) : IRequestHandler<CreateUserCommand, Guid>
{
    public async Task<Guid> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (!await permissionService.CanManageGlobalUsersAsync())
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        var existingUser = await db.Users
            .FirstOrDefaultAsync(u =>
                u.Username == request.Username.Trim() ||
                u.Email == request.Email.Trim() ||
                u.Phone == request.Phone.Trim(), cancellationToken);

        if (existingUser != null)
        {
            if (existingUser.Inactive)
                throw new Exception("This user already exists but has been inactivated.");

            var conflicts = new List<string>();

            if (existingUser.Username == request.Username.Trim()) conflicts.Add("Username");
            if (existingUser.Email == request.Email.Trim()) conflicts.Add("Email");
            if (existingUser.Phone == request.Phone.Trim()) conflicts.Add("Phone Number");

            string conflictMessage = conflicts.Count > 1
                ? $"{string.Join(", ", conflicts.Take(conflicts.Count - 1))} and {conflicts.Last()}"
                : conflicts.First();

            throw new Exception($"{conflictMessage} already exists.");
        }

        string actor = currentUser.GetUsername() ?? "Unknown";
        var actorId = currentUser.GetUserId();
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        var keycloakId = await keycloakService.CreateUserAsync(
            request.Username.Trim(),
            request.Email.Trim(),
            request.Password,
            request.FirstName?.Trim() ?? "",
            request.LastName?.Trim() ?? "");

        var user = new User
        {
            Id = keycloakId,
            Username = request.Username.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone.Trim(),
            FirstName = request.FirstName?.Trim(),
            LastName = request.LastName?.Trim(),
            Inactive = false,
            CreatedDt = DateTime.UtcNow,
            UpdatedDt = DateTime.UtcNow
        };

        user.AddMetadataLog("update_history", $"Created by {actor}({actorId}) at {timestamp}");

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}