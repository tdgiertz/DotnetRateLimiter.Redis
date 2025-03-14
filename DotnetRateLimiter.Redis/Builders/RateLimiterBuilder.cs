using System;
using System.Threading.RateLimiting;
using DotnetRateLimiter.Redis.RateLimiting.Models;
using StackExchange.Redis;

namespace DotnetRateLimiter.Redis.Builders;

public abstract class RateLimiterBuilder<TSettings> where TSettings : RequestSettings
{
    protected readonly IConnectionMultiplexer _redis;
    protected readonly TSettings _settings;

    public RateLimiterBuilder(TSettings settings, IConnectionMultiplexer redis, string rateLimiterKey)
    {
        if(redis == null)
        {
            throw new ArgumentNullException(nameof(redis));
        }
        if(string.IsNullOrEmpty(rateLimiterKey))
        {
            throw new ArgumentException($"Argument {nameof(rateLimiterKey)} must have a value");
        }
        
        _redis = redis;
        _settings = settings;
        _settings.Key = rateLimiterKey;
    }

    public RateLimiterBuilder<TSettings> WithDatabaseId(int databaseId)
    {
        _settings.DatabaseId = databaseId;

        return this;
    }

    public abstract RateLimiter Build();
}
