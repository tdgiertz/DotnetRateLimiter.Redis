using DotnetRateLimiter.RateLimiting;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
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

        public long? Count()
        {
            var redisValue = _redis.GetDatabase(_settings.DatabaseId).StringGet(_settings.Key);

            return RedisValueToLong(redisValue);
        }

        public Task<long?> CountAsync(CancellationToken cancellationToken = default)
        {
            return _redis.GetDatabase(_settings.DatabaseId).StringGetAsync(_settings.Key)
                    .ContinueWith(async task => RedisValueToLong(await task)).Unwrap();
        }

        internal override string GetLuaScript(Dictionary<Parameter, string> parameterNameLookup)
        {
            return $@"
                local results = {{}}

                local capacity = tonumber({parameterNameLookup[Parameter.Rate]})
                local currentTicks = tonumber({parameterNameLookup[Parameter.Now]})
                local refillSpanTicks = tonumber({parameterNameLookup[Parameter.IntervalTicks]})
                local refillRate = tonumber({parameterNameLookup[Parameter.RefillRate]})
                local incrementAmount = tonumber({parameterNameLookup[Parameter.IncrementAmount]})
                local nextRefillTicks = refillSpanTicks + currentTicks

                local json = cjson.encode({{['NextRefillTicks']=nextRefillTicks,['TokenCount']={(_settings.IsEmptyOnStart ? "0" : "capacity")}}})
                
                local doesExist = redis.call('EXISTS', KEYS[1])

                if doesExist == 1 then
                    json = redis.call('GET', KEYS[1])
                end

                local value = cjson.decode(json)

                nextRefillTicks = tonumber(value['NextRefillTicks'])
                local tokensAvailable = tonumber(value['TokenCount'])

                local refillTokenCount = 0

                if currentTicks >= nextRefillTicks then
                    local refillAmount = math.max((currentTicks - nextRefillTicks) / refillSpanTicks, 1)
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

                redis.call('SET', KEYS[1], json)

                results[#results+1] = tokensAvailable
                results[#results+1] = isSuccessful
                return results";
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

        internal override int SetParameters()
        {
            var totalCount = base.SetParameters();

            var parameterName = GetParameterName(totalCount++);
            _parameterNameLookup.Add(Parameter.Rate, parameterName);
            _defaultParameterValues.Add(Parameter.Rate, new RedisValue(_settings.Capacity.ToString()));
            _parameterNameOrder.Add(Parameter.Rate);

            parameterName = GetParameterName(totalCount++);
            _parameterNameLookup.Add(Parameter.RefillRate, parameterName);
            _defaultParameterValues.Add(Parameter.RefillRate, new RedisValue(_settings.RefillRate.ToString()));
            _parameterNameOrder.Add(Parameter.RefillRate);

            return totalCount;
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