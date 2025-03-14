using System;
using DotnetRateLimiter.Redis.RateLimiting.Models;
using StackExchange.Redis;

namespace DotnetRateLimiter.Redis.Builders;

public abstract class WindowRateLimiterBuilder<TSettings>(TSettings settings, IConnectionMultiplexer redis, string rateLimiterKey) : RateLimiterBuilder<TSettings>(settings, redis, rateLimiterKey) where TSettings : WindowRequestSettings
{
    public WindowRateLimiterBuilder<TSettings> WithWindowRate(long rate)
    {
        _settings.Rate = rate;

        return this;
    }

    public WindowRateLimiterBuilder<TSettings> WithExpiration(Func<DateTime> expiration)
    {
        _settings.GetExpirationUtc = expiration;

        return this;
    }

    public WindowRateLimiterBuilder<TSettings> WithRecordOnlySuccess(bool doRecordOnlyOnSuccess)
    {
        _settings.DoRecordOnlyOnSuccess = doRecordOnlyOnSuccess;

        return this;
    }

    public WindowRateLimiterBuilder<TSettings> WithWindowSize(Func<TimeSpan> getWindowSize)
    {
        ArgumentNullException.ThrowIfNull(getWindowSize);

        _settings.GetInterval = getWindowSize;

        return this;
    }

    public WindowRateLimiterBuilder<TSettings> WithGetNowUtc(Func<DateTime> getNowUtc)
    {
        ArgumentNullException.ThrowIfNull(getNowUtc);

        _settings.GetNowUtc = getNowUtc;

        return this;
    }
}
