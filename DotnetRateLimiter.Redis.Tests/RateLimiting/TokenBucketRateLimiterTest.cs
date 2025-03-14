using DotnetRateLimiter.Redis.Redis.Internal.RateLimiting;
using DotnetRateLimiter.Redis.RateLimiting.Models;
using StackExchange.Redis;
using System;
using System.Threading;
using Xunit;

namespace DotnetRateLimiter.Redis.Tests.RateLimiting;

public class TokenBucketRateLimiterTest
{
    private static readonly ConnectionMultiplexer _redis = ConnectionMultiplexer.Connect("localhost");

    [Fact]
    public void Should_Have_Zero_Count_With_No_Key()
    {
        var key = GetKey();
        _redis.GetDatabase().KeyDelete(key);

        var interval = TimeSpan.FromSeconds(5);
        var rate = 5;
        var capacity = 5L;

        var rateLimiter = new TokenBucketRateLimiter(_redis, GetSettings(key, interval, capacity, rate));

        _redis.GetDatabase().KeyDelete(key);

        var count = rateLimiter.Count();

        Assert.Equal(0, count);

        _redis.GetDatabase().KeyDelete(key);
    }

    [Fact]
    public void Should_Not_Have_Active_Count()
    {
        var key = GetKey();
        _redis.GetDatabase().KeyDelete(key);

        var interval = TimeSpan.FromSeconds(5);
        var rate = 5;
        var capacity = 5L;

        var settings = GetSettings(key, interval, capacity, rate);

        var response = LimitNumber(5, settings);

        Assert.Equal(0, response?.ActiveCount);
        Assert.True(response?.IsSuccessful);

        _redis.GetDatabase().KeyDelete(key);
    }

    [Fact]
    public void Should_Not_Succeed_Past_Rate()
    {
        var key = GetKey();
        _redis.GetDatabase().KeyDelete(key);

        var interval = TimeSpan.FromSeconds(1);
        var rate = 5;
        var capacity = 5L;

        var settings = GetSettings(key, interval, capacity, rate);

        var response = LimitNumber(6, settings);

        Assert.False(response?.IsSuccessful);

        _redis.GetDatabase().KeyDelete(key);
    }

    [Fact]
    public void Should_Refill_Bucket()
    {
        var key = GetKey();
        _redis.GetDatabase().KeyDelete(key);

        var interval = TimeSpan.FromSeconds(1);
        var rate = 5;
        var capacity = 5L;

        var settings = GetSettings(key, interval, capacity, rate);

        var response = LimitNumber(rate, settings);

        Assert.True(response?.IsSuccessful);

        Thread.Sleep((int)interval.TotalMilliseconds);

        response = LimitNumber(5, settings);

        Assert.True(response?.IsSuccessful);

        _redis.GetDatabase().KeyDelete(key);
    }

    [Fact]
    public void Should_Not_Exceed_Rate_Count()
    {
        var key = GetKey();
        _redis.GetDatabase().KeyDelete(key);

        var interval = TimeSpan.FromSeconds(5);
        var rate = 5L;
        var capacity = 5L;

        var settings = GetSettings(key, interval, capacity, rate);

        var response = LimitNumber(6, settings);

        Assert.Equal(0, response?.ActiveCount);
        Assert.False(response?.IsSuccessful);

        _redis.GetDatabase().KeyDelete(key);
    }

    private static RateLimitResponse? LimitNumber(int number, TokenBucketRequestSettings settings)
    {
        var rateLimiter = new TokenBucketRateLimiter(_redis, settings);

        RateLimitResponse? response = null;

        for(var i = 0; i < number; i++)
        {
            response = rateLimiter.Limit(1);
        }

        return response;
    }

    private static TokenBucketRequestSettings GetSettings(string key, TimeSpan interval, long capacity, long rate)
    {
        return new TokenBucketRequestSettings
        {
            Key = key,
            GetInterval = () => interval,
            GetNowUtc = () => DateTime.UtcNow,
            Capacity = capacity,
            RefillRate = rate
        };
    }

    private string GetKey()
    {
        var key = $"{GetType().Name}_{GetRelativeMethodName(2)}";

        return key;
    }

    private static string? GetRelativeMethodName(int relativeIndex)
    {
        var st = new System.Diagnostics.StackTrace();
        var sf = st.GetFrame(relativeIndex);

        return sf?.GetMethod()?.Name;
    }
}