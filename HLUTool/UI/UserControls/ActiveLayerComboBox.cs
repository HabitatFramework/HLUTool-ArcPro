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
    /// Represents a ComboBox control that allows the user to select the active layerName.
    /// </summary>
    internal class ActiveLayerComboBox : ComboBox
    {
        private static ActiveLayerComboBox _hluLayerComboBox;
        private static ObservableCollection<string> _availableHLULayerNames;
        private static string _selectedHLULayerName;
        private static string _oldSelectedHLULayerName;

        private bool _isInitialized;
        private bool _isEnabled;

        public static event Action<string> OnComboBoxSelectionChanged;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ActiveLayerComboBox()
        {
            _hluLayerComboBox = this;

            if (!_isInitialized)
            {
                Initialize();
                _isInitialized = true;
            }
        }

        /// <summary>
        /// Gets the instance of the ActiveLayerComboBox.
        /// </summary>
        /// <returns></returns>
        public static ActiveLayerComboBox GetInstance()
        {
            return _hluLayerComboBox;
        }

        /// <summary>
        /// Called periodically by the framework once the tool has been created.
        /// </summary>
        protected override void OnUpdate()
        {
            if (this.SelectedItem == null)
            {
                this.SelectedItem = _selectedHLULayerName;
                OnSelectionChange(this.SelectedItem);
            }

            Enabled = _isEnabled;
        }

        /// <summary>
        /// Called when the drop-down is opened.
        /// </summary>
        protected override void OnDropDownOpened()
        {
            if (!_isInitialized)
            {
                Initialize();
                _isInitialized = true;
            }
        }

        /// <summary>
        /// Initializes the ComboBox.
        /// </summary>
        public void Initialize()
        {
            // Clear existing items
            Clear();

            // Clear the selected item.
            this.SelectedItem = null;
            OnSelectionChange(null);

            // Load the available layers to the list.
            if (_availableHLULayerNames != null)
            {
                // Add new layers from the ViewModel.
                foreach (var layerName in _availableHLULayerNames)
                {
                    Add(new ComboBoxItem(layerName));
                }

                _isEnabled = true;
            }
            else
            {
                _isEnabled = false;
            }

            //// Set the selected item.
            //if (this.SelectedItem == null && _selectedHLULayerName != null)
            //    SetSelectedItem(_selectedHLULayerName);
        }

        public void UpdateState(bool enabled)
        {
            _isEnabled = enabled;
        }

        /// <summary>
        /// Updates the items in the ComboBox.
        /// </summary>
        /// <param name="newLayerNames"></param>
        public static void UpdateLayerNames(ObservableCollection<string> newLayerNames)
        {
            _availableHLULayerNames = newLayerNames;

            // Force combobox to refresh
            //_isInitialized = false;
            //OnDropDownOpened();
        }

        /// <summary>
        /// Updates the active layer in the ComboBox.
        /// </summary>
        /// <param name="activeLayerName"></param>
        public static void UpdateActiveLayer(string activeLayerName)
        {
            // Set the selected layer name.
            if (_selectedHLULayerName == null)
                _selectedHLULayerName = activeLayerName;

            // Force combobox to refresh
            //_isInitialized = false;
            //OnDropDownOpened();
        }

        /// <summary>
        /// Called when the selection changes.
        /// </summary>
        /// <param name="item"></param>
        protected override void OnSelectionChange(ComboBoxItem item)
        {
            if (item != null)
            {
                //TODO: Switch active layerName
                // Raise event
                //OnComboBoxSelectionChanged?.Invoke(item.Text);
                //ViewModelWindowMain.HandleComboBoxSelectionStatic(item.Text);

                // Get the dockpane DAML id.
                DockPane pane = FrameworkApplication.DockPaneManager.Find(ViewModelWindowMain.DockPaneID);
                if (pane == null)
                    return;

                // Get the ViewModel by casting the dockpane.
                ViewModelWindowMain vm = pane as ViewModelWindowMain;

                //TODO: Switch active layer (if different).
                vm.HandleComboBoxSelection(item.Text);

                // Update the selected layer name.
                _selectedHLULayerName = item.Text;
            }
        }

        /// <summary>
        /// Sets the selected item in the ComboBox.
        /// </summary>
        /// <param name="value"></param>
        public void SetSelectedItem(string value)
        {
            // Check if the ItemCollection is not null and has items.
            if (this.ItemCollection != null && this.ItemCollection.Count > 0)
            {
                // Check if the item exists.
                foreach (var item in this.ItemCollection)
                {
                    // Set the selected item if found.
                    if (item.ToString() == value)
                    {
                        this.SelectedItem = value;
                        break;
                    }
                }
            }
        }
    }
}
