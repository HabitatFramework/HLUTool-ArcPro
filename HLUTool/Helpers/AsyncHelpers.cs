// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
//
// This file is part of HLUTool.
//
// HLUTool is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// HLUTool is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with HLUTool.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.Helpers
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