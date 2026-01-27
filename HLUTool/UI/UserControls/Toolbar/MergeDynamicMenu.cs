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
    /// Merge menu populated at runtime so it can be enabled/disabled in OnUpdate.
    /// </summary>
    internal sealed class MergeDynamicMenu : DynamicMenu
    {
        #region Fields

        private ViewModelWindowMain _viewModel;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        public MergeDynamicMenu()
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

            // Enable or disable the button based on CanMerge and main grid visibility.
            bool CanMerge = (_viewModel.CanMerge && _viewModel.GridMainVisibility == Visibility.Visible);
            Enabled = CanMerge;

            // Optional: explain why it is disabled.
            if (!CanMerge)
            {
                DisabledTooltip = "Available only when physical or logical merge are possible.";
            }
            else
            {
                DisabledTooltip = string.Empty;
            }
        }

        /// <inheritdoc />
        protected override void OnPopup()
        {
            AddReference("HLUTool_btnPhysicalMerge");
            AddReference("HLUTool_btnLogicalMerge");
        }
    }
}