using MediatR;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Application.Common.Interfaces;

namespace TrafficSigns.Web.Features.Accounts.Commands;

public record UpdateAccountCommand(
    Guid Id,
    string Name,
    string? Desc,
    string? Email,
    string? Phone,
    bool System = false
) : IRequest<bool>;

public class UpdateAccountHandler(
    AppDbContext db,
    ICurrentUserService currentUser,
    IPermissionService permissionService) : IRequestHandler<UpdateAccountCommand, bool>
{
    public async Task<bool> Handle(UpdateAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.FindAsync([request.Id], cancellationToken);
        if (account == null) return false;

        bool isUpdatingSystemField = request.System != account.System;

        if (!await permissionService.CanUpdateAccountAsync(request.Id, isUpdatingSystemField))
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        string actor = currentUser.GetUsername() ?? "Unknown";
        var actorId = currentUser.GetUserId();
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        account.Name = request.Name.Trim();
        account.Desc = request.Desc?.Trim();
        account.Email = request.Email?.Trim();
        account.Phone = request.Phone?.Trim();
        account.System = request.System;
        account.UpdatedDt = DateTime.UtcNow;

        account.AddMetadataLog("update_history", $"Updated by {actor}({actorId}) at {timestamp}");

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

public static class UpdateAccountEndpoint
{
    public static void MapUpdateAccount(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/accounts/{id:guid}", async (Guid id, UpdateAccountCommand command, IMediator mediator) =>
        {
            if (id != command.Id) return Results.BadRequest(new { Message = "Id mismatch" });

            try
            {
                var success = await mediator.Send(command);
                return success ? Results.NoContent() : Results.NotFound();
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
        .WithTags("Accounts")
        .RequireAuthorization();
    }
}