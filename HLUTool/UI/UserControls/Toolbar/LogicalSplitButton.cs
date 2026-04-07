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

using HLU.UI.View;
using HLU.UI.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using HLU.UI;
using Xceed.Wpf.Toolkit.Primitives;

namespace HLU.UI.UserControls.Toolbar
{
    /// <summary>
    /// Button implementation to start the logical split process.
    /// </summary>
    internal class LogicalSplitButton : Button
    {
        #region Fields

        private ViewModelWindowMain _viewModel;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        public LogicalSplitButton()
        {
            // Get the dockpane DAML id.
            DockPane pane = FrameworkApplication.DockPaneManager.Find(ViewModelWindowMain.DockPaneID);
            if (pane == null)
                return;

            // Get the ViewModel by casting the dockpane.
            _viewModel = pane as ViewModelWindowMain;
        }

        #endregion Constructor

        #region Overrides

        /// <summary>
        /// Initiate the logical split process. Called when the button is clicked.
        /// </summary>
        protected override async void OnClick()
        {
            // Logically split the features.
            try
            {
                await _viewModel.LogicalSplitAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        /// <summary>
        /// Called periodically by the framework to update button state.
        /// </summary>
        protected override void OnUpdate()
        {
            // If the main ViewModel is not available, disable the button and show a tooltip indicating that the main window is not available.
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

            // Enable or disable the button based on CanLogicallySplit and main window visibility.
            bool canLogicallySplit = _viewModel.CanLogicallySplit && _viewModel.GridMainVisibility == Visibility.Visible;
            Enabled = canLogicallySplit;

            // Set the disabled tool tip text (for when it is disabled).
            DisabledTooltip = "Unavailable when:\n\u2022 There are no features to split\n\u2022 The main window is not visible";
        }

        #endregion Overrides
    }
}