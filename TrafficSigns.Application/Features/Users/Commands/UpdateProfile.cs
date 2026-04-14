using MediatR;
using Microsoft.EntityFrameworkCore;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Application.Common.Validations;
using FluentValidation;

namespace TrafficSigns.Application.Features.Users.Commands;

public record UpdateProfileCommand(
    string Email,
    string Phone,
    string? FirstName = null,
    string? LastName = null
) : IRequest<bool>;

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
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

public class UpdateProfileHandler(
    IApplicationDbContext db,
    IKeycloakAdminService keycloakService,
    ICurrentUserService currentUser) : IRequestHandler<UpdateProfileCommand, bool>
{
    public async Task<bool> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.GetUserId();
        if (userId == null) throw new UnauthorizedAccessException();

        var user = await db.Users.FindAsync([userId], cancellationToken);
        if (user == null || user.IsDeleted) return false;

        var email = request.Email.Trim().ToLower();
        var phone = request.Phone.Trim();

        var duplicates = await db.Users
            .Where(u => u.Id != userId && (u.Email == email || u.Phone == phone))
            .Select(u => new { u.Email, u.Phone })
            .ToListAsync(cancellationToken);

        if (duplicates.Any())
        {
            var conflicts = new List<string>();
            if (duplicates.Any(u => u.Email == email)) conflicts.Add("Email");
            if (duplicates.Any(u => u.Phone == phone)) conflicts.Add("Phone Number");

            string conflictMessage = conflicts.Count > 1
                ? $"{string.Join(", ", conflicts.Take(conflicts.Count - 1))} and {conflicts.Last()}"
                : conflicts.First();

            throw new Exception($"{conflictMessage} already exists in the system.");
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