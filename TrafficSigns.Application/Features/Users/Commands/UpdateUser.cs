using MediatR;
using Microsoft.EntityFrameworkCore;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Application.Common.Validations;
using FluentValidation;
using FluentValidation.Results;
using System.Collections.Generic;

namespace TrafficSigns.Application.Features.Users.Commands;

public record UpdateUserCommand(
    Guid Id,
    string Email,
    string Phone,
    string? FirstName = null,
    string? LastName = null
) : IRequest<bool>;

public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.FirstName).Must(UserValidationRules.IsValidName)
            .WithMessage("Firstname can't be empty or too long");

        RuleFor(x => x.LastName).Must(UserValidationRules.IsValidName)
            .WithMessage("Lastname can't be empty or too long");

        RuleFor(x => x.Email).Must(UserValidationRules.IsValidEmail)
            .WithMessage("Email format invalid");

        RuleFor(x => x.Phone).Must(UserValidationRules.IsValidPhone)
            .WithMessage("Phone format invalid");
    }
}

public class UpdateUserHandler(
    IApplicationDbContext db,
    IKeycloakAdminService keycloakService,
    IPermissionService permissionService) : IRequestHandler<UpdateUserCommand, bool>
{
    public async Task<bool> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        if (!await permissionService.CanManageGlobalUsersAsync())
            throw new UnauthorizedAccessException("Access denied");

        var user = await db.Users.FindAsync([request.Id], cancellationToken);

        if (user == null || user.IsDeleted)
            throw new KeyNotFoundException($"User with ID {request.Id} not found or is inactive");

        var email = request.Email.Trim().ToLower();
        var phone = request.Phone.Trim();

        var duplicates = await db.Users
            .Where(u => u.Id != request.Id && (u.Email == email || u.Phone == phone))
            .Select(u => new { u.Email, u.Phone })
            .ToListAsync(cancellationToken);

        if (duplicates.Count > 0)
        {
            var failures = new List<ValidationFailure>();

            if (duplicates.Any(u => u.Email == email))
                failures.Add(new ValidationFailure(nameof(request.Email), "This email is already registered to another user"));

            if (duplicates.Any(u => u.Phone == phone))
                failures.Add(new ValidationFailure(nameof(request.Phone), "This phone number is already in use by another user"));

            throw new ValidationException(failures);
        }

        await keycloakService.UpdateUserAsync(
            user.Id,
            email,
            phone,
            request.FirstName?.Trim() ?? "",
            request.LastName?.Trim() ?? "");

        user.Email = email;
        user.Phone = phone;
        user.FirstName = request.FirstName?.Trim();
        user.LastName = request.LastName?.Trim();
        user.UpdatedDt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}