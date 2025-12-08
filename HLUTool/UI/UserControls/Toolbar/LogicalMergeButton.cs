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
    /// Button implementation to start the logical merge process.
    /// </summary>
    internal class LogicalMergeButton : Button
    {
        #region Fields

        private ViewModelWindowMain _viewModel;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        public LogicalMergeButton()
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
        /// Initiate the logical merge process. Called when the button is clicked.
        /// </summary>
        protected override void OnClick()
        {
            _viewModel.LogicalMergeAsync();
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

            bool canLogicallyMerge = _viewModel.CanLogicallyMerge;

            // Enable or disable the button based on CanLogicallyMerge.
            Enabled = canLogicallyMerge;

            // Optional: explain why it is disabled.
            if (!canLogicallyMerge)
            {
                DisabledTooltip = "Available only when not in bulk or OSMM update mode, editing is active, and multiple features are selected from more than one INCID.";
            }
            else
            {
                DisabledTooltip = string.Empty;
            }
        }
    }
}