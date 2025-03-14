using System;
using System.Diagnostics.CodeAnalysis;

namespace DotnetRateLimiter.Redis.RateLimiting.Models;

public abstract class RateLimitRequestSettings : RequestSettings
{
    public Func<DateTime>? GetNowUtc { get; set; }
    [NotNull]
    public Func<TimeSpan>? GetInterval { get; set; }
}