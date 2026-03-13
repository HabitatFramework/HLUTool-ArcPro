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
using HLU.UI.View;
using HLU.UI.ViewModel;
using System;
using System.Linq;
using System.Windows;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.UI.UserControls.Toolbar
{
    /// <summary>
    /// Launcher button to open the Options window on the Export tab.
    /// </summary>
    internal class ExportOptionsLauncherButton : Button
    {
        #region Fields

        private WindowOptions _windowOptions;
        private ViewModelWindowOptions _viewModelOptions;
        private ViewModelWindowMain _viewModel;

        #endregion Fields

        #region Overrides

        /// <summary>
        /// Handle the click event to open the Options window on the Export tab.
        /// </summary>
        protected override void OnClick()
        {
            try
            {
                // Get the main ViewModel
                DockPane pane = FrameworkApplication.DockPaneManager.Find(ViewModelWindowMain.DockPaneID);
                if (pane == null)
                    return;

                _viewModel = pane as ViewModelWindowMain;

                // Ensure the window is shown on the UI thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // Initialize the options window and its ViewModel
                    _windowOptions = new()
                    {
                        Owner = FrameworkApplication.Current.MainWindow,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Topmost = true
                    };

                    // Initialize the ViewModel for the options window
                    _viewModelOptions = new()
                    {
                        DisplayName = "Options"
                    };

                    // Set the selected tab to "Export" in the User category
                    var exportTab = _viewModelOptions.NavigationItems
                        .FirstOrDefault(n => n.Category == "User" && n.Name == "Export");

                    // If the Export tab is found, set it as the selected view
                    if (exportTab != null)
                        _viewModelOptions.SelectedView = exportTab;

                    // Subscribe to the RequestClose event to handle closing the options window and applying settings if needed
                    _viewModelOptions.RequestClose += (applySettings) =>
                    {
                        _windowOptions.Close();
                        if (applySettings)
                            _viewModel.ApplySettings();
                    };

                    // Set the DataContext of the options window to its ViewModel and show the window
                    _windowOptions.DataContext = _viewModelOptions;
                    _windowOptions.ShowDialog();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "HLU: Options", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Update the enabled state of the button based on the visibility of the main grid in the
        /// main ViewModel. If the main ViewModel is not available, disable the button and show a
        /// tooltip indicating that the main window is not available.
        /// </summary>
        protected override void OnUpdate()
        {
            // If the ViewModel is not set, attempt to get it from the dockpane.
            if (_viewModel == null)
            {
                DockPane pane = FrameworkApplication.DockPaneManager.Find(ViewModelWindowMain.DockPaneID);
                _viewModel = pane as ViewModelWindowMain;
            }

            // If the ViewModel is still not available, disable the button and set a tooltip.
            if (_viewModel == null)
            {
                Enabled = false;
                DisabledTooltip = "HLU main window is not available.";
                return;
            }

            // Enable or disable the button based on the main grid visibility in the ViewModel.
            Enabled = _viewModel.GridMainVisibility == Visibility.Visible;
        }

        #endregion Overrides
    }
}