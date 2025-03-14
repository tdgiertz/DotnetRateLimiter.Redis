using DotnetRateLimiter.Redis.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace DotnetRateLimiter.Redis.App;

public class Program
{
    private static long _currentNumberOfLeasedWorkers = 0;
    private static int _totalLeases = 0;

    public static async Task Main(string[] args)
    {
        var redis = ConnectionMultiplexer.Connect("localhost");

        var key = "some-redis-key";

        var hostBuilder = Host.CreateDefaultBuilder(args);
        hostBuilder
            .ConfigureServices(services =>
            {
                services.AddTokenBucketRateLimiter(redis, key, builder =>
                {
                    builder
                        .WithCapacity(100)
                        .WithEmptyBucketOnStart(false)
                        .WithRefillRate(5)
                        .WithInterval(() => TimeSpan.FromSeconds(30))
                        .WithGetNowUtc(() => DateTime.UtcNow);
                });
                //var windowSize = TimeSpan.FromSeconds(10);
                //services.AddFixedWindowRateLimiter(redis, key, builder =>
                //{
                //    builder
                //        .WithWindowRate(1)
                //        // .WithExpiration(() => DateTime.UtcNow + windowSize)
                //        .WithWindowSize(() => windowSize)
                //        .WithGetNowUtc(() => DateTime.UtcNow)
                //        .WithRecordOnlySuccess(false);
                //});
                //services.AddSlidingWindowBucketRateLimiter(redis, key, builder =>
                //{
                //    builder
                //        .WithWindowRate(1)
                //        // .WithExpiration(() => DateTime.UtcNow + windowSize)
                //        .WithWindowSize(() => windowSize)
                //        .WithGetNowUtc(() => DateTime.UtcNow)
                //        .WithRecordOnlySuccess(true);
                //});
            })
            .UseConsoleLifetime();

        var host = await hostBuilder.StartAsync();
        
        _totalLeases = 0;
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var rateLimiter = host.Services.GetRequiredService<RateLimiter>();

        //WriteStatistics(rateLimiter);

        var cancellationToken = new CancellationTokenSource();
        var tasks = new List<Task>();
        var numberOfWorkers = 10;
        for (var i = 1; i <= numberOfWorkers; i++)
        {
            tasks.Add(RunAquireAsync(rateLimiter, i, cancellationToken.Token));
        }

        Console.CancelKeyPress += (_, __) => cancellationToken.Cancel();
        await Task.WhenAll(tasks);

        var totalSeconds = stopwatch.ElapsedMilliseconds / 1000.0;

        NonBlockingConsole.WriteLine($"Leases per second: {Math.Round(_totalLeases / totalSeconds, 2)}");

        await host.StopAsync();
    }

    private static async Task RunAquireAsync(RateLimiter rateLimiter, int workerIndex, CancellationToken cancellationToken)
    {
        try
        {
            var workerName = $"Worker #{workerIndex}";
            var random = new Random((int)DateTime.Now.Ticks);
            int targetCount = 1;
            while (!cancellationToken.IsCancellationRequested)
            {
                await WithLeaseAsync(workerName, random, rateLimiter, targetCount, async () =>
                {
                    RateLimitLease? lease = null;
                    do
                    {
                        lease = rateLimiter.AttemptAcquire(targetCount);

                        if (!lease.IsAcquired)
                        {
                            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                        }
                    } while (!lease.IsAcquired && !cancellationToken.IsCancellationRequested);

                    return lease;
                }, cancellationToken).ConfigureAwait(false);
            }
        }
        catch(TaskCanceledException)
        {
            // 
        }
    }

    private static async Task RunWaitAsync(RateLimiter rateLimiter, int workerIndex, CancellationToken cancellationToken)
    {
        try
        {
            var workerName = $"Worker #{workerIndex}";
            var random = new Random(workerIndex);
            int targetCount = 1;
            while (!cancellationToken.IsCancellationRequested)
            {
                await WithLeaseAsync(workerName, random, rateLimiter, targetCount, async () =>
                {
                    return await rateLimiter.AcquireAsync(targetCount);
                }, cancellationToken).ConfigureAwait(false);
            }
        }
        catch(TaskCanceledException)
        {
            // 
        }
    }

    private static async Task WithLeaseAsync(string workerName, Random random, RateLimiter rateLimiter, int targetCount, Func<Task<RateLimitLease>> getLease, CancellationToken cancellationToken)
    {
        var lease = await getLease();

        if (!lease.IsAcquired || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            Interlocked.Add(ref _totalLeases, targetCount);

            WriteStatistics(rateLimiter);

            var currentLeasedWorkers = Interlocked.Increment(ref _currentNumberOfLeasedWorkers);
            NonBlockingConsole.WriteLine($"{workerName} has been leased, {currentLeasedWorkers} total leased");
            await Task.Delay(random.Next(500, 10000), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lease.Dispose();
            var currentLeasedWorkers = Interlocked.Decrement(ref _currentNumberOfLeasedWorkers);
            NonBlockingConsole.WriteLine($"{workerName} has been released, {currentLeasedWorkers} total leased");
        }
    }

    private static void WriteStatistics(RateLimiter rateLimiter)
    {
        var statistics = rateLimiter.GetStatistics();

        if (statistics == null)
        {
            return;
        }

        NonBlockingConsole.WriteLine($"RateLimiter Statistics: {statistics.TotalSuccessfulLeases} successful, {statistics.TotalFailedLeases} failed, {statistics.CurrentAvailablePermits} available, {statistics.CurrentQueuedCount} queued");
    }
}