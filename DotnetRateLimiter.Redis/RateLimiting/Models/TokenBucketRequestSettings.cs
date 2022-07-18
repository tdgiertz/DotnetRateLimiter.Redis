namespace DotnetRateLimiter.RateLimiting
{
    public class TokenBucketRequestSettings : RateLimitRequestSettings
    {
        public long Capacity { get; set; }
        public long RefillRate { get; set; } = 1;
        public bool IsEmptyOnStart { get; set; }
    }
}
