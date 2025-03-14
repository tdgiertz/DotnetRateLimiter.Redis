using System.Collections.Generic;
using System.Threading.RateLimiting;

namespace DotnetRateLimiter.Redis.Redis.RateLimiting;

public class Lease(bool isAcquired) : RateLimitLease
{
    public override bool IsAcquired { get; } = isAcquired;

    public override IEnumerable<string> MetadataNames => throw new System.NotImplementedException();

    public override bool TryGetMetadata(string metadataName, out object? metadata)
    {
        throw new System.NotImplementedException();
    }
}