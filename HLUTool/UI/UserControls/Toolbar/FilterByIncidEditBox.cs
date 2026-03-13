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
using System.Text.RegularExpressions;
using ArcGIS.Desktop.Framework.Contracts;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using HLU.UI;
using Xceed.Wpf.Toolkit.Primitives;
using System.Windows.Controls;
using Azure.Core;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.UI.UserControls.Toolbar
{
    /// <summary>
    /// Edit box implementation used to filter features by INCID.
    /// </summary>
    internal partial class FilterByIncidEditBox : EditBox
    {
        #region Fields

        private ViewModelWindowMain _viewModel;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        public FilterByIncidEditBox()
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
        /// Filter by the specified Incid. Called when the user presses
        /// Enter in the edit box or the control loses keyboard focus.
        /// </summary>
        protected override async void OnEnter()
        {
            // If the ViewModel is not available, disable the button and show a tooltip indicating why.
            if (_viewModel == null)
            {
                Enabled = false;
                DisabledTooltip = "HLU main window is not available.";
                return;
            }

            // Get the text from the edit box and trim whitespace.
            string incidText = Text?.Trim();
            if (String.IsNullOrEmpty(incidText))
            {
                // Nothing to filter on.
                return;
            }

            // Validate format nnnn:nnnnnnn.
            if (!IncidRegex().IsMatch(incidText))
            {
                MessageBox.Show("Incid must be in the format 'nnnn:nnnnnnn'.", "HLU Tool", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Filter by the specified Incid.
            await _viewModel.FilterByIncidAsync(incidText);

            // Clear the edit box.
            Text = null;
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
                DisabledTooltip = "HLU main window is not available.";
                return;
            }

            // Enable or disable the button based on CanFilterByIncid and main grid visibility.
            bool canFilterByIncid = _viewModel.CanFilterByIncid && _viewModel.GridMainVisibility == Visibility.Visible;
            Enabled = canFilterByIncid;
        }

        #endregion Overrides

        #region Regex

        // Regular expression to validate Incid format nnnn:nnnnnnn.
        [GeneratedRegex(@"^\d{4}:\d{7}$")]
        private static partial Regex IncidRegex();

        #endregion Regex
    }
}