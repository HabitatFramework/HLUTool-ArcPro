using HLU.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ComboBox = ArcGIS.Desktop.Framework.Contracts.ComboBox;
using System.Runtime.CompilerServices;
using HLU.UI.ViewModel;

namespace HLU.UI.UserControls
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

            // Initialize the ComboBox (if it's not already).
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
            // Initialize the ComboBox if it's not already.
            if (!_isInitialized)
                Initialize();

            // Select the active layer if it hasn't been selected.
            if (this.SelectedItem == null && _viewModel?.ActiveLayerName != null)
            {
                this.SelectedItem = _viewModel.ActiveLayerName;
                OnSelectionChange(this.SelectedItem);
            }

            // Enable or disable the ComboBox.
            Enabled = _isEnabled;
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
            this.SelectedItem = null;
            OnSelectionChange(null);

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
        protected override void OnSelectionChange(ComboBoxItem item)
        {
            // Switch the active layer (if different).
            if (item != null)
                _viewModel?.SwitchGISLayer(item.Text);
        }

        /// <summary>
        /// Sets the selected item in the ComboBox.
        /// </summary>
        /// <param name="value"></param>
        public void SetSelectedItem(string value)
        {
            // Check if the ItemCollection is not null and has items.
            if (this.ItemCollection?.Any() == true)
            {
                // Find and set the selected item if found.
                this.SelectedItem = this.ItemCollection.FirstOrDefault(item => item.ToString() == value);
            }
        }

        #endregion Methods

    }
}
