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
using System.Linq;
using System.Windows;
using ComboBox = ArcGIS.Desktop.Framework.Contracts.ComboBox;

namespace HLU.UI.UserControls.Toolbar
{
    /// <summary>
    /// Represents a ComboBox control that allows the user to select the process for updates.
    /// </summary>
    internal class ProcessComboBox : ComboBox
    {
        #region Fields

        private static ProcessComboBox _processComboBox;
        private ViewModelWindowMain _viewModel;

        private bool _isInitialized;

        private string _previousProcess;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessComboBox"/> class.
        /// </summary>
        public ProcessComboBox()
        {
            // Get this instance of the ComboBox.
            _processComboBox ??= this;

            // Get the dockpane DAML id.
            DockPane pane = FrameworkApplication.DockPaneManager.Find(ViewModelWindowMain.DockPaneID);
            if (pane == null)
                return;

            // Get the ViewModel by casting the dockpane.
            _viewModel = pane as ViewModelWindowMain;

            // Initialize the ComboBox (if it's not already).
            Initialize();

            // Sync with ViewModel's current Process value (if it's already set)
            if (!string.IsNullOrEmpty(_viewModel?.Process))
            {
                SetSelectedItem(_viewModel.Process);
            }

            // Update error state on initialization
            UpdateErrorState();
        }

        #endregion Constructor

        #region Overrides

        /// <summary>
        /// Called periodically by the framework once the tool has been created.
        /// </summary>
        protected override void OnUpdate()
        {
            // If the main ViewModel is not available, disable the ComboBox and show a tooltip indicating that the main window is not available.
            if (_viewModel == null)
            {
                Enabled = false;
                DisabledTooltip = "Unavailable when the main window is not visible.";
                return;
            }

            // Initialize the ComboBox if it's not already.
            if (!_isInitialized)
                Initialize();

            // If the tool is processing, disable the ComboBox and show a tooltip indicating why.
            if (_viewModel.IsToolProcessing)
            {
                Enabled = false;
                DisabledTooltip = "Unavailable while the tool is processing.";
                return;
            }

            // Enable or disable the ComboBox based on ReasonProcessEnabled and main window visibility.
            bool reasonProcessEnabled = _viewModel.ReasonProcessEnabled && _viewModel.GridMainVisibility == Visibility.Visible;
            Enabled = reasonProcessEnabled;

            // Set the disabled tool tip text (for when it is disabled).
            DisabledTooltip = "Unavailable when: \n\u2022 Bulk Update mode is active\n\u2022 OSMM Review mode is active\n\u2022 The main window is not visible.";

            // Update error state periodically to catch any changes
            UpdateErrorState();
        }

        /// <summary>
        /// Called when the drop-down is opened.
        /// </summary>
        protected override void OnDropDownOpened()
        {
            // Initialize the ComboBox if it's not already.
            if (!_isInitialized)
                Initialize();
        }

        /// <summary>
        /// Called when the selection changes.
        /// </summary>
        /// <param name="item"></param>
        protected override void OnSelectionChange(ComboBoxItem item)
        {
            // Store the new value
            string newProcess = item?.Text;

            // Return if the value hasn't actually changed.
            if (String.Equals(_previousProcess, newProcess, StringComparison.Ordinal))
                return;

            // Store the old value.
            _previousProcess = newProcess;

            // Update the main view model.
            _viewModel.Process = newProcess;

            // Notify the ViewModel of the selection change.
            _viewModel?.RefreshReasonProcess();
        }

        #endregion Overrides

        #region Methods

        /// <summary>
        /// Initializes the ComboBox.
        /// </summary>
        internal void Initialize()
        {
            // Ensure the ViewModel is available.
            if (_viewModel == null)
                return;

            // Clear existing items.
            Clear();

            // Clear the selected item.
            this.SelectedItem = null;
            OnSelectionChange(null);

            // Load the processs into the ComboBox list.
            LoadProcesss();

            _isInitialized = true;
        }

        /// <summary>
        /// Gets the instance of the ProcessComboBox.
        /// </summary>
        /// <returns>The instance of the <see cref="ProcessComboBox"/>.</returns>
        public static ProcessComboBox GetInstance()
        {
            // Return the instance of the ComboBox.
            return _processComboBox;
        }

        /// <summary>
        /// Loads the processs from the ViewModel and adds them to the ComboBox.
        /// </summary>
        private void LoadProcesss()
        {
            if (_viewModel?.ProcessCodes?.Length != 0)
            {
                // Add new layers from the ViewModel.
                foreach (var processCode in _viewModel.ProcessCodes)
                {
                    Add(new ComboBoxItem(processCode.description));
                }
            }
        }

        /// <summary>
        /// Gets the currently selected process.
        /// </summary>
        /// <value>The currently selected process.</value>
        public string Process
        {
            get
            {
                return (SelectedItem as ComboBoxItem)?.Text;
            }
        }

        /// <summary>
        /// Sets the selected item in the ComboBox based on the process description.
        /// </summary>
        /// <param name="processDescription">The description of the process to select.</param>
        public void SetSelectedItem(string processDescription)
        {
            if (string.IsNullOrEmpty(processDescription))
            {
                SelectedItem = null;

                // Update error state when clearing selection
                UpdateErrorState();

                return;
            }

            // Find the ComboBox item by matching the description directly
            var item = ItemCollection.FirstOrDefault(i =>
                string.Equals((i as ComboBoxItem)?.Text, processDescription, StringComparison.Ordinal));

            if (item != null)
            {
                // Temporarily store the previous value to prevent circular updates
                string temp = _previousProcess;
                _previousProcess = processDescription;

                // Set the selected item
                SelectedItem = item;

                // Restore if the selection failed
                if (SelectedItem != item)
                    _previousProcess = temp;
            }

            // Update error state
            UpdateErrorState();
        }

        /// <summary>
        /// Updates the error state and tooltip for the ComboBox.
        /// </summary>
        private void UpdateErrorState()
        {
            // Set the tooltip based on error state
            if (HasError)
            {
                Tooltip = ErrorMessage;
            }
            else
            {
                Tooltip = "Select the process for attribute updates";
            }
        }

        #endregion Methods

        #region Validation

        /// <summary>
        /// Gets the error message for the current process selection, if any.
        /// </summary>
        /// <value>The error message, or null if there is no error.</value>
        public string ErrorMessage
        {
            get
            {
                // Check if the process is required but not set
                if (_viewModel != null && _viewModel.ReasonProcessEnabled)
                {
                    if (string.IsNullOrEmpty(_viewModel.Process))
                        return "Process is required for updates";
                }
                return null;
            }
        }

        /// <summary>
        /// Gets a value indicating whether there is an error with the current process selection.
        /// </summary>
        /// <value><c>true</c> if there is an error; otherwise, <c>false</c>.</value>
        public bool HasError
        {
            get
            {
                return !string.IsNullOrEmpty(ErrorMessage);
            }
        }

        #endregion Validation
    }
}