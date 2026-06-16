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

using ArcGIS.Core.Data;
using ArcGIS.Desktop.Mapping;
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
    /// ViewModel for the Bulk Load setup dialog.
    /// Lets the user choose the input (non-HLU) layer and map each of its
    /// fields to the five <c>lut_osmm_habitat_xref</c> lookup attributes:
    /// <c>make</c>, <c>desc_group</c>, <c>desc_term</c>, <c>theme</c> and <c>feat_code</c>.
    /// </summary>
    internal class ViewModelWindowBulkLoad : ViewModelBase, IDataErrorInfo
    {
        #region Enums

        /// <summary>
        /// The GIS output format chosen by the user in the Bulk Load window.
        /// </summary>
        public enum OutputType
        {
            Shapefile,
            FileGeodatabase
        }

        #endregion Enums

        #region Fields

        private readonly ViewModelWindowMain _viewModelMain;

        private ICommand _okCommand;
        private ICommand _cancelCommand;

        private string _displayName = "Bulk Load";

        private string _selectedLayerName;
        private ObservableCollection<string> _availableLayerNames = [];
        private ObservableCollection<string> _availableFields = [];
        private ObservableCollection<string> _availableFieldsWithNone = [];

        private string _toidField;
        private string _makeField;
        private string _descGroupField;
        private string _descTermField;
        private string _themeField;
        private string _featCodeField;

        // Output type
        private OutputType _outputType = OutputType.Shapefile;

        // Feature counts and selection
        private int _selectedNumber;
        private long _totalCount;
        private bool _selectedOnly;

        // Constants
        private const string NoneOptionText = "<None>";

        #endregion Fields

        #region Constructor

        public ViewModelWindowBulkLoad(ViewModelWindowMain viewModelMain)
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

        /// <summary>
        /// Event raised when the user clicks Ok or Cancel in the dialog. The event args indicate
        /// whether the user clicked Ok or Cancel, the field mappings they chose (if Ok) and whether
        /// to load only selected features.
        /// </summary>
        /// <param name="apply">Indicates whether the user clicked Ok (true) or Cancel (false).</param>
        /// <param name="mapping">The field mappings chosen by the user, if Ok was clicked.</param>
        /// <param name="selectedOnly">Indicates whether to load only selected features.</param>
        /// <param name="outputType">The GIS output format chosen by the user.</param>
        public delegate void RequestCloseEventHandler(
            bool apply,
            OsmmFieldMapping mapping,
            bool selectedOnly,
            OutputType outputType);
        public event RequestCloseEventHandler RequestClose;

        #endregion RequestClose

        #region LoadAsync

        /// <summary>
        /// Asynchronously populates the list of non-HLU feature layers available in the
        /// current map. Call this from the <c>Loaded</c> event of the dialog window.
        /// </summary>
        public async Task LoadAsync()
        {
            // Get every feature layer currently in the map.
            List<FeatureLayer> allLayers =
                await ArcGIS.Desktop.Framework.Threading.Tasks.QueuedTask.Run(
                    () => _viewModelMain.GISApplication.GetFeatureLayers());

            // If there are no layers, there's nothing to do and the dialog will just show an empty layer list and disable Ok.
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

            // Set the available layer names for the ComboBox.
            _availableLayerNames = new ObservableCollection<string>(nonHluNames);
            OnPropertyChanged(nameof(AvailableLayerNames));

            // Pre-select the first layer if exactly one exists.
            // Inline the setter logic and await RefreshFeatureCountsAsync so that
            // _selectedNumber is populated before SelectedOnly is initialised below.
            if (_availableLayerNames.Count == 1)
            {
                _selectedLayerName = _availableLayerNames[0];
                OnPropertyChanged(nameof(SelectedLayerName));
                ClearFieldMappings();
                _ = RefreshFieldsAsync();
                await RefreshFeatureCountsAsync();
            }

            // Initialize SelectedOnly based on whether features are selected.
            _selectedOnly = _selectedNumber > 0;
            OnPropertyChanged(nameof(SelectedOnly));
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

                // Update feature counts.
                _ = RefreshFeatureCountsAsync();
            }
        }

        #endregion Properties — Layer

        #region Properties — Feature Counts

        /// <summary>Gets a value indicating whether there are selected features.</summary>
        public bool HaveSelection => _selectedNumber > 0;

        /// <summary>
        /// Gets a formatted string indicating the number of selected features and the total number
        /// of features in the layer.
        /// </summary>
        public string SelectionText
        {
            get
            {
                // If no layer is selected or the layer has no features, return an empty string.
                if (string.IsNullOrEmpty(_selectedLayerName) || _totalCount == 0)
                    return string.Empty;

                // Return "X of Y features" if there is a selection, otherwise just "Y features".
                return HaveSelection
                    ? String.Format("({0} of {1} feature{2})",
                        _selectedNumber.ToString("N0"),
                        _totalCount.ToString("N0"),
                        _totalCount > 1 ? "s" : String.Empty)
                    : String.Format("({0} feature{1})",
                        _totalCount.ToString("N0"),
                        _totalCount > 1 ? "s" : String.Empty);
            }
        }

        #endregion Properties — Feature Counts

        #region Properties — Fields

        /// <summary>Gets the field names for the currently selected layer (excluding <None> option).</summary>
        public ObservableCollection<string> AvailableFields => _availableFields;

        /// <summary>Gets the field names for the currently selected layer (including <None> option for TOID).</summary>
        public ObservableCollection<string> AvailableFieldsWithNone => _availableFieldsWithNone;

        /// <summary>Gets or sets the input layer field mapped to the TOID attribute (optional).</summary>
        public string ToidField
        {
            get => _toidField;
            set { _toidField = value; OnPropertyChanged(nameof(ToidField)); OnPropertyChanged(nameof(CanOk)); }
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

        /// <summary>
        /// Gets the list of available output type items for the ComboBox. Each item exposes a
        /// <c>Value</c> and a <c>Display</c> string.
        /// </summary>
        public List<OutputTypeItem> OutputTypes
        {
            get;
        } =
        [
            new OutputTypeItem(OutputType.Shapefile,       "Shapefile (.shp)"),
            new OutputTypeItem(OutputType.FileGeodatabase, "File Geodatabase (.gdb)")
        ];

        /// <summary>Gets or sets the currently selected output type.</summary>
        public OutputType SelectedOutputType
        {
            get => _outputType;
            set
            {
                _outputType = value;
                OnPropertyChanged(nameof(SelectedOutputType));
            }
        }

        #endregion Properties — Output

        #region Properties — Selected Only

        /// <summary>Gets or sets a value indicating whether only selected features should be loaded.</summary>
        public bool SelectedOnly
        {
            get => _selectedOnly;
            set
            {
                _selectedOnly = value;
                OnPropertyChanged(nameof(SelectedOnly));
            }
        }

        #endregion Properties — Selected Only

        #region Ok Command

        /// <summary>Gets whether the Ok button should be enabled.</summary>
        public bool CanOk =>
            !string.IsNullOrEmpty(_selectedLayerName) &&
            !string.IsNullOrEmpty(_toidField) &&
            !string.IsNullOrEmpty(_makeField) &&
            !string.IsNullOrEmpty(_descGroupField) &&
            !string.IsNullOrEmpty(_descTermField) &&
            !string.IsNullOrEmpty(_themeField) &&
            !string.IsNullOrEmpty(_featCodeField);

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
            // If ToidField is "<None>", pass null instead.
            string toidFieldValue = (_toidField == NoneOptionText) ? null : _toidField;

            // Close the dialog and pass the field mappings and other options back to the main ViewModel.
            RequestClose?.Invoke(
                true,
                new OsmmFieldMapping(
                    _selectedLayerName,
                    toidFieldValue,
                    _makeField,
                    _descGroupField,
                    _descTermField,
                    _themeField,
                    _featCodeField),
                _selectedOnly,
                _outputType);
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
            RequestClose?.Invoke(false, null, false, _outputType);
        }

        #endregion Cancel Command

        #region OutputTypeItem helper

        /// <summary>
        /// A simple display-value pair used to populate the Output Type ComboBox.
        /// </summary>
        public sealed class OutputTypeItem
        {
            /// <summary>
            /// Initializes a new instance of <see cref="OutputTypeItem"/>.
            /// </summary>
            /// <param name="value">The enum value associated with this item.</param>
            /// <param name="display">The string to display in the ComboBox.</param>
            public OutputTypeItem(OutputType value, string display)
            {
                Value = value;
                Display = display;
            }

            /// <summary>Gets the enum value.</summary>
            public OutputType Value { get; }

            /// <summary>Gets the display string.</summary>
            public string Display { get; }
        }

        #endregion OutputTypeItem helper

        #region IDataErrorInfo

        public string Error
        {
            get
            {
                if (string.IsNullOrEmpty(_toidField))
                    return "Please select a TOID field or choose '<None>'";
                if (string.IsNullOrEmpty(_makeField))
                    return "Please select a make field";
                if (string.IsNullOrEmpty(_descGroupField))
                    return "Please select a desc_group field";
                if (string.IsNullOrEmpty(_descTermField))
                    return "Please select a desc_term field";
                if (string.IsNullOrEmpty(_themeField))
                    return "Please select a theme field";
                if (string.IsNullOrEmpty(_featCodeField))
                    return "Please select a feat_code field";
                return null;
            }
        }

        public string this[string columnName]
        {
            get
            {
                string error = null;

                switch (columnName)
                {
                    case "ToidField":
                        if (string.IsNullOrEmpty(_toidField))
                            error = "Error: You must select a TOID field or choose '<None>'";
                        break;
                    case "MakeField":
                        if (string.IsNullOrEmpty(_makeField))
                            error = "Error: You must select a make field";
                        break;
                    case "DescGroupField":
                        if (string.IsNullOrEmpty(_descGroupField))
                            error = "Error: You must select a desc_group field";
                        break;
                    case "DescTermField":
                        if (string.IsNullOrEmpty(_descTermField))
                            error = "Error: You must select a desc_term field";
                        break;
                    case "ThemeField":
                        if (string.IsNullOrEmpty(_themeField))
                            error = "Error: You must select a theme field";
                        break;
                    case "FeatCodeField":
                        if (string.IsNullOrEmpty(_featCodeField))
                            error = "Error: You must select a feat_code field";
                        break;
                }

                return error;
            }
        }

        #endregion IDataErrorInfo

        #region Private Helpers

        /// <summary>
        /// Clears the field mappings and raises PropertyChanged for each mapping property and CanOk.
        /// </summary>
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

        /// <summary>
        /// Refreshes the feature counts for the currently selected layer and updates the related properties.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task RefreshFeatureCountsAsync()
        {
            // If no layer is selected, reset counts and return.
            if (string.IsNullOrEmpty(_selectedLayerName))
            {
                _selectedNumber = 0;
                _totalCount = 0;
                OnPropertyChanged(nameof(HaveSelection));
                OnPropertyChanged(nameof(SelectionText));
                return;
            }

            try
            {
                // Get the feature layer by name.
                FeatureLayer layer = await ArcGIS.Desktop.Framework.Threading.Tasks.QueuedTask.Run(() =>
                {
                    List<FeatureLayer> allLayers = _viewModelMain.GISApplication.GetFeatureLayers();

                    // Return the layer with the matching name, ignoring case. If not found, return null.
                    return allLayers?.FirstOrDefault(l => l.Name.Equals(_selectedLayerName, StringComparison.OrdinalIgnoreCase));
                });

                // If the layer can't be found for some reason, reset counts.
                if (layer == null)
                {
                    _selectedNumber = 0;
                    _totalCount = 0;
                }
                else
                {
                    // Get counts on the QueuedTask thread.
                    (int selected, long total) = await ArcGIS.Desktop.Framework.Threading.Tasks.QueuedTask.Run(() =>
                    {
                        int selCount = layer.SelectionCount;
                        long totalCount;

                        using (Table table = layer.GetTable())
                        {
                            totalCount = table.GetCount();
                        }

                        return (selCount, totalCount);
                    });

                    // Set the counts in the ViewModel.
                    _selectedNumber = selected;
                    _totalCount = total;
                }
            }
            catch
            {
                // If an error occurs while refreshing counts, reset counts.
                _selectedNumber = 0;
                _totalCount = 0;
            }

            // Raise PropertyChanged for the counts and related properties.
            OnPropertyChanged(nameof(HaveSelection));
            OnPropertyChanged(nameof(SelectionText));
        }

        /// <summary>
        /// Refreshes the list of available fields for the currently selected layer and raises PropertyChanged.
        /// </summary>
        /// <returns></returns>
        private async Task RefreshFieldsAsync()
        {
            if (string.IsNullOrEmpty(_selectedLayerName))
            {
                _availableFields.Clear();
                OnPropertyChanged(nameof(AvailableFields));
                return;
            }

            // Get the field names for the selected layer from the GIS application.
            List<string> fieldNames =
                await _viewModelMain.GISApplication.GetFCFieldNamesAsync(_selectedLayerName);

            // Create a list with "<None>" as the first item for the TOID field only.
            List<string> fieldsWithNone = [NoneOptionText, .. (fieldNames?.OrderBy(f => f) ?? Enumerable.Empty<string>())];

            // Create a list without "<None>" for the required fields.
            List<string> fieldsOnly = [.. (fieldNames?.OrderBy(f => f) ?? Enumerable.Empty<string>())];

            // Set the available fields for the ComboBoxes.
            _availableFieldsWithNone = new ObservableCollection<string>(fieldsWithNone);
            _availableFields = new ObservableCollection<string>(fieldsOnly);

            // Raise PropertyChanged for both available fields collections.
            OnPropertyChanged(nameof(AvailableFieldsWithNone));
            OnPropertyChanged(nameof(AvailableFields));

            // Automatically preselect fields if matching field names are found.
            PreselectFieldMappings(fieldNames);
        }

        /// <summary>
        /// Automatically preselects field mappings if the layer contains fields that match
        /// the expected default names (case-insensitive).
        /// </summary>
        /// <param name="fieldNames">The list of field names from the selected layer.</param>
        private void PreselectFieldMappings(List<string> fieldNames)
        {
            if (fieldNames == null || fieldNames.Count == 0)
                return;

            // Create a case-insensitive lookup dictionary for faster matching.
            var fieldLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string fieldName in fieldNames)
            {
                if (!fieldLookup.ContainsKey(fieldName))
                    fieldLookup[fieldName] = fieldName;
            }

            // Define the expected default field names for each mapping.
            var defaultMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "toid", "toid" },
                { "make", "make" },
                { "descriptivegroup", "descriptive_group" },
                { "descriptiveterm", "descriptive_term" },
                { "theme", "theme" },
                { "featurecode", "feature_code" }
            };

            // Try to match each expected field name against the available fields.
            // If a match is found, set the corresponding property.
            if (fieldLookup.TryGetValue("toid", out string toidMatch))
            {
                _toidField = toidMatch;
                OnPropertyChanged(nameof(ToidField));
            }
            else
            {
                // If TOID field is not found, default to "<None>" since it's optional.
                _toidField = NoneOptionText;
                OnPropertyChanged(nameof(ToidField));
            }

            if (fieldLookup.TryGetValue("make", out string makeMatch))
            {
                _makeField = makeMatch;
                OnPropertyChanged(nameof(MakeField));
            }

            // Try multiple possible variations for descriptive_group
            if (fieldLookup.TryGetValue("descriptive_group", out string descGroupMatch) ||
                fieldLookup.TryGetValue("descriptivegroup", out descGroupMatch))
            {
                _descGroupField = descGroupMatch;
                OnPropertyChanged(nameof(DescGroupField));
            }

            // Try multiple possible variations for descriptive_term
            if (fieldLookup.TryGetValue("descriptive_term", out string descTermMatch) ||
                fieldLookup.TryGetValue("descriptiveterm", out descTermMatch))
            {
                _descTermField = descTermMatch;
                OnPropertyChanged(nameof(DescTermField));
            }

            if (fieldLookup.TryGetValue("theme", out string themeMatch))
            {
                _themeField = themeMatch;
                OnPropertyChanged(nameof(ThemeField));
            }

            // Try multiple possible variations for feature_code
            if (fieldLookup.TryGetValue("feature_code", out string featCodeMatch) ||
                fieldLookup.TryGetValue("featurecode", out featCodeMatch))
            {
                _featCodeField = featCodeMatch;
                OnPropertyChanged(nameof(FeatCodeField));
            }

            // Raise PropertyChanged for CanOk to update the Ok button state.
            OnPropertyChanged(nameof(CanOk));
        }

        #endregion Private Helpers
    }
}