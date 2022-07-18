using System.Threading.RateLimiting;
using DotnetRateLimiter.RateLimiting;
using StackExchange.Redis;

namespace DotnetRateLimiter.Builders
{
    public class SlidingWindowRateLimiterBuilder : WindowRateLimiterBuilder<WindowRequestSettings>
    {
        public SlidingWindowRateLimiterBuilder(IConnectionMultiplexer redis, string rateLimiterKey) : base(new WindowRequestSettings(), redis, rateLimiterKey)
        {
        }

        public override RateLimiter Build()
        {
            var redisRateLimiter = new DotnetRateLimiter.Redis.Internal.RateLimiting.SlidingWindowRateLimiter(_redis, _settings);
            
            return new DotnetRateLimiter.Redis.RateLimiting.RedisRateLimiter(redisRateLimiter);
        }
    }
}
