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
using HLU.Properties;
using HLU.UI;
using HLU.UI.View;
using HLU.UI.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Xceed.Wpf.Toolkit.Primitives;

namespace HLU.UI.UserControls.Toolbar
{
    /// <summary>
    /// Button implementation to toggle auto select on GIS. When enabled, selecting a record in the
    /// UI will automatically select the corresponding feature on the map.
    /// </summary>
    internal class AutoSelectOnGisButton : CheckBox
    {
        #region Fields

        private ViewModelWindowMain _viewModel;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoSelectOnGisButton"/> class.
        /// </summary>
        public AutoSelectOnGisButton()
        {
            // Get the dockpane DAML id.
            DockPane pane = FrameworkApplication.DockPaneManager.Find(ViewModelWindowMain.DockPaneID);
            if (pane == null)
                return;

            // Get the ViewModel by casting the dockpane.
            _viewModel = pane as ViewModelWindowMain;

            // Set the checkbox value and state.
            AutoSelectOnGisEnabled = _viewModel.AutoSelectOnGis;
            IsChecked = AutoSelectOnGisEnabled;
        }

        #endregion Constructor

        #region Overrides

        /// <summary>
        /// Set the auto select enabled state. Called when the checkbox is clicked.
        /// </summary>
        protected override void OnClick()
        {
            // Toggle the auto select state.
            AutoSelectOnGisEnabled = !AutoSelectOnGisEnabled;

            // Update the checkbox state.
            IsChecked = AutoSelectOnGisEnabled;

            // Update the main window state.
            _viewModel.SetAutoSelectOnGis(AutoSelectOnGisEnabled);
        }

        /// <summary>
        /// Called periodically by the framework to update checkbox state.
        /// </summary>
        protected override void OnUpdate()
        {
            // If the main ViewModel is not available, disable the button and show a tooltip indicating that the main window is not available.
            if (_viewModel == null)
            {
                Enabled = false;
                DisabledTooltip = "HLU main window is not available.";
                return;
            }

            // If the tool is processing, disable the button and show a tooltip indicating why.
            if (_viewModel.IsToolProcessing)
            {
                Enabled = false;
                DisabledTooltip = "Unavailable while the tool is processing.";
                return;
            }

            // Enable or disable the button based on the main grid visibility.
            bool isEnabled = _viewModel.GridMainVisibility == Visibility.Visible;
            Enabled = isEnabled;
        }

        #endregion Overrides

        #region Properties

        /// <summary>
        /// Gets or sets the auto select on GIS enabled state.
        /// </summary>
        /// <value><c>true</c> if auto select on GIS is enabled; otherwise, <c>false</c>.</value>
        public static bool AutoSelectOnGisEnabled
        {
            get;
            private set;
        }

        #endregion Properties
    }
}