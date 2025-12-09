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
    /// Button implementation to start the physical split process.
    /// </summary>
    internal class PhysicalSplitButton : Button
    {
        #region Fields

        private ViewModelWindowMain _viewModel;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        public PhysicalSplitButton()
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
        /// Initiate the physical split process. Called when the button is clicked.
        /// </summary>
        protected override void OnClick()
        {
            // Call the ViewModel to start the physical split process.
            _viewModel.PhysicalSplitAsync();
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

            bool canPhysicallySplit = _viewModel.CanPhysicallySplit;

            // Enable or disable the button based on CanPhysicallySplit.
            Enabled = canPhysicallySplit;

            // Optional: explain why it is disabled.
            if (!canPhysicallySplit)
            {
                DisabledTooltip = "Available only when not in bulk or OSMM update mode, editing is active, and more than one feature is selected that share the same INCID, TOID, and fragment ID.";
            }
            else
            {
                DisabledTooltip = string.Empty;
            }
        }
    }
}