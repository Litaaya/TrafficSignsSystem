using MediatR;
using TrafficSigns.Application.Features.Commands;

namespace TrafficSigns.Web.Features.Accounts.Commands;

public static class CreateAccountEndpoint
{
    public static void MapCreateAccount(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/accounts", async (CreateAccountCommand command, IMediator mediator) =>
        {
            var accountId = await mediator.Send(command);
            return Results.Created($"/api/accounts/{accountId}", new { Id = accountId });
        })
        .WithTags("Accounts")
        .RequireAuthorization();
    }
}