using DotnetRateLimiter.RateLimiting;
using DotnetRateLimiter.Redis.Internal.RateLimiting;
using StackExchange.Redis;
using System;
using System.Threading;
using Xunit;

namespace DotnetRateLimiter.Redis.Tests.Redis.RateLimiting
{
    public class SlidingWindowRateLimiterTest
    {
        private static IConnectionMultiplexer _redis = ConnectionMultiplexer.Connect("localhost");

        [Fact]
        public void Should_Have_Rate_Count()
        {
            var key = GetKey();
            _redis.GetDatabase().KeyDelete(key);

            var interval = TimeSpan.FromSeconds(5);
            var rate = 5;

            var settings = GetSettings(key, interval, rate);

            LimitNumber(5, settings);

            Assert.Equal(rate, GetCount(key, interval, rate));

            _redis.GetDatabase().KeyDelete(key);
        }

        [Fact]
        public void Should_Have_Active_Count()
        {
            var key = GetKey();
            _redis.GetDatabase().KeyDelete(key);

            var interval = TimeSpan.FromSeconds(5);
            var rate = 5;

            var settings = GetSettings(key, interval, rate);

            var (response, allSuccessful) = LimitNumber(5, settings);

            Assert.Equal(rate, response?.ActiveCount);
            Assert.True(allSuccessful);

            _redis.GetDatabase().KeyDelete(key);
        }

        [Fact]
        public void Should_Not_Succeed_Past_Rate()
        {
            var key = GetKey();
            _redis.GetDatabase().KeyDelete(key);

            var interval = TimeSpan.FromSeconds(2);
            var rate = 5;

            var settings = GetSettings(key, interval, rate);

            var (response, allSuccessful) = LimitNumber(6, settings);

            Assert.False(allSuccessful);

            _redis.GetDatabase().KeyDelete(key);
        }

        [Fact]
        public void Should_Expire_All()
        {
            var key = GetKey();
            _redis.GetDatabase().KeyDelete(key);

            var interval = TimeSpan.FromSeconds(1);
            var rate = 5;

            var settings = GetSettings(key, interval, rate);

            LimitNumber(5, settings);

            Thread.Sleep((int)interval.TotalMilliseconds + 100);

            Assert.Null(GetCount(key, interval, rate));

            _redis.GetDatabase().KeyDelete(key);
        }

        [Fact]
        public void Should_Not_Exceed_Rate_Count()
        {
            var key = GetKey();
            _redis.GetDatabase().KeyDelete(key);

            var interval = TimeSpan.FromSeconds(5);
            var rate = 5;

            var settings = GetSettings(key, interval, rate);

            LimitNumber(50, settings);

            Assert.Equal(rate, GetCount(key, interval, rate));

            _redis.GetDatabase().KeyDelete(key);
        }

        [Fact]
        public void Should_Exceed_Rate_Count()
        {
            var key = GetKey();
            Console.WriteLine(key);
            _redis.GetDatabase().KeyDelete(key);

            var interval = TimeSpan.FromSeconds(100);
            var rate = 5;

            var settings = GetSettings(key, interval, rate);

            settings.DoRecordOnlyOnSuccess = false;

            LimitNumber(50, settings);

            Assert.Equal(50, GetCount(key, interval, rate));

            _redis.GetDatabase().KeyDelete(key);
        }

        private (RateLimitResponse? Response, bool AllSuccessful) LimitNumber(int number, WindowRequestSettings settings)
        {
            var rateLimiter = new SlidingWindowRateLimiter(_redis, settings);

            bool isSuccessful = true;
            RateLimitResponse? response = null;

            for(var i = 0; i < number; i++)
            {
                response = rateLimiter.Limit(1);
                isSuccessful = isSuccessful && response.IsSuccessful;
            }

            return (response, isSuccessful);
        }

        private long? GetCount(string key, TimeSpan interval, long rate)
        {
            var rateLimiter = new SlidingWindowRateLimiter(_redis, GetSettings(key, interval, rate));

            return rateLimiter.Count();
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