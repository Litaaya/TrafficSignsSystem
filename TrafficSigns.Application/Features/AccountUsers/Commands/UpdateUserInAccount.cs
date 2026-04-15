using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using FluentValidation;
using FluentValidation.Results;

namespace TrafficSigns.Application.Features.AccountUsers.Commands;

public record UpdateUserInAccountRequest(string Role);

public record UpdateUserInAccountCommand(
    Guid AccountId,
    Guid UserId,
    string Role
) : IRequest<bool>;

public class UpdateUserInAccountHandler(
    IApplicationDbContext db,
    IPermissionService permissionService) : IRequestHandler<UpdateUserInAccountCommand, bool>
{
    private readonly string[] allowedRoles = ["Viewer", "Member", "Owner"];

    public async Task<bool> Handle(UpdateUserInAccountCommand request, CancellationToken cancellationToken)
    {
        if (!await permissionService.CanManageAccountUsersAsync(request.AccountId))
        {
            throw new UnauthorizedAccessException("Access denied");
        }

        if (!allowedRoles.Contains(request.Role))
        {
            var failure = new ValidationFailure(nameof(request.Role), $"Invalid role. Allowed roles are: {string.Join(", ", allowedRoles)}");
            throw new ValidationException(new[] { failure });
        }

        var accountUser = await db.AccountUsers
            .FirstOrDefaultAsync(au => au.AccountId == request.AccountId
                                    && au.UserId == request.UserId
                                    && !au.IsDeleted, cancellationToken);

        if (accountUser == null)
        {
            throw new KeyNotFoundException("User association not found or is inactive");
        }

        if (accountUser.Role == "Owner" && request.Role != "Owner")
        {
            var otherOwnersExist = await db.AccountUsers
                .AnyAsync(au => au.AccountId == request.AccountId
                                && au.UserId != request.UserId
                                && au.Role == "Owner"
                                && !au.IsDeleted, cancellationToken);

            if (!otherOwnersExist)
            {
                var failure = new ValidationFailure(nameof(request.Role), "Cannot change the role of the last owner in this account");
                throw new ValidationException(new[] { failure });
            }
        }

        accountUser.Role = request.Role;
        accountUser.UpdatedDt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

