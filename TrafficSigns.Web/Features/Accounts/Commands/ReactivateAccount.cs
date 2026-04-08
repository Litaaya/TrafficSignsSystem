using MediatR;
using TrafficSigns.Application.Features.Accounts.Commands;

namespace TrafficSigns.Web.Features.Accounts.Commands;

public static class ReactivateAccountEndpoint
{
    public static void MapReactivateAccount(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/accounts/{id:guid}/reactivate", async (Guid id, IMediator mediator) =>
        {
            try
            {
                var command = new ReactivateAccountCommand(id);
                var accountId = await mediator.Send(command);

                return Results.Ok(new
                {
                    Message = "Account reactivated successfully",
                    Id = accountId
                });
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