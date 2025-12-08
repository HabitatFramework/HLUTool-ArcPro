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
    /// Button implementation to open the options window.
    /// </summary>
    internal class OptionsWindowButton : Button
    {
        #region Fields

        private WindowOptions _windowOptions;
        private ViewModelOptions _viewModelOptions;

        private ViewModelWindowMain _viewModel;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        public OptionsWindowButton()
        {
            // Get the dockpane DAML id.
            DockPane pane = FrameworkApplication.DockPaneManager.Find(ViewModelWindowMain.DockPaneID);
            if (pane == null)
                return;

            // Get the ViewModel by casting the dockpane.
            _viewModel = pane as ViewModelWindowMain;
        }

        #endregion Constructor

        /// <summary>
        /// Show the options window. Called when the button is clicked.
        /// </summary>
        protected override void OnClick()
        {
            try
            {
                // Ensure the window is shown on the UI thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // Create Options window
                    _windowOptions = new()
                    {
                        // Set ArcGIS Pro as the parent
                        Owner = FrameworkApplication.Current.MainWindow,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Topmost = true
                    };

                    // create ViewModel to which window binds
                    _viewModelOptions = new()
                    {
                        DisplayName = "Options"
                    };

                    // when ViewModel asks to be closed, close window
                    _viewModelOptions.RequestClose -= _viewModelOptions_RequestClose; // Safety: avoid double subscription.
                    _viewModelOptions.RequestClose +=
                        new ViewModelOptions.RequestCloseEventHandler(_viewModelOptions_RequestClose);

                    // allow all controls in window to bind to ViewModel by setting DataContext
                    _windowOptions.DataContext = _viewModelOptions;

                    // show window
                    _windowOptions.ShowDialog();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "HLU: Options", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Save the options settings when the options window is closed.
        /// </summary>
        /// <param name="applySettings">if set to <c>true</c> [save settings].</param>
        void _viewModelOptions_RequestClose(bool applySettings)
        {
            _viewModelOptions.RequestClose -= _viewModelOptions_RequestClose;
            _windowOptions.Close();

            // Apply updated settings in the main window.
            if (applySettings)
                _viewModel.ApplySettings();
        }
    }
}