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
        bool isFormatValid = true;
        string? formatMsg = null;

        switch (request.Field.ToLower())
        {
            case "name":
                isFormatValid = AccountValidationRules.IsValidName(val);
                if (!isFormatValid) formatMsg = $"Account name must be {AccountValidationRules.NameMin}-{AccountValidationRules.NameMax} characters.";
                break;
            case "email":
                isFormatValid = AccountValidationRules.IsValidEmail(val);
                if (!isFormatValid) formatMsg = "Email format invalid.";
                break;
            case "phone":
                isFormatValid = AccountValidationRules.IsValidPhone(val);
                if (!isFormatValid) formatMsg = "Phone format invalid.";
                break;
            case "desc":
                isFormatValid = AccountValidationRules.IsValidDescription(val);
                if (!isFormatValid) formatMsg = $"Description maximum is {AccountValidationRules.DescMax} characters.";
                break;
        }

        if (!isFormatValid) return new ValidateAccountFieldResult(false, formatMsg!);

        bool isDup = false;
        string msg = string.Empty;

        switch (request.Field.ToLower())
        {
            case "name":
                isDup = await db.Accounts.AnyAsync(a => a.Name.ToLower() == val.ToLower() && a.Id != request.ExcludeId, cancellationToken);
                if (isDup) msg = "Account name already exists.";
                break;
            case "email":
                if (!string.IsNullOrEmpty(val))
                {
                    isDup = await db.Accounts.AnyAsync(a => a.Email == val && a.Id != request.ExcludeId, cancellationToken);
                    if (isDup) msg = "This email is already registered.";
                }
                break;
            case "phone":
                if (!string.IsNullOrEmpty(val))
                {
                    isDup = await db.Accounts.AnyAsync(a => a.Phone == val && a.Id != request.ExcludeId, cancellationToken);
                    if (isDup) msg = "This phone is already registered.";
                }
                break;
        }

        return new ValidateAccountFieldResult(!isDup, msg);
    }
}