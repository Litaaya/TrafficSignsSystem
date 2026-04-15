using MediatR;
using Microsoft.EntityFrameworkCore;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Application.Common.Validations;

namespace TrafficSigns.Application.Features.Accounts.Queries;

public record ValidateAccountField(string Field, string Value, Guid? ExcludeId = null) : IRequest<ValidateAccountFieldResult>;

public record ValidateAccountFieldResult(bool IsValid, string Message);

public class ValidateAccountFieldHandler(
    IApplicationDbContext db,
    IPermissionService permissionService) : IRequestHandler<ValidateAccountField, ValidateAccountFieldResult>
{
    public async Task<ValidateAccountFieldResult> Handle(ValidateAccountField request, CancellationToken cancellationToken)
    {
        if (request.ExcludeId.HasValue)
        {
            if (!await permissionService.CanManageAccountAsync(request.ExcludeId.Value))
                throw new UnauthorizedAccessException("Access denied");
        }
        else if (!permissionService.IsAdmin())
        {
            throw new UnauthorizedAccessException("Access denied");
        }

        var val = request.Value?.Trim() ?? string.Empty;
        bool isDup = false;
        string? msg = null;

        switch (request.Field.ToLower())
        {
            case "name":
                if (val.Length < 1 || val.Length > 100)
                    return new ValidateAccountFieldResult(false, "Name length invalid");

                isDup = await db.Accounts.AnyAsync(u => u.Name.ToLower() == val.ToLower() && u.Id != request.ExcludeId, cancellationToken);
                if (isDup) msg = "Account name already exists";
                break;

            case "email":
                if (!string.IsNullOrEmpty(val) && !AccountValidationRules.IsValidEmail(val))
                    return new ValidateAccountFieldResult(false, "Email format invalid");

                isDup = await db.Accounts.AnyAsync(u => u.Email == val && u.Id != request.ExcludeId, cancellationToken);
                if (isDup) msg = "This email is already registered";
                break;

            case "phone":
                if (!string.IsNullOrEmpty(val) && !AccountValidationRules.IsValidPhone(val))
                    return new ValidateAccountFieldResult(false, "Phone format invalid");

                isDup = await db.Accounts.AnyAsync(u => u.Phone == val && u.Id != request.ExcludeId, cancellationToken);
                if (isDup) msg = "This phone is already registered";
                break;

            case "desc":
                if (val.Length > 500)
                    return new ValidateAccountFieldResult(false, "Description maximum letters is 500");
                break;

            default:
                throw new ArgumentException($"Field '{request.Field}' is not supported for validation.");
        }

        return new ValidateAccountFieldResult(!isDup, msg ?? string.Empty);
    }
}