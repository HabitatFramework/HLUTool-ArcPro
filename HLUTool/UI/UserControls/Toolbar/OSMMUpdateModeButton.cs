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
    /// Toggle button to switch to OSMM Review mode.
    /// Allows review and acceptance/rejection of OSMM updates for individual incids.
    /// </summary>
    internal sealed class OSMMReviewModeButton : Button
    {
        #region Fields

        private ViewModelWindowMain _viewModel;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Constructor gets the main view model from the dockpane.
        /// </summary>
        public OSMMReviewModeButton()
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
        protected override async void OnClick()
        {
            // Set the work mode to OSMM Review.
            try
            {
                await _viewModel.SetWorkMode(WorkMode.Edit | WorkMode.OSMMReview);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting OSMM Review Mode: {ex}");
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
                DisabledTooltip = "Unavailable when the main window is not visible.";
                return;
            }

            // If the tool is processing, disable the button and show a tooltip indicating why.
            if (_viewModel.IsToolProcessing)
            {
                Enabled = false;
                DisabledTooltip = "Unavailable while the tool is processing.";
                return;
            }

            // Update checked state based on current work mode
            IsChecked = _viewModel.WorkMode.HasFlag(WorkMode.OSMMReview);

            // Enable or disable the button if OSMM Review mode can be activated.
            Enabled = _viewModel.CanOSMMReviewMode;

            // Set the disabled tool tip text (for when it is disabled).
            DisabledTooltip = "Unavailable when:\n\u2022 No OSMM updates are pending\n\u2022 The user is not authorised\n\u2022 Bulk Update mode is active\n\u2022 The main window is not visible";
        }

        #endregion Overrides
    }
}