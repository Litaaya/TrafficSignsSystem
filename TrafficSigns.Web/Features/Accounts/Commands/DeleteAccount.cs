using MediatR;
using TrafficSigns.Application.Features.Accounts.Commands;

namespace TrafficSigns.Web.Features.Accounts.Commands;

public static class DeleteAccountEndpoint
{
    public static void MapDeleteAccount(this IEndpointRouteBuilder app)
    {
        app.MapDelete("/api/accounts/{accountId:guid}", async (Guid accountId, IMediator mediator) =>
        {
            try
            {
                var command = new DeleteAccountCommand(accountId);
                var success = await mediator.Send(command);
                return success ? Results.Ok(new { Message = "Account deleted successfully" }) : Results.NotFound();
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