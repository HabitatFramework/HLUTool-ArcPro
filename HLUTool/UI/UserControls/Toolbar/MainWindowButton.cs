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

using HLU.UI.ViewModel;
using System;
using System.Collections.Generic;
using ArcGIS.Desktop.Framework.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework.Dialogs;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.UI.UserControls.Toolbar
{
    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class MainWindowButton : Button
    {
        #region Overrides

        protected override async void OnClick()
        {
            try
            {
                // Show the dock pane. If connection settings are needed, InitializeOnceAsync
                // will prompt the user before proceeding with initialisation.
                await ViewModelWindowMain.ShowDockPane();
            }
            catch (Exception ex)
            {
                // Surface hidden exceptions while debugging.
                MessageBox.Show($"Error starting HLU Tool:{Environment.NewLine}{ex.Message}.", "HLU Tool error.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        #endregion Overrides
    }
}