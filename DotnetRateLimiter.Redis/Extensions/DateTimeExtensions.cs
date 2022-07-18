using System;

namespace DotnetRateLimiter.Redis.Extensions
{
    internal static class DateTimeExtensions
    {
        private static readonly DateTime _unixDateTime = new DateTime(1970, 1, 1);

        public static double ToRedisSeconds(this DateTime dateTime)
        {
            return dateTime.Subtract(_unixDateTime).TotalSeconds;
        }

        public static double ToRedisMilliseconds(this DateTime dateTime)
        {
            return dateTime.Subtract(_unixDateTime).TotalMilliseconds;
        }

        public static long ToRedisTicks(this DateTime dateTime)
        {
            return dateTime.Subtract(_unixDateTime).Ticks;
        }
    }
}