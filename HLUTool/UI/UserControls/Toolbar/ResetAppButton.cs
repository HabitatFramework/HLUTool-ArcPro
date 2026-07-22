// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2025-2026 Andy Foy Consulting
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

using ArcGIS.Desktop.Framework.Contracts;
using HLU.UI.ViewModel;
using System;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.UI.UserControls.Toolbar
{
    /// <summary>
    /// Button implementation to reset and reopen the HLU Tool DockPane.
    /// This provides users with an explicit way to restart the application with fresh state.
    /// </summary>
    internal class ResetAppButton : Button
    {
        #region Overrides

        protected override async void OnClick()
        {
            try
            {
                // Confirm with the user before resetting
                var result = MessageBox.Show(
                    "This will restart the HLU Tool with a fresh state. Any unsaved changes will be lost.\n\nDo you want to continue?",
                    "Restart HLU Tool",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    // Reset, close, and reopen the dockpane
                    await ViewModelWindowMain.ResetAndReopenDockPaneAsync();
                }
            }
            catch (Exception ex)
            {
                // Surface hidden exceptions while debugging.
                MessageBox.Show($"Error resetting HLU Tool:{Environment.NewLine}{ex.Message}.", "HLU Tool error.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        #endregion Overrides
    }
}
