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

namespace DotnetRateLimiter.Redis.Test
{
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
                            .WithInterval(() => TimeSpan.FromSeconds(1))
                            .WithGetNowUtc(() => DateTime.UtcNow);
                    });
                    var windowSize = TimeSpan.FromSeconds(20);
                    // services.AddFixedWindowRateLimiter(redis, key, builder =>
                    // {
                    //     builder
                    //         .WithWindowRate(100)
                    //         // .WithExpiration(() => DateTime.UtcNow + windowSize)
                    //         .WithWindowSize(() => windowSize)
                    //         .WithGetNowUtc(() => DateTime.UtcNow)
                    //         .WithRecordOnlySuccess(true);
                    // });
                    // services.AddSlidingWindowBucketRateLimiter(redis, key, builder =>
                    // {
                    //     builder
                    //         .WithWindowRate(100)
                    //         // .WithExpiration(() => DateTime.UtcNow + windowSize)
                    //         .WithWindowSize(() => windowSize)
                    //         .WithGetNowUtc(() => DateTime.UtcNow)
                    //         .WithRecordOnlySuccess(true);
                    // });
                })
                .UseConsoleLifetime();

            var host = await hostBuilder.StartAsync();
            
            _totalLeases = 0;
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var rateLimiter = host.Services.GetRequiredService<RateLimiter>();

            NonBlockingConsole.WriteLine($"Starting permit count {rateLimiter.GetAvailablePermits()}");

            var cancellationToken = new CancellationTokenSource();
            var tasks = new List<Task>();
            var numberOfWorkers = 10;
            for (var i = 1; i <= numberOfWorkers; i++)
            {
                tasks.Add(RunWaitAsync(rateLimiter, i, cancellationToken.Token));
            }

            Console.CancelKeyPress += (_, __) => cancellationToken.Cancel();
            await Task.WhenAll(tasks);

            var totalSeconds = (stopwatch.ElapsedMilliseconds / 1000.0);

            NonBlockingConsole.WriteLine($"Leases per second: {Math.Round(_totalLeases / totalSeconds, 2)}");

            await host.StopAsync();
        }

        private static async Task RunAquireAsync(RateLimiter rateLimiter, int workerIndex, CancellationToken cancellationToken)
        {
            try
            {
                var workerName = $"Worker #{workerIndex}";
                var random = new Random((int)DateTime.Now.Ticks);
                int targetCount = 5;
                while (!cancellationToken.IsCancellationRequested)
                {
                    RateLimitLease lease;
                    do
                    {
                        lease = rateLimiter.Acquire(targetCount);

                        if(lease.IsAcquired)
                        {
                            Interlocked.Add(ref _totalLeases, targetCount);

                            NonBlockingConsole.WriteLine($"GetAvailablePermits: {rateLimiter.GetAvailablePermits()}");

                            break;
                        }
                    } while (!lease.IsAcquired && !cancellationToken.IsCancellationRequested);

                    var currentLeasedWorkers = Interlocked.Increment(ref _currentNumberOfLeasedWorkers);
                    NonBlockingConsole.WriteLine($"{workerName} has been leased, {currentLeasedWorkers} total leased");
                    await Task.Delay(random.Next(500, 10000), cancellationToken);
                    lease.Dispose();
                    currentLeasedWorkers = Interlocked.Decrement(ref _currentNumberOfLeasedWorkers);
                    NonBlockingConsole.WriteLine($"{workerName} has been released, {currentLeasedWorkers} total leased");
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
                var random = new Random((int)DateTime.Now.Ticks);
                int targetCount = 5;
                while (!cancellationToken.IsCancellationRequested)
                {
                    var lease = await rateLimiter.WaitAsync(targetCount, cancellationToken);

                    NonBlockingConsole.WriteLine($"GetAvailablePermits: {rateLimiter.GetAvailablePermits()}");
                    
                    if(lease.IsAcquired)
                    {
                        Interlocked.Add(ref _totalLeases, targetCount);
                    }

                    var currentLeasedWorkers = Interlocked.Increment(ref _currentNumberOfLeasedWorkers);
                    NonBlockingConsole.WriteLine($"{workerName} has been leased, {currentLeasedWorkers} total leased");
                    await Task.Delay(random.Next(500, 10000), cancellationToken);
                    lease.Dispose();
                    currentLeasedWorkers = Interlocked.Decrement(ref _currentNumberOfLeasedWorkers);
                    NonBlockingConsole.WriteLine($"{workerName} has been released, {currentLeasedWorkers} total leased");
                }
            }
            catch(TaskCanceledException)
            {
                // 
            }
        }
    }
}