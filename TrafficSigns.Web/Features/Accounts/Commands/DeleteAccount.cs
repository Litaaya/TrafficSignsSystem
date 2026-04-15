using MediatR;
using TrafficSigns.Application.Features.Accounts.Commands;

namespace TrafficSigns.Web.Features.Accounts.Commands;

public static class DeleteAccountEndpoint
{
    public static void MapDeleteAccount(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/accounts/{accountId:guid}", async (Guid accountId, IMediator mediator) =>
        {
            await mediator.Send(new DeleteAccountCommand(accountId));

            return Results.Ok(new { Message = "Account deleted successfully" });
        })
        .WithTags("Accounts")
        .RequireAuthorization();
    }
}