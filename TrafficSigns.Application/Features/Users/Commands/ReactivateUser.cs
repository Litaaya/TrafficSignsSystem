using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Configuration;
using TrafficSigns.Application.Common.Interfaces;

namespace TrafficSigns.Application.Features.Users.Commands;

public record ReactivateUserRequest(string NewPassword);

public record ReactivateUserCommand(Guid UserId) : IRequest<Guid>;

public class ReactivateUserHandler(
    IKeycloakAdminService keycloakService,
    IApplicationDbContext db,
    IPermissionService permissionService,
    IConfiguration configuration) : IRequestHandler<ReactivateUserCommand, Guid>
{
    public async Task<Guid> Handle(ReactivateUserCommand request, CancellationToken cancellationToken)
    {
        if (!await permissionService.CanManageGlobalUsersAsync())
        {
            throw new UnauthorizedAccessException("Access denied");
        }

        var existingUser = await db.Users.FindAsync([request.UserId], cancellationToken);

        if (existingUser == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        if (!existingUser.IsDeleted)
        {
            var failure = new ValidationFailure(nameof(request.UserId), "User is already active");
            throw new ValidationException(new[] { failure });
        }

        var defaultTempPassword = configuration["Keycloak:DefaultTempPassword"] ?? "somethinghere";

        await keycloakService.UpdateUserStatusAsync(existingUser.Id, true);

        await keycloakService.ResetPasswordAsync(existingUser.Id, defaultTempPassword, isTemporary: true);

        existingUser.IsDeleted = false;
        existingUser.UpdatedDt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return existingUser.Id;
    }
}