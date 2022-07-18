using System;
using System.Threading.RateLimiting;
using DotnetRateLimiter.Builders;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace DotnetRateLimiter.Redis.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddTokenBucketRateLimiter(this IServiceCollection services, IConnectionMultiplexer redis, string rateLimiterKey, Action<TokenBucketRateLimiterBuilder> builder)
        {
            var tokenBuilder = new TokenBucketRateLimiterBuilder(redis, rateLimiterKey);
            builder(tokenBuilder);
            services.AddSingleton<RateLimiter>(tokenBuilder.Build());
            return services;
        }

        public static IServiceCollection AddSlidingWindowBucketRateLimiter(this IServiceCollection services, IConnectionMultiplexer redis, string rateLimiterKey, Action<SlidingWindowRateLimiterBuilder> builder)
        {
            var windowBuilder = new SlidingWindowRateLimiterBuilder(redis, rateLimiterKey);
            builder(windowBuilder);
            services.AddSingleton<RateLimiter>(windowBuilder.Build());
            return services;
        }

        public static IServiceCollection AddFixedWindowRateLimiter(this IServiceCollection services,IConnectionMultiplexer redis, string rateLimiterKey, Action<FixedWindowRateLimiterBuilder> builder)
        {
            var windowBuilder = new FixedWindowRateLimiterBuilder(redis, rateLimiterKey);
            builder(windowBuilder);
            services.AddSingleton<RateLimiter>(windowBuilder.Build());
            return services;
        }
    }
}