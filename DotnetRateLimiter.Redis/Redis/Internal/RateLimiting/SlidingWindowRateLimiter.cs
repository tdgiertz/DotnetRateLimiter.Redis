using DotnetRateLimiter.RateLimiting;
using DotnetRateLimiter.Redis.Extensions;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;

namespace DotnetRateLimiter.Redis.Internal.RateLimiting
{
    internal class SlidingWindowRateLimiter : RateLimiter<WindowRequestSettings>, IRateLimiter
    {
        public SlidingWindowRateLimiter(IConnectionMultiplexer redis, WindowRequestSettings settings) : base(redis, settings)
        {
        }

        internal override void InitialSetup()
        {
            var script = @"
                local doesExist = redis.call('EXISTS', KEYS[1])

                if doesExist == 0 then
                    return 0
                end

                local type = redis.call('TYPE', KEYS[1])
                if type['ok'] ~= 'zset' then
                    redis.call('DEL', KEYS[1])
                    return 1
                end
                
                return 0";

            _redis.GetDatabase(_settings.DatabaseId).ScriptEvaluate(script, new RedisKey[] { _settings.Key });
        }

        public long Count()
        {
            return _settings.Rate -  _redis.GetDatabase(_settings.DatabaseId).SortedSetLength(_settings.Key);
        }

        public Task<long> CountAsync(CancellationToken cancellationToken = default)
        {
            return _redis.GetDatabase(_settings.DatabaseId).SortedSetLengthAsync(_settings.Key).ContinueWith(async task => _settings.Rate - await task.ConfigureAwait(false)).Unwrap();
        }

        internal override string GetLuaScript()
        {
            var zaddStatement = $@"
                local existingAtScore = redis.call('ZCOUNT', @Key, score, score)
                local count = tonumber(@IncrementAmount)
                local items = {{}}

                local index = existingAtScore;
                for i = 1, count do
                    items[#items+1] = score
                    index = index + 1
                    local member = score .. '-' .. index
                    items[#items+1] = member
                end

                addedCount = redis.call('ZADD', @Key, unpack(items))";

            var gatedZaddStatement = $@"local difference = tonumber(@Rate) - active - tonumber(@IncrementAmount)
                if difference >= 0 then
                    {zaddStatement}
                end";

            var fullZAddStatement = _settings.DoRecordOnlyOnSuccess ? gatedZaddStatement : zaddStatement;

            var expirationStatement = _settings.GetExpirationUtc != null
                ? $"redis.call('EXPIREAT', @Key, tonumber(@Expiration))"
                : $"redis.call('EXPIRE', @Key, tonumber(@IntervalSeconds))";

            return $@"
                local results = {{}}
                local addedCount = 0
                local score = @Now
                local removed = redis.call('ZREMRANGEBYSCORE', @Key, 0, (score - @IntervalTicks))
                local active = redis.call('ZCARD', @Key)
                {fullZAddStatement}
                {expirationStatement}
                results[#results+1] = addedCount
                results[#results+1] = active
                return results";
        }

        internal override object GetParameters(int count)
        {
            var tickDivisor = 10000;
            var now = _settings.GetNowUtc?.Invoke() ?? DateTime.UtcNow;
            var nowTicks = now.Ticks / tickDivisor;
            var interval = _settings.GetInterval();
            var intervalTicks = interval.Ticks / tickDivisor;

            if(_settings.GetExpirationUtc is not null)
            {
                var expiration = _settings.GetExpirationUtc();

                return new
                {
                    Key = _settings.Key,
                    IncrementAmount = new RedisValue(count.ToString()),
                    Now = new RedisValue(nowTicks.ToString()),
                    IntervalSeconds = new RedisValue(interval.TotalSeconds.ToString()),
                    IntervalTicks = new RedisValue(intervalTicks.ToString()),
                    Expiration = new RedisValue(((long)expiration.ToRedisSeconds()).ToString()),
                    Rate = new RedisValue(_settings.Rate.ToString())
                };
            }

            return new
            {
                Key = _settings.Key,
                IncrementAmount = new RedisValue(count.ToString()),
                Now = new RedisValue(nowTicks.ToString()),
                IntervalSeconds = new RedisValue(interval.TotalSeconds.ToString()),
                IntervalTicks = new RedisValue(intervalTicks.ToString()),
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

            if(isAdded)
            {
                activeCount += count;
            }

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