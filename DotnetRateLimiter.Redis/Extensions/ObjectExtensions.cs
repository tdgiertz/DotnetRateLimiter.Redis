using System;

namespace DotnetRateLimiter.Redis.Extensions;

internal static class ObjectExtensions
{
    public static T? ConvertTo<T>(this object value)
    {
        if (value is T valueAsT)
        {
            return valueAsT;
        }
        else
        {
            var type = typeof(T);
            var nullableType = Nullable.GetUnderlyingType(type);

            if(value == null)
            {
                return default;
            }

            return (T)Convert.ChangeType(value, nullableType ?? type);
        }
    }

    public static object? ConvertTo(this object value, Type type)
    {
        var methodInfo = typeof(ObjectExtensions).GetMethod(nameof(ObjectExtensions.ConvertTo), [typeof(object)]);
        methodInfo = methodInfo?.MakeGenericMethod([type]);
        return methodInfo?.Invoke(null, [value]);
    }
}