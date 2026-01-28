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

using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using HLU.Data;
using HLU.Data.Model;
using HLU.UI.ViewModel;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HLU.UI.View
{
    //TODO: Do I need to trap <Ctrl> here for ButtonOSMMAccept and ButtonOSMMReject?
    /// <summary>
    /// Interaction logic for WindowMain.xaml
    /// </summary>
    public partial class WindowMain : UserControl
    {
        private bool _initialiseStarted;

        private ViewModelWindowMain _viewModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowMain"/> class.
        /// </summary>
        public WindowMain()
        {
            InitializeComponent();

            //TODO: Unwire for now?
            DataContextChanged += OnDataContextChanged;

            //TODO: Following code disabled for now
            // Initialise the ViewModel for the WindowMain (just to load the combo box sources).
            //_viewModel = new ViewModelWindowMain(true);

            // Set the DataContext for the WindowMain.
            //this.DataContext = _viewModel;

            //// Assign items source to the combo box columns in the secondary habitat data grid
            //DataGridComboBoxSecondaryGroup.ItemsSource = _viewModel.SecondaryGroupCodesAll;
            //DataGridComboBoxSecondaryCode.ItemsSource = _viewModel.SecondaryHabitatCodesAll;

            //// Assign items source to the combo box columns in the primary BAP data grid
            //DataGridComboBoxPrimaryBapHabitatCodes.ItemsSource = _viewModel.BapHabitatCodes;
            //DataGridComboBoxPrimaryBapDeterminationQualityCodesUser.ItemsSource = _viewModel.BapDeterminationQualityCodesAuto;
            //DataGridComboBoxPrimaryyBapInterpretationQualityCodes.ItemsSource = _viewModel.BapInterpretationQualityCodes;

            //// Assign items source to the combo box columns in the secondary BAP data grid
            //DataGridComboBoxSecondaryBapHabitatCodes.ItemsSource = _viewModel.BapHabitatCodes;
            //DataGridComboBoxSecondaryBapDeterminationQualityCodesUser.ItemsSource = _viewModel.BapDeterminationQualityCodesUser;
            //DataGridComboBoxSecondaryBapInterpretationQualityCodes.ItemsSource = _viewModel.BapInterpretationQualityCodes;
        }

        /// <summary>
        /// Handles the DataContext changed event to initialise the real dockpane view model.
        /// </summary>
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            //TODO: Needed?
            if (e.NewValue is not ViewModelWindowMain vm)
                return;

            // Ensure the tool is initialised.
            AsyncHelpers.ObserveTask(
                vm.InitializeAndCheckAsync(),
                "HLU Tool",
                "The HLU Tool encountered an error initialising.");
        }

        ///// <summary>
        ///// Handles the view loaded event.
        ///// </summary>
        //private async void OnLoaded(object sender, RoutedEventArgs e)
        //{
        //    if (_initialiseStarted)
        //        return;

        //    _initialiseStarted = true;

        //    if (DataContext is ViewModelWindowMain vm)
        //    {
        //        try
        //        {
        //            await vm.EnsureInitializedAsync();
        //            await vm.CheckActiveMapAsync();
        //        }
        //        catch (Exception ex)
        //        {
        //            ArcGIS.Desktop.Framework.Dialogs.MessageBox.Show(
        //                ex.Message,
        //                "HLU Tool");
        //        }
        //    }
        //}

        /// <summary>
        /// Handles the preview key down event for the secondary habitats data grid.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DataGridSecondaryHabitats_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Get the view model if needed.
            if (DataContext is not ViewModelWindowMain viewModel)
            {
                DockPane pane =
                    FrameworkApplication.DockPaneManager.Find(ViewModelWindowMain.DockPaneID);

                viewModel = pane as ViewModelWindowMain;
            }

            // If no view model, exit.
            if (viewModel == null)
                return;

            // Handle delete key to remove selected secondary habitats.
            if (e.Key == Key.Delete)
            {
                var grid = sender as DataGrid;
                var selectedItems = grid.SelectedItems.Cast<SecondaryHabitat>().ToList();

                // Remove the selected items from the view model collection.
                foreach (var item in selectedItems)
                {
                    viewModel.IncidSecondaryHabitats.Remove(item);
                }
            }
        }
    }
}