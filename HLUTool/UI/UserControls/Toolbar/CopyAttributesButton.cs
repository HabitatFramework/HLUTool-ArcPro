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
    /// Button implementation to copy attributes from the current Incid.
    /// </summary>
    internal class CopyAttributesButton : Button
    {
        #region Fields

        private ViewModelWindowMain _viewModel;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        public CopyAttributesButton()
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
        /// Copy attribute values. Called when the button is clicked.
        /// </summary>
        protected override void OnClick()
        {
            // Copy attribute values.
            _viewModel.CopyAttributes();
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

            bool canCopy = _viewModel.CanCopy;

            // Enable or disable the button based on CanCopy.
            Enabled = canCopy;

            // Optional: explain why it is disabled.
            if (!canCopy)
            {
                DisabledTooltip = "Available only when an INCID is loaded and at least one copy option is enabled.";
            }
            else
            {
                DisabledTooltip = string.Empty;
            }
        }
    }
}