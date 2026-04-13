using Microsoft.Extensions.Caching.Memory;
using TrafficSigns.Application.Common.Interfaces;

namespace TrafficSigns.Web.Middleware;

public class LogUserActivityMiddleware
{
    private readonly RequestDelegate _next;

    public LogUserActivityMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IMemoryCache cache, ICurrentUserService currentUserService, IApplicationDbContext dbContext)
    {
        var userId = currentUserService.GetUserId();

        if (userId != null && userId != Guid.Empty)
        {
            var cacheKey = $"LastActive_{userId}";

            if (!cache.TryGetValue(cacheKey, out _))
            {
                var user = await dbContext.Users.FindAsync(new object[] { userId.Value }, context.RequestAborted);

                if (user != null)
                {
                    user.LastActiveDt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(context.RequestAborted);
                    cache.Set(cacheKey, true, TimeSpan.FromMinutes(30));
                }
            }
        }

        await _next(context);
    }
}