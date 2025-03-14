using System.Threading.RateLimiting;
using DotnetRateLimiter.Redis.RateLimiting.Models;
using StackExchange.Redis;

namespace DotnetRateLimiter.Redis.Builders;

public class SlidingWindowRateLimiterBuilder(IConnectionMultiplexer redis, string rateLimiterKey) : WindowRateLimiterBuilder<WindowRequestSettings>(new WindowRequestSettings(), redis, rateLimiterKey)
{
    public override RateLimiter Build()
    {
        var redisRateLimiter = new Redis.Internal.RateLimiting.SlidingWindowRateLimiter(_redis, _settings);
        
        return new Redis.RateLimiting.RedisRateLimiter(redisRateLimiter);
    }
}
