using DotnetRateLimiter.Redis.RateLimiting.Models;
using StackExchange.Redis;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DotnetRateLimiter.Redis.Redis.Internal.RateLimiting;

internal abstract partial class RateLimiter<TSettings> where TSettings : RateLimitRequestSettings
{
    protected readonly IConnectionMultiplexer _redis;
    protected readonly TSettings _settings;
    protected readonly LuaScript _limitScript;

    [GeneratedRegex(@"\s+")]
    private static partial Regex MatchWhitespaceRegex();

    public RateLimiter(IConnectionMultiplexer redis, TSettings settings)
    {
        _redis = redis;
        _settings = settings;
        _limitScript = LuaScript.Prepare(MatchWhitespaceRegex().Replace(GetLuaScript(), " "));

        CheckArguments();
        InitialSetup();
    }

    public RateLimitResponse Limit(int count)
    {
        RateLimiter<TSettings>.CheckCountArgument(count);

        var result = _redis.GetDatabase(_settings.DatabaseId).ScriptEvaluate(_limitScript, GetParameters(count));

        return GetRateLimitResponse(count, result);
    }

    public Task<RateLimitResponse> LimitAsync(int count, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<RateLimitResponse>(cancellationToken);
        }

        RateLimiter<TSettings>.CheckCountArgument(count);

        return _redis.GetDatabase(_settings.DatabaseId).ScriptEvaluateAsync(_limitScript, GetParameters(count))
                .ContinueWith(async task => GetRateLimitResponse(count, await task.ConfigureAwait(false))).Unwrap();
    }

    public bool Delete()
    {
        RateLimiter<TSettings>.CheckKeyArgument(_settings.Key);

        return _redis.GetDatabase(_settings.DatabaseId).KeyDelete(_settings.Key);
    }

    public Task<bool> DeleteAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<bool>(cancellationToken);
        }

        return _redis.GetDatabase(_settings.DatabaseId).KeyDeleteAsync(_settings.Key);
    }

    internal virtual void CheckArguments()
    {
        RateLimiter<TSettings>.CheckKeyArgument(_settings.Key);
        if(_settings.GetInterval == null)
        {
            throw new ArgumentException($"Argument {nameof(_settings.GetInterval)} must be set.");
        }
    }

    internal static void CheckKeyArgument(string key)
    {
        if(string.IsNullOrEmpty(key))
        {
            throw new ArgumentException($"Argument Key must contain a value.");
        }
    }

    internal static void CheckCountArgument(int count)
    {
        if(count <= 0)
        {
            throw new ArgumentException($"Argument count must be greater than 0.");
        }
    }

    internal abstract object GetParameters(int count);
    internal abstract void InitialSetup();
    internal abstract RateLimitResponse GetRateLimitResponse(int count, RedisResult redisResult);
    internal abstract string GetLuaScript();

    internal static string GetParameterName(int index)
    {
        return $"ARGV[{index + 1}]";
    }
}