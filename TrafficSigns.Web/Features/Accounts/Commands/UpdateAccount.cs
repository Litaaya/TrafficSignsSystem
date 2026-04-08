using MediatR;
using TrafficSigns.Application.Features.Accounts.Commands;

namespace TrafficSigns.Web.Features.Accounts.Commands;

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