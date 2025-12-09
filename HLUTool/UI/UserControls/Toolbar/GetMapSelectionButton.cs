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
    /// Button implementation to get the map selection.
    /// </summary>
    internal class GetMapSelectionButton : Button
    {
        #region Fields

        private ViewModelWindowMain _viewModel;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        public GetMapSelectionButton()
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
        /// Read the map selection. Called when the button is clicked.
        /// </summary>
        protected override void OnClick()
        {
            // Call the ViewModel to read the map selection.
            _viewModel.ReadMapSelectionAsync(true);
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

            bool canReadMapSelection = _viewModel.CanReadMapSelection;

            // Enable or disable the button based on CanReadMapSelection.
            Enabled = canReadMapSelection;

            // Optional: explain why it is disabled.
            if (!canReadMapSelection)
            {
                DisabledTooltip = "Available only when not in bulk update mode.";
            }
            else
            {
                DisabledTooltip = string.Empty;
            }
        }
    }
}