using HLU.UI.View;
using HLU.UI.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using ArcGIS.Desktop.Framework;
using System.Text.RegularExpressions;
using ArcGIS.Desktop.Framework.Contracts;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using HLU.UI;
using Xceed.Wpf.Toolkit.Primitives;
using System.Windows.Controls;
using Azure.Core;

namespace HLU.UI.UserControls.Toolbar
{
    /// <summary>
    /// Edit box implementation used to filter features by INCID.
    /// </summary>
    internal partial class FilterByIncidEditBox : EditBox
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
        protected override async void OnEnter()
        {
            if (_viewModel == null)
            {
                Enabled = false;
                DisabledTooltip = "HLU main window is not available.";
                return;
            }

            string incidText = Text?.Trim();
            if (String.IsNullOrEmpty(incidText))
            {
                // Nothing to filter on.
                return;
            }

            // Validate format nnnn:nnnnnnn.
            if (!IncidRegex().IsMatch(incidText))
            {
                MessageBox.Show("Incid must be in the format 'nnnn:nnnnnnn'.", "HLU Tool", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Filter by the specified Incid.
            await _viewModel.FilterByIncidAsync(incidText);

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

            // Enable or disable the button based on CanFilterByIncid and main grid visibility.
            bool canFilterByIncid = _viewModel.CanFilterByIncid && _viewModel.GridMainVisibility == Visibility.Visible;
            Enabled = canFilterByIncid;
        }

        #region Regex

        [GeneratedRegex(@"^\d{4}:\d{7}$")]
        private static partial Regex IncidRegex();

        #endregion Regex
    }
}