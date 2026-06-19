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
        private int _totalSourceFeatures = -1;
        private bool _isCountingTotalFeatures;

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
            // Set up the view model fields
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
                    ruleRow.PropertyChanged += RuleRow_PropertyChanged;
                    _ruleRows.Add(ruleRow);

                    // Start counting features asynchronously
                    _ = CountFeaturesForRuleAsync(ruleRow);
                }
            }

            // Start counting total features in the source layer
            _ = CountTotalSourceFeaturesAsync();
        }

        #endregion Constructor

        #region ViewModelBase Members

        /// <summary>
        /// Gets or sets the display name of the dialog window.
        /// </summary>
        public override string DisplayName
        {
            get => _displayName;
            set => _displayName = value;
        }

        /// <summary>
        /// Gets the window title for the dialog, which is the same as the display name.
        /// </summary>
        public override string WindowTitle => _displayName;

        #endregion ViewModelBase Members

        #region Events

        /// <summary>
        /// Fired when the user clicks OK to process all rules. Passes a list of
        /// (targetLayerName, rule) pairs for rules not marked as <Skip>.
        /// </summary>
        public delegate void RequestProcessAllEventHandler(List<(string targetLayerName, ReassignRule rule)> assignments);

        public event RequestProcessAllEventHandler RequestProcessAll;

        /// <summary>
        /// Fired when the user clicks Cancel to close the window.
        /// </summary>
        public delegate void RequestCloseEventHandler();

        /// <summary>
        /// Fired when the user clicks Cancel to close the window.
        /// </summary>
        public event RequestCloseEventHandler RequestClose;

        #endregion Events

        #region Ok Command

        /// <summary>
        /// Gets the command that is executed when the user clicks the OK button.
        /// </summary>
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

        /// <summary> Executes when the user clicks the OK button. Builds a list of
        /// (targetLayerName, rule) assignments for rules not marked as <Skip> and raises the
        /// RequestProcessAll event. </summary> <param name="param"></param>
        private void OkCommandClick(object param)
        {
            // Build the list of (targetLayerName, rule) assignments for rules not marked as <Skip>
            var assignments = _ruleRows
                .Where(r => !string.IsNullOrEmpty(r.SelectedTargetLayer) && r.SelectedTargetLayer != "<Skip>")
                .Select(r => (r.SelectedTargetLayer, r.Rule))
                .ToList();

            RequestProcessAll?.Invoke(assignments);
        }

        /// <summary>
        /// Gets whether the OK button can be clicked. The OK button is enabled if at least one rule
        /// has a valid target layer selected and no rules are still counting features.
        /// </summary>
        private bool CanOk
        {
            get
            {
                // Can OK if at least one rule has a valid target layer selected and no rules are
                // still counting
                return _ruleRows != null &&
                       _ruleRows.Any(r => !string.IsNullOrEmpty(r.SelectedTargetLayer) && r.SelectedTargetLayer != "<Skip>") &&
                       !_ruleRows.Any(r => r.IsCountingFeatures);
            }
        }

        #endregion Ok Command

        #region Cancel Command

        /// <summary>
        /// Gets the command that is executed when the user clicks the Cancel button.
        /// </summary>
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

        /// <summary>
        /// Executes when the user clicks the Cancel button. Raises the RequestClose event to close
        /// the window.
        /// </summary>
        /// <param name="param"></param>
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

        /// <summary>
        /// Gets the total number of features in the source layer.
        /// </summary>
        public int TotalSourceFeatures
        {
            get => _totalSourceFeatures;
            private set
            {
                if (_totalSourceFeatures != value)
                {
                    _totalSourceFeatures = value;
                    OnPropertyChanged(nameof(TotalSourceFeatures));
                    OnPropertyChanged(nameof(TotalSourceFeaturesText));
                    OnPropertyChanged(nameof(TotalRuleFeaturesText));
                    OnPropertyChanged(nameof(ShowFeatureCountMismatch));
                    OnPropertyChanged(nameof(FeatureCountMismatchMessage));
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the total source features count is being calculated.
        /// </summary>
        public bool IsCountingTotalFeatures
        {
            get => _isCountingTotalFeatures;
            private set
            {
                if (_isCountingTotalFeatures != value)
                {
                    _isCountingTotalFeatures = value;
                    OnPropertyChanged(nameof(IsCountingTotalFeatures));
                    OnPropertyChanged(nameof(TotalSourceFeaturesText));
                }
            }
        }

        /// <summary>
        /// Gets the display text for the total source features count.
        /// </summary>
        public string TotalSourceFeaturesText
        {
            get
            {
                if (_isCountingTotalFeatures)
                    return "Counting…";
                if (_totalSourceFeatures < 0)
                    return "Unknown";
                return _totalSourceFeatures.ToString("N0");
            }
        }

        /// <summary>
        /// Gets the total number of features represented by all rules.
        /// </summary>
        public int TotalRuleFeatures
        {
            get
            {
                if (_ruleRows == null || _ruleRows.Any(r => r.IsCountingFeatures))
                    return -1;
                return _ruleRows.Where(r => r.FeatureCount >= 0).Sum(r => r.FeatureCount);
            }
        }

        /// <summary>
        /// Gets the display text for the total rule features count.
        /// </summary>
        public string TotalRuleFeaturesText
        {
            get
            {
                if (_ruleRows != null && _ruleRows.Any(r => r.IsCountingFeatures))
                    return "Counting…";

                int total = TotalRuleFeatures;
                if (total < 0)
                    return "Unknown";

                return total.ToString("N0");
            }
        }

        /// <summary>
        /// Gets a value indicating whether to show the feature count mismatch message.
        /// </summary>
        public bool ShowFeatureCountMismatch
        {
            get
            {
                if (_isCountingTotalFeatures || _totalSourceFeatures < 0)
                    return false;

                if (_ruleRows != null && _ruleRows.Any(r => r.IsCountingFeatures))
                    return false;

                int totalRules = TotalRuleFeatures;
                if (totalRules < 0)
                    return false;

                return totalRules != _totalSourceFeatures;
            }
        }

        /// <summary>
        /// Gets the feature count mismatch message.
        /// </summary>
        public string FeatureCountMismatchMessage
        {
            get
            {
                if (!ShowFeatureCountMismatch)
                    return string.Empty;

                int totalRules = TotalRuleFeatures;
                int difference = Math.Abs(_totalSourceFeatures - totalRules);

                if (totalRules < _totalSourceFeatures)
                    return $"Warning: Rules account for {difference:N0} fewer features than the source layer.";
                else
                    return $"Warning: Rules account for {difference:N0} more features than the source layer (possible overlap).";
            }
        }

        #endregion Control Properties

        #region Feature Count

        /// <summary>
        /// Queries the source layer for the number of features matching the rule's WHERE
        /// clause, then updates the RuleRow's feature count.
        /// </summary>
        /// <param name="ruleRow">The rule row to update with the feature count.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task CountFeaturesForRuleAsync(RuleRow ruleRow)
        {
            // If the count delegate is not provided or the ruleRow is null, set feature count to -1
            // and return.
            if (_countFeaturesAsync == null || ruleRow == null)
            {
                ruleRow.FeatureCount = -1;
                ruleRow.IsCountingFeatures = false;
                return;
            }

            // Set the IsCountingFeatures flag to true to indicate that the feature count is being calculated.
            ruleRow.IsCountingFeatures = true;

            // Perform the feature count asynchronously and update the RuleRow with the result.
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
                // Reset the IsCountingFeatures flag to false to indicate that the feature count
                // calculation is complete.
                ruleRow.IsCountingFeatures = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// Queries the source layer for the total number of features (no WHERE clause).
        /// </summary>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task CountTotalSourceFeaturesAsync()
        {
            // If the count delegate is not provided, set total feature count to -1 and return.
            if (_countFeaturesAsync == null)
            {
                TotalSourceFeatures = -1;
                IsCountingTotalFeatures = false;
                return;
            }

            // Set the IsCountingTotalFeatures flag to true.
            IsCountingTotalFeatures = true;

            // Perform the total feature count asynchronously (empty WHERE clause = all features).
            try
            {
                int count = await _countFeaturesAsync(string.Empty);

                // Reset the IsCountingTotalFeatures flag.
                IsCountingTotalFeatures = false;

                TotalSourceFeatures = count;
            }
            catch (Exception)
            {
                TotalSourceFeatures = -1;
            }
            finally
            {
                // Reset the IsCountingTotalFeatures flag.
                IsCountingTotalFeatures = false;
            }
        }

        /// <summary>
        /// Handles property changes in rule rows to update the total rule features count.
        /// </summary>
        /// <param name="sender">The rule row that raised the event.</param>
        /// <param name="e">The property changed event arguments.</param>
        private void RuleRow_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // When a rule row's feature count changes, update the total rule features properties.
            if (e.PropertyName == nameof(RuleRow.FeatureCount) || e.PropertyName == nameof(RuleRow.IsCountingFeatures))
            {
                OnPropertyChanged(nameof(TotalRuleFeaturesText));
                OnPropertyChanged(nameof(ShowFeatureCountMismatch));
                OnPropertyChanged(nameof(FeatureCountMismatchMessage));
            }
        }

        #endregion Feature Count

        #region IDataErrorInfo

        /// <summary>
        /// Gets the error message for the entire object. Not used in this view model.
        /// </summary>
        public string Error => null;

        /// <summary>
        /// Gets the error message for a specific property. Not used in this view model.
        /// </summary>
        /// <param name="columnName">The name of the property to retrieve the error message for.</param>
        /// <returns>The error message for the specified property.</returns>
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

            /// <summary>
            /// Constructs a RuleRow for the given rule and available target layers.
            /// </summary>
            /// <param name="rule">The rule associated with this row.</param>
            /// <param name="availableTargetLayers">The list of available target layers for reassignment.</param>
            public RuleRow(ReassignRule rule, List<string> availableTargetLayers)
            {
                Rule = rule;
                AvailableTargetLayers = availableTargetLayers;
                // Default to <Skip> so users must explicitly choose a target
                _selectedTargetLayer = "<Skip>";
            }

            /// <summary>
            /// Gets the rule associated with this row.
            /// </summary>
            public ReassignRule Rule
            {
                get;
            }

            /// <summary>
            /// Gets the name of the rule for display in the DataGrid.
            /// </summary>
            public string RuleName => Rule?.RuleName ?? string.Empty;

            /// <summary>
            /// Gets the WHERE clause of the rule for display in the DataGrid.
            /// </summary>
            public string WhereClause => Rule?.WhereClause ?? string.Empty;

            /// <summary>
            /// Gets the list of available target layers for reassignment, including the <Skip> option.
            /// </summary>
            public List<string> AvailableTargetLayers
            {
                get;
            }

            /// <summary>
            /// Gets or sets the selected target layer for reassignment.
            /// </summary>
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

            /// <summary>
            /// Gets or sets the feature count for this rule.
            /// </summary>
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

            /// <summary>
            /// Gets or sets a value indicating whether the feature count is being calculated.
            /// </summary>
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

            /// <summary>
            /// Gets or sets the error message for the feature count calculation.
            /// </summary>
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

            /// <summary>
            /// Gets a display string for the feature count.
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

            /// <summary>
            /// Gets whether this rule can be reassigned (has features and no errors).
            /// </summary>
            public bool CanReassign => _featureCount > 0 && string.IsNullOrEmpty(_featureCountError) && !_isCountingFeatures;

            /// <summary>
            /// Gets or sets the event handler for property changes, used to notify the UI when properties change.
            /// </summary>
            public event PropertyChangedEventHandler PropertyChanged;

            /// <summary>
            /// Raises the PropertyChanged event for the specified property name.
            /// </summary>
            /// <param name="propertyName"></param>
            protected void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion RuleRow Class
    }
}