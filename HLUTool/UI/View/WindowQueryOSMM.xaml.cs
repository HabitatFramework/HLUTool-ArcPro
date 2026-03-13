// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2019 London & South East Record Centres (LaSER)
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

using System.Windows;
using System.Reflection;
using System.Windows.Controls;
using HLU.UI.ViewModel;
using ArcGIS.Desktop.Framework.Controls;

namespace HLU.UI.View
{
    /// <summary>
    /// Interaction logic for WindowQueryOSMM.xaml
    /// </summary>
    public partial class WindowQueryOSMM : ProWindow
    {
        public WindowQueryOSMM()
        {
            InitializeComponent();
        }

        #region Methods

        /// <summary>
        /// Handles the SelectionChanged event of the OSMMUpdates DataGrid. When a row is selected,
        /// it calls the OSMMUpdatesSelectedRow method in the ViewModel, passing the selected row as
        /// a parameter.
        /// </summary>
        /// <param name="sender">The DataGrid that triggered the event.</param>
        /// <param name="e">The event arguments.</param>
        void OSMMUpdates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender != null)
            {
                ViewModelWindowQueryOSMM _viewModel = (ViewModelWindowQueryOSMM)this.DataContext;

                OSMMUpdates selectedRow = (OSMMUpdates)DataGridOSMMUpdatesSummary.SelectedItem;

                _viewModel.OSMMUpdatesSelectedRow(selectedRow);
            }
        }

        #endregion Methods
    }
}