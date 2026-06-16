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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
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
        private readonly List<string> _targetLayerNamesWithSkip;
        private readonly ObservableCollection<RuleRow> _ruleRows;

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
            _targetLayerNames = targetLayerNames ?? [];
            _countFeaturesAsync = countFeaturesAsync;

            // Create the target layer list with <Skip> option
            _targetLayerNamesWithSkip = ["<Skip>", .. _targetLayerNames];

            // Create a RuleRow for each rule and start counting features
            _ruleRows = [];
            if (rules != null)
            {
                foreach (var rule in rules)
                {
                    var ruleRow = new RuleRow(rule, _targetLayerNamesWithSkip);
                    _ruleRows.Add(ruleRow);

                    // Start counting features asynchronously
                    _ = CountFeaturesForRuleAsync(ruleRow);
                }
            }
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

        /// <summary>Fired when the user clicks OK to process all rules. Passes a list of (targetLayerName, rule) pairs for rules not marked as <Skip>.</summary>
        public delegate void RequestProcessAllEventHandler(List<(string targetLayerName, ReassignRule rule)> assignments);
        public event RequestProcessAllEventHandler RequestProcessAll;

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
            // Build the list of (targetLayerName, rule) assignments for rules not marked as <Skip>
            var assignments = _ruleRows
                .Where(r => !string.IsNullOrEmpty(r.SelectedTargetLayer) && r.SelectedTargetLayer != "<Skip>")
                .Select(r => (r.SelectedTargetLayer, r.Rule))
                .ToList();

            RequestProcessAll?.Invoke(assignments);
        }

        private bool CanOk
        {
            get
            {
                // Can OK if at least one rule has a valid target layer selected and no rules are still counting
                return _ruleRows != null &&
                       _ruleRows.Any(r => !string.IsNullOrEmpty(r.SelectedTargetLayer) && r.SelectedTargetLayer != "<Skip>") &&
                       !_ruleRows.Any(r => r.IsCountingFeatures);
            }
        }

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
        /// Gets the list of available target HLU layer names (with <Skip> option).
        /// </summary>
        public List<string> TargetLayerNamesWithSkip => _targetLayerNamesWithSkip;

        /// <summary>
        /// Gets the observable collection of rule rows for display in the DataGrid.
        /// </summary>
        public ObservableCollection<RuleRow> RuleRows => _ruleRows;

        #endregion Control Properties

        #region Feature Count

        /// <summary>
        /// Queries the source layer for the number of features matching the rule's WHERE
        /// clause, then updates the RuleRow's feature count.
        /// </summary>
        private async Task CountFeaturesForRuleAsync(RuleRow ruleRow)
        {
            if (_countFeaturesAsync == null || ruleRow == null)
            {
                ruleRow.FeatureCount = -1;
                ruleRow.IsCountingFeatures = false;
                return;
            }

            ruleRow.IsCountingFeatures = true;

            try
            {
                int count = await _countFeaturesAsync(ruleRow.Rule.WhereClause);
                ruleRow.FeatureCount = count;
                ruleRow.FeatureCountError = null;
            }
            catch (Exception ex)
            {
                ruleRow.FeatureCount = -1;
                ruleRow.FeatureCountError = string.IsNullOrWhiteSpace(ex.Message)
                    ? "The where clause is invalid."
                    : ex.Message;
            }
            finally
            {
                ruleRow.IsCountingFeatures = false;
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
                // No validation needed at the view model level anymore;
                // validation is per-row in the RuleRow class
                return null;
            }
        }

        #endregion IDataErrorInfo

        #region RuleRow Class

        /// <summary>
        /// Represents a single row in the rules DataGrid, containing a rule,
        /// its feature count, and the selected target layer.
        /// </summary>
        public class RuleRow : INotifyPropertyChanged
        {
            private string _selectedTargetLayer;
            private int _featureCount = -1;
            private bool _isCountingFeatures;
            private string _featureCountError;

            public RuleRow(ReassignRule rule, List<string> availableTargetLayers)
            {
                Rule = rule;
                AvailableTargetLayers = availableTargetLayers;
                // Default to <Skip> so users must explicitly choose a target
                _selectedTargetLayer = "<Skip>";
            }

            public ReassignRule Rule { get; }

            public string RuleName => Rule?.RuleName ?? string.Empty;

            public string WhereClause => Rule?.WhereClause ?? string.Empty;

            public List<string> AvailableTargetLayers { get; }

            public string SelectedTargetLayer
            {
                get => _selectedTargetLayer;
                set
                {
                    if (_selectedTargetLayer != value)
                    {
                        _selectedTargetLayer = value;
                        OnPropertyChanged(nameof(SelectedTargetLayer));
                    }
                }
            }

            public int FeatureCount
            {
                get => _featureCount;
                set
                {
                    if (_featureCount != value)
                    {
                        _featureCount = value;
                        OnPropertyChanged(nameof(FeatureCount));
                        OnPropertyChanged(nameof(FeatureCountText));
                        OnPropertyChanged(nameof(CanReassign));
                    }
                }
            }

            public bool IsCountingFeatures
            {
                get => _isCountingFeatures;
                set
                {
                    if (_isCountingFeatures != value)
                    {
                        _isCountingFeatures = value;
                        OnPropertyChanged(nameof(IsCountingFeatures));
                        OnPropertyChanged(nameof(FeatureCountText));
                        OnPropertyChanged(nameof(CanReassign));
                    }
                }
            }

            public string FeatureCountError
            {
                get => _featureCountError;
                set
                {
                    if (_featureCountError != value)
                    {
                        _featureCountError = value;
                        OnPropertyChanged(nameof(FeatureCountError));
                        OnPropertyChanged(nameof(CanReassign));
                    }
                }
            }

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

            /// <summary>
            /// Gets whether this rule can be reassigned (has features and no errors).
            /// </summary>
            public bool CanReassign => _featureCount > 0 && string.IsNullOrEmpty(_featureCountError) && !_isCountingFeatures;

            public event PropertyChangedEventHandler PropertyChanged;

            protected void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion RuleRow Class
    }
}
