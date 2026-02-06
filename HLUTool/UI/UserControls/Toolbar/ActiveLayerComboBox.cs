using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using HLU.Data;
using HLU.UI.ViewModel;
using Microsoft.IdentityModel.Tokens;
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
    /// Represents a ComboBox control that allows the user to select the active layer name.
    /// </summary>
    internal class ActiveLayerComboBox : ComboBox
    {
        #region Fields

        private static ActiveLayerComboBox _hluLayerComboBox;
        private ViewModelWindowMain _viewModel;

        private bool _isInitialized;
        private bool _isEnabled;
        private bool _suppressSelectionChange;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        public ActiveLayerComboBox()
        {
            // Get this instance of the ComboBox.
            if (_hluLayerComboBox == null)
                _hluLayerComboBox = this;

            // Get the dockpane DAML id.
            DockPane pane = FrameworkApplication.DockPaneManager.Find(ViewModelWindowMain.DockPaneID);
            if (pane == null)
                return;

            // Get the ViewModel by casting the dockpane.
            _viewModel = pane as ViewModelWindowMain;

            // Initialize the ComboBox.
            Initialize();
        }

        #endregion Constructor

        #region Methods

        /// <summary>
        /// Gets the instance of the ActiveLayerComboBox.
        /// </summary>
        /// <returns></returns>
        public static ActiveLayerComboBox GetInstance()
        {
            // Return the instance of the ComboBox.
            return _hluLayerComboBox;
        }

        /// <summary>
        /// Called periodically by the framework once the tool has been created.
        /// </summary>
        protected override void OnUpdate()
        {
            if (_viewModel == null)
            {
                Enabled = false;
                DisabledTooltip = "HLU main window is not available.";
                return;
            }

            // Initialize the ComboBox if it's not already.
            if (!_isInitialized)
                Initialize();

            // Select the active layer if it hasn't been selected.
            if (SelectedItem == null && _viewModel?.ActiveLayerName != null)
            {
                SelectedItem = _viewModel.ActiveLayerName;
                OnSelectionChange(SelectedItem);
            }

            //TODO: Fix CanSwitchGISLayer
            bool canSwitchGISLayer = _viewModel.CanSwitchGISLayer;
            canSwitchGISLayer = true;

            // Enable or disable the combobox based on CanSwitchGISLayer and main grid visibility.
            Enabled = canSwitchGISLayer && _viewModel.GridMainVisibility == Visibility.Visible;

            // Optional: explain why it is disabled.
            if (!canSwitchGISLayer)
            {
                DisabledTooltip = "Available only when not in a bulk or OSMM update mode.";
            }
            else
            {
                DisabledTooltip = string.Empty;
            }
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
            OnSelectionChange(SelectedItem);

            _isEnabled = false;

            // Load the available layers into the ComboBox list.
            LoadAvailableLayers();

            _isInitialized = true;
        }

        /// <summary>
        /// Loads the available layers from the ViewModel and adds them to the ComboBox.
        /// </summary>
        private void LoadAvailableLayers()
        {
            if (_viewModel?.AvailableHLULayerNames?.Any() == true)
            {
                // Add new layers from the ViewModel.
                foreach (var layerName in _viewModel.AvailableHLULayerNames)
                    Add(new ComboBoxItem(layerName));

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
        /// Called when the selection changes.
        /// </summary>
        /// <param name="item"></param>
        protected override async void OnSelectionChange(ComboBoxItem item)
        {
            // If suppressing selection change, exit.
            if (_suppressSelectionChange)
                return;

            // If the ComboBox is not enabled, exit.
            if (item == null)
                await _viewModel?.SwitchGISLayerAsync(item.Text);

            // If the selected item is the same as the active layer, exit.
            if (string.Equals(_viewModel?.ActiveLayerName, item.Text, StringComparison.Ordinal))
                return;

            // Switch the GIS layer in the ViewModel.
            await _viewModel.SwitchGISLayerAsync(item.Text);
        }

        /// <summary>
        /// Sets the selected item in the ComboBox.
        /// </summary>
        /// <param name="value"></param>
        public void SetSelectedItem(string value)
        {
            // Check if the ItemCollection is not null and has items.
            if (ItemCollection?.Any() == true)
            {
                // If a selected item is required.
                if (!String.IsNullOrEmpty(value))
                    // Find and set the selected item if found.
                    SelectedItem = ItemCollection.FirstOrDefault(item => item.ToString() == value);
                else
                    // Selected the first item.
                    SelectedItem = ItemCollection.First();
            }
            else
            {
                SelectedItem = null;
            }
        }

        /// <summary>
        /// Sets the selected item in the ComboBox.
        /// </summary>
        /// <param name="value">The layer name to select.</param>
        /// <param name="suppressSwitch">Suppress switching the active GIS layer.</param>
        /// If true, suppresses OnSelectionChange from switching the active GIS layer.
        /// </param>
        public void SetSelectedItem(string value, bool suppressSwitch = false)
        {
            try
            {
                _suppressSelectionChange = suppressSwitch;
                SetSelectedItem(value);
            }
            finally
            {
                _suppressSelectionChange = false;
            }
        }

        #endregion Methods
    }
}
