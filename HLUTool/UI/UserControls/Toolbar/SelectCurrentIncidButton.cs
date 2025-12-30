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
    /// Button implementation to select the current incid on the map.
    /// </summary>
    internal class SelectCurrentIncidButton : Button
    {
        #region Fields

        private ViewModelWindowMain _viewModel;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        public SelectCurrentIncidButton()
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
        /// Select the current incid on the map. Called when the button is clicked.
        /// </summary>
        protected override async void OnClick()
        {
            if (_viewModel == null)
            {
                Enabled = false;
                DisabledTooltip = "HLU main window is not available.";
                return;
            }

            // Select the current incid.
            try
            {
                await _viewModel.SelectCurrentOnMapAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
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

            bool canSelectOnMap = _viewModel.CanSelectOnMap;

            // Enable or disable the button based on CanSelectOnMap.
            Enabled = canSelectOnMap;
        }
    }
}