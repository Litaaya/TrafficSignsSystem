using MediatR;
using TrafficSigns.Application.Features.Users.Commands;

namespace TrafficSigns.Web.Features.Users.Commands;

public static class CreateUserEndpoint
{
    public static void MapCreateUser(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/users", async (CreateUserCommand command, IMediator mediator) =>
        {
            try
            {
                var userId = await mediator.Send(command);
                return Results.Created($"/api/users/{userId}", new { Id = userId });
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
        .WithTags("Users")
        .RequireAuthorization();
    }
}