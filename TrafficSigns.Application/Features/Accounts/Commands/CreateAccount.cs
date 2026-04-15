using FluentValidation;
using FluentValidation.Results;
using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Application.Common.Validations;
using TrafficSigns.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace TrafficSigns.Application.Features.Commands;

public record CreateAccountCommand(
    string Name,
    string? Desc,
    string? Email,
    string? Phone,
    bool System = false
) : IRequest<Guid>;

public class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    public CreateAccountCommandValidator()
    {
        RuleFor(x => x.Name)
            .Must(AccountValidationRules.IsValidName)
            .WithMessage($"Account name is required and cannot exceed {AccountValidationRules.NameMax} characters");

        RuleFor(x => x.Desc)
            .Must(AccountValidationRules.isValidDescription)
            .WithMessage($"Description maximum letters is {AccountValidationRules.DescMax}");

        RuleFor(x => x.Email)
            .Must(AccountValidationRules.IsValidEmail)
            .WithMessage("Account email format is invalid");

        RuleFor(x => x.Phone)
            .Must(AccountValidationRules.IsValidPhone)
            .WithMessage("Account phone format is invalid");
    }
}

public class CreateAccountHandler(
    IApplicationDbContext db,
    IPermissionService permissionService) : IRequestHandler<CreateAccountCommand, Guid>
{
    public async Task<Guid> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        if (!permissionService.IsAdmin())
            throw new UnauthorizedAccessException("Access denied");

        var isDuplicate = await db.Accounts
            .AnyAsync(a => a.Name.ToLower() == request.Name.Trim().ToLower(), cancellationToken);

        if (isDuplicate)
        {
            var failure = new ValidationFailure(nameof(request.Name), "Account name already exists");
            throw new ValidationException(new[] { failure });
        }

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Desc = request.Desc?.Trim(),
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            System = request.System,
            IsDeleted = false,
            CreatedDt = DateTime.UtcNow,
            UpdatedDt = DateTime.UtcNow
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);

        return account.Id;
    }
}

