using MediatR;
using TrafficSigns.Infrastructure.Persistence;
using TrafficSigns.Domain.Models;
using TrafficSigns.Application.Common.Interfaces;
namespace TrafficSigns.Web.Features.Accounts.Commands;

public record CreateAccountCommand(
    string Name,
    string? Desc,
    string? Email,
    string? Phone,
    bool System = false
) : IRequest<Guid>;

public class CreateAccountHandler(
    AppDbContext db,
    ICurrentUserService currentUser,
    IPermissionService permissionService) : IRequestHandler<CreateAccountCommand, Guid>
{
    public async Task<Guid> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        if (!permissionService.IsAdmin())
        {
            throw new UnauthorizedAccessException("Access denied.");
        }

        string actor = currentUser.GetUsername() ?? "Unknown";
        var actorId = currentUser.GetUserId();
        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Desc = request.Desc?.Trim(),
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            System = request.System,
            Inactive = false,
            CreatedDt = DateTime.UtcNow,
            UpdatedDt = DateTime.UtcNow
        };

        account.AddMetadataLog("update_history", $"Created by {actor}({actorId}) at {timestamp}");

        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);

        return account.Id;
    }
}

public static class CreateAccountEndpoint
{
    public static void MapCreateAccount(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/accounts", async (CreateAccountCommand command, IMediator mediator) =>
        {
            try
            {
                var accountId = await mediator.Send(command);
                return Results.Created($"/api/accounts/{accountId}", new { Id = accountId });
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