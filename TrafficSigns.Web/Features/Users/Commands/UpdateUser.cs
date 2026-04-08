using MediatR;
using TrafficSigns.Application.Features.Users.Commands;

namespace TrafficSigns.Web.Features.Users.Commands;

public static class UpdateUserEndpoint
{
    public static void MapUpdateUser(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/users/{id:guid}", async (Guid id, UpdateUserCommand command, IMediator mediator) =>
        {
            if (id != command.Id) return Results.BadRequest(new { Message = "Id mismatch" });

            try
            {
                var success = await mediator.Send(command);
                return success ? Results.NoContent() : Results.NotFound();
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Message = ex.Message });
            }
        })
        .WithTags("Users")
        .RequireAuthorization(policy => policy.RequireRole("admin"));
    }
}