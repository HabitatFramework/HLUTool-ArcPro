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
using HLU.Helpers;
using HLU.UI.UserControls;
using HLU.UI.ViewModel;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ComboBox = System.Windows.Controls.ComboBox;

namespace HLU.UI.View
{
    /// <summary>
    /// Interaction logic for WindowMain.xaml
    /// </summary>
    public partial class WindowMain : UserControl
    {
        #region Fields

        private ComboBox[] _comboBoxes;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowMain"/> class.
        /// </summary>
        public WindowMain()
        {
            // Standard initialisation of the WPF Window.
            InitializeComponent();

            // Subscribe to the OnDataContextChanged event to initialise the real dockpane
            // view model when the DataContext is set.
            DataContextChanged += OnDataContextChanged;
        }

        #endregion Constructor

        #region Event handlers

        /// <summary>
        /// Handles the Loaded event for the Window.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Find all combo boxes in the main grid and store them for later use (to check
            // if any are open during key up events).
            _comboBoxes = FindControls.FindLogicalChildren<ComboBox>(this.GridMain).ToArray();
        }

        /// <summary>
        /// Handles the DataContext changed event to initialise the real dockpane view model.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data containing the old and new DataContext values.</param>
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Check if the new DataContext is the expected view model type.
            if (e.NewValue is not ViewModelWindowMain vm)
                return;

            // Ensure the tool is initialised.
            AsyncHelpers.ObserveTask(
                vm.InitializeAndCheckAsync(),
                "HLU Tool",
                "The HLU Tool encountered an error initialising.");
        }

        /// <summary>
        /// Handles the preview key down event for the secondary habitats data grid.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
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

        /// <summary>
        /// Handles the preview key down event for the Window to set the Tag properties in the view
        /// model to "Ctrl" when the Ctrl key is pressed, and clear them when released. These Tag
        /// properties are used in the XAML to apply different content to the Accept and Reject
        /// buttons in the view model when the Ctrl key is pressed, and to process the buttons
        /// differently in the view model when the Ctrl key is pressed (to accept or reject all
        /// records in the current filter rather than just the current record).
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // If the key down event is for the left or right Ctrl key,
            // set the Tag properties in the view model to "Ctrl"
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                if (DataContext is ViewModelWindowMain viewModel)
                {
                    viewModel.OSMMAcceptTag = "Ctrl";
                    viewModel.OSMMRejectTag = "Ctrl";
                }
            }
        }

        /// <summary>
        /// Handles the preview key up event for the Window to clear the Tag properties in the view
        /// model when the Ctrl key is released. Also handles key up events for movement keys (Home, End,
        /// Page Up, Page Down, Tab, Enter) to navigate through records, but only if focus is not
        /// on a text box or data grid cell, and no combo boxes are currently open (to avoid
        /// interfering with normal key behaviour in those controls). If Ctrl + Page Up or
        /// Ctrl + Page Down is pressed, moves to the first or last record respectively.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            IInputElement focusedElement = Keyboard.FocusedElement;
            Control focusedControl = focusedElement as Control;

            // Ignore keyup if focus is on a data grid cell
            if ((focusedControl != null) && (focusedControl.Parent is DataGridCell))
                return;

            // Ignore keyup if any comboboxes are currently open
            foreach (ComboBox cbx in _comboBoxes)
                if (cbx.IsDropDownOpen)
                    return;

            // Clear the Ctrl flag when Ctrl key is released
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
            {
                if (DataContext is ViewModelWindowMain viewModel)
                {
                    viewModel.OSMMAcceptTag = "";
                    viewModel.OSMMRejectTag = "";
                }
            }

            // Action any "movement" keys
            switch (e.Key)
            {
                case Key.Tab:
                    if (this.TextBoxOSMMRecordNumber.IsFocused)
                    {
                        this.TextBoxIncid.Focus();
                        this.TextBoxOSMMRecordNumber.Focus();
                    }
                    break;
                case Key.Return:
                    if (this.TextBoxRecordNumber.IsFocused)
                    {
                        this.TextBoxIncid.Focus();
                        this.TextBoxRecordNumber.Focus();
                    }
                    break;
                case Key.Home:
                    if ((focusedControl != null) && (focusedControl is TextBox))
                        return;
                    if (this.ButtonFirstRecord.Command.CanExecute(null))
                        this.ButtonFirstRecord.Command.Execute(null);
                    break;
                case Key.Prior:
                    if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                        goto case Key.Home;
                    else if (this.ButtonPreviousRecord.Command.CanExecute(null))
                        this.ButtonPreviousRecord.Command.Execute(null);
                    break;
                case Key.Next:
                    if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                        goto case Key.End;
                    else if (this.ButtonNextRecord.Command.CanExecute(null))
                        this.ButtonNextRecord.Command.Execute(null);
                    break;
                case Key.End:
                    if ((focusedControl != null) && (focusedControl is TextBox))
                        return;
                    if (this.ButtonLastRecord.Command.CanExecute(null))
                        this.ButtonLastRecord.Command.Execute(null);
                    break;
            }
        }

        #endregion Event handlers

    }
}