using FluentValidation;
using FluentValidation.Results;
using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Application.Common.Validations;
using Microsoft.EntityFrameworkCore;

namespace TrafficSigns.Application.Features.Accounts.Commands;

public class UpdateAccountCommandValidator : AbstractValidator<UpdateAccountCommand>
{
    public UpdateAccountCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Name)
            .Must(AccountValidationRules.IsValidName)
            .WithMessage($"Account name is required and cannot exceed {AccountValidationRules.NameMax} characters");

        RuleFor(x => x.Desc)
            .Must(AccountValidationRules.IsValidDescription)
            .WithMessage($"Description maximum letters is {AccountValidationRules.DescMax} characters");

        RuleFor(x => x.Email)
            .Must(AccountValidationRules.IsValidEmail)
            .WithMessage("Account email format is invalid");

        RuleFor(x => x.Phone)
            .Must(AccountValidationRules.IsValidPhone)
            .WithMessage("Account phone format is invalid");
    }
}

public record UpdateAccountCommand(
    Guid Id,
    string Name,
    string? Desc,
    string? Email,
    string? Phone,
    bool System = false
) : IRequest<bool>;

public class UpdateAccountHandler(
    IApplicationDbContext db,
    IPermissionService permissionService) : IRequestHandler<UpdateAccountCommand, bool>
{
    public async Task<bool> Handle(UpdateAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.FindAsync([request.Id], cancellationToken);
        if (account == null) 
            throw new KeyNotFoundException("Account not found");

        var isDuplicateName = await db.Accounts.AnyAsync(a =>
            a.Id != request.Id &&
            a.Name.ToLower() == request.Name.Trim().ToLower(),
            cancellationToken);

        if (isDuplicateName)
        {
            var failure = new ValidationFailure(nameof(request.Name), "Another account with this name already exists");
            throw new ValidationException(new[] { failure });
        }

        bool isUpdatingSystemField = request.System != account.System;

        if (!await permissionService.CanUpdateAccountAsync(request.Id, isUpdatingSystemField))
            throw new UnauthorizedAccessException("Access denied");

        account.Name = request.Name.Trim();
        account.Desc = request.Desc?.Trim();
        account.Email = request.Email?.Trim();
        account.Phone = request.Phone?.Trim();
        account.System = request.System;
        account.UpdatedDt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

