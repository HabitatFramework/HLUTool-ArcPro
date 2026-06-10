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
        public void InitiateReassign()
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

            // Build the list of target layers: all available HLU layers except the active source layer.
            string sourceLayerName = _viewModelMain.ActiveLayerName;
            List<string> targetLayerNames = [.. _viewModelMain.AvailableHLULayerNames.Where(n => n != sourceLayerName)];

            if (targetLayerNames.Count == 0)
            {
                MessageBox.Show(
                    "No other HLU layers are available to reassign features to.\n\n" +
                    "Please ensure at least two HLU layers are present in the active map.",
                    "HLU: Reassign Features",
                    MessageBoxButton.OK,
                    MessageBoxImage.Exclamation);
                return;
            }

            // Get the reassign rules configured in the options.
            List<ReassignRule> rules = _viewModelMain.AddInSettings?.ReassignRules ?? [];

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
            _viewModelReassign.RequestRun -= ViewModelReassign_RequestRun;
            _viewModelReassign.RequestRun +=
                new ViewModelWindowReassign.RequestRunEventHandler(ViewModelReassign_RequestRun);

            _viewModelReassign.RequestClose -= ViewModelReassign_RequestClose;
            _viewModelReassign.RequestClose +=
                new ViewModelWindowReassign.RequestCloseEventHandler(ViewModelReassign_RequestClose);

            _windowReassign.DataContext = _viewModelReassign;
            _windowReassign.ShowDialog();
        }

        /// <summary>
        /// Handles the RequestRun event from the Reassign dialog (OK button).
        /// Runs the reassign without closing the window so the user can run further rules.
        /// </summary>
        private async void ViewModelReassign_RequestRun(string targetLayerName, ReassignRule rule)
        {
            await ExecuteReassignAsync(targetLayerName, rule);
        }

        /// <summary>
        /// Handles the RequestClose event from the Reassign dialog (Cancel button).
        /// </summary>
        private void ViewModelReassign_RequestClose()
        {
            _viewModelReassign.RequestRun -= ViewModelReassign_RequestRun;
            _viewModelReassign.RequestClose -= ViewModelReassign_RequestClose;
            _windowReassign.Close();
        }

        /// <summary>
        /// Executes the feature reassignment: moves features matching the rule's WHERE clause
        /// from the active source HLU layer to <paramref name="targetLayerName"/>.
        /// </summary>
        private async Task ExecuteReassignAsync(string targetLayerName, ReassignRule rule)
        {
            // Hide the dialog and show the main UI processing indicator.
            _windowReassign.Hide();
            _viewModelMain.ChangeCursor(Cursors.Wait, $"Reassigning features using rule '{rule.RuleName}'…");

            try
            {
                // Build and execute the GIS edit operation.
                EditOperation editOperation = new()
                {
                    Name = $"Reassign Features – {rule.RuleName}",
                    ProgressMessage = $"Moving features to '{targetLayerName}'…"
                };

                int moved = await _viewModelMain.GISApplication.ReassignFeaturesAsync(
                    targetLayerName,
                    rule.WhereClause,
                    editOperation);

                if (moved < 0)
                {
                    MessageBox.Show(
                        $"The target layer '{targetLayerName}' could not be found in the active map.",
                        "HLU: Reassign Features",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (moved == 0)
                {
                    MessageBox.Show(
                        $"No features matched the rule '{rule.RuleName}'.\n\nNo changes were made.",
                        "HLU: Reassign Features",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
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

                _viewModelMain.ShowInfo(
                    $"{moved} feature(s) reassigned to layer '{targetLayerName}' using rule '{rule.RuleName}'.",
                    MessageCategory.Update);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    $"Reassign Features failed.\n\n{ex.Message}",
                    "HLU: Reassign Features",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                // Restore the cursor and show the dialog again.
                _viewModelMain.ChangeCursor(Cursors.Arrow);
                _windowReassign.Show();
            }
        }

        #endregion Methods
    }
}
