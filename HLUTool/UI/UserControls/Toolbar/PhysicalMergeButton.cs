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
    /// Button implementation to start the physical merge process.
    /// </summary>
    internal class PhysicalMergeButton : Button
    {
        #region Fields

        private ViewModelWindowMain _viewModel;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        public PhysicalMergeButton()
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
        /// Initiate the physical merge process. Called when the button is clicked.
        /// </summary>
        protected override void OnClick()
        {
            _viewModel.PhysicalMergeAsync();
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

            bool canPhysicallyMerge = _viewModel.CanPhysicallyMerge;

            // Enable or disable the button based on CanPhysicallyMerge.
            Enabled = canPhysicallyMerge;

            // Optional: explain why it is disabled.
            if (!canPhysicallyMerge)
            {
                DisabledTooltip = "Available only when not in bulk or OSMM update mode, editing is active, and multiple features are selected for the same INCID and TOID but different fragments.";
            }
            else
            {
                DisabledTooltip = string.Empty;
            }
        }
    }
}