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

using HLU.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// View model for the Reassign Features dialog.
    /// </summary>
    internal class ViewModelWindowReassign : ViewModelBase, IDataErrorInfo
    {
        #region Fields

        private ICommand _okCommand;
        private ICommand _cancelCommand;

        private readonly string _sourceLayerName;
        private readonly List<string> _targetLayerNames;
        private string _targetLayerName;
        private readonly List<ReassignRule> _rules;
        private ReassignRule _selectedRule;
        private readonly int _featureCount;

        private string _displayName = "Reassign Features";

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initialises the Reassign Features dialog view model.
        /// </summary>
        /// <param name="sourceLayerName">Name of the currently active HLU layer.</param>
        /// <param name="targetLayerNames">All valid HLU layer names (excluding the source layer).</param>
        /// <param name="rules">Reassign rules configured in the options.</param>
        /// <param name="featureCount">Number of features that will be reassigned.</param>
        public ViewModelWindowReassign(
            string sourceLayerName,
            List<string> targetLayerNames,
            List<ReassignRule> rules,
            int featureCount)
        {
            _sourceLayerName = sourceLayerName;
            _targetLayerNames = targetLayerNames;
            _rules = rules;
            _featureCount = featureCount;
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

        public delegate void RequestCloseEventHandler(string targetLayerName, ReassignRule rule);

        public event RequestCloseEventHandler RequestClose;

        #endregion RequestClose

        #region Ok Command

        public ICommand OkCommand
        {
            get
            {
                if (_okCommand == null)
                {
                    Action<object> okAction = new(this.OkCommandClick);
                    _okCommand = new RelayCommand(okAction, param => this.CanOk);
                }
                return _okCommand;
            }
        }

        private void OkCommandClick(object param)
        {
            RequestClose?.Invoke(_targetLayerName, _selectedRule);
        }

        private bool CanOk =>
            !string.IsNullOrEmpty(_targetLayerName) && _selectedRule != null;

        #endregion Ok Command

        #region Cancel Command

        public ICommand CancelCommand
        {
            get
            {
                if (_cancelCommand == null)
                {
                    Action<object> cancelAction = new(this.CancelCommandClick);
                    _cancelCommand = new RelayCommand(cancelAction);
                }
                return _cancelCommand;
            }
        }

        private void CancelCommandClick(object param)
        {
            RequestClose?.Invoke(null, null);
        }

        #endregion Cancel Command

        #region Control Properties

        /// <summary>
        /// Gets the name of the source (active) HLU layer.
        /// </summary>
        public string SourceLayerName => _sourceLayerName;

        /// <summary>
        /// Gets the list of available target HLU layer names.
        /// </summary>
        public List<string> TargetLayerNames => _targetLayerNames;

        /// <summary>
        /// Gets or sets the selected target layer name.
        /// </summary>
        public string TargetLayerName
        {
            get => _targetLayerName;
            set
            {
                if (_targetLayerName != value)
                {
                    _targetLayerName = value;
                    OnPropertyChanged(nameof(TargetLayerName));
                }
            }
        }

        /// <summary>
        /// Gets the list of reassign rules available for selection.
        /// </summary>
        public List<ReassignRule> Rules => _rules;

        /// <summary>
        /// Gets or sets the selected reassign rule.
        /// </summary>
        public ReassignRule SelectedRule
        {
            get => _selectedRule;
            set
            {
                if (_selectedRule != value)
                {
                    _selectedRule = value;
                    OnPropertyChanged(nameof(SelectedRule));
                }
            }
        }

        /// <summary>
        /// Gets the feature count text to display.
        /// </summary>
        public string FeatureCountText =>
            _featureCount > 0 ? _featureCount.ToString("N0") : "All";

        #endregion Control Properties

        #region IDataErrorInfo

        public string Error => null;

        public string this[string columnName]
        {
            get
            {
                string error = null;
                switch (columnName)
                {
                    case nameof(TargetLayerName):
                        if (string.IsNullOrEmpty(_targetLayerName))
                            error = "A target layer must be selected.";
                        break;
                    case nameof(SelectedRule):
                        if (_selectedRule == null)
                            error = "A reassign rule must be selected.";
                        break;
                }
                return error;
            }
        }

        #endregion IDataErrorInfo
    }
}
