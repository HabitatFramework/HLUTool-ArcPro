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
using System.Windows.Controls;

namespace HLU.UI.UserControls.Toolbar
{
    /// <summary>
    /// Edit box implementation used to filter features by INCID.
    /// </summary>
    internal class FilterByIncidEditBox : EditBox
    {
        #region Fields

        private ViewModelWindowMain _viewModel;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        public FilterByIncidEditBox()
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
        /// Filter by the specified Incid. Called when the user presses
        /// Enter in the edit box or the control loses keyboard focus.
        /// </summary>
        protected override void OnEnter()
        {
            if (_viewModel == null)
            {
                Enabled = false;
                DisabledTooltip = "HLU main window is not available.";
                return;
            }

            string incidText = Text?.Trim();
            if (string.IsNullOrEmpty(incidText))
            {
                // Nothing to filter on.
                return;
            }

            // Filter by the specified Incid.
            _viewModel.FilterByIncid(incidText);

            // Clear the edit box.
            Text = null;
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

            bool canFilterByIncid = _viewModel.CanFilterByIncid;

            // Enable or disable the button based on CanFilterByIncid.
            Enabled = canFilterByIncid;
        }
    }
}