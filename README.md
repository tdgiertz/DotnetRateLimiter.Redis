# Redis Rate Limit with .NET 9.0 System.Threading.RateLimiting

A test project to implement the System.Threading.RateLimiting.RateLimiter abstract class on top of Redis. Lua scripting is used through the StackExchange.Redis library instead of the standard c# methods that call Redis commands. This is done to ensure that each rate limit interaction is done in an atomic fashion and the update from one interaction is completed before another starts.

## Token bucket algorithm
Uses Redis String data type. A json representation of the bucket state is stored in a value determined by the key provided. The json object contains the current count of tokens available for use and the next time that tokens will be refilled. Each time the rate limit is checked, the number of refill tokens is calculated and added to the current count. The current count of tokens will never exceed the capacity defined for bucket. If sufficient tokens exist, the count to tokens desired is subtracted from the current count and the state is saved back as json.

## Fixed window algorithm
Uses Redis String data type. The current number of rate limit requests are stored in a value determined by the key provided. If the number requested is less than or equal to the (rate - (current number of rate limit requests)), the operation is allowed and the value is updated. An expiration value is applied to the key to automatically expire the key once the window is exeeded.

## Sliding window algorithm
Uses Redis Sorted Set data type. The sorted set member is a unique value generated from the current time in ticks with a number appened to handle any collisions if more than one is added at the same instant in time. The score is equal to the time in ticks when added and can be the same across multiple members. The Redis command ZREMRANGEBYSCORE is called before determining any availability using the current time and interval to remove anything outside of the window.

## Project Structure
* DotnetRateLimiter.Redis
  * Redis implementation for token bucket, fixed and sliding window algorithms
  * A Fluent API for registering System.Threading.RateLimiting implementations
* DotnetRateLimiter.Redis.App
  * Test app for interacting with the rate limit
* DotnetRateLimiter.Redis.Tests
  * Integration tests for the basic functionality of the rate limiting 

## Notes
* The sliding window algorithm has the potential to use a lot of memory. A member is added for each rate limit request up to the maximum rate defined (when DoRecordOnlyOnSuccess == true) or up to an infinite number (when DoRecordOnlyOnSuccess == false)
* All time values are decided by the client not by Redis therefore anything with a relative offset instead of an exact time will be more reliable
  * An example of this is using GetInterval instead of GetExpirationUtc where the interval is a TimeSpan and the expiration is a DateTime
* The test project consists of integration tests more than unit tests due to difficulty testing lua scripts alone
* Due to the time-sensitive nature of the code (e.g. Redis key timeout, etc.), the tests can fail under various scenarios. Since it comes down to basically a race condition between Redis expiration and a test completing things like network latency can play a big role in and failure
 