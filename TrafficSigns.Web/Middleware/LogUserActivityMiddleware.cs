using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using TrafficSigns.Application.Common.Interfaces;

namespace TrafficSigns.Web.Middleware;

public class LogUserActivityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IMemoryCache cache, IServiceProvider serviceProvider)
    {
        var currentUserService = context.RequestServices.GetRequiredService<ICurrentUserService>();
        var userId = currentUserService.GetUserId();

        if (userId != null && userId != Guid.Empty)
        {
            var cacheKey = $"LastActive_{userId}";

            if (!cache.TryGetValue(cacheKey, out _))
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

                await dbContext.Users
                    .Where(u => u.Id == userId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(u => u.LastActiveDt, DateTime.UtcNow),
                        context.RequestAborted);

                cache.Set(cacheKey, true, TimeSpan.FromMinutes(30));
            }
        }

        await next(context);
    }
}