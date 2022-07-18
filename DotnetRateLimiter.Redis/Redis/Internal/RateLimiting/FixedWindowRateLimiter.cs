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
            var incrStatement = $@"
                activeAfter = redis.call('INCRBY', KEYS[1], tonumber({parameterNameLookup[Parameter.IncrementAmount]}))
                addedCount = activeAfter - activeBefore";

            var gatedIncrStatement = $@"local difference = tonumber({parameterNameLookup[Parameter.Rate]}) - activeBefore - tonumber({parameterNameLookup[Parameter.IncrementAmount]})
                if difference >= 0 then
                    {incrStatement}
                end";

            var fullIncrStatement = _settings.DoRecordOnlyOnSuccess ? gatedIncrStatement : incrStatement;

            var expirationStatement = _settings.GetExpirationUtc != null
                ? $"redis.call('EXPIREAT', KEYS[1], tonumber({parameterNameLookup[Parameter.Expiration]}))"
                : $"redis.call('EXPIRE', KEYS[1], tonumber({parameterNameLookup[Parameter.IntervalSeconds]}))";

            return $@"
                local results = {{}}
                local addedCount = 0
                local activeBefore = tonumber(redis.call('GET', KEYS[1])) or 0
                local activeAfter = 0
                {fullIncrStatement}
                if tonumber(activeAfter) == tonumber({parameterNameLookup[Parameter.IncrementAmount]}) then
                    {expirationStatement}
                end
                results[#results+1] = addedCount
                results[#results+1] = activeAfter
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

            return new RateLimitResponse { ActiveCount = activeCount, IsSuccessful = isAdded };
        }

        internal override int SetParameters()
        {
            var totalCount = base.SetParameters();

            var parameterName = GetParameterName(totalCount++);
            _parameterNameLookup.Add(Parameter.Rate, parameterName);
            _defaultParameterValues.Add(Parameter.Rate, new RedisValue(_settings.Rate.ToString()));
            _parameterNameOrder.Add(Parameter.Rate);

            if(_settings.GetExpirationUtc != null)
            {
                parameterName = GetParameterName(totalCount++);
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