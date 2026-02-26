using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using HLU.Data.Model;
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
using static HLU.Data.Model.HluDataSet;

namespace HLU.UI.UserControls.Toolbar
{
    /// <summary>
    /// Button implementation to open the options window.
    /// </summary>
    internal class OptionsWindowButton : Button
    {
        #region Fields

        private WindowOptions _windowOptions;
        private ViewModelWindowOptions _viewModelOptions;

        private HluDataSet.lut_habitat_classRow[] _habitatClasses;
        private HluDataSet.lut_secondary_groupRow[] _secondaryGroupsAll;

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

            // Get the habitat classes and secondary groups for the options window.
            _habitatClasses = _viewModel.HabitatClasses;
            _secondaryGroupsAll = _viewModel.SecondaryGroupCodesWithAll;
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

                    // Create ViewModel to which window binds and pass in necessary data
                    _viewModelOptions = new(
                        habitatClasses: _habitatClasses,
                        secondaryGroupsAll: _secondaryGroupsAll
                    )
                    {
                        DisplayName = "Options"
                    };

                    // when ViewModel asks to be closed, close window
                    _viewModelOptions.RequestClose -= _viewModelOptions_RequestClose; // Safety: avoid double subscription.
                    _viewModelOptions.RequestClose +=
                        new ViewModelWindowOptions.RequestCloseEventHandler(_viewModelOptions_RequestClose);

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
        /// Called periodically by the framework to update button state.
        /// </summary>
        protected override void OnUpdate()
        {
            if (_viewModel == null)
            {
                Enabled = false;
                DisabledTooltip = "HLU main window is not available.";
                return;
            }

            // Enable or disable the button based on the main grid visibility.
            bool isEnabled = _viewModel.GridMainVisibility == Visibility.Visible;
            Enabled = isEnabled;
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