using System;
using System.Collections.Generic;

namespace DotnetRateLimiter.Redis.Test
{
    public static class NonBlockingConsole
    {
        private static System.Threading.SemaphoreSlim _semaphore = new(1, 1);
        private static System.Collections.Concurrent.BlockingCollection<object> _queue = new System.Collections.Concurrent.BlockingCollection<object>();

        static NonBlockingConsole()
        {
            var thread = new System.Threading.Thread(
              () =>
              {
                  while(true)
                  {
                      Console.WriteLine(_queue.Take());
                  }
              })
            {
                IsBackground = true
            };
            thread.Start();
        }

        public static async void WriteLine(object value)
        {
            await _semaphore.WaitAsync();

            try
            {
                _queue.Add(value);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public static async void WriteLines(IEnumerable<object> values)
        {
            await _semaphore.WaitAsync();

            try
            {
                foreach(var value in values)
                {
                    _queue.Add(value);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}