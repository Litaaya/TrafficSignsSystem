using MediatR;
using TrafficSigns.Application.Features.Users.Queries;

namespace TrafficSigns.Web.Features.Users.Queries;

public static class GetUsersEndpoint
{
    public static void MapGetUsers(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users", async ([AsParameters] GetUsersQuery query, IMediator mediator) =>
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
        .WithTags("Users")
        .RequireAuthorization();

        app.MapGet("/api/users/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            try
            {
                var result = await mediator.Send(new GetUserByIdQuery(id));
                return result is not null ? Results.Ok(result) : Results.NotFound(new { Message = "User not found." });
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