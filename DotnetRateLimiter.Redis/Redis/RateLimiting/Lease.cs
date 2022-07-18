using System.Collections.Generic;
using System.Threading.RateLimiting;

namespace DotnetRateLimiter.Redis.RateLimiting
{
    public class Lease : RateLimitLease
    {
        public override bool IsAcquired { get; }

        public override IEnumerable<string> MetadataNames => throw new System.NotImplementedException();

        public Lease(bool isAcquired)
        {
            IsAcquired = isAcquired;
        }

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            throw new System.NotImplementedException();
        }
    }
}