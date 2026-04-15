using MediatR;
using TrafficSigns.Application.Features.Users.Queries;

namespace TrafficSigns.Web.Features.Users.Queries;

public static class GetUsersEndpoint
{
    public static void MapGetUsers(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users", async ([AsParameters] GetUsersQuery query, IMediator mediator) =>
        {
            var result = await mediator.Send(query);
            return Results.Ok(result);
        })
        .WithTags("Users")
        .RequireAuthorization();

        app.MapGet("/api/users/{id:guid}", async (Guid id, IMediator mediator) =>
        {
            var result = await mediator.Send(new GetUserByIdQuery(id));
            return result is not null ? Results.Ok(result) : Results.NotFound(new { Message = "User not found." });
        })
        .WithTags("Users")
        .RequireAuthorization();
    }
}