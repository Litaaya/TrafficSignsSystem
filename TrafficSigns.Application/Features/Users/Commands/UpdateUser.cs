using MediatR;
using Microsoft.EntityFrameworkCore;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Application.Common.Validations;
using FluentValidation;

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
    ICurrentUserService currentUser,
    IPermissionService permissionService) : IRequestHandler<UpdateUserCommand, bool>
{
    public async Task<bool> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        if (!await permissionService.CanManageGlobalUsersAsync()) throw new UnauthorizedAccessException("Access denied");

        var user = await db.Users.FindAsync([request.Id], cancellationToken);
        if (user == null || user.IsDeleted) return false;

        var email = request.Email.Trim().ToLower();
        var phone = request.Phone.Trim();

        var duplicates = await db.Users
            .Where(u => u.Id != request.Id && (u.Email == email || u.Phone == phone))
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

            throw new Exception($"{conflictMessage} already exists in the system");
        }

        await keycloakService.UpdateUserAsync(
            user.Id,
            request.Email.Trim(),
            request.FirstName?.Trim() ?? "",
            request.LastName?.Trim() ?? "");

        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        string actor = currentUser.GetUsername() ?? "Unknown system";
        var actorId = currentUser.GetUserId();

        user.Email = request.Email.Trim();
        user.Phone = request.Phone.Trim();
        user.FirstName = request.FirstName?.Trim();
        user.LastName = request.LastName?.Trim();
        user.UpdatedDt = DateTime.UtcNow;

        user.Metadata ??= new Dictionary<string, string>();
        var updatedMetadata = new Dictionary<string, string>(user.Metadata);
        updatedMetadata["update_history"] = $"Updated by {actor}({actorId}) at {timestamp}";
        user.Metadata = updatedMetadata;

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
