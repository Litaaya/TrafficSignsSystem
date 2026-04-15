using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Application.Common.Validations;

namespace TrafficSigns.Application.Features.Users.Commands;

public record ChangePasswordCommand(
    string OldPassword,
    string NewPassword,
    string ConfirmPassword
) : IRequest<bool>;

public class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(x => x.OldPassword).NotEmpty().WithMessage("Can't empty");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .Must(UserValidationRules.IsStrongPassword)
            .WithMessage("The new password isn't strong enough");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword)
            .WithMessage("New password confirmation isn't match");
    }
}

public class ChangePasswordHandler(
    IKeycloakAdminService keycloakService,
    ICurrentUserService currentUser,
    IPermissionService permissionService,
    IApplicationDbContext db) : IRequestHandler<ChangePasswordCommand, bool>
{
    public async Task<bool> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.GetUserId();
        var username = currentUser.GetUsername();

        if (userId == null || string.IsNullOrEmpty(username))
            throw new UnauthorizedAccessException("Invalid login session");

        if (!await permissionService.CanChangePasswordAsync(userId.Value))
            throw new UnauthorizedAccessException("Access denied");

        var isOldPasswordValid = await keycloakService.VerifyUserPasswordAsync(username, request.OldPassword);
        if (!isOldPasswordValid)
        {
            var failure = new ValidationFailure(nameof(request.OldPassword), "Current password is incorrect");
            throw new ValidationException(new[] { failure });
        }

        await keycloakService.ResetPasswordAsync(userId.Value, request.NewPassword);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
            throw new KeyNotFoundException("User profile not found");

        user.UpdatedDt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }
}