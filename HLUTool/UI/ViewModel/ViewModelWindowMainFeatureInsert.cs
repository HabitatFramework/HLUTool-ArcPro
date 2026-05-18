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
using ArcGIS.Desktop.Mapping;
using HLU.Data.Connection;
using HLU.Data.Model;
using HLU.Enums;
using HLU.Exceptions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// View model for registering newly created GIS features (features with a null INCID)
    /// against new database records.  Supports two modes:
    /// <list type="bullet">
    ///   <item>All selected features ? a single new INCID (same-INCID insert).</item>
    ///   <item>Each selected feature ? its own new INCID (separate-INCID insert).</item>
    /// </list>
    /// </summary>
    internal class ViewModelWindowMainFeatureInsert
    {
        #region Fields

        private readonly ViewModelWindowMain _viewModelMain;

        /// <summary>Width of the formatted fragment ID strings (e.g. 5 ? "00001").</summary>
        private const int FragIdWidth = 5;

        /// <summary>First fragment ID string for a brand-new INCID.</summary>
        private static readonly string FirstFragId = "1".PadLeft(FragIdWidth, '0');

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of <see cref="ViewModelWindowMainFeatureInsert"/>.
        /// </summary>
        public ViewModelWindowMainFeatureInsert(ViewModelWindowMain viewModelMain)
        {
            _viewModelMain = viewModelMain;
        }

        #endregion Constructor

        #region Public Entry Points

        /// <summary>
        /// Registers all currently selected new (null-INCID) features under a single new INCID,
        /// assigning each a unique sequential fragment ID.
        /// </summary>
        /// <returns>A tuple of (success, featureCount, incidCount).</returns>
        internal Task<(bool success, int featureCount, int incidCount)> InsertFeaturesSameIncidAsync() =>
            PerformInsertAsync(sameIncid: true);

        /// <summary>
        /// Registers each currently selected new (null-INCID) feature under its own new INCID,
        /// assigning every feature fragment ID "00001".
        /// </summary>
        /// <returns>A tuple of (success, featureCount, incidCount).</returns>
        internal Task<(bool success, int featureCount, int incidCount)> InsertFeaturesSeparateIncidsAsync() =>
            PerformInsertAsync(sameIncid: false);

        #endregion Public Entry Points

        #region Core Logic

        /// <summary>
        /// Core insert logic shared by both entry points.
        /// </summary>
        /// <returns>A tuple of (success, featureCount, incidCount).</returns>
        private async Task<(bool success, int featureCount, int incidCount)> PerformInsertAsync(bool sameIncid)
        {
            _viewModelMain.ChangeCursor(Cursors.Wait, "Inserting features ...");
            bool success = false;
            int featureCount = 0;
            int incidCount = 0;

            _viewModelMain.DataBase.BeginTransaction(true, IsolationLevel.ReadCommitted);

            EditOperation editOperation = new()
            {
                Name = sameIncid ? "Insert Features — Same INCID" : "Insert Features — Separate INCIDs"
            };

            bool gisExecuted = false;

            try
            {
                // ---------------------------------------------------------------
                // 1. Get the selected OIDs from the active map selection.
                //    Must run on the Queued Task thread.
                // ---------------------------------------------------------------
                IReadOnlyList<long> selectedOids = await ArcGIS.Desktop.Framework.Threading.Tasks.QueuedTask.Run(() =>
                {
                    var selection = MapView.Active?.Map
                        ?.GetLayersAsFlattenedList()
                        .OfType<FeatureLayer>()
                        .FirstOrDefault(l => l.Name == _viewModelMain.ActiveLayerName)
                        ?.GetSelection();

                    return (IReadOnlyList<long>)(selection?.GetObjectIDs() ?? []);
                });

                if (selectedOids.Count == 0)
                    throw new HLUToolException("No features are selected in the active layer.");

                // ---------------------------------------------------------------
                // 2. Build the OID ? (incid, fragid) assignment map.
                // ---------------------------------------------------------------
                // Round timestamp to whole seconds to avoid rounding differences.
                DateTime currDtTm = DateTime.Now;
                DateTime nowDtTm = new(currDtTm.Year, currDtTm.Month, currDtTm.Day,
                    currDtTm.Hour, currDtTm.Minute, currDtTm.Second, DateTimeKind.Local);

                List<long> orderedOids = [.. selectedOids.OrderBy(o => o)];
                Dictionary<long, (string incid, string fragid)> oidAssignments = [];

                // Tracks all (incid, fragid) pairs that need mm rows, in OID order.
                List<(string incid, string fragid)> mmRowsToCreate = [];

                if (sameIncid)
                {
                    // All features share one new INCID; each gets a sequential fragid.
                    string newIncid = _viewModelMain.NextIncid;
                    CreateIncidRow(newIncid, nowDtTm);

                    int fragNum = 1;
                    foreach (long oid in orderedOids)
                    {
                        string fragid = fragNum.ToString().PadLeft(FragIdWidth, '0');
                        oidAssignments[oid] = (newIncid, fragid);
                        mmRowsToCreate.Add((newIncid, fragid));
                        fragNum++;
                    }

                    featureCount = orderedOids.Count;
                    incidCount = 1;
                }
                else
                {
                    // Each feature gets its own new INCID, all with fragment "00001".
                    foreach (long oid in orderedOids)
                    {
                        string newIncid = _viewModelMain.NextIncid;
                        CreateIncidRow(newIncid, nowDtTm);
                        oidAssignments[oid] = (newIncid, FirstFragId);
                        mmRowsToCreate.Add((newIncid, FirstFragId));
                    }

                    featureCount = orderedOids.Count;
                    incidCount = orderedOids.Count;
                }

                // ---------------------------------------------------------------
                // 3. Insert incid_mm_* rows into the database shadow copy.
                // ---------------------------------------------------------------
                foreach (var (incid, fragid) in mmRowsToCreate)
                    CreateMMRow(incid, fragid);

                // ---------------------------------------------------------------
                // 4. Write incid + fragid to the GIS layer; capture history.
                // ---------------------------------------------------------------
                DataTable historyTable = await _viewModelMain.GISApplication.RegisterNewFeaturesAsync(
                    oidAssignments,
                    _viewModelMain.HistoryColumns,
                    editOperation);

                if (historyTable == null || historyTable.Rows.Count == 0)
                    throw new HLUToolException("No GIS features were updated during the insert.");

                // ---------------------------------------------------------------
                // 5. Execute the queued GIS edits.
                // ---------------------------------------------------------------
                bool executed = await editOperation.ExecuteAsync();
                if (!executed)
                {
                    string details = editOperation.ErrorMessage;
                    if (String.IsNullOrWhiteSpace(details))
                        details = "No additional details were provided by the edit operation.";
                    throw new HLUToolException($"Failed to update GIS layer. {details}");
                }

                bool saved = await ArcGIS.Desktop.Core.Project.Current.SaveEditsAsync();
                if (!saved)
                    throw new HLUToolException("GIS edits were applied but could not be saved.");

                gisExecuted = true;

                // ---------------------------------------------------------------
                // 6. Write history — one call per unique INCID.
                // ---------------------------------------------------------------
                ViewModelWindowMainHistory vmHist = new(_viewModelMain);

                // Remove the "oid" helper column before passing to HistoryWrite.
                const string OidColumnName = "oid";
                DataTable historyForWrite = historyTable.Copy();
                if (historyForWrite.Columns.Contains(OidColumnName))
                    historyForWrite.Columns.Remove(OidColumnName);

                // Group history rows by the incid column so HistoryWrite gets the right fixedValues.
                string incidColName = _viewModelMain.HluDataset.history.incidColumn.ColumnName;
                int incidColOrdinal = _viewModelMain.HluDataset.history.incidColumn.Ordinal;

                // Find the incid column in the history table (may be named "incid" or "modified_incid").
                string histIncidColName = historyForWrite.Columns.Contains(incidColName)
                    ? incidColName
                    : historyForWrite.Columns.Contains("modified_" + incidColName)
                        ? "modified_" + incidColName
                        : null;

                if (histIncidColName != null)
                {
                    var groups = historyForWrite.AsEnumerable()
                        .GroupBy(r => r[histIncidColName]?.ToString());

                    foreach (var grp in groups)
                    {
                        string groupIncid = grp.Key;
                        DataTable groupTable = historyForWrite.Clone();
                        foreach (DataRow r in grp)
                            groupTable.ImportRow(r);

                        Dictionary<int, string> fixedValues = new()
                        {
                            { incidColOrdinal, groupIncid }
                        };

                        vmHist.HistoryWrite(fixedValues, groupTable, Operations.FeatureInsert, nowDtTm);
                    }
                }
                else
                {
                    // Fallback: write all rows without a per-incid fixedValue (history will be incomplete).
                    vmHist.HistoryWrite(null, historyForWrite, Operations.FeatureInsert, nowDtTm);
                }

                // ---------------------------------------------------------------
                // 7. Commit and tidy up.
                // ---------------------------------------------------------------
                _viewModelMain.DataBase.CommitTransaction();
                _viewModelMain.HluDataset.AcceptChanges();

                success = true;
            }
            catch (Exception ex)
            {
                _viewModelMain.DataBase.RollbackTransaction();

                if (!gisExecuted)
                {
                    try { editOperation.Abort(); }
                    catch { /* ignore */ }
                }

                string exMessage = DbBase.GetSqlErrorMessage(ex);
                MessageBox.Show(
                    $"Feature insert failed. The error message returned was:\n\n{exMessage}",
                    "HLU: Insert Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (success)
                {
                    // Recount and refresh so the new INCIDs appear in the UI.
                    _viewModelMain.IncidRowCount(true);
                    await _viewModelMain.ClearFilterAsync(false);
                    _viewModelMain.RefillIncidTable = true;
                    await _viewModelMain.GetMapSelectionAsync(false);
                }

                _viewModelMain.ChangeCursor(Cursors.Arrow);
            }

            return (success, featureCount, incidCount);
        }

        #endregion Core Logic

        #region Helpers

        /// <summary>
        /// Creates a minimal <c>incid</c> row in the database for the supplied INCID string,
        /// setting the required audit fields and mandatory defaults.
        /// <c>habitat_version</c> is set to "0" (no primary code assigned yet) and
        /// <c>boundary_base_map</c>/<c>digitisation_base_map</c> default to "UK" (unknown).
        /// The row is rejected from the in-memory DataTable immediately after the DB INSERT
        /// so that normal navigation is unaffected.
        /// </summary>
        private void CreateIncidRow(string newIncid, DateTime nowDtTm)
        {
            HluDataSet.incidRow newRow = _viewModelMain.IncidTable.NewincidRow();
            newRow.incid = newIncid;
            newRow.habitat_version = "0";
            newRow.boundary_base_map = "UK";
            newRow.digitisation_base_map = "UK";
            newRow.created_date = nowDtTm;
            newRow.created_user_id = _viewModelMain.UserID;
            newRow.last_modified_date = nowDtTm;
            newRow.last_modified_user_id = _viewModelMain.UserID;

            _viewModelMain.IncidTable.AddincidRow(newRow);

            if (_viewModelMain.HluTableAdapterManager.incidTableAdapter.Update(
                    _viewModelMain.HluDataset.incid) == -1)
                throw new Exception($"Failed to insert row into table [{_viewModelMain.HluDataset.incid.TableName}].");

            // Discard the added row from in-memory; it was persisted to the DB above.
            _viewModelMain.IncidTable.RejectChanges();
        }

        /// <summary>
        /// Creates one row in the appropriate <c>incid_mm_*</c> shadow table for the
        /// geometry type of the active GIS layer, then INSERTs it into the database.
        /// The row is rejected from the in-memory DataTable afterwards.
        /// All optional columns (toid, habitat fields, shape dimensions) are left as
        /// <see cref="DBNull"/> — they are populated when the user saves attributes later.
        /// </summary>
        private void CreateMMRow(string incid, string fragid)
        {
            switch (_viewModelMain.GisLayerType)
            {
                case HluGeometryTypes.Line:
                {
                    var table = _viewModelMain.HluDataset.incid_mm_lines;
                    HluDataSet.incid_mm_linesRow row = table.Newincid_mm_linesRow();
                    row.incid = incid;
                    row.fragid = fragid;
                    // toid is left as NULL — user-created features have no OS MasterMap toid.
                    // Remaining habitat / geometry columns default to DBNull on a new row.
                    table.Addincid_mm_linesRow(row);

                    _viewModelMain.HluTableAdapterManager.incid_mm_linesTableAdapter ??=
                        new HluTableAdapter<HluDataSet.incid_mm_linesDataTable, HluDataSet.incid_mm_linesRow>(_viewModelMain.DataBase);

                    if (_viewModelMain.HluTableAdapterManager.incid_mm_linesTableAdapter.Update(table) == -1)
                        throw new Exception($"Failed to insert row into table [{table.TableName}].");

                    table.RejectChanges();
                    break;
                }

                case HluGeometryTypes.Point:
                {
                    var table = _viewModelMain.HluDataset.incid_mm_points;
                    HluDataSet.incid_mm_pointsRow row = table.Newincid_mm_pointsRow();
                    row.incid = incid;
                    row.fragid = fragid;
                    // toid is left as NULL — user-created features have no OS MasterMap toid.
                    // Remaining habitat columns default to DBNull on a new row.
                    table.Addincid_mm_pointsRow(row);

                    _viewModelMain.HluTableAdapterManager.incid_mm_pointsTableAdapter ??=
                        new HluTableAdapter<HluDataSet.incid_mm_pointsDataTable, HluDataSet.incid_mm_pointsRow>(_viewModelMain.DataBase);

                    if (_viewModelMain.HluTableAdapterManager.incid_mm_pointsTableAdapter.Update(table) == -1)
                        throw new Exception($"Failed to insert row into table [{table.TableName}].");

                    table.RejectChanges();
                    break;
                }

                default: // Polygon
                {
                    var table = _viewModelMain.HluDataset.incid_mm_polygons;
                    HluDataSet.incid_mm_polygonsRow row = table.Newincid_mm_polygonsRow();
                    row.incid = incid;
                    row.fragid = fragid;
                    // toid is left as NULL — user-created features have no OS MasterMap toid.
                    // Remaining habitat fields and shape dimensions default to DBNull on a new row.
                    table.Addincid_mm_polygonsRow(row);

                    _viewModelMain.HluTableAdapterManager.incid_mm_polygonsTableAdapter ??=
                        new HluTableAdapter<HluDataSet.incid_mm_polygonsDataTable, HluDataSet.incid_mm_polygonsRow>(_viewModelMain.DataBase);

                    if (_viewModelMain.HluTableAdapterManager.incid_mm_polygonsTableAdapter.Update(table) == -1)
                        throw new Exception($"Failed to insert row into table [{table.TableName}].");

                    table.RejectChanges();
                    break;
                }
            }
        }

        #endregion Helpers
    }
}
