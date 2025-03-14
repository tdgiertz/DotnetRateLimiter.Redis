using DotnetRateLimiter.Redis.RateLimiting;
using DotnetRateLimiter.Redis.RateLimiting.Models;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace DotnetRateLimiter.Redis.Redis.RateLimiting;

public class RedisRateLimiter(IRateLimiter limiter) : RateLimiter
{
    private static readonly RateLimitLease SuccessfulLease = new Lease(true);
    private static readonly RateLimitLease FailedLease = new Lease(false);

    private readonly IRateLimiter _limiter = limiter;
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _queue = new();
    private readonly SemaphoreSlim _disposedSemaphore = new(1, 1);

    private bool _disposed = false;
    private long _failedLeaseCount = 0;
    private long _successfulLeaseCount = 0;

    public override TimeSpan? IdleDuration => null;

    public override RateLimiterStatistics? GetStatistics()
    {
        ThrowIfDisposed();

        return new RateLimiterStatistics
        {
            CurrentAvailablePermits = _limiter.AvailableCount(),
            TotalFailedLeases = _failedLeaseCount,
            TotalSuccessfulLeases = _successfulLeaseCount,
            CurrentQueuedCount = _queue.Count
        };
    }

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        ThrowIfDisposed();

        if (permitCount == 0)
        {
            if (_limiter.AvailableCount() > 0)
            {
                return SuccessfulLease;
            }

            return FailedLease;
        }

        var response = _limiter.Limit(permitCount);

        return GetLease(response);
    }

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (permitCount == 0)
        {
            if (_limiter.AvailableCount() > 0)
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
            return await resultTask.ConfigureAwait(false);
        }).Unwrap();

        return new ValueTask<RateLimitLease>(task);
    }

    private async Task<RateLimitLease> WaitAsyncInternal(int permitCount, CancellationToken cancellationToken)
    {
        while(!cancellationToken.IsCancellationRequested)
        {
            var limitResult = _limiter.Limit(permitCount);

            if(limitResult.IsSuccessful)
            {
                return GetLease(limitResult);
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        return FailedLease;
    }

    private RateLimitLease GetLease(RateLimitResponse rateLimitResponse)
    {
        if (rateLimitResponse.IsSuccessful)
        {
            Interlocked.Increment(ref _successfulLeaseCount);
            return SuccessfulLease;
        }

        Interlocked.Increment(ref _failedLeaseCount);
        return FailedLease;
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        _disposedSemaphore.Wait();
        try
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
        }
        finally
        {
            _disposedSemaphore.Release();
        }

        foreach (var item in _queue)
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
        _disposedSemaphore.Wait();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, typeof(RedisRateLimiter));
        }
        finally
        {
            _disposedSemaphore.Release();
        }
    }
}

internal readonly struct RequestRegistration(int requestedCount, TaskCompletionSource<RateLimitLease> tcs, CancellationTokenRegistration cancellationTokenRegistration)
{
    public int Count { get; } = requestedCount;

    public TaskCompletionSource<RateLimitLease> Tcs { get; } = tcs;

    public CancellationTokenRegistration CancellationTokenRegistration { get; } = cancellationTokenRegistration;
}