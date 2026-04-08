using MediatR;
using TrafficSigns.Application.Features.Accounts.Queries;
namespace TrafficSigns.Web.Features.Accounts.Queries;

public static class GetAccountsEndpoint
{
    public static void MapGetAccounts(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/accounts", async ([AsParameters] GetAccountsQuery query, IMediator mediator) =>
        {
            try
            {
                var result = await mediator.Send(query);
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
        .WithTags("Accounts")
        .RequireAuthorization();

        app.MapGet("/api/accounts/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            try
            {
                var result = await mediator.Send(new GetAccountByIdQuery(id));
                return result is not null ? Results.Ok(result) : Results.NotFound(new { Message = "Account not found." });
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