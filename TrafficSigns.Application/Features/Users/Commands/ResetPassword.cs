using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Application.Common.Validations;

namespace TrafficSigns.Application.Features.Users.Commands;

public record ResetPasswordByAdminCommand(
    Guid Id,
    string NewPassword,
    string ConfirmPassword
) : IRequest<bool>;

public class ResetPasswordByAdminValidator : AbstractValidator<ResetPasswordByAdminCommand>
{
    public ResetPasswordByAdminValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .Must(UserValidationRules.IsStrongPassword)
            .WithMessage("Password is not strong enough");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword)
            .WithMessage("Password confirmation is incorrect");
    }
}

public class ResetPasswordByAdminHandler(
    IKeycloakAdminService keycloakService,
    IPermissionService permissionService,
    IApplicationDbContext db) : IRequestHandler<ResetPasswordByAdminCommand, bool>
{
    public async Task<bool> Handle(ResetPasswordByAdminCommand request, CancellationToken cancellationToken)
    {
        if (!await permissionService.CanManageGlobalUsersAsync())
            throw new UnauthorizedAccessException("Access denied"); 

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);
        if (user == null) throw new Exception("User invalid");

        await keycloakService.ResetPasswordAsync(request.Id, request.NewPassword);

        user.UpdatedDt = DateTime.UtcNow;

        user.AddMetadataLog("security_event", $"Password reset by admin at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");

        await db.SaveChangesAsync(cancellationToken);

        return true;
    }
}