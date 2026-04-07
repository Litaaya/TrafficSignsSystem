using MediatR;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace TrafficSigns.Web.Features.Users.Commands;

public record UpdateUserCommand(
    Guid Id,
    string Email,
    string Phone,
    string? FirstName = null,
    string? LastName = null
    ) : IRequest<bool>;

public class UpdateUserHandler(
    AppDbContext db,
    IKeycloakAdminService keycloakService,
    ICurrentUserService currentUser) : IRequestHandler<UpdateUserCommand, bool>
{
    public async Task<bool> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([request.Id], cancellationToken);
        if (user == null || user.Inactive) return false;

        var duplicate = await db.Users
            .FirstOrDefaultAsync(u => u.Id != request.Id &&
                (u.Email == request.Email.Trim() || u.Phone == request.Phone.Trim()),
                cancellationToken);

        if (duplicate != null)
        {
            var conflicts = new List<string>();
            if (duplicate.Email == request.Email.Trim()) conflicts.Add("Email");
            if (duplicate.Phone == request.Phone.Trim()) conflicts.Add("Phone Number");

            string conflictMessage = conflicts.Count > 1
                ? $"{conflicts[0]} and {conflicts[1]}"
                : conflicts[0];

            throw new Exception($"{conflictMessage} is already taken by another user.");
        }

        await keycloakService.UpdateUserAsync(
            user.Id,
            request.Email.Trim(),
            request.FirstName?.Trim() ?? "",
            request.LastName?.Trim() ?? "");

        var isDuplicate = await db.Users.AnyAsync(u =>
            (u.Email == request.Email.Trim() || u.Phone == request.Phone.Trim())
            && u.Id != request.Id, cancellationToken);

        if (isDuplicate)
            throw new Exception("Invalid Email/Phone Number");

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

public static class UpdateUserEndpoint
{
    public static void MapUpdateUser(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/users/{id:guid}", async (Guid id, UpdateUserCommand command, IMediator mediator) =>
        {
            if (id != command.Id) return Results.BadRequest(new { Message = "Id mismatch" });

            try
            {
                var success = await mediator.Send(command);
                return success ? Results.NoContent() : Results.NotFound();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Message = ex.Message });
            }
        })
        .WithTags("Users")
        .RequireAuthorization(policy => policy.RequireRole("admin"));
    }
}