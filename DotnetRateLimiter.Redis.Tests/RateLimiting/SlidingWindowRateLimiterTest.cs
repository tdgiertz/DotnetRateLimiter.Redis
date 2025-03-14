using DotnetRateLimiter.Redis.RateLimiting.Models;
using DotnetRateLimiter.Redis.Redis.Internal.RateLimiting;
using StackExchange.Redis;
using System;
using System.Threading;
using Xunit;

namespace DotnetRateLimiter.Redis.Tests.RateLimiting;

public class SlidingWindowRateLimiterTest
{
    private static readonly ConnectionMultiplexer _redis = ConnectionMultiplexer.Connect("localhost");

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Should_Have_Zero_Count_With_No_Key(bool doRecordOnlyOnSuccess)
    {
        var key = GetKey();
        _redis.GetDatabase().KeyDelete(key);

        var interval = TimeSpan.FromSeconds(5);
        var rate = 5;

        var rateLimiter = new SlidingWindowRateLimiter(_redis, GetSettings(key, interval, rate, doRecordOnlyOnSuccess));

        _redis.GetDatabase().KeyDelete(key);

        var count = rateLimiter.Count();

        Assert.Equal(0, count);

        _redis.GetDatabase().KeyDelete(key);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Should_Have_Rate_Count(bool doRecordOnlyOnSuccess)
    {
        var key = GetKey();
        _redis.GetDatabase().KeyDelete(key);

        var interval = TimeSpan.FromSeconds(5);
        var rate = 5;

        var settings = GetSettings(key, interval, rate, doRecordOnlyOnSuccess);
        var rateLimiter = new SlidingWindowRateLimiter(_redis, settings);

        LimitNumber(5, settings, rateLimiter);

        Assert.Equal(rate, rateLimiter.Count());

        _redis.GetDatabase().KeyDelete(key);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Should_Have_Active_Count(bool doRecordOnlyOnSuccess)
    {
        var key = GetKey();
        _redis.GetDatabase().KeyDelete(key);

        var interval = TimeSpan.FromSeconds(5);
        var rate = 5;

        var settings = GetSettings(key, interval, rate, doRecordOnlyOnSuccess);

        var (response, allSuccessful) = LimitNumber(5, settings);

        Assert.Equal(rate, response?.ActiveCount);
        Assert.True(allSuccessful);

        _redis.GetDatabase().KeyDelete(key);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Should_Not_Succeed_Past_Rate(bool doRecordOnlyOnSuccess)
    {
        var key = GetKey();
        _redis.GetDatabase().KeyDelete(key);

        var interval = TimeSpan.FromSeconds(2);
        var rate = 5;

        var settings = GetSettings(key, interval, rate, doRecordOnlyOnSuccess);

        var (_, allSuccessful) = LimitNumber(6, settings);

        Assert.False(allSuccessful);

        _redis.GetDatabase().KeyDelete(key);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Should_Expire_All(bool doRecordOnlyOnSuccess)
    {
        var key = GetKey();
        _redis.GetDatabase().KeyDelete(key);

        var interval = TimeSpan.FromSeconds(1);
        var rate = 5;

        var settings = GetSettings(key, interval, rate, doRecordOnlyOnSuccess);
        var rateLimiter = new SlidingWindowRateLimiter(_redis, settings);

        LimitNumber(5, settings, rateLimiter);

        Thread.Sleep((int)interval.TotalMilliseconds + 100);

        Assert.Equal(0, rateLimiter.Count());

        _redis.GetDatabase().KeyDelete(key);
    }

    [Theory]
    [InlineData(true, 50, 5, 0, 5)]
    [InlineData(false, 50, 50, -45, 49)]
    public void Should_Not_Exceed_Rate_Count(bool doRecordOnlyOnSuccess, int limitCount, int expectedCount, int expectedAvailableCount, int expectedActiveCount)
    {
        var key = GetKey();
        _redis.GetDatabase().KeyDelete(key);

        var interval = TimeSpan.FromSeconds(5);
        var rate = 5;

        var settings = GetSettings(key, interval, rate, doRecordOnlyOnSuccess);

        var rateLimiter = new SlidingWindowRateLimiter(_redis, settings);

        var (response, _) = LimitNumber(limitCount, settings, rateLimiter);

        var count = rateLimiter.Count();
        var availableCount = rateLimiter.AvailableCount();

        Assert.Equal(expectedAvailableCount, availableCount);
        Assert.Equal(expectedCount, count);
        Assert.Equal(expectedActiveCount, response?.ActiveCount);
        Assert.False(response?.IsSuccessful);

        _redis.GetDatabase().KeyDelete(key);
    }

    [Theory]
    [InlineData(true, 50, 5)]
    [InlineData(false, 50, 50)]
    public void Should_Exceed_Rate_Count(bool doRecordOnlyOnSuccess, int limitCount, int expectedCount)
    {
        var key = GetKey();
        _redis.GetDatabase().KeyDelete(key);

        var interval = TimeSpan.FromSeconds(100);
        var rate = 5;

        var settings = GetSettings(key, interval, rate, doRecordOnlyOnSuccess);
        var rateLimiter = new SlidingWindowRateLimiter(_redis, settings);

        settings.DoRecordOnlyOnSuccess = false;

        LimitNumber(limitCount, settings, rateLimiter);

        Assert.Equal(expectedCount, rateLimiter.Count());

        _redis.GetDatabase().KeyDelete(key);
    }

    private static (RateLimitResponse? Response, bool AllSuccessful) LimitNumber(int number, WindowRequestSettings settings, SlidingWindowRateLimiter? rateLimiter = null)
    {
        rateLimiter ??= new SlidingWindowRateLimiter(_redis, settings);

        bool isSuccessful = true;
        RateLimitResponse? response = null;

        for(var i = 0; i < number; i++)
        {
            response = rateLimiter.Limit(1);
            isSuccessful = isSuccessful && response.IsSuccessful;
        }

        return (response, isSuccessful);
    }

    private static WindowRequestSettings GetSettings(string key, TimeSpan interval, long rate, bool doRecordOnlyOnSuccess)
    {
        return new WindowRequestSettings
        {
            Key = key,
            GetInterval = () => interval,
            GetNowUtc = () => DateTime.UtcNow,
            Rate = rate,
            DoRecordOnlyOnSuccess = doRecordOnlyOnSuccess
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