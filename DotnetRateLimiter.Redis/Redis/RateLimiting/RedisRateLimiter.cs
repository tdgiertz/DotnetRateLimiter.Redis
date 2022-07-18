using System.Threading.Tasks;
using System.Threading.RateLimiting;
using System;
using System.Threading;
using DotnetRateLimiter.RateLimiting;
using System.Collections.Concurrent;

namespace DotnetRateLimiter.Redis.RateLimiting
{
    public class RedisRateLimiter : RateLimiter
    {
        private static readonly RateLimitLease SuccessfulLease = new Lease(true);
        private static readonly RateLimitLease FailedLease = new Lease(false);

        private readonly IRateLimiter _limiter;
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _queue = new();

        private bool _disposed = false;

        public override TimeSpan? IdleDuration => null;

        public RedisRateLimiter(IRateLimiter limiter)
        {
            _limiter = limiter;
        }

        public override int GetAvailablePermits()
        {
            ThrowIfDisposed();

            return (int)(_limiter.Count() ?? 0);
        }

        protected override RateLimitLease AcquireCore(int permitCount)
        {
            ThrowIfDisposed();

            if(permitCount == 0)
            {
                if(_limiter.Count() > 0)
                {
                    return SuccessfulLease;
                }

                return FailedLease;
            }

            var response = _limiter.Limit(permitCount);

            return GetLease(response);
        }

        protected override ValueTask<RateLimitLease> WaitAsyncCore(int permitCount, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if(permitCount == 0)
            {
                if(_limiter.Count() > 0)
                {
                    return new ValueTask<RateLimitLease>(SuccessfulLease);
                }

                return new ValueTask<RateLimitLease>(FailedLease);
            }

            var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            _queue.TryAdd(source.GetHashCode(), source);

            var task = WaitAsyncInternal(permitCount, source.Token).ContinueWith(async resultTask =>
            {
                _queue.TryRemove(source.GetHashCode(), out _);
                return await resultTask;
            }).Unwrap();

            return new ValueTask<RateLimitLease>(task);
        }

        private async Task<RateLimitLease> WaitAsyncInternal(int permitCount, CancellationToken cancellationToken)
        {
            while(!cancellationToken.IsCancellationRequested)
            {
                var limitResult = await _limiter.LimitAsync(permitCount, cancellationToken);

                if(limitResult.IsSuccessful)
                {
                    return GetLease(limitResult);
                }

                await Task.Delay(100, cancellationToken);
            }

            return FailedLease;
        }

        private static RateLimitLease GetLease(RateLimitResponse rateLimitResponse)
        {
            if (rateLimitResponse.IsSuccessful)
            {
                return SuccessfulLease;
            }

            return FailedLease;
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            if (_disposed)
            {
                return;
            }
            _disposed = true;

            foreach(var item in _queue)
            {
                item.Value.Cancel();
            }
        }

        protected override ValueTask DisposeAsyncCore()
        {
            Dispose(true);

            return default;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RedisRateLimiter));
            }
        }
    }

    internal readonly struct RequestRegistration
    {
        public int Count { get; }

        public TaskCompletionSource<RateLimitLease> Tcs { get; }

        public CancellationTokenRegistration CancellationTokenRegistration { get; }

        public RequestRegistration(int requestedCount, TaskCompletionSource<RateLimitLease> tcs, CancellationTokenRegistration cancellationTokenRegistration)
        {
            Count = requestedCount;
            Tcs = tcs;
            CancellationTokenRegistration = cancellationTokenRegistration;
        }
    }
}