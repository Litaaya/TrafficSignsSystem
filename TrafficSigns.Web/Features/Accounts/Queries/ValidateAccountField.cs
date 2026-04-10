using MediatR;
using Microsoft.AspNetCore.Mvc;
using TrafficSigns.Application.Features.Accounts.Queries;

namespace TrafficSigns.Web.Features.Accounts.Queries;

public static class CheckAccountDuplicationEndpoint
{
    public static void MapValidateAccountField(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/accounts/validate-field", async (
            [FromQuery] string field,
            [FromQuery] string value,
            [FromQuery] Guid? excludeId,
            IMediator mediator) =>
        {
            try
            {
                var result = await mediator.Send(new ValidateAccountField(field, value, excludeId));
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
        .WithTags("Accounts")
        .RequireAuthorization();
    }
}