using DotnetRateLimiter.Redis.RateLimiting.Models;
using StackExchange.Redis;
using System;
using System.Threading.RateLimiting;

namespace DotnetRateLimiter.Redis.Builders;

public class TokenBucketRateLimiterBuilder(IConnectionMultiplexer redis, string rateLimiterKey) : RateLimiterBuilder<TokenBucketRequestSettings>(new TokenBucketRequestSettings(), redis, rateLimiterKey)
{
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
        ArgumentNullException.ThrowIfNull(getInterval);

        _settings.GetInterval = getInterval;

        return this;
    }

    public TokenBucketRateLimiterBuilder WithGetNowUtc(Func<DateTime> getNowUtc)
    {
        ArgumentNullException.ThrowIfNull(getNowUtc);

        _settings.GetNowUtc = getNowUtc;

        return this;
    }

    public override RateLimiter Build()
    {
        var redisRateLimiter = new Redis.Internal.RateLimiting.TokenBucketRateLimiter(_redis, _settings);
        
        return new Redis.RateLimiting.RedisRateLimiter(redisRateLimiter);
    }
}
