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

using System.Threading.Tasks;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// View model for registering newly drawn GIS features against the database.
    /// Handles both "all features to one INCID" and "each feature to its own INCID" modes.
    /// </summary>
    /// <remarks>
    /// Full implementation is added in stage 4/5. This stub exists so that the ribbon
    /// controls and ViewModel wiring compile before the insert logic is written.
    /// </remarks>
    internal class ViewModelWindowMainFeatureInsert
    {
        #region Fields

        private readonly ViewModelWindowMain _viewModelMain;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ViewModelWindowMainFeatureInsert"/> class.
        /// </summary>
        /// <param name="viewModelMain">The main window view model.</param>
        public ViewModelWindowMainFeatureInsert(ViewModelWindowMain viewModelMain)
        {
            _viewModelMain = viewModelMain;
        }

        #endregion Constructor

        #region Methods

        /// <summary>
        /// Registers all selected new (null-incid) features under a single new INCID.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// <see langword="true"/> if the operation succeeded; otherwise <see langword="false"/>.
        /// </returns>
        internal Task<bool> InsertFeaturesSameIncidAsync()
        {
            // TODO: implement in stage 4/5.
            return Task.FromResult(false);
        }

        /// <summary>
        /// Registers each selected new (null-incid) feature under its own new INCID.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous operation. The task result contains
        /// <see langword="true"/> if the operation succeeded; otherwise <see langword="false"/>.
        /// </returns>
        internal Task<bool> InsertFeaturesSeparateIncidsAsync()
        {
            // TODO: implement in stage 4/5.
            return Task.FromResult(false);
        }

        #endregion Methods
    }
}
