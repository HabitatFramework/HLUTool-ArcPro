// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2025-2026 Andy Foy Consulting
//
// This file is part of HLUTool.
//
// HLUTool is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// HLUTool is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with HLUTool.  If not, see <http://www.gnu.org/licenses/>.

using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using HLU.UI.ViewModel;
using System;
using System.Windows;

namespace HLU.UI.UserControls.Toolbar
{
    /// <summary>
    /// Button implementation that registers each currently selected new (null-INCID) GIS feature
    /// under its own new INCID, using habitat attributes already on the feature, and tags history
    /// with the OSMMLoad operation code.
    /// </summary>
    internal class BulkLoadButton : Button
    {
        #region Fields

        private ViewModelWindowMain _viewModel;

        #endregion Fields

        #region Constructor

        public BulkLoadButton()
        {
            DockPane pane = FrameworkApplication.DockPaneManager.Find(ViewModelWindowMain.DockPaneID);
            if (pane == null)
                return;

            _viewModel = pane as ViewModelWindowMain;
        }

        #endregion Constructor

        #region Overrides

        protected override async void OnClick()
        {
            try
            {
                await _viewModel.OSMMLoadAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        protected override void OnUpdate()
        {
            if (_viewModel == null)
            {
                Enabled = false;
                DisabledTooltip = "Unavailable when the main window is not loaded.";
                return;
            }

            if (_viewModel.IsToolProcessing)
            {
                Enabled = false;
                DisabledTooltip = "Unavailable while the tool is processing.";
                return;
            }

            Enabled = _viewModel.CanBulkLoad &&
                      _viewModel.GridMainVisibility == Visibility.Visible;

            DisabledTooltip = "Unavailable when:\n" +
                "\u2022 The tool is not in normal update mode\n" +
                "\u2022 The main window is not visible";
        }

        #endregion Overrides
    }
}