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

using ArcGIS.Desktop.Framework.Dialogs;
using HLU.Enums;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// Contains the view model for the main window reassign features functionality.
    /// </summary>
    internal class ViewModelWindowMainReassign
    {
        #region Fields

        private readonly ViewModelWindowMain _viewModelMain;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="viewModelMain">The main window view model.</param>
        public ViewModelWindowMainReassign(ViewModelWindowMain viewModelMain)
        {
            _viewModelMain = viewModelMain;
        }

        #endregion Constructor

        #region Methods

        /// <summary>
        /// Initiates the reassign features process.
        /// </summary>
        public void InitiateReassign()
        {
            if (_viewModelMain == null)
                return;

            // Guard: must be in normal edit mode with an editable active layer.
            if (!_viewModelMain.CanReassign)
            {
                _viewModelMain.ShowError(
                    "Reassign Features is only available in normal edit mode with an editable active layer.",
                    MessageCategory.Update);
                return;
            }

            // TODO: implement the full reassign dialog and process in a future iteration.
            MessageBox.Show(
                "The Reassign Features process is not yet implemented.",
                "HLU: Reassign Features",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        #endregion Methods
    }
}
