using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace HLU
{
    internal static class AsyncHelpers
    {
        /// <summary>
        /// Runs a task without awaiting it, but safely observes exceptions.
        /// </summary>
        /// <param name="task">The task to run.</param>
        /// <param name="onException">Optional exception callback.</param>
        public static async void SafeFireAndForget(Task task, Action<Exception> onException = null)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                onException?.Invoke(ex);

                // Fallback if no handler is supplied.
                Debug.WriteLine(ex);
            }
        }
    }
}