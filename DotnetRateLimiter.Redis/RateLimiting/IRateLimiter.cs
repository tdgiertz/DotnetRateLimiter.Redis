using DotnetRateLimiter.Redis.RateLimiting.Models;
using System.Threading;
using System.Threading.Tasks;

namespace DotnetRateLimiter.Redis.RateLimiting;

public interface IRateLimiter
{
    RateLimitResponse Limit(int count);
    Task<RateLimitResponse> LimitAsync(int count, CancellationToken cancellationToken = default);
    bool Delete();
    Task<bool> DeleteAsync(CancellationToken cancellationToken = default);
    long AvailableCount();
    Task<long> AvailableCountAsync(CancellationToken cancellationToken = default);
    long Count();
    Task<long> CountAsync(CancellationToken cancellationToken = default);
}