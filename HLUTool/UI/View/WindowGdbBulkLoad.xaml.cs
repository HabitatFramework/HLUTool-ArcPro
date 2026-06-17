// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
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

using HLU.UI.ViewModel;
using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.UI.View
{
    /// <summary>
    /// Code-behind for the Bulk Load Staging Layer to File Geodatabase dialog.
    /// Collects a validated .gdb workspace path and feature class name.
    /// </summary>
    public partial class WindowGdbBulkLoad :
        ArcGIS.Desktop.Framework.Controls.ProWindow
    {
        #region Fields

        private readonly ViewModelWindowGdbExport _viewModel;

        #endregion Fields

        #region Properties

        /// <summary>
        /// The confirmed .gdb folder path, or empty string if cancelled.
        /// </summary>
        public string GdbPath => _viewModel.GdbPath;

        /// <summary>
        /// The confirmed feature class name, or empty string if cancelled.
        /// </summary>
        public string FeatureClassName => _viewModel.FeatureClassName;

        #endregion Properties

        #region Constructor

        /// <summary>
        /// Initialises the dialog with optional pre-populated values.
        /// </summary>
        /// <param name="initialGdbPath">Pre-populated .gdb path, or empty string for none.</param>
        /// <param name="initialFeatureName">Pre-populated feature class name suggestion.</param>
        public WindowGdbBulkLoad(
            string initialGdbPath = "",
            string initialFeatureName = "HLU_Staging")
        {
            InitializeComponent();

            // Initialise the view model with the optional pre-populated values.
            _viewModel = new ViewModelWindowGdbExport(
                initialGdbPath, initialFeatureName);

            // Set the dialog's data context to the view model so that the XAML bindings work.
            DataContext = _viewModel;

            // When the dialog is loaded, set focus to the feature class name box and select all text.
            Loaded += (_, _) =>
            {
                TextBoxFeatureClassName.Focus();
                TextBoxFeatureClassName.SelectAll();
            };
        }

        #endregion Constructor

        #region Event Handlers

        /// <summary>
        /// Opens a folder browser to select the target .gdb workspace.
        /// Updates the view model's GdbPath and auto-suggests a feature
        /// class name when the box still holds its default value.
        /// </summary>
        private void ButtonBrowse_Click(object sender, RoutedEventArgs e)
        {
            // Open the browser in the parent of the current GDB path
            // (if set and valid), otherwise fall back to an empty string
            // so the dialog opens at the default location.
            string startDir = String.Empty;

            // If the user has already selected a GDB path, use its parent folder as the starting directory.
            if (!String.IsNullOrWhiteSpace(_viewModel.GdbPath))
            {
                string parent = Path.GetDirectoryName(_viewModel.GdbPath);
                if (!String.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
                    startDir = parent;
            }

            // Set up the folder browser dialog with a description and the starting directory.
            using var folderDialog = new FolderBrowserDialog
            {
                Description = "Select the File Geodatabase (.gdb) for the staging layer:",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false,
                SelectedPath = startDir
            };

            // Show the dialog and check if the user clicked OK. If not, exit the method.
            if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            // Store the selected path in a variable for further validation.
            string chosen = folderDialog.SelectedPath;

            // If the selected folder does not end with ".gdb", show a warning message and exit the method.
            if (!chosen.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    "The selected folder is not a File Geodatabase.\n\n" +
                    "Please select a folder whose name ends in .gdb.",
                    "Invalid Geodatabase",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Capture the current feature class name BEFORE updating GdbPath
            // to avoid any side effects from property notifications.
            string currentName = _viewModel.FeatureClassName?.Trim() ?? String.Empty;

            // Update the VM — this triggers validation and re-evaluates IsValid.
            _viewModel.GdbPath = chosen;

            // Auto-suggest the GDB base name only when the feature class name
            // box still holds the default, to avoid overwriting a deliberate edit.
            string baseName = Path.GetFileNameWithoutExtension(chosen);

            // If the current feature class name is empty or still the default "HLU_Staging", update it to the base name of the selected GDB.
            if (String.IsNullOrWhiteSpace(currentName) ||
                currentName == "HLU_Staging")
            {
                _viewModel.FeatureClassName = baseName;
            }

            // Set focus back to the feature class name box and select all text for easy editing.
            TextBoxFeatureClassName.Focus();
            TextBoxFeatureClassName.SelectAll();
        }

        /// <summary>
        /// Closes the dialog with a result of <see langword="true"/> if the validation
        /// passed, or keeps the dialog open if there are errors.
        /// </summary>
        /// <param name="sender">The button that was clicked.</param>
        /// <param name="e">The event arguments.</param>
        private void ButtonOk_Click(object sender, RoutedEventArgs e)
        {
            if (!_viewModel.IsValid)
                return;

            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Closes the dialog with a result of <see langword="false"/>.
        /// </summary>
        /// <param name="sender">The button that was clicked.</param>
        /// <param name="e">The event arguments.</param>
        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion Event Handlers
    }
}