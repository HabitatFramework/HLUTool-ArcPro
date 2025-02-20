// The DataTools are a suite of ArcGIS Pro addins used to extract, sync
// and manage biodiversity information from ArcGIS Pro and SQL Server
// based on pre-defined or user specified criteria.
//
// Copyright © 2024 Andy Foy Consulting.
//
// This file is part of DataTools suite of programs..
//
// DataTools are free software: you can redistribute it and/or modify
// them under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// DataTools are distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with with program.  If not, see <http://www.gnu.org/licenses/>.

using HLU.Data.Model;
using HLU.UI.ViewModel;
using HLU.Data;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;

namespace HLU.UI.View
{
    /// <summary>
    /// Interaction logic for WindowMain.xaml
    /// </summary>
    public partial class WindowMain : UserControl
    {
        private ViewModelWindowMain viewModel;

        public WindowMain()
        {
            InitializeComponent();

            // Initialise the ViewModel for the WindowMain (just to load the combo box sources).
            ViewModelWindowMain viewModel;
            viewModel = new ViewModelWindowMain(true);

            // Set the DataContext for the WindowMain.
            this.DataContext = viewModel;

            // Assign items source to the combo box columns in the secondary habitat data grid
            DataGridComboBoxSecondaryGroup.ItemsSource = viewModel.SecondaryGroupCodesAll;
            DataGridComboBoxSecondaryCode.ItemsSource = viewModel.SecondaryHabitatCodesAll;

            // Assign items source to the combo box columns in the primary BAP data grid
            DataGridComboBoxPrimaryBapHabitatCodes.ItemsSource = viewModel.BapHabitatCodes;
            DataGridComboBoxPrimaryBapDeterminationQualityCodesUser.ItemsSource = viewModel.BapDeterminationQualityCodesAuto;
            DataGridComboBoxPrimaryyBapInterpretationQualityCodes.ItemsSource = viewModel.BapInterpretationQualityCodes;

            // Assign items source to the combo box columns in the secondary BAP data grid
            DataGridComboBoxSecondaryBapHabitatCodes.ItemsSource = viewModel.BapHabitatCodes;
            DataGridComboBoxSecondaryBapDeterminationQualityCodesUser.ItemsSource = viewModel.BapDeterminationQualityCodesUser;
            DataGridComboBoxSecondaryBapInterpretationQualityCodes.ItemsSource = viewModel.BapInterpretationQualityCodes;
        }

        private void DataGridSecondaryHabitats_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Set the view model if not already set.
            if (viewModel == null)
            {
                // Get the dockpane DAML id.
                DockPane pane = FrameworkApplication.DockPaneManager.Find(ViewModelWindowMain.DockPaneID);
                if (pane == null)
                    return;

                // Get the real ViewModel by casting the dockpane.
                viewModel = pane as ViewModelWindowMain;
            }

            if (e.Key == Key.Delete)
            {
                var grid = sender as DataGrid;
                var selectedItems = grid.SelectedItems.Cast<SecondaryHabitat>().ToList();
                foreach (var item in selectedItems)
                {
                    viewModel.IncidSecondaryHabitats.Remove(item);
                }
            }
        }
    }
}