using MediatR;
using Microsoft.EntityFrameworkCore;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Application.Common.Validations;

namespace TrafficSigns.Application.Features.Users.Queries;

public record ValidateUserField(string Field, string Value, Guid? ExcludeId = null) : IRequest<ValidateUserFieldResult>;

public record ValidateUserFieldResult(bool IsValid, string Message);

public class ValidateUserFieldHandler(
    IApplicationDbContext db,
    IPermissionService permissionService) : IRequestHandler<ValidateUserField, ValidateUserFieldResult>
{
    public async Task<ValidateUserFieldResult> Handle(ValidateUserField request, CancellationToken cancellationToken)
    {
        if (!await permissionService.CanManageGlobalUsersAsync())
        {
            throw new UnauthorizedAccessException("Access denied");
        }

        var val = request.Value.Trim();
        bool isDup = false;
        string msg = string.Empty;

        switch (request.Field.ToLower())
        {
            case "username":
                if (val.Length < 1 || val.Length > 12)
                    return new ValidateUserFieldResult(false, "Username length invalid");
                if (val.Contains(" "))
                    return new ValidateUserFieldResult(false, "Username format invalid");

                isDup = await db.Users.AnyAsync(u => u.Username == val && u.Id != request.ExcludeId, cancellationToken);
                if (isDup) msg = "This username is already taken.";
                break;

            case "email":
                if (!UserValidationRules.IsValidEmail(val))
                    return new ValidateUserFieldResult(false, "Email format invalid");

                isDup = await db.Users.AnyAsync(u => u.Email == val && u.Id != request.ExcludeId, cancellationToken);
                if (isDup) msg = "This email is already registered.";
                break;

            case "phone":
                if (!UserValidationRules.IsValidPhone(val))
                    return new ValidateUserFieldResult(false, "Phone format invalid");

                isDup = await db.Users.AnyAsync(u => u.Phone == val && u.Id != request.ExcludeId, cancellationToken);
                if (isDup) msg = "This phone number is already in use.";
                break;

            default:
                throw new ArgumentException("Invalid field");
        }

        return new ValidateUserFieldResult(!isDup, msg);
    }
}