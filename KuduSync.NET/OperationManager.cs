using System;
using System.Threading;

namespace KuduSync.NET
{
    internal static class OperationManager
    {
        private const int DefaultRetries = 10;
        private const int DefaultDelayBeforeRetry = 250; // 250 ms

        public static void Attempt(Action action, int retries = DefaultRetries, int delayBeforeRetry = DefaultDelayBeforeRetry)
        {
            OperationManager.Attempt<object>(() =>
            {
                action();
                return null;
            }, retries, delayBeforeRetry);
        }

        public static T Attempt<T>(Func<T> action, int retries = DefaultRetries, int delayBeforeRetry = DefaultDelayBeforeRetry)
        {
            T result = default(T);

            while (retries > 0)
            {
                try
                {
                    result = action();
                    break;
                }
                catch
                {
                    retries--;
                    if (retries == 0)
                    {
                        throw;
                    }
                }

                Thread.Sleep(delayBeforeRetry);
            }

            return result;
        }
    }
}
