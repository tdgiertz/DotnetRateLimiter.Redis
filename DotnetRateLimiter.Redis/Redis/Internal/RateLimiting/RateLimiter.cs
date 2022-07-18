using DotnetRateLimiter.RateLimiting;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DotnetRateLimiter.Redis.Internal.RateLimiting
{
    internal abstract class RateLimiter<TSettings> where TSettings : RateLimitRequestSettings
    {
        protected readonly IConnectionMultiplexer _redis;
        protected readonly TSettings _settings;
        protected readonly string _limitScript;
        protected readonly Dictionary<Parameter, RedisValue> _defaultParameterValues = new();
        protected readonly Dictionary<Parameter, string> _parameterNameLookup = new();
        protected readonly List<Parameter> _parameterNameOrder = new();
        protected readonly List<string> _parameterNames = new();

        public RateLimiter(IConnectionMultiplexer redis, TSettings settings)
        {
            _redis = redis;
            _settings = settings;
            SetParameters();
            _limitScript = Regex.Replace(GetLuaScript(_parameterNameLookup), @"\s+", " ");

            CheckArguments();
            InitialSetup();
        }

        public RateLimitResponse Limit(int count)
        {
            CheckCountArgument(count);

            var parameters = GetOrderedParameters(count);

            var result = _redis.GetDatabase(_settings.DatabaseId).ScriptEvaluate(_limitScript, new RedisKey[] { _settings.Key }, parameters);

            return GetRateLimitResponse(count, result);
        }

        public Task<RateLimitResponse> LimitAsync(int count, CancellationToken cancellationToken = default)
        {
            CheckCountArgument(count);
            
            var parameters = GetOrderedParameters(count);

            return _redis.GetDatabase(_settings.DatabaseId).ScriptEvaluateAsync(_limitScript, new RedisKey[] { _settings.Key }, parameters)
                    .ContinueWith(async task => GetRateLimitResponse(count, await task)).Unwrap();
        }

        public bool Delete()
        {
            CheckKeyArgument(_settings.Key);

            return _redis.GetDatabase(_settings.DatabaseId).KeyDelete(_settings.Key);
        }

        public Task<bool> DeleteAsync(CancellationToken cancellationToken = default)
        {
            return _redis.GetDatabase(_settings.DatabaseId).KeyDeleteAsync(_settings.Key);
        }

        internal virtual void CheckArguments()
        {
            CheckKeyArgument(_settings.Key);
            if(_settings.GetInterval == null)
            {
                throw new ArgumentException($"Argument {nameof(_settings.GetInterval)} must be set.");
            }
        }

        internal void CheckKeyArgument(string key)
        {
            if(string.IsNullOrEmpty(key))
            {
                throw new ArgumentException($"Argument Key must contain a value.");
            }
        }

        internal void CheckCountArgument(int count)
        {
            if(count <= 0)
            {
                throw new ArgumentException($"Argument count must be greater than 0.");
            }
        }

        internal abstract void InitialSetup();
        internal abstract RateLimitResponse GetRateLimitResponse(int count, RedisResult redisResult);
        internal abstract string GetLuaScript(Dictionary<Parameter, string> parameterNameLookup);

        internal virtual int SetParameters()
        {
            if(_settings == null)
            {
                throw new ArgumentNullException(nameof(_settings));
            }

            var totalCount = 0;

            var parameterName = GetParameterName(totalCount++);
            _parameterNameLookup.Add(Parameter.IntervalTicks, parameterName);
            _defaultParameterValues.Add(Parameter.IntervalTicks, new RedisValue());
            _parameterNameOrder.Add(Parameter.IntervalTicks);

            parameterName = GetParameterName(totalCount++);
            _parameterNameLookup.Add(Parameter.IntervalSeconds, parameterName);
            _defaultParameterValues.Add(Parameter.IntervalSeconds, new RedisValue());
            _parameterNameOrder.Add(Parameter.IntervalSeconds);

            parameterName = GetParameterName(totalCount++);
            _parameterNameLookup.Add(Parameter.Now, parameterName);
            _defaultParameterValues.Add(Parameter.Now, new RedisValue());
            _parameterNameOrder.Add(Parameter.Now);

            parameterName = GetParameterName(totalCount++);
            _parameterNameLookup.Add(Parameter.IncrementAmount, parameterName);
            _defaultParameterValues.Add(Parameter.IncrementAmount, new RedisValue());
            _parameterNameOrder.Add(Parameter.IncrementAmount);

            return totalCount;
        }

        private RedisValue[] GetOrderedParameters(int count)
        {
            if(_parameterNameOrder.Count == 0)
            {
                throw new InvalidOperationException();
            }

            var index = 0;
            var parameters = new RedisValue[_parameterNameOrder.Count];
            foreach(var parameter in _parameterNameOrder)
            {
                var parameterValue = GetParameterValue(parameter, count);

                if(parameterValue.HasValue)
                {
                    parameters[index++] = parameterValue.Value;
                    continue;
                }

                parameters[index++] = _defaultParameterValues[parameter];
            }

            return parameters;
        }

        internal virtual RedisValue? GetParameterValue(Parameter parameter, int count)
        {
            if(parameter == Parameter.IncrementAmount)
            {
                return new RedisValue(count.ToString());
            }
            if(parameter == Parameter.Now)
            {
                var now = _settings.GetNowUtc?.Invoke() ?? DateTime.UtcNow;
                return new RedisValue(now.Ticks.ToString());
            }
            if(parameter == Parameter.IntervalSeconds)
            {
                var interval = _settings.GetInterval();
                return new RedisValue(interval.TotalSeconds.ToString());
            }
            if(parameter == Parameter.IntervalTicks)
            {
                var interval = _settings.GetInterval();
                return new RedisValue(interval.Ticks.ToString());
            }

            return null;
        }

        internal static string GetParameterName(int index)
        {
            return $"ARGV[{index + 1}]";
        }

        internal static long? RedisValueToLong(RedisValue redisValue)
        {
            if(redisValue.TryParse(out long value))
            {
                return value;
            }

            return null;
        }
    }
}