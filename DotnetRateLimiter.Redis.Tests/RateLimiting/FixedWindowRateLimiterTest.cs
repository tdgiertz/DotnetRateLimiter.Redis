using DotnetRateLimiter.RateLimiting;
using DotnetRateLimiter.Redis.Internal.RateLimiting;
using StackExchange.Redis;
using System;
using System.Diagnostics;
using System.Threading;
using Xunit;

namespace DotnetRateLimiter.Redis.Tests.RateLimiting
{
    public class FixedWindowRateLimiterTest
    {
        private static IConnectionMultiplexer _redis = ConnectionMultiplexer.Connect("localhost");

        [Fact]
        public void Should_Have_Active_Count()
        {
            var key = GetKey();
            _redis.GetDatabase().KeyDelete(key);

            var interval = TimeSpan.FromSeconds(5);
            var rate = 5;

            var settings = GetSettings(key, interval, rate);

            var response = LimitNumber(5, settings);

            Assert.Equal(rate, response?.ActiveCount);
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

            var settings = GetSettings(key, interval, rate);

            var response = LimitNumber(6, settings);

            Assert.False(response?.IsSuccessful);

            _redis.GetDatabase().KeyDelete(key);
        }

        [Fact]
        public void Should_Refill_Window()
        {
            var key = GetKey();
            _redis.GetDatabase().KeyDelete(key);

            var interval = TimeSpan.FromSeconds(1);
            var rate = 5;

            var settings = GetSettings(key, interval, rate);

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

            var settings = GetSettings(key, interval, rate);

            var response = LimitNumber(50, settings);

            Assert.Equal(0, response?.ActiveCount);
            Assert.False(response?.IsSuccessful);

            _redis.GetDatabase().KeyDelete(key);
        }

        [Fact]
        public void Should_Exceed_Rate_Count()
        {
            var key = GetKey();
            _redis.GetDatabase().KeyDelete(key);

            var interval = TimeSpan.FromSeconds(10);
            var rate = 5;

            var settings = GetSettings(key, interval, rate);

            settings.DoRecordOnlyOnSuccess = false;

            var response = LimitNumber(50, settings);

            Assert.Equal(50, response?.ActiveCount);

            _redis.GetDatabase().KeyDelete(key);
        }

        [Fact]
        public void Should_Expire_Key()
        {
            var key = GetKey();

            var interval = TimeSpan.FromSeconds(2);
            var rate = 5;

            var settings = GetSettings(key, interval, DateTime.UtcNow, rate);

            _redis.GetDatabase(settings.DatabaseId).KeyDelete(key);

            var response = LimitNumber(1, settings);

            Assert.True(_redis.GetDatabase(settings.DatabaseId).KeyExists(key));

            Thread.Sleep((int)interval.TotalMilliseconds + 1000);

            Assert.False(_redis.GetDatabase(settings.DatabaseId).KeyExists(key));

            _redis.GetDatabase(settings.DatabaseId).KeyDelete(key);
        }

        private RateLimitResponse? LimitNumber(int number, WindowRequestSettings settings)
        {
            var rateLimiter = new FixedWindowRateLimiter(_redis, settings);

            RateLimitResponse? response = null;

            for(var i = 0; i < number; i++)
            {
                response = rateLimiter.Limit(1);
            }

            return response;
        }

        private WindowRequestSettings GetSettings(string key, TimeSpan interval, long rate)
        {
            return new WindowRequestSettings
            {
                Key = key,
                GetInterval = () => interval,
                GetNowUtc = () => DateTime.UtcNow,
                Rate = rate
            };
        }

        private WindowRequestSettings GetSettings(string key, TimeSpan interval, DateTime startDate, long rate)
        {
            return new WindowRequestSettings
            {
                Key = key,
                GetInterval = () => interval,
                GetExpirationUtc = () => startDate + interval,
                GetNowUtc = () => DateTime.UtcNow,
                Rate = rate
            };
        }

        private string GetKey()
        {
            var key = $"{GetType().Name}_{GetRelativeMethodName(2)}";

            return key;
        }

        private string? GetRelativeMethodName(int relativeIndex)
        {
            var st = new System.Diagnostics.StackTrace();
            var sf = st.GetFrame(relativeIndex);

            return sf?.GetMethod()?.Name;
        }
    }
}