using System.Diagnostics.CodeAnalysis;

namespace DotnetRateLimiter.RateLimiting
{
    public class RequestSettings
    {
        public int DatabaseId { get; set; } = -1;
        [NotNull]
        public string? Key { get; set; }
    }
}