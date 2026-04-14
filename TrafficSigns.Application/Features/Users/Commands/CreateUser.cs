using MediatR;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Application.Common.Validations;
using TrafficSigns.Domain.Models;
using Microsoft.EntityFrameworkCore;
using FluentValidation;

namespace TrafficSigns.Application.Features.Users.Commands;

public record CreateUserCommand(
    string Username,
    string Password,
    string Email,
    string Phone,
    string? FirstName = null,
    string? LastName = null
) : IRequest<Guid>;

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.Username).Must(UserValidationRules.IsValidUsername)
            .WithMessage($"Username invalid (minimum {UserValidationRules.UsernameMin} symbols, maximum {UserValidationRules.UsernameMax} symbols, no space)");

        RuleFor(x => x.Password).Must(UserValidationRules.IsStrongPassword)
            .WithMessage("Password is too weak");

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

public class CreateUserHandler(
    IKeycloakAdminService keycloakService,
    IApplicationDbContext db,
    IPermissionService permissionService) : IRequestHandler<CreateUserCommand, Guid>
{
    public async Task<Guid> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        if (!await permissionService.CanManageGlobalUsersAsync()) throw new UnauthorizedAccessException("Access denied.");

        var username = request.Username.Trim();
        var email = request.Email.Trim().ToLower();
        var phone = request.Phone.Trim();

        var existingUser = await db.Users
            .FirstOrDefaultAsync(u =>
                u.Username.ToLower() == username.ToLower() ||
                (u.Email != null && u.Email.ToLower() == email) ||
                (u.Phone != null && u.Phone == phone), cancellationToken);

        if (existingUser != null)
        {
            if (existingUser.IsDeleted)
                throw new Exception("This user already exists but has been inactivated.");

            var conflicts = new List<string>();

            if (existingUser.Username.Equals(username, StringComparison.OrdinalIgnoreCase)) conflicts.Add("Username");
            if ((existingUser.Email ?? "").Equals(email, StringComparison.OrdinalIgnoreCase)) conflicts.Add("Email");
            if (existingUser.Phone == phone) conflicts.Add("Phone Number");

            string conflictMessage = conflicts.Count > 1
                ? $"{string.Join(", ", conflicts.Take(conflicts.Count - 1))} and {conflicts.Last()}"
                : conflicts.First();

            throw new Exception($"{conflictMessage} already exists.");
        }
                
        var keycloakId = await keycloakService.CreateUserAsync
        (
            username,
            email,
            phone,
            request.Password,
            request.FirstName?.Trim() ?? "",
            request.LastName?.Trim() ?? ""            
        );

        var user = new User
        {
            Id = keycloakId,
            Username = username,
            Email = email,
            Phone = phone,
            FirstName = request.FirstName?.Trim(),
            LastName = request.LastName?.Trim(),
            IsDeleted = false,
            CreatedDt = DateTime.UtcNow,
            UpdatedDt = DateTime.UtcNow
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}