using System;

namespace DotnetRateLimiter.Redis.RateLimiting.Models;

public class WindowRequestSettings : RateLimitRequestSettings
{
    public long Rate { get; set; }
    public Func<DateTime>? GetExpirationUtc { get; set; }
    public bool DoRecordOnlyOnSuccess { get; set; } = true;
}