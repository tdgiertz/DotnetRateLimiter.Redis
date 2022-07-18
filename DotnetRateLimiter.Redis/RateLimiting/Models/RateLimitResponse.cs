namespace DotnetRateLimiter.RateLimiting
{
    public class RateLimitResponse
    {
        public bool IsSuccessful { get; set; }
        public long ActiveCount { get; set; }
    }
}