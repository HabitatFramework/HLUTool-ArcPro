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

using HLU.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

        private string _makeField;
        private string _descGroupField;
        private string _descTermField;
        private string _themeField;
        private string _featCodeField;

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
            // Get every feature layer currently in the map.
            List<ArcGIS.Desktop.Mapping.FeatureLayer> allLayers =
                await ArcGIS.Desktop.Framework.Threading.Tasks.QueuedTask.Run(
                    () => _viewModelMain.GISApplication.GetFeatureLayers());

            if (allLayers == null)
                return;

            // Exclude the active HLU layer(s).
            IEnumerable<string> hluNames = _viewModelMain.GISApplication.ValidHluLayerNames
                ?? Enumerable.Empty<string>();

            List<string> nonHluNames = allLayers
                .Select(l => l.Name)
                .Where(n => !hluNames.Contains(n, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .ToList();

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

        #region Ok Command

        /// <summary>Gets whether the Ok button should be enabled.</summary>
        public bool CanOk =>
            !string.IsNullOrEmpty(_selectedLayerName) &&
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
            RequestClose?.Invoke(true, new OsmmFieldMapping(
                _selectedLayerName,
                _makeField,
                _descGroupField,
                _descTermField,
                _themeField,
                _featCodeField));
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
            _makeField = null;
            _descGroupField = null;
            _descTermField = null;
            _themeField = null;
            _featCodeField = null;

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
