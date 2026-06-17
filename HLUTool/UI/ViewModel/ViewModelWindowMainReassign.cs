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

using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using HLU.Enums;
using HLU.UI.View;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// Contains the view model for the main window reassign features functionality.
    /// </summary>
    internal class ViewModelWindowMainReassign
    {
        #region Fields

        private readonly ViewModelWindowMain _viewModelMain;

        private WindowReassign _windowReassign;
        private ViewModelWindowReassign _viewModelReassign;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="viewModelMain">The main window view model.</param>
        public ViewModelWindowMainReassign(ViewModelWindowMain viewModelMain)
        {
            _viewModelMain = viewModelMain;
        }

        #endregion Constructor

        #region Methods

        /// <summary>
        /// Initiates the reassign features process by opening the Reassign dialog.
        /// </summary>
        public async void InitiateReassign()
        {
            if (_viewModelMain == null)
                return;

            // Guard: must be in normal edit mode with an editable active layer.
            if (!_viewModelMain.CanReassign)
            {
                _viewModelMain.ShowError(
                    "Reassign Features is only available in normal edit mode with an editable active layer.",
                    MessageCategory.Update);
                return;
            }

            // Build the list of target layers: all available HLU layers except the active source layer
            // and only those with matching geometry type.
            string sourceLayerName = _viewModelMain.ActiveLayerName;
            HluGeometryTypes sourceGeometryType = _viewModelMain.GISApplication.HluGeometryType;

            // Filter target layers to only include those with matching geometry type.
            List<string> candidateLayerNames = [.. _viewModelMain.AvailableHLULayerNames.Where(n => n != sourceLayerName)];
            List<string> targetLayerNames = [];

            // Loop through candidate layers and check their geometry type asynchronously.
            foreach (string layerName in candidateLayerNames)
            {
                HluGeometryTypes layerGeometryType = await _viewModelMain.GISApplication.GetLayerGeometryTypeAsync(layerName);
                if (layerGeometryType != HluGeometryTypes.Unknown && layerGeometryType == sourceGeometryType)
                {
                    targetLayerNames.Add(layerName);
                }
            }

            // If no target layers are available, show a message and exit.
            if (targetLayerNames.Count == 0)
            {
                MessageBox.Show(
                    $"No other HLU layers with matching geometry type ({sourceGeometryType}) are available to reassign features to.\n\n" +
                    "Please ensure at least two HLU layers with the same geometry type are present in the active map.",
                    "HLU: Reassign Features",
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);
                return;
            }

            // Get the reassign rules configured in the options.
            List<ReassignRule> rules = _viewModelMain.AddInSettings?.ReassignRules ?? [];

            // If no rules are configured, show a message and exit.
            if (rules.Count == 0)
            {
                MessageBox.Show(
                    "No reassign rules have been configured.\n\n" +
                    "Please add at least one rule in Options → Application → Reassign.",
                    "HLU: Reassign Features",
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);
                return;
            }

            // Open the Reassign dialog.
            _windowReassign = new WindowReassign
            {
                Owner = FrameworkApplication.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Topmost = true
            };

            // Create the view model for the Reassign dialog.
            _viewModelReassign = new ViewModelWindowReassign(
                sourceLayerName,
                targetLayerNames,
                rules,
                countFeaturesAsync: whereClause =>
                    _viewModelMain.GISApplication.CountFeaturesMatchingWhereClauseAsync(whereClause))
            {
                DisplayName = "Reassign Features"
            };

            // Guard against double subscription.
            _viewModelReassign.RequestProcessAll -= ViewModelReassign_RequestProcessAll;
            _viewModelReassign.RequestProcessAll +=
                new ViewModelWindowReassign.RequestProcessAllEventHandler(ViewModelReassign_RequestProcessAll);

            _viewModelReassign.RequestClose -= ViewModelReassign_RequestClose;
            _viewModelReassign.RequestClose +=
                new ViewModelWindowReassign.RequestCloseEventHandler(ViewModelReassign_RequestClose);

            // Show the dialog and set its DataContext to the view model.
            _windowReassign.DataContext = _viewModelReassign;
            _windowReassign.ShowDialog();
        }

        /// <summary>
        /// Handles the RequestProcessAll event from the Reassign dialog (OK button).
        /// Processes all rule assignments sequentially, then closes the dialog.
        /// </summary>
        /// <param name="assignments">List of tuples containing target layer name and reassign rule to process.</param>
        private async void ViewModelReassign_RequestProcessAll(List<(string targetLayerName, ReassignRule rule)> assignments)
        {
            // If no assignments were provided, exit early.
            if (assignments == null || assignments.Count == 0)
                return;

            // Hide the dialog and show the main UI processing indicator.
            _windowReassign.Hide();

            try
            {
                int totalMoved = 0;
                int successCount = 0;
                int failureCount = 0;
                List<string> errorMessages = [];

                // Process each rule assignment sequentially
                foreach (var (targetLayerName, rule) in assignments)
                {
                    _viewModelMain.ChangeCursor(Cursors.Wait, $"Reassigning features using rule '{rule.RuleName}'…");

                    try
                    {
                        // Execute the reassignment for the current rule and target layer.
                        int moved = await ExecuteSingleReassignAsync(targetLayerName, rule);

                        if (moved >= 0)
                        {
                            totalMoved += moved;
                            successCount++;
                        }
                        else
                        {
                            failureCount++;
                            errorMessages.Add($"Rule '{rule.RuleName}': target layer '{targetLayerName}' not found.");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        failureCount++;
                        errorMessages.Add($"Rule '{rule.RuleName}': {ex.Message}");
                    }
                }

                // Show summary message.
                string summaryMessage = $"{totalMoved:N0} feature(s) reassigned using {successCount:N0} rule(s).";

                // If there were any failures, show a warning message with details.
                if (failureCount > 0)
                {
                    summaryMessage += $"\n\n{failureCount:N0} rule(s) failed:";
                    foreach (var errMsg in errorMessages)
                    {
                        summaryMessage += $"\n• {errMsg}";
                    }

                    MessageBox.Show(
                        summaryMessage,
                        "HLU: Reassign Features",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else
                {
                    // Show a success message in the main window's info area.
                    _viewModelMain.ShowInfo(summaryMessage, MessageCategory.Update);
                }

                // Close the dialog
                _viewModelReassign.RequestProcessAll -= ViewModelReassign_RequestProcessAll;
                _viewModelReassign.RequestClose -= ViewModelReassign_RequestClose;
                _windowReassign.Close();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    $"Reassign Features process failed.\n\n{ex.Message}",
                    "HLU: Reassign Features",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                _windowReassign.Show();
            }
            finally
            {
                _viewModelMain.ChangeCursor(Cursors.Arrow);
            }
        }

        /// <summary>
        /// Handles the RequestClose event from the Reassign dialog (Cancel button).
        /// </summary>
        private void ViewModelReassign_RequestClose()
        {
            _viewModelReassign.RequestProcessAll -= ViewModelReassign_RequestProcessAll;
            _viewModelReassign.RequestClose -= ViewModelReassign_RequestClose;
            _windowReassign.Close();
        }

        /// <summary>
        /// Executes a single feature reassignment: moves features matching the rule's WHERE clause
        /// from the active source HLU layer to <paramref name="targetLayerName"/>.
        /// </summary>
        /// <param name="targetLayerName">The name of the target HLU layer to move features to.</param>
        /// <param name="rule">The reassign rule containing the WHERE clause to filter features.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the number of features moved, or -1 if the target layer was not found.</returns>
        private async Task<int> ExecuteSingleReassignAsync(string targetLayerName, ReassignRule rule)
        {
            // Build and execute the GIS edit operation.
            EditOperation editOperation = new()
            {
                Name = $"Reassign Features – {rule.RuleName}",
                ProgressMessage = $"Moving features to '{targetLayerName}'…"
            };

            // Queue the reassignment operation.
            int moved = await _viewModelMain.GISApplication.ReassignFeaturesAsync(
                targetLayerName,
                rule.WhereClause,
                editOperation);

            // If the target layer was not found, return -1 to indicate failure.
            if (moved < 0)
            {
                return -1;
            }

            // If no features matched the rule, return 0 to indicate that no features were moved.
            if (moved == 0)
            {
                return 0;
            }

            // Execute the queued edits.
            bool executed = await editOperation.ExecuteAsync();
            if (!executed)
            {
                string details = editOperation.ErrorMessage;
                if (string.IsNullOrWhiteSpace(details))
                    details = "No additional details were provided by the edit operation.";

                throw new System.Exception($"GIS edit operation failed. {details}");
            }

            // Save edits.
            bool saved = await ArcGIS.Desktop.Core.Project.Current.SaveEditsAsync();
            if (!saved)
                throw new System.Exception("Features were moved but edits could not be saved.");

            return moved;
        }

        #endregion Methods
    }
}