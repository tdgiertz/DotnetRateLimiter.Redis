using System;
using DotnetRateLimiter.RateLimiting;
using StackExchange.Redis;

namespace DotnetRateLimiter.Builders
{
    public abstract class WindowRateLimiterBuilder<TSettings> : RateLimiterBuilder<TSettings> where TSettings : WindowRequestSettings
    {
        public WindowRateLimiterBuilder(TSettings settings, IConnectionMultiplexer redis, string rateLimiterKey) : base(settings, redis, rateLimiterKey)
        {
        }

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
            if(getWindowSize == null)
            {
                throw new ArgumentNullException(nameof(getWindowSize));
            }

            _settings.GetInterval = getWindowSize;

            return this;
        }

        public WindowRateLimiterBuilder<TSettings> WithGetNowUtc(Func<DateTime> getNowUtc)
        {
            if(getNowUtc == null)
            {
                throw new ArgumentNullException(nameof(getNowUtc));
            }

            _settings.GetNowUtc = getNowUtc;

            return this;
        }
    }
}
