using MediatR;
using TrafficSigns.Application.Features.Accounts.Queries;
namespace TrafficSigns.Web.Features.Accounts.Queries;

public static class GetAccountsEndpoint
{
    public static void MapGetAccounts(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/accounts", async ([AsParameters] GetAccountsQuery query, IMediator mediator) =>
        {
            var result = await mediator.Send(query);
            return Results.Ok(result);
        })
        .WithTags("Accounts")
        .RequireAuthorization();

        app.MapGet("/api/accounts/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetAccountByIdQuery(id));
            return Results.Ok(result);
        })
        .WithTags("Accounts")
        .RequireAuthorization();
    }
}