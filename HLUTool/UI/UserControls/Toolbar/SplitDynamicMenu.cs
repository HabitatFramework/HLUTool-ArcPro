using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using HLU.UI.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace HLU.UI.UserControls.Toolbar
{
    /// <summary>
    /// Split menu populated at runtime so it can be enabled/disabled in OnUpdate.
    /// </summary>
    internal sealed class SplitDynamicMenu : DynamicMenu
    {
        #region Fields

        private ViewModelWindowMain _viewModel;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        public SplitDynamicMenu()
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

            // Enable or disable the button based on CanSplit and main grid visibility.
            bool canSplit = (_viewModel.CanSplit && _viewModel.GridMainVisibility == Visibility.Visible);
            Enabled = canSplit;

            // Set the disabled tool tip text (for when it is disabled).
            DisabledTooltip = "Available only when a physical or logical split is possible.";
        }

        /// <summary>
        /// Handles initialization logic when the popup is displayed. Adds references to UI elements required for the
        /// popup's functionality.
        /// </summary>
        /// <remarks>Override this method to customize the setup of UI components when the popup appears.
        /// This method is called automatically as part of the popup lifecycle.</remarks>
        protected override void OnPopup()
        {
            AddReference("HLUTool_btnPhysicalSplit");
            AddReference("HLUTool_btnLogicalSplit");
        }
    }
}