using System;
using System.Threading.RateLimiting;
using DotnetRateLimiter.RateLimiting;
using StackExchange.Redis;

namespace DotnetRateLimiter.Builders
{
    public class TokenBucketRateLimiterBuilder : RateLimiterBuilder<TokenBucketRequestSettings>
    {
        public TokenBucketRateLimiterBuilder(IConnectionMultiplexer redis, string rateLimiterKey) : base(new TokenBucketRequestSettings(), redis, rateLimiterKey)
        {
        }

        public TokenBucketRateLimiterBuilder WithCapacity(long capacity)
        {
            _settings.Capacity = capacity;

            return this;
        }

        public TokenBucketRateLimiterBuilder WithRefillRate(long refillRate)
        {
            _settings.RefillRate = refillRate;

            return this;
        }

        public TokenBucketRateLimiterBuilder WithEmptyBucketOnStart(bool isEmptyOnStart)
        {
            _settings.IsEmptyOnStart = isEmptyOnStart;

            return this;
        }

        public TokenBucketRateLimiterBuilder WithInterval(Func<TimeSpan> getInterval)
        {
            if(getInterval == null)
            {
                throw new ArgumentNullException(nameof(getInterval));
            }

            _settings.GetInterval = getInterval;

            return this;
        }

        public TokenBucketRateLimiterBuilder WithGetNowUtc(Func<DateTime> getNowUtc)
        {
            if(getNowUtc == null)
            {
                throw new ArgumentNullException(nameof(getNowUtc));
            }

            _settings.GetNowUtc = getNowUtc;

            return this;
        }

        public override RateLimiter Build()
        {
            var redisRateLimiter = new DotnetRateLimiter.Redis.Internal.RateLimiting.TokenBucketRateLimiter(_redis, _settings);
            
            return new DotnetRateLimiter.Redis.RateLimiting.RedisRateLimiter(redisRateLimiter);
        }
    }
}
