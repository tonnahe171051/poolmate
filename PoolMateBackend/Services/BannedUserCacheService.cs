using Microsoft.Extensions.Caching.Distributed;
using PoolMate.Api.Middleware;

namespace PoolMate.Api.Services;

public interface IBannedUserCacheService
{
    Task<bool> BanUserAsync(string userId, string? reason = null, CancellationToken ct = default);
    Task<bool> UnbanUserAsync(string userId, CancellationToken ct = default);
    Task<bool> IsUserBannedAsync(string userId, CancellationToken ct = default);
}


public class BannedUserCacheService : IBannedUserCacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<BannedUserCacheService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _defaultCacheDuration;

    public BannedUserCacheService(
        IDistributedCache cache,
        ILogger<BannedUserCacheService> logger,
        IConfiguration configuration)
    {
        _cache = cache;
        _logger = logger;
        _configuration = configuration;

        // Read token lifetime from config, default to 60 minutes
        var tokenLifetimeMinutes = _configuration.GetValue<int>("Jwt:AccessTokenExpirationMinutes", 60);
        _defaultCacheDuration = TimeSpan.FromMinutes(tokenLifetimeMinutes);
        
        _logger.LogInformation(
            "BannedUserCacheService initialized with cache duration: {Duration} minutes",
            _defaultCacheDuration.TotalMinutes);
    }

    public async Task<bool> BanUserAsync(string userId, string? reason = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Attempted to ban user with null/empty userId");
            return false;
        }

        var cacheKey = $"{BannedUserMiddleware.BannedUserCacheKeyPrefix}{userId}";
        
        try
        {
            // Store ban metadata for debugging/auditing
            var banData = new BanCacheEntry
            {
                UserId = userId,
                Reason = reason ?? "Manual deactivation by admin",
                BannedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.Add(_defaultCacheDuration)
            };

            var options = new DistributedCacheEntryOptions
            {
                // Absolute expiration ensures cleanup even if server restarts
                AbsoluteExpirationRelativeToNow = _defaultCacheDuration
            };

            await _cache.SetStringAsync(
                cacheKey,
                System.Text.Json.JsonSerializer.Serialize(banData),
                options,
                ct);

            _logger.LogInformation(
                "User {UserId} added to banned cache. Expires at: {ExpiresAt}",
                userId, banData.ExpiresAt);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add user {UserId} to banned cache", userId);
            return false;
        }
    }

    public async Task<bool> UnbanUserAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Attempted to unban user with null/empty userId");
            return false;
        }

        var cacheKey = $"{BannedUserMiddleware.BannedUserCacheKeyPrefix}{userId}";

        try
        {
            await _cache.RemoveAsync(cacheKey, ct);
            
            _logger.LogInformation("User {UserId} removed from banned cache", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove user {UserId} from banned cache", userId);
            return false;
        }
    }

    public async Task<bool> IsUserBannedAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(userId))
            return false;

        var cacheKey = $"{BannedUserMiddleware.BannedUserCacheKeyPrefix}{userId}";

        try
        {
            var value = await _cache.GetStringAsync(cacheKey, ct);
            return !string.IsNullOrEmpty(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check banned status for user {UserId}", userId);
            return false; // Fail open - let database check handle it
        }
    }

    /// <summary>
    /// Internal class for serializing ban metadata to cache.
    /// </summary>
    private class BanCacheEntry
    {
        public string UserId { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTimeOffset BannedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
