using System;
using System.Diagnostics;
using System.Threading.Tasks;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

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

        /// <summary>
        /// Observes a task and reports any exceptions to the user.
        /// </summary>
        /// <param name="task">The task to observe.</param>
        /// <param name="title">The message box title.</param>
        /// <param name="message">A user-friendly message prefix.</param>
        public static async void ObserveTask(
            Task task,
            string title,
            string message)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(
                        $"{message}{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                        title);
                });

                Debug.WriteLine(ex);
            }
        }
    }
}