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

using ArcGIS.Desktop.Framework.Controls;
using HLU.UI.ViewModel;
using System.Windows.Controls;

namespace HLU.UI.View
{
    /// <summary>
    /// Interaction logic for WindowQueryOSMM.xaml
    /// </summary>
    public partial class WindowQueryOSMM : ProWindow
    {
        /// <summary>
        /// Constructor initializes the WindowQueryOSMM and sets up a Loaded event handler to call
        /// the LoadAsync method in the ViewModel when the window is loaded. This ensures that any
        /// necessary data is loaded and ready
        /// </summary>
        public WindowQueryOSMM()
        {
            InitializeComponent();

            // Set up the Loaded event handler to call LoadAsync in the ViewModel when the window is loaded.
            Loaded += async (_, _) =>
            {
                if (DataContext is ViewModelWindowQueryOSMM vm)
                    await vm.LoadAsync();
            };
        }

        #region Methods

        /// <summary>
        /// Handles the SelectionChanged event of the OSMMUpdates DataGrid. When a row is selected,
        /// it calls the OSMMUpdatesSelectedRow method in the ViewModel, passing the selected row as
        /// a parameter.
        /// </summary>
        /// <param name="sender">The DataGrid that triggered the event.</param>
        /// <param name="e">The event arguments.</param>
        private void OSMMUpdates_SelectionChanged(object sender, SelectionChangedEventArgs e)
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