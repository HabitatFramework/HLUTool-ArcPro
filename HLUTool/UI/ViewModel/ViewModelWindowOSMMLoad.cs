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

using HLU.Properties;
using HLU.UI;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// ViewModel for the OSMM Load setup dialog.
    /// Lets the user choose the input (non-HLU) layer and map each of its
    /// fields to the five <c>lut_osmm_habitat_xref</c> lookup attributes:
    /// <c>make</c>, <c>desc_group</c>, <c>desc_term</c>, <c>theme</c> and <c>feat_code</c>.
    /// </summary>
    internal class ViewModelWindowOSMMLoad : ViewModelBase, IDataErrorInfo
    {
        #region Fields

        private readonly ViewModelWindowMain _viewModelMain;

        private ICommand _okCommand;
        private ICommand _cancelCommand;

        private string _displayName = "OSMM Load Setup";

        private string _selectedLayerName;
        private ObservableCollection<string> _availableLayerNames = [];
        private ObservableCollection<string> _availableFields = [];

        private string _toidField;
        private string _makeField;
        private string _descGroupField;
        private string _descTermField;
        private string _themeField;
        private string _featCodeField;

        // Output layer
        private string _outputWorkspace;
        private string _outputFeatureClassName;
        private ICommand _browseOutputCommand;

        #endregion Fields

        #region Constructor

        public ViewModelWindowOSMMLoad(ViewModelWindowMain viewModelMain)
        {
            _viewModelMain = viewModelMain;
        }

        #endregion Constructor

        #region ViewModelBase Members

        public override string DisplayName
        {
            get => _displayName;
            set => _displayName = value;
        }

        public override string WindowTitle => _displayName;

        #endregion ViewModelBase Members

        #region RequestClose

        public delegate void RequestCloseEventHandler(bool apply, OsmmFieldMapping mapping);
        public event RequestCloseEventHandler RequestClose;

        #endregion RequestClose

        #region LoadAsync

        /// <summary>
        /// Asynchronously populates the list of non-HLU feature layers available in the
        /// current map. Call this from the <c>Loaded</c> event of the dialog window.
        /// </summary>
        public async Task LoadAsync()
        {
            // Pre-populate the output path from the application setting.
            string defaultLayer = _viewModelMain.AddInSettings?.DefaultOSMMBulkLoadLayer;
            if (!string.IsNullOrWhiteSpace(defaultLayer))
                ApplyOutputPath(defaultLayer);

            // Get every feature layer currently in the map.
            List<ArcGIS.Desktop.Mapping.FeatureLayer> allLayers =
                await ArcGIS.Desktop.Framework.Threading.Tasks.QueuedTask.Run(
                    () => _viewModelMain.GISApplication.GetFeatureLayers());

            if (allLayers == null)
                return;

            // Exclude the active HLU layer(s).
            IEnumerable<string> hluNames = _viewModelMain.GISApplication.ValidHluLayerNames
                ?? Enumerable.Empty<string>();

            List<string> nonHluNames = [.. allLayers
                .Select(l => l.Name)
                .Where(n => !hluNames.Contains(n, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)];

            _availableLayerNames = new ObservableCollection<string>(nonHluNames);
            OnPropertyChanged(nameof(AvailableLayerNames));

            // Pre-select the first layer if exactly one exists.
            if (_availableLayerNames.Count == 1)
                SelectedLayerName = _availableLayerNames[0];
        }

        #endregion LoadAsync

        #region Properties — Layer

        /// <summary>Gets the list of non-HLU feature layers available for selection.</summary>
        public ObservableCollection<string> AvailableLayerNames => _availableLayerNames;

        /// <summary>Gets or sets the currently selected input layer name.</summary>
        public string SelectedLayerName
        {
            get => _selectedLayerName;
            set
            {
                if (_selectedLayerName == value)
                    return;

                _selectedLayerName = value;
                OnPropertyChanged(nameof(SelectedLayerName));

                // Reset field mappings whenever the layer changes.
                ClearFieldMappings();

                // Load fields for the newly selected layer.
                _ = RefreshFieldsAsync();
            }
        }

        #endregion Properties — Layer

        #region Properties — Fields

        /// <summary>Gets the field names for the currently selected layer.</summary>
        public ObservableCollection<string> AvailableFields => _availableFields;

        /// <summary>Gets or sets the input layer field mapped to the TOID attribute (optional).</summary>
        public string ToidField
        {
            get => _toidField;
            set { _toidField = value; OnPropertyChanged(nameof(ToidField)); }
        }

        /// <summary>Gets or sets the input layer field mapped to <c>lut_osmm_habitat_xref.make</c>.</summary>
        public string MakeField
        {
            get => _makeField;
            set { _makeField = value; OnPropertyChanged(nameof(MakeField)); OnPropertyChanged(nameof(CanOk)); }
        }

        /// <summary>Gets or sets the input layer field mapped to <c>lut_osmm_habitat_xref.desc_group</c>.</summary>
        public string DescGroupField
        {
            get => _descGroupField;
            set { _descGroupField = value; OnPropertyChanged(nameof(DescGroupField)); OnPropertyChanged(nameof(CanOk)); }
        }

        /// <summary>Gets or sets the input layer field mapped to <c>lut_osmm_habitat_xref.desc_term</c>.</summary>
        public string DescTermField
        {
            get => _descTermField;
            set { _descTermField = value; OnPropertyChanged(nameof(DescTermField)); OnPropertyChanged(nameof(CanOk)); }
        }

        /// <summary>Gets or sets the input layer field mapped to <c>lut_osmm_habitat_xref.theme</c>.</summary>
        public string ThemeField
        {
            get => _themeField;
            set { _themeField = value; OnPropertyChanged(nameof(ThemeField)); OnPropertyChanged(nameof(CanOk)); }
        }

        /// <summary>Gets or sets the input layer field mapped to <c>lut_osmm_habitat_xref.feat_code</c>.</summary>
        public string FeatCodeField
        {
            get => _featCodeField;
            set { _featCodeField = value; OnPropertyChanged(nameof(FeatCodeField)); OnPropertyChanged(nameof(CanOk)); }
        }

        #endregion Properties — Fields

        #region Properties — Output

        /// <summary>Gets or sets the output workspace (folder for shapefile, .gdb path for GDB feature class).</summary>
        public string OutputWorkspace
        {
            get => _outputWorkspace;
            set
            {
                _outputWorkspace = value;
                OnPropertyChanged(nameof(OutputWorkspace));
                OnPropertyChanged(nameof(OutputDisplayPath));
                OnPropertyChanged(nameof(OutputExistsWarning));
                OnPropertyChanged(nameof(CanOk));
            }
        }

        /// <summary>Gets or sets the output feature class name (including .shp for shapefiles).</summary>
        public string OutputFeatureClassName
        {
            get => _outputFeatureClassName;
            set
            {
                _outputFeatureClassName = value;
                OnPropertyChanged(nameof(OutputFeatureClassName));
                OnPropertyChanged(nameof(OutputDisplayPath));
                OnPropertyChanged(nameof(OutputExistsWarning));
                OnPropertyChanged(nameof(CanOk));
            }
        }

        /// <summary>Gets the combined display path shown in the text box.</summary>
        public string OutputDisplayPath
        {
            get
            {
                if (string.IsNullOrEmpty(_outputWorkspace) || string.IsNullOrEmpty(_outputFeatureClassName))
                    return string.Empty;
                return Path.Combine(_outputWorkspace, _outputFeatureClassName);
            }
        }

        /// <summary>Returns a warning message when the output already exists, otherwise null.</summary>
        public string OutputExistsWarning
        {
            get
            {
                if (string.IsNullOrEmpty(_outputWorkspace) || string.IsNullOrEmpty(_outputFeatureClassName))
                    return null;

                bool isShp = _outputFeatureClassName.EndsWith(".shp", StringComparison.OrdinalIgnoreCase);
                bool exists;

                if (isShp)
                {
                    exists = File.Exists(Path.Combine(_outputWorkspace, _outputFeatureClassName));
                }
                else
                {
                    // For a GDB feature class the workspace must be a .gdb folder.
                    // The feature class itself is stored as a subfolder inside the .gdb.
                    exists = _outputWorkspace.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase)
                          && Directory.Exists(_outputWorkspace)
                          && Directory.Exists(Path.Combine(_outputWorkspace, _outputFeatureClassName));
                }

                return exists ? "Warning: The output already exists and will be overwritten." : null;
            }
        }

        /// <summary>Gets the command to browse for the output location.</summary>
        public ICommand BrowseOutputCommand
        {
            get
            {
                _browseOutputCommand ??= new RelayCommand(_ => BrowseOutput());
                return _browseOutputCommand;
            }
        }

        private void BrowseOutput()
        {
            // Determine initial directory from current output or the export path setting.
            string initialDir = _outputWorkspace;
            if (string.IsNullOrWhiteSpace(initialDir))
                initialDir = Settings.Default.ExportPath;
            if (string.IsNullOrWhiteSpace(initialDir) || !Directory.Exists(initialDir))
                initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            // Show a SaveFileDialog that handles both shapefile and GDB feature class.
            // Because a combined shapefile/GDB filter works well with SaveFileDialog:
            var dlg = new SaveFileDialog
            {
                Title = "Save Output Staging Layer",
                Filter = "Shapefile (*.shp)|*.shp|File Geodatabase Feature Class|*.gdb",
                FilterIndex = 1,
                InitialDirectory = initialDir,
                FileName = _outputFeatureClassName ?? "OSMM_Staging"
            };

            if (dlg.ShowDialog() != true)
                return;

            ApplyOutputPath(dlg.FileName);
        }

        /// <summary>
        /// Splits a full output path into workspace + feature class name and updates the bound properties.
        /// Handles both shapefile paths (ends in .shp) and plain GDB paths (parent ends in .gdb).
        /// </summary>
        private void ApplyOutputPath(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return;

            if (fullPath.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
            {
                OutputWorkspace = Path.GetDirectoryName(fullPath);
                OutputFeatureClassName = Path.GetFileName(fullPath);
            }
            else if (fullPath.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase))
            {
                // Caller passed just a .gdb path — treat the file stem as the feature class name.
                OutputWorkspace = Path.GetDirectoryName(fullPath);
                OutputFeatureClassName = Path.GetFileNameWithoutExtension(fullPath);
            }
            else
            {
                // Could be a bare name, a GDB feature class path like C:\data\my.gdb\MyFC,
                // or a folder\name combo without extension.
                string parent = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(parent) &&
                    parent.EndsWith(".gdb", StringComparison.OrdinalIgnoreCase) &&
                    Directory.Exists(parent))
                {
                    OutputWorkspace = parent;
                    OutputFeatureClassName = Path.GetFileName(fullPath);
                }
                else
                {
                    // Treat as shapefile without extension — add .shp.
                    OutputWorkspace = string.IsNullOrEmpty(parent)
                        ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                        : parent;
                    string name = Path.GetFileName(fullPath);
                    OutputFeatureClassName = name.EndsWith(".shp", StringComparison.OrdinalIgnoreCase)
                        ? name
                        : name + ".shp";
                }
            }
        }

        #endregion Properties — Output

        #region Ok Command

        /// <summary>Gets whether the Ok button should be enabled.</summary>
        public bool CanOk =>
            !string.IsNullOrEmpty(_selectedLayerName) &&
            !string.IsNullOrEmpty(_makeField) &&
            !string.IsNullOrEmpty(_descGroupField) &&
            !string.IsNullOrEmpty(_descTermField) &&
            !string.IsNullOrEmpty(_themeField) &&
            !string.IsNullOrEmpty(_featCodeField) &&
            !string.IsNullOrEmpty(_outputWorkspace) &&
            !string.IsNullOrEmpty(_outputFeatureClassName);

        public ICommand OkCommand
        {
            get
            {
                _okCommand ??= new RelayCommand(
                    _ => OkCommandClick(),
                    _ => CanOk);
                return _okCommand;
            }
        }

        private void OkCommandClick()
        {
            RequestClose?.Invoke(true, new OsmmFieldMapping(
                _selectedLayerName,
                _toidField,
                _makeField,
                _descGroupField,
                _descTermField,
                _themeField,
                _featCodeField,
                _outputWorkspace,
                _outputFeatureClassName));
        }

        #endregion Ok Command

        #region Cancel Command

        public ICommand CancelCommand
        {
            get
            {
                _cancelCommand ??= new RelayCommand(_ => CancelCommandClick());
                return _cancelCommand;
            }
        }

        private void CancelCommandClick()
        {
            RequestClose?.Invoke(false, null);
        }

        #endregion Cancel Command

        #region IDataErrorInfo

        public string Error => null;

        public string this[string columnName] => null;

        #endregion IDataErrorInfo

        #region Private Helpers

        private void ClearFieldMappings()
        {
            _toidField = null;
            _makeField = null;
            _descGroupField = null;
            _descTermField = null;
            _themeField = null;
            _featCodeField = null;

            OnPropertyChanged(nameof(ToidField));
            OnPropertyChanged(nameof(MakeField));
            OnPropertyChanged(nameof(DescGroupField));
            OnPropertyChanged(nameof(DescTermField));
            OnPropertyChanged(nameof(ThemeField));
            OnPropertyChanged(nameof(FeatCodeField));
            OnPropertyChanged(nameof(CanOk));
        }

        private async Task RefreshFieldsAsync()
        {
            if (string.IsNullOrEmpty(_selectedLayerName))
            {
                _availableFields.Clear();
                OnPropertyChanged(nameof(AvailableFields));
                return;
            }

            List<string> fieldNames =
                await _viewModelMain.GISApplication.GetFCFieldNamesAsync(_selectedLayerName);

            _availableFields = new ObservableCollection<string>(
                fieldNames?.OrderBy(f => f) ?? Enumerable.Empty<string>());

            OnPropertyChanged(nameof(AvailableFields));
        }

        #endregion Private Helpers
    }
}
