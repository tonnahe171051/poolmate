using Microsoft.Extensions.Caching.Distributed;
using System.Security.Claims;

namespace PoolMate.Api.Middleware;


public class BannedUserMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BannedUserMiddleware> _logger;
    
    public const string BannedUserCacheKeyPrefix = "banned:";

    public BannedUserMiddleware(RequestDelegate next, ILogger<BannedUserMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IDistributedCache cache)
    {
        // ═══════════════════════════════════════════════════════════════════
        // STEP 1: SKIP ANONYMOUS REQUESTS
        // Only check authenticated users to avoid unnecessary cache lookups
        // ═══════════════════════════════════════════════════════════════════
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // ═══════════════════════════════════════════════════════════════════
        // STEP 2: EXTRACT USER ID FROM CLAIMS
        // Support both NameIdentifier and "sub" claim (JWT standard)
        // ═══════════════════════════════════════════════════════════════════
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) 
                     ?? context.User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
        {
            // No user ID in token - proceed normally (edge case)
            await _next(context);
            return;
        }

        // ═══════════════════════════════════════════════════════════════════
        // STEP 3: CHECK BANNED STATUS IN CACHE (FAST PATH)
        // 
        // Performance Notes:
        // - GetStringAsync is non-blocking
        // - Redis: O(1) complexity, ~0.1ms latency
        // - MemoryCache: Even faster, ~0.01ms
        // - We only check existence, not value (any non-null = banned)
        // ═══════════════════════════════════════════════════════════════════
        var cacheKey = $"{BannedUserCacheKeyPrefix}{userId}";
        
        try
        {
            var bannedFlag = await cache.GetStringAsync(cacheKey);
            
            if (!string.IsNullOrEmpty(bannedFlag))
            {
                // ═══════════════════════════════════════════════════════════
                // USER IS BANNED - REJECT IMMEDIATELY
                // ═══════════════════════════════════════════════════════════
                _logger.LogWarning(
                    "Blocked request from banned user {UserId}. Path: {Path}, Method: {Method}",
                    userId, context.Request.Path, context.Request.Method);

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                
                await context.Response.WriteAsJsonAsync(new
                {
                    success = false,
                    message = "Your account has been deactivated. Please contact support.",
                    errorCode = "ACCOUNT_DEACTIVATED",
                    timestamp = DateTimeOffset.UtcNow
                });
                
                return; // Short-circuit pipeline
            }
        }
        catch (Exception ex)
        {
            // ═══════════════════════════════════════════════════════════════
            // CACHE FAILURE - FAIL OPEN (Security vs Availability trade-off)
            // 
            // Decision: Allow request to proceed if cache is unavailable
            // Reason: Database lockout check will still protect the system
            // Alternative: "Fail closed" - return 503 if cache is down
            // ═══════════════════════════════════════════════════════════════
            _logger.LogError(ex, 
                "Failed to check banned status for user {UserId}. Allowing request to proceed.", 
                userId);
        }

        // ═══════════════════════════════════════════════════════════════════
        // STEP 4: USER IS NOT BANNED - CONTINUE PIPELINE
        // ═══════════════════════════════════════════════════════════════════
        await _next(context);
    }
}


public static class BannedUserMiddlewareExtensions
{
    public static IApplicationBuilder UseBannedUserCheck(this IApplicationBuilder app)
    {
        return app.UseMiddleware<BannedUserMiddleware>();
    }
}

