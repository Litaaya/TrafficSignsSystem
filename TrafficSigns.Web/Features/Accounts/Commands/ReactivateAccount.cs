using MediatR;
using TrafficSigns.Application.Features.Accounts.Commands;

namespace TrafficSigns.Web.Features.Accounts.Commands;

public static class ReactivateAccountEndpoint
{
    public static void MapReactivateAccount(this IEndpointRouteBuilder app)
    {
        app.MapPatch("/api/accounts/{id:guid}/reactivate", async (Guid id, IMediator mediator) =>
        {
            var accountId = await mediator.Send(new ReactivateAccountCommand(id));

            return Results.Ok(new
            {
                Message = "Account reactivated successfully",
                Id = accountId
            });
        })
        .WithTags("Accounts")
        .RequireAuthorization();
    }
}