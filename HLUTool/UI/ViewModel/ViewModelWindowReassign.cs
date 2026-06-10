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
using System.Threading.Tasks;
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
        private int _featureCount = -1;
        private bool _isCountingFeatures;
        private string _featureCountError;

        /// <summary>
        /// Async delegate supplied by the orchestrator that counts features matching a WHERE clause
        /// on the active source layer.
        /// </summary>
        private readonly Func<string, Task<int>> _countFeaturesAsync;

        private string _displayName = "Reassign Features";

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initialises the Reassign Features dialog view model.
        /// </summary>
        /// <param name="sourceLayerName">Name of the currently active HLU layer.</param>
        /// <param name="targetLayerNames">All valid HLU layer names (excluding the source layer).</param>
        /// <param name="rules">Reassign rules configured in the options.</param>
        /// <param name="countFeaturesAsync">
        /// Async delegate that, given a SQL WHERE clause, returns the number of matching features
        /// in the source layer.
        /// </param>
        public ViewModelWindowReassign(
            string sourceLayerName,
            List<string> targetLayerNames,
            List<ReassignRule> rules,
            Func<string, Task<int>> countFeaturesAsync)
        {
            _sourceLayerName = sourceLayerName;
            _targetLayerNames = targetLayerNames;
            _rules = rules;
            _countFeaturesAsync = countFeaturesAsync;
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

        #region Events

        /// <summary>Fired when the user clicks OK to run the reassign. The window stays open.</summary>
        public delegate void RequestRunEventHandler(string targetLayerName, ReassignRule rule);
        public event RequestRunEventHandler RequestRun;

        /// <summary>Fired when the user clicks Cancel to close the window.</summary>
        public delegate void RequestCloseEventHandler();
        public event RequestCloseEventHandler RequestClose;

        #endregion Events

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
            RequestRun?.Invoke(_targetLayerName, _selectedRule);
        }

        private bool CanOk =>
            !string.IsNullOrEmpty(_targetLayerName) && _selectedRule != null && !_isCountingFeatures;

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
            RequestClose?.Invoke();
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
        /// Gets or sets the selected reassign rule. Automatically triggers a feature count refresh.
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
                    OnPropertyChanged(nameof(WhereClauseText));
                    _ = RefreshFeatureCountAsync(value);
                }
            }
        }

        /// <summary>
        /// Gets the SQL WHERE clause of the currently selected rule, or an empty string if none is selected.
        /// </summary>
        public string WhereClauseText =>
            _selectedRule?.WhereClause ?? string.Empty;

        /// <summary>
        /// Gets the feature count text to display. Shows a counting indicator while the query runs.
        /// </summary>
        public string FeatureCountText
        {
            get
            {
                if (_isCountingFeatures)
                    return "Counting…";
                if (_featureCount < 0)
                    return string.Empty;

                return _featureCount.ToString("N0");
            }
        }

        #endregion Control Properties

        #region Feature Count

        /// <summary>
        /// Queries the source layer for the number of features matching the selected rule's WHERE
        /// clause, then updates <see cref="FeatureCountText"/>.
        /// </summary>
        private async Task RefreshFeatureCountAsync(ReassignRule rule)
        {
            _featureCountError = null;

            if (_countFeaturesAsync == null || rule == null)
            {
                _featureCount = -1;
                _isCountingFeatures = false;
                OnPropertyChanged(nameof(FeatureCountText));
                OnPropertyChanged(nameof(SelectedRule));
                return;
            }

            _isCountingFeatures = true;
            OnPropertyChanged(nameof(FeatureCountText));

            try
            {
                int count = await _countFeaturesAsync(rule.WhereClause);
                _featureCount = count;
            }
            catch (Exception ex)
            {
                _featureCount = -1;
                _featureCountError = string.IsNullOrWhiteSpace(ex.Message)
                    ? "The where clause is invalid."
                    : ex.Message;
            }
            finally
            {
                _isCountingFeatures = false;
                OnPropertyChanged(nameof(FeatureCountText));
                OnPropertyChanged(nameof(SelectedRule));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        #endregion Feature Count

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
                        else if (!string.IsNullOrEmpty(_featureCountError))
                            error = _featureCountError;
                        break;
                }
                return error;
            }
        }

        #endregion IDataErrorInfo
    }
}
