using DotnetRateLimiter.RateLimiting;
using DotnetRateLimiter.Redis.Extensions;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;

namespace DotnetRateLimiter.Redis.Internal.RateLimiting
{
    internal class FixedWindowRateLimiter : RateLimiter<WindowRequestSettings>, IRateLimiter
    {
        public FixedWindowRateLimiter(IConnectionMultiplexer redis, WindowRequestSettings settings) : base(redis, settings)
        {
        }

        internal override void InitialSetup()
        {
            var jsonStart = "{";
            var script = @$"
                local doesExist = redis.call('EXISTS', KEYS[1])

                if doesExist == 0 then
                    return 0
                end

                local type = redis.call('TYPE', KEYS[1])
                if type['ok'] ~= 'string' then
                    redis.call('DEL', KEYS[1])
                    return type
                end
                
                local value = redis.call('GET', KEYS[1])
                if string.len(value) > 0 then
                    local firstChar = string.sub(value, 1, 1)

                    if firstChar == '{jsonStart}' then
                        redis.call('DEL', KEYS[1])
                        return firstChar
                    end
                end
                
                return 0";

            _redis.GetDatabase(_settings.DatabaseId).ScriptEvaluate(script, new RedisKey[] { _settings.Key });
        }

        public long Count()
        {
            var redisValue = _redis.GetDatabase(_settings.DatabaseId).StringGet(_settings.Key);

            return RedisValueToLong(redisValue);
        }

        public Task<long> CountAsync(CancellationToken cancellationToken = default)
        {
            return _redis.GetDatabase(_settings.DatabaseId).StringGetAsync(_settings.Key)
                .ContinueWith(async task => RedisValueToLong(await task.ConfigureAwait(false))).Unwrap();
        }

        public long AvailableCount()
        {
            var redisValue = _redis.GetDatabase(_settings.DatabaseId).StringGet(_settings.Key);

            return RedisValueToLong(redisValue);
        }

        public Task<long> AvailableCountAsync(CancellationToken cancellationToken = default)
        {
            return _redis.GetDatabase(_settings.DatabaseId).StringGetAsync(_settings.Key)
                .ContinueWith(async task => _settings.Rate - RedisValueToLong(await task.ConfigureAwait(false))).Unwrap();
        }

        private long RedisValueToLong(RedisValue redisValue)
        {
            if(redisValue != RedisValue.Null && redisValue.TryParse(out long value))
            {
                return value;
            }

            return 0;
        }

        internal override string GetLuaScript()
        {
            var incrStatement = $@"
                activeAfter = redis.call('INCRBY', @Key, tonumber(@IncrementAmount))
                addedCount = activeAfter - activeBefore";

            var gatedIncrStatement = $@"local difference = tonumber(@Rate) - activeBefore - tonumber(@IncrementAmount)
                if difference >= 0 then
                    {incrStatement}
                end";

            var fullIncrStatement = _settings.DoRecordOnlyOnSuccess ? gatedIncrStatement : incrStatement;

            var expirationStatement = _settings.GetExpirationUtc != null
                ? $"redis.call('EXPIREAT', @Key, tonumber(@Expiration))"
                : $"redis.call('EXPIRE', @Key, tonumber(@IntervalSeconds))";

            return $@"
                local results = {{}}
                local addedCount = 0
                local activeBefore = tonumber(redis.call('GET', @Key)) or 0
                local activeAfter = 0
                {fullIncrStatement}
                if tonumber(activeAfter) == tonumber(@IncrementAmount) then
                    {expirationStatement}
                end
                results[#results+1] = addedCount
                results[#results+1] = activeAfter
                return results";
        }

        internal override object GetParameters(int count)
        {
            var now = _settings.GetNowUtc?.Invoke() ?? DateTime.UtcNow;
            var interval = _settings.GetInterval();

            if(_settings.GetExpirationUtc is not null)
            {
                var expiration = _settings.GetExpirationUtc();

                return new
                {
                    Key = _settings.Key,
                    IncrementAmount = new RedisValue(count.ToString()),
                    IntervalSeconds = new RedisValue(interval.TotalSeconds.ToString()),
                    Expiration = new RedisValue(((long)expiration.ToRedisSeconds()).ToString()),
                    Rate = new RedisValue(_settings.Rate.ToString())
                };
            }

            return new
            {
                Key = _settings.Key,
                IncrementAmount = new RedisValue(count.ToString()),
                IntervalSeconds = new RedisValue(interval.TotalSeconds.ToString()),
                Rate = new RedisValue(_settings.Rate.ToString())
            };
        }

        internal override RateLimitResponse GetRateLimitResponse(int count, RedisResult redisResult)
        {
            var values = (RedisResult[]?)redisResult;

            Debug.Assert(values is not null);
            Debug.Assert(values.Length > 1);

            var addedCount = (long)values[0];
            var activeCount = (long)values[1];
            var isAdded = addedCount > 0;

            return new RateLimitResponse { ActiveCount = activeCount, IsSuccessful = isAdded };
        }

        internal override void CheckArguments()
        {
            base.CheckArguments();
            if(_settings.Rate <= 0)
            {
                throw new ArgumentException($"Argument {nameof(_settings.Rate)} must be greater than 0.");
            }
        }
    }
}