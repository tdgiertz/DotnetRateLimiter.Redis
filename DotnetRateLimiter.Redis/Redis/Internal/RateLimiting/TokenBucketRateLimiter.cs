using DotnetRateLimiter.RateLimiting;
using StackExchange.Redis;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DotnetRateLimiter.Redis.Internal.RateLimiting
{
    internal class TokenBucketRateLimiter : RateLimiter<TokenBucketRequestSettings>, IRateLimiter
    {
        public TokenBucketRateLimiter(IConnectionMultiplexer redis, TokenBucketRequestSettings settings) : base(redis, settings)
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
                    return 1
                end
                
                local value = redis.call('GET', KEYS[1])
                if string.len(value) > 0 then
                    local firstChar = string.sub(value, 1, 1)

                    if firstChar ~= '{jsonStart}' then
                        redis.call('DEL', KEYS[1])
                        return 1
                    end
                end
                
                return 0";

            _redis.GetDatabase(_settings.DatabaseId).ScriptEvaluate(script, new RedisKey[] { _settings.Key });
        }

        public long Count()
        {
            var redisValue = _redis.GetDatabase(_settings.DatabaseId).StringGet(_settings.Key);

            if(redisValue == RedisValue.Null)
            {
                return 0;
            }

            return _settings.Capacity - RedisValueToLong(redisValue);
        }

        public Task<long> CountAsync(CancellationToken cancellationToken = default)
        {
            return _redis.GetDatabase(_settings.DatabaseId).StringGetAsync(_settings.Key)
                .ContinueWith(async task =>
                {
                    var redisValue = await task.ConfigureAwait(false);

                    if(redisValue == RedisValue.Null)
                    {
                        return 0;
                    }

                    return _settings.Capacity - RedisValueToLong(redisValue);
                }).Unwrap();
        }

        public long AvailableCount()
        {
            var redisValue = _redis.GetDatabase(_settings.DatabaseId).StringGet(_settings.Key);

            return RedisValueToLong(redisValue);
        }

        public Task<long> AvailableCountAsync(CancellationToken cancellationToken = default)
        {
            return _redis.GetDatabase(_settings.DatabaseId).StringGetAsync(_settings.Key)
                .ContinueWith(async task => RedisValueToLong(await task.ConfigureAwait(false))).Unwrap();
        }

        private static long RedisValueToLong(RedisValue redisValue)
        {
            if(redisValue != RedisValue.Null)
            {
                return System.Text.Json.JsonDocument.Parse(redisValue.ToString()).RootElement.GetProperty("TokenCount").GetInt64();
            }

            return 0;
        }

        internal override string GetLuaScript()
        {
            return $@"
                local results = {{}}

                local capacity = tonumber(@Rate)
                local currentTicks = tonumber(@Now)
                local refillSpanTicks = tonumber(@IntervalTicks)
                local refillRate = tonumber(@RefillRate)
                local incrementAmount = tonumber(@IncrementAmount)
                local nextRefillTicks = refillSpanTicks + currentTicks

                local json = cjson.encode({{['NextRefillTicks']=nextRefillTicks,['TokenCount']={(_settings.IsEmptyOnStart ? "0" : "capacity")}}})
                
                local doesExist = redis.call('EXISTS', @Key)

                if doesExist == 1 then
                    json = redis.call('GET', @Key)
                end

                local value = cjson.decode(json)

                nextRefillTicks = tonumber(value['NextRefillTicks'])
                local tokensAvailable = tonumber(value['TokenCount'])

                local refillTokenCount = 0

                if currentTicks >= nextRefillTicks then
                    local refillAmount = math.max(math.floor((currentTicks - nextRefillTicks) / refillSpanTicks), 1)
                    nextRefillTicks = nextRefillTicks + (refillSpanTicks * refillAmount)

                    refillTokenCount = refillRate * refillAmount
                end

                local refillTokens = math.min(capacity, refillTokenCount)
                tokensAvailable = math.max(0, math.min(tokensAvailable + refillTokens, capacity))

                local isSuccessful = false
                if incrementAmount <= tokensAvailable then                
                    tokensAvailable = tokensAvailable - incrementAmount
                    isSuccessful = true
                end

                value['NextRefillTicks'] = nextRefillTicks
                value['TokenCount'] = tokensAvailable

                json = cjson.encode(value)

                redis.call('SET', @Key, json)

                results[#results+1] = tokensAvailable
                results[#results+1] = isSuccessful
                return results";
        }

        internal override object GetParameters(int count)
        {
            var tickDivisor = 10000;
            var now = _settings.GetNowUtc?.Invoke() ?? DateTime.UtcNow;
            var nowTicks = now.Ticks / tickDivisor;
            var interval = _settings.GetInterval();
            var intervalTicks = interval.Ticks / tickDivisor;

            return new
            {
                Key = _settings.Key,
                IncrementAmount = new RedisValue(count.ToString()),
                Now = new RedisValue(nowTicks.ToString()),
                IntervalSeconds = new RedisValue(interval.TotalSeconds.ToString()),
                IntervalTicks = new RedisValue(intervalTicks.ToString()),
                Rate = new RedisValue(_settings.Capacity.ToString()),
                RefillRate = new RedisValue(_settings.RefillRate.ToString()),
            };
        }

        internal override RateLimitResponse GetRateLimitResponse(int count, RedisResult redisResult)
        {
            var values = (RedisResult[]?)redisResult;

            Debug.Assert(values is not null);
            Debug.Assert(values.Length > 1);

            var activeCount = (long)values[0];
            var isSuccessful = (bool)values[1];

            return new RateLimitResponse { ActiveCount = activeCount, IsSuccessful = isSuccessful };
        }

        internal override void CheckArguments()
        {
            base.CheckArguments();
            if(_settings.Capacity <= 0)
            {
                throw new ArgumentException($"Argument {nameof(_settings.Capacity)} must be greater than 0.");
            }
        }
    }
}