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
using HLU.Data;
using HLU.UI.ViewModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ComboBox = ArcGIS.Desktop.Framework.Contracts.ComboBox;

namespace HLU.UI.UserControls.Toolbar
{
    /// <summary>
    /// Represents a ComboBox control that allows the user to select the reason for updates.
    /// </summary>
    internal class ReasonComboBox : ComboBox
    {
        #region Fields

        private static ReasonComboBox _reasonComboBox;
        private ViewModelWindowMain _viewModel;

        private bool _isInitialized;
        private bool _isEnabled; //TODO: Needed?

        private string _previousReason;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="ReasonComboBox"/> class.
        /// </summary>
        public ReasonComboBox()
        {
            // Get this instance of the ComboBox.
            _reasonComboBox ??= this;

            // Get the dockpane DAML id.
            DockPane pane = FrameworkApplication.DockPaneManager.Find(ViewModelWindowMain.DockPaneID);
            if (pane == null)
                return;

            // Get the ViewModel by casting the dockpane.
            _viewModel = pane as ViewModelWindowMain;

            // Initialize the ComboBox (if it's not already).
            Initialize();

            // Sync with ViewModel's current Reason value (if it's already set)
            if (!string.IsNullOrEmpty(_viewModel?.Reason))
            {
                SetSelectedItem(_viewModel.Reason);
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
                DisabledTooltip = "HLU main window is not available.";
                return;
            }

            // Initialize the ComboBox if it's not already.
            if (!_isInitialized)
                Initialize();

            // Enable or disable the ComboBox based on ReasonProcessEnabled and main grid visibility.
            bool reasonProcessEnabled = _viewModel.ReasonProcessEnabled && _viewModel.GridMainVisibility == Visibility.Visible;
            Enabled = reasonProcessEnabled;

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
            string newReason = item?.Text;

            // Return if the value hasn't actually changed.
            if (String.Equals(_previousReason, newReason, StringComparison.Ordinal))
                return;

            // Store the old value.
            _previousReason = newReason;

            // Update the main view model.
            _viewModel.Reason = newReason;

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
            SelectedItem = null;
            OnSelectionChange(null);

            //TODO: Needed?
            _isEnabled = false;

            // Load the reasons into the ComboBox list.
            LoadReasons();

            _isInitialized = true;
        }

        /// <summary>
        /// Gets the instance of the ReasonComboBox.
        /// </summary>
        /// <returns></returns>
        public static ReasonComboBox GetInstance()
        {
            // Return the instance of the ComboBox.
            return _reasonComboBox;
        }

        /// <summary>
        /// Loads the reasons from the ViewModel and adds them to the ComboBox.
        /// </summary>
        private void LoadReasons()
        {
            if (_viewModel?.ReasonCodes?.Length != 0)
            {
                // Add new layers from the ViewModel.
                foreach (var reasonCode in _viewModel.ReasonCodes)
                {
                    Add(new ComboBoxItem(reasonCode.description));
                }

                //TODO: Needed?
                _isEnabled = true;
            }
        }

        /// <summary>
        /// Updates the state of the ComboBox.
        /// </summary>
        /// <param name="enabled"></param>
        public void UpdateState(bool enabled)
        {
            _isEnabled = enabled;
        }

        /// <summary>
        /// Gets the currently selected reason.
        /// </summary>
        /// <value>The currently selected reason.</value>
        public string Reason
        {
            get { return (SelectedItem as ComboBoxItem)?.Text; }
        }

        /// <summary>
        /// Sets the selected item in the ComboBox based on the reason code.
        /// </summary>
        /// <param name="reasonCode">The code of the reason to select.</param>
        public void SetSelectedItem(string reasonCode)
        {
            if (string.IsNullOrEmpty(reasonCode))
            {
                SelectedItem = null;

                // Update error state when clearing selection
                UpdateErrorState();

                return;
            }

            // Find the matching reason code in the ViewModel
            var matchingReason = _viewModel?.ReasonCodes?.FirstOrDefault(r =>
                string.Equals(r.code, reasonCode, StringComparison.Ordinal));

            // If the reason code matches one of the options
            if (matchingReason != null)
            {
                // Find the ComboBox item by matching the description
                var item = ItemCollection.FirstOrDefault(i =>
                    string.Equals((i as ComboBoxItem)?.Text, matchingReason.description, StringComparison.Ordinal));

                if (item != null)
                {
                    // Temporarily store the previous value to prevent circular updates
                    string temp = _previousReason;
                    _previousReason = matchingReason.description;

                    // Set the selected item
                    SelectedItem = item;

                    // Restore if the selection failed
                    if (SelectedItem != item)
                        _previousReason = temp;
                }
            }

            // Update error state when clearing selection
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
                Tooltip = "Select the reason for attribute updates";
            }
        }

        #endregion Methods

        #region Validation

        /// <summary>
        /// Gets the error message for the current reason selection, if any.
        /// </summary>
        /// <value>The error message, or null if there is no error.</value>
        public string ErrorMessage
        {
            get
            {
                // Check if the reason is required but not set
                if (_viewModel != null && _viewModel.ReasonProcessEnabled)
                {
                    if (string.IsNullOrEmpty(_viewModel.Reason))
                        return "Reason is required for updates";
                }
                return null;
            }
        }

        /// <summary>
        /// Gets a value indicating whether there is an error with the current reason selection.
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