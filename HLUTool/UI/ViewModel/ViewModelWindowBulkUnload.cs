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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// Represents a single HLU layer entry in the OSMM Unload layer-picker checklist.
    /// </summary>
    internal class OsmmUnloadLayerItem : INotifyPropertyChanged
    {
        private bool _isChecked;

        public string LayerName { get; init; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value)
                    return;
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// ViewModel for the OSMM Unload layer-picker dialog.
    /// Presents the user with a checklist of valid HLU layers so they can choose
    /// which layers to include in the unload operation.
    /// </summary>
    internal class ViewModelWindowBulkUnload : ViewModelBase
    {
        #region Fields

        private ICommand _okCommand;
        private ICommand _cancelCommand;
        private string _displayName = "OSMM Unload — Select Layers";

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initialises the ViewModel with the supplied list of valid HLU layer names.
        /// The currently active layer is pre-checked.
        /// </summary>
        /// <param name="availableLayerNames">All valid HLU layer names in the current map.</param>
        /// <param name="activeLayerName">The currently active HLU layer name (pre-checked).</param>
        public ViewModelWindowBulkUnload(IEnumerable<string> availableLayerNames, string activeLayerName)
        {
            Layers = new ObservableCollection<OsmmUnloadLayerItem>(
                (availableLayerNames ?? [])
                .Select(n => new OsmmUnloadLayerItem
                {
                    LayerName = n,
                    IsChecked = string.Equals(n, activeLayerName, System.StringComparison.OrdinalIgnoreCase)
                }));
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

        public delegate void RequestCloseEventHandler(bool proceed, IReadOnlyList<string> selectedLayerNames);
        public event RequestCloseEventHandler RequestClose;

        #endregion RequestClose

        #region Properties

        /// <summary>Gets the checklist items, one per valid HLU layer.</summary>
        public ObservableCollection<OsmmUnloadLayerItem> Layers { get; }

        /// <summary>Gets whether the OK button should be enabled (at least one layer checked).</summary>
        public bool CanOk => Layers.Any(l => l.IsChecked);

        #endregion Properties

        #region Commands

        public ICommand OkCommand
        {
            get
            {
                _okCommand ??= new RelayCommand(
                    _ => RequestClose?.Invoke(true, [.. Layers.Where(l => l.IsChecked).Select(l => l.LayerName)]),
                    _ => CanOk);
                return _okCommand;
            }
        }

        public ICommand CancelCommand
        {
            get
            {
                _cancelCommand ??= new RelayCommand(
                    _ => RequestClose?.Invoke(false, []));
                return _cancelCommand;
            }
        }

        #endregion Commands
    }
}