namespace DotnetRateLimiter.Redis.RateLimiting.Models;

public class RateLimitResponse
{
    public bool IsSuccessful { get; set; }
    public long ActiveCount { get; set; }
}