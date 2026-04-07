using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrafficSigns.Application.Common.Interfaces;
using TrafficSigns.Infrastructure.Persistence;

namespace TrafficSigns.Web.Features.Users.Queries;

public record CheckUserDuplicateQuery(string Field, string Value, Guid? ExcludeId = null) : IRequest<CheckUserDuplicateResult>;

public record CheckUserDuplicateResult(bool IsDuplicate, string Message);

public class CheckUserDuplicateHandler(
    AppDbContext db,
    IPermissionService permissionService) : IRequestHandler<CheckUserDuplicateQuery, CheckUserDuplicateResult>
{
    public async Task<CheckUserDuplicateResult> Handle(CheckUserDuplicateQuery request, CancellationToken cancellationToken)
    {
        if (!await permissionService.CanManageGlobalUsersAsync())
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        var val = request.Value.Trim();
        bool isDup = false;
        string msg = string.Empty;

        switch (request.Field.ToLower())
        {
            case "username":
                isDup = await db.Users.AnyAsync(u => u.Username == val && u.Id != request.ExcludeId, cancellationToken);
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
            default:
                throw new ArgumentException("Invalid field specified for duplicate check.");
        }

        return new CheckUserDuplicateResult(isDup, msg);
    }
}

public static class CheckUserDuplicateEndpoint
{
    public static void MapCheckUserDuplicate(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users/check-duplicate", async (
            [FromQuery] string field,
            [FromQuery] string value,
            [FromQuery] Guid? excludeId,
            IMediator mediator) =>
        {
            try
            {
                var result = await mediator.Send(new CheckUserDuplicateQuery(field, value, excludeId));
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Message = ex.Message });
            }
        })
        .WithTags("Users")
        .RequireAuthorization();
    }
}