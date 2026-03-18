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

using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using HLU.UI.ViewModel;
using HLU.Enums;
using System;

namespace HLU.UI.UserControls.Toolbar
{
    /// <summary>
    /// Toggle button to switch to Bulk OSMM Update mode.
    /// Allows applying accepted OSMM updates to multiple incids in bulk.
    /// </summary>
    internal sealed class OSMMBulkUpdateModeButton : Button
    {
        #region Fields

        private ViewModelWindowMain _viewModel;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Constructor gets the main view model from the dockpane.
        /// </summary>
        public OSMMBulkUpdateModeButton()
        {
            // Get the dockpane DAML id.
            DockPane pane = FrameworkApplication.DockPaneManager.Find(ViewModelWindowMain.DockPaneID);

            // Get the ViewModel by casting the dockpane.
            _viewModel = pane as ViewModelWindowMain;
        }

        #endregion Constructor

        #region Overrides

        /// <summary>
        /// Override OnClick to toggle the work mode in the main view model.
        /// </summary>
        protected override void OnClick()
        {
            // Set the work mode to Bulk OSMM Update.
            try
            {
                _viewModel.SetWorkMode(WorkMode.Edit | WorkMode.Bulk | WorkMode.OSMMBulk);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting Bulk OSMM Update Mode: {ex}");
            }
        }

        /// <summary>
        /// Called by framework to update button state.
        /// </summary>
        protected override void OnUpdate()
        {
            // If the ViewModel is not available, disable the button and show a tooltip indicating why.
            if (_viewModel == null)
            {
                Enabled = false;
                DisabledTooltip = "HLU main window is not available.";
                return;
            }

            // Update checked state based on current work mode
            IsChecked = _viewModel.WorkMode.HasFlag(WorkMode.OSMMBulk);

            // Enable or disable the button if Bulk OSMM Update mode can be activated.
            Enabled = _viewModel.CanOSMMBulkUpdateMode;
        }

        #endregion Overrides
    }
}