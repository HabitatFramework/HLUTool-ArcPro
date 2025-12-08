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
    /// Button implementation to paste attributes to the current Incid.
    /// </summary>
    internal class PasteAttributesButton : Button
    {
        #region Fields

        private ViewModelWindowMain _viewModel;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        public PasteAttributesButton()
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
        /// Paste attribute values. Called when the button is clicked.
        /// </summary>
        protected override void OnClick()
        {
            // Paste attribute values.
            _viewModel.PasteAttributes();
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

            bool canPaste = _viewModel.CanPaste;

            // Enable or disable the button based on CanPaste.
            Enabled = canPaste;

            // Optional: explain why it is disabled.
            if (!canPaste)
            {
                DisabledTooltip = "Available only when an INCID is loaded and at least one value has been copied.";
            }
            else
            {
                DisabledTooltip = string.Empty;
            }
        }
    }
}