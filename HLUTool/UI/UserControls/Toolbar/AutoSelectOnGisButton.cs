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
    ///     /// Button implementation to select if auto zoom is on or off.
    /// </summary>
    internal class AutoSelectOnGisButton : Button
    {
        #region Fields

        private WindowAbout _windowAbout;
        private ViewModelWindowAbout _viewModelAbout;

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
        /// Show the about window. Called when the button is clicked.
        /// </summary>
        protected override void OnClick()
        {
            if (_viewModel == null)
            {
                Enabled = false;
                DisabledTooltip = "HLU main window is not available.";
                return;
            }

            // Toggle the auto zoom state.
            AutoSelectOnGisEnabled = !AutoSelectOnGisEnabled;

            // Update the button checked state.
            IsChecked = AutoSelectOnGisEnabled;
        }
    }
}