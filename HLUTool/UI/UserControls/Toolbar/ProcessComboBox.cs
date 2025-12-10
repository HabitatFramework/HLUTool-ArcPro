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
        private bool _isEnabled;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        public ProcessComboBox()
        {
            // Get this instance of the ComboBox.
            if (_processComboBox == null)
                _processComboBox = this;

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
        /// Gets the instance of the ProcessComboBox.
        /// </summary>
        /// <returns></returns>
        public static ProcessComboBox GetInstance()
        {
            // Return the instance of the ComboBox.
            return _processComboBox;
        }

        /// <summary>
        /// Called periodically by the framework once the tool has been created.
        /// </summary>
        protected override void OnUpdate()
        {
            // Initialize the ComboBox if it's not already.
            if (!_isInitialized)
                Initialize();

            // Select the process if it hasn't been selected.
            if (this.SelectedItem == null && _viewModel?.Process != null)
            {
                this.SelectedItem = _viewModel.Process;
                OnSelectionChange(this.SelectedItem);
            }

            //if (_viewModel == null)
            //{
            //    Enabled = false;
            //    DisabledTooltip = "HLU main window is not available.";
            //    return;
            //}

            bool reasonProcessEnabled = _viewModel.ReasonProcessEnabled;

            // Enable or disable the ComboBox based on ReasonProcessEnabled.
            Enabled = reasonProcessEnabled;
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

            // Load the processs into the ComboBox list.
            LoadProcesss();

            _isInitialized = true;
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

        public string Process
        {
            get { return this.SelectedItem?.ToString(); }
        }

        ///// <summary>
        ///// Called when the selection changes.
        ///// </summary>
        ///// <param name="item"></param>
        //protected override void OnSelectionChange(ComboBoxItem item)
        //{
        //    // Switch the active layer (if different).
        //    if (item != null)
        //        _viewModel?.SwitchGISLayer(item.Text);
        //}

        ///// <summary>
        ///// Sets the selected item in the ComboBox.
        ///// </summary>
        ///// <param name="value"></param>
        //public void SetSelectedItem(string value)
        //{
        //    // Check if the ItemCollection is not null and has items.
        //    if (this.ItemCollection?.Any() == true)
        //    {
        //        // Find and set the selected item if found.
        //        this.SelectedItem = this.ItemCollection.FirstOrDefault(item => item.ToString() == value);
        //    }
        //}

        #endregion Methods

    }
}
