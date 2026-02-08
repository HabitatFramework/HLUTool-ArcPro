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

        /// <inheritdoc />
        protected override void OnUpdate()
        {
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

        /// <inheritdoc />
        protected override void OnPopup()
        {
            AddReference("HLUTool_btnPhysicalSplit");
            AddReference("HLUTool_btnLogicalSplit");
        }
    }
}