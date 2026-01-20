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
    ///     /// Checkbox implementation to select if auto zoom is on or off.
    /// </summary>
    internal class AutoSelectOnGisButton : CheckBox
    {
        #region Fields

        private ViewModelWindowMain _viewModel;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Constructor.
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

        /// <summary>
        /// Gets or sets the auto select on GIS enabled state.
        /// </summary>
        public static bool AutoSelectOnGisEnabled
        {
            get;
            private set;
        }

        /// <summary>
        /// Set the auto select enabled state. Called when the checkbox is clicked.
        /// </summary>
        protected override void OnClick()
        {
            if (_viewModel == null)
            {
                Enabled = false;
                DisabledTooltip = "HLU main window is not available.";
                return;
            }

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
    }
}