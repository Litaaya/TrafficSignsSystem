using MediatR;
using TrafficSigns.Application.Features.Accounts.Commands;

namespace TrafficSigns.Web.Features.Accounts.Commands;

public static class UpdateAccountEndpoint
{
    public static void MapUpdateAccount(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/accounts/{id:guid}", async (Guid id, UpdateAccountCommand command, IMediator mediator) =>
        {
            if (id != command.Id)
                return Results.BadRequest(new { Message = "Id mismatch" });

            await mediator.Send(command);

            return Results.NoContent();
        })
        .WithTags("Accounts")
        .RequireAuthorization();
    }
}