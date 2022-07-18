using System;

namespace DotnetRateLimiter.RateLimiting
{
    public class WindowRequestSettings : RateLimitRequestSettings
    {
        public long Rate { get; set; }
        public Func<DateTime>? GetExpirationUtc { get; set; }
        public bool DoRecordOnlyOnSuccess { get; set; } = true;
    }
}