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
    /// Launcher button to open the Options window on the Filter INCIDs tab.
    /// </summary>
    internal class FilterIncidsOptionsLauncherButton : Button
    {
        private WindowOptions _windowOptions;
        private ViewModelWindowOptions _viewModelOptions;
        private ViewModelWindowMain _viewModel;

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

                    // Set the selected tab to "SQL" in the User category
                    var sqlTab = _viewModelOptions.NavigationItems
                        .FirstOrDefault(n => n.Category == "User" && n.Name == "SQL");

                    // If the SQL tab is found, set it as the selected view
                    if (sqlTab != null)
                        _viewModelOptions.SelectedView = sqlTab;

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
    }
}