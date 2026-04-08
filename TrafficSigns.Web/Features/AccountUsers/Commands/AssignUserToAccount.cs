using MediatR;
using TrafficSigns.Application.Features.AccountUsers.Commands;

namespace TrafficSigns.Web.Features.AccountUsers.Commands;

public static class AssignUserToAccountEndpoint
{
    public static void MapAssignUserToAccount(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/accounts/assign-user", async (AssignUserToAccountCommand command, IMediator mediator) =>
        {
            try
            {
                var accountUserId = await mediator.Send(command);
                return Results.Ok(new
                {
                    Message = "User has been assigned successfully",
                    Id = accountUserId
                });
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
        .WithTags("AccountUsers")
        .RequireAuthorization();
    }
}