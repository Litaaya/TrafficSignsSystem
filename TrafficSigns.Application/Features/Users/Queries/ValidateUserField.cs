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

        var val = request.Value?.Trim() ?? string.Empty;
        bool isValidFormat = false;
        string msg = string.Empty;

        switch (request.Field.ToLower())
        {
            case "username":
                isValidFormat = UserValidationRules.IsValidUsername(val);
                if (!isValidFormat) msg = $"Username must be {UserValidationRules.UsernameMin}-{UserValidationRules.UsernameMax} chars and valid format.";
                break;

            case "email":
                isValidFormat = UserValidationRules.IsValidEmail(val);
                if (!isValidFormat) msg = "Invalid email format.";
                break;

            case "phone":
                isValidFormat = UserValidationRules.IsValidPhone(val);
                if (!isValidFormat) msg = "Invalid phone format.";
                break;
            case "password":
                bool isStrong = UserValidationRules.IsStrongPassword(val);
                if (!isStrong)
                {
                    return new ValidateUserFieldResult(false,
                        "Password must be at least 8 characters, include uppercase, lowercase, number and special character.");
                }
                return new ValidateUserFieldResult(true, string.Empty);

            default:
                throw new ArgumentException($"Field '{request.Field}' not supported");
        }

        if (!isValidFormat) return new ValidateUserFieldResult(false, msg);

        bool isDup = false;
        switch (request.Field.ToLower())
        {
            case "username":
                isDup = await db.Users.AnyAsync(u => u.Username.ToLower() == val.ToLower() && u.Id != request.ExcludeId, cancellationToken);
                if (isDup) msg = "This username is already taken.";
                break;
            case "email":
                isDup = await db.Users.AnyAsync(u => u.Email == val && u.Id != request.ExcludeId, cancellationToken);
                if (isDup) msg = "This email is already registered.";
                break;
            case "phone":
                isDup = await db.Users.AnyAsync(u => u.Phone == val && u.Id != request.ExcludeId, cancellationToken);
                if (isDup) msg = "This phone number is already in use.";
                break;
        }

        return new ValidateUserFieldResult(!isDup, msg);
    }
}