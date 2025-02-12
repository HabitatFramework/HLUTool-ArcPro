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
using System.Windows.Controls;

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

            // Initialize ViewModel and set DataContext
            viewModel = new ViewModelWindowMain();
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
    }
}