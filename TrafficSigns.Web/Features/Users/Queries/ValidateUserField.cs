using MediatR;
using Microsoft.AspNetCore.Mvc;
using TrafficSigns.Application.Features.Users.Queries;

namespace TrafficSigns.Web.Features.Users.Queries;

public static class CheckUserDuplicateEndpoint
{
    public static void MapValidateUserField(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/users/validate-field", async (
            [FromQuery] string field,
            [FromQuery] string value,
            [FromQuery] Guid? excludeId,
            IMediator mediator) =>
        {
            try
            {
                var result = await mediator.Send(new ValidateUserField(field, value, excludeId));
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
    }
}