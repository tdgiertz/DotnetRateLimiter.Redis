using DotnetRateLimiter.RateLimiting;
using DotnetRateLimiter.Redis.Extensions;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
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

        public long? Count()
        {
            if(_redis.GetDatabase(_settings.DatabaseId).KeyExists(_settings.Key))
            {
                return _redis.GetDatabase(_settings.DatabaseId).SortedSetLength(_settings.Key);
            }

            return null;
        }

        public async Task<long?> CountAsync(CancellationToken cancellationToken = default)
        {
            if(await _redis.GetDatabase(_settings.DatabaseId).KeyExistsAsync(_settings.Key))
            {
                return await _redis.GetDatabase(_settings.DatabaseId).SortedSetLengthAsync(_settings.Key);
            }

            return null;
        }

        internal override string GetLuaScript(Dictionary<Parameter, string> parameterNameLookup)
        {
            var zaddStatement = $@"
                local existingAtScore = redis.call('ZCOUNT', KEYS[1], score, score)
                local count = tonumber({parameterNameLookup[Parameter.IncrementAmount]})
                local items = {{}}

                local index = existingAtScore;
                for i = 1, count do
                    items[#items+1] = score
                    index = index + 1
                    local member = score .. '-' .. index
                    items[#items+1] = member
                end

                addedCount = redis.call('ZADD', KEYS[1], unpack(items))";

            var gatedZaddStatement = $@"local difference = tonumber({parameterNameLookup[Parameter.Rate]}) - active - tonumber({parameterNameLookup[Parameter.IncrementAmount]})
                if difference >= 0 then
                    {zaddStatement}
                end";

            var fullZAddStatement = _settings.DoRecordOnlyOnSuccess ? gatedZaddStatement : zaddStatement;

            var expirationStatement = _settings.GetExpirationUtc != null
                ? $"redis.call('EXPIREAT', KEYS[1], tonumber({parameterNameLookup[Parameter.Expiration]}))"
                : $"redis.call('EXPIRE', KEYS[1], tonumber({parameterNameLookup[Parameter.IntervalSeconds]}))";

            return $@"
                local results = {{}}
                local addedCount = 0
                local score = {parameterNameLookup[Parameter.Now]}
                local removed = redis.call('ZREMRANGEBYSCORE', KEYS[1], 0, (score - tonumber({parameterNameLookup[Parameter.IntervalTicks]})))
                local active = redis.call('ZCARD', KEYS[1])
                {fullZAddStatement}
                {expirationStatement}
                results[#results+1] = addedCount
                results[#results+1] = active
                return results";
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

        internal override int SetParameters()
        {
            var totalCount = base.SetParameters();

            var parameterName = GetParameterName(totalCount);
            _parameterNameLookup.Add(Parameter.Rate, parameterName);
            _defaultParameterValues.Add(Parameter.Rate, new RedisValue(_settings.Rate.ToString()));
            _parameterNameOrder.Add(Parameter.Rate);

            if(_settings.GetExpirationUtc != null)
            {
                parameterName = GetParameterName(totalCount);
                _parameterNameLookup.Add(Parameter.Expiration, parameterName);
                _defaultParameterValues.Add(Parameter.Expiration, new RedisValue());
                _parameterNameOrder.Add(Parameter.Expiration);
            }

            return totalCount;
        }

        internal override RedisValue? GetParameterValue(Parameter parameter, int count)
        {
            if(parameter == Parameter.Expiration && _settings.GetExpirationUtc is not null)
            {
                var expiration = _settings.GetExpirationUtc();
                return new RedisValue(((long)expiration.ToRedisSeconds()).ToString());
            }

            return base.GetParameterValue(parameter, count);
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