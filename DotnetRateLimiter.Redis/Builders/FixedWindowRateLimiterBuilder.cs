using System.Threading.RateLimiting;
using DotnetRateLimiter.RateLimiting;
using StackExchange.Redis;

namespace DotnetRateLimiter.Builders
{
    public class FixedWindowRateLimiterBuilder : WindowRateLimiterBuilder<WindowRequestSettings>
    {
        public FixedWindowRateLimiterBuilder(IConnectionMultiplexer redis, string rateLimiterKey) : base(new WindowRequestSettings(), redis, rateLimiterKey)
        {
        }

        public override RateLimiter Build()
        {
            var redisRateLimiter = new DotnetRateLimiter.Redis.Internal.RateLimiting.FixedWindowRateLimiter(_redis, _settings);
            
            return new DotnetRateLimiter.Redis.RateLimiting.RedisRateLimiter(redisRateLimiter);
        }
    }
}
