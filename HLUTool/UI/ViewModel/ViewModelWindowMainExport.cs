﻿// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2014, 2018 Sussex Biodiversity Record Centre
// Copyright © 2019 London & South East Record Centres (LaSER)
// Copyright © 2019-2022 Greenspace Information for Greater London CIC
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
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Globalization;
using HLU.Data.Connection;
using HLU.Data.Model;
using HLU.GISApplication;
using HLU.Properties;
using HLU.UI.View;
using HLU.Data;
using HLU.Date;

//DONE: using Microsoft.Office.Interop.Access.Dao
using Microsoft.Office.Interop.Access.Dao;

namespace HLU.UI.ViewModel
{
    partial class ViewModelWindowMainExport
    {
        public static HluDataSet HluDatasetStatic = null;

        ViewModelWindowMain _viewModelMain;
        private WindowExport _windowExport;
        private ViewModelExport _viewModelExport;

        private string _lastTableName;
        private int _tableCount;
        private int _fieldCount;
        private int _incidOrdinal;
        private int _conditionIdOrdinal;
        private int _conditionDateStartOrdinal;
        private int _conditionDateEndOrdinal;
        private int _conditionDateTypeOrdinal;
        private int _matrixIdOrdinal;
        private int _formationIdOrdinal;
        private int _managementIdOrdinal;
        private int _complexIdOrdinal;
        private int _bapIdOrdinal;
        private int _bapTypeOrdinal;
        private int _bapQualityOrdinal;
        private int _sourceIdOrdinal;
        private List<int> _sourceDateStartOrdinals;
        private List<int> _sourceDateEndOrdinals;
        private List<int> _sourceDateTypeOrdinals;
        private int _attributesLength;

        public ViewModelWindowMainExport(ViewModelWindowMain viewModelMain)
        {
            _viewModelMain = viewModelMain;
        }

        public void InitiateExport()
        {
            _windowExport = new WindowExport
            {
                //DONE: App.Current.MainWindow
                //_windowExport.Owner = App.Current.MainWindow;
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            // Fill all export formats if there are any export fields
            // defined for the export format.
            _viewModelMain.HluTableAdapterManager.exportsTableAdapter.ClearBeforeFill = true;
            _viewModelMain.HluTableAdapterManager.exportsTableAdapter.Fill(_viewModelMain.HluDataset.exports,
                String.Format("EXISTS (SELECT {0}.{1} FROM {0} WHERE {0}.{1} = {2}.{3})",
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.exports_fields.TableName),
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.exports_fields.export_idColumn.ColumnName),
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.exports.TableName),
                _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.exports.export_idColumn.ColumnName)));

            // If there are no exports formats defined that have any
            // export fields then exit.
            if (_viewModelMain.HluDataset.exports.Count == 0)
            {
                MessageBox.Show("Cannot export: There are no export formats defined.",
                    "HLU: Export", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            //---------------------------------------------------------------------
            // FIX: 102 Display correct number of selected features on export.
            //
            // Display the export interface to prompt the user
            // to select which export format they want to use.
            int fragCount = 0;
            _viewModelMain.GISApplication.CountMapSelection(ref fragCount);
            //_viewModelExport = new ViewModelExport(_viewModelMain.GisSelection == null ? 0 :
            //_viewModelMain.GisSelection.Rows.Count, _viewModelMain.GISApplication.HluLayerName,
            //_viewModelMain.GISApplication.ApplicationType, _viewModelMain.HluDataset.exports);
            _viewModelExport = new(_viewModelMain.GisSelection == null ? 0 :
                fragCount, _viewModelMain.GISApplication.HluLayerName,
                _viewModelMain.HluDataset.exports)
            {
                DisplayName = "Export"
            };
            //---------------------------------------------------------------------
            _viewModelExport.RequestClose += new ViewModelExport.RequestCloseEventHandler(_viewModelExport_RequestClose);

            _windowExport.DataContext = _viewModelExport;

            _windowExport.ShowDialog();
        }

        //---------------------------------------------------------------------
        // CHANGED: CR14 (Exporting IHS codes or descriptions)
        // Enable users to specify if individual fields should be
        // exported with descriptions, rather than the whole export,
        // by moving this option to the exports_fields table.
        //
        private void _viewModelExport_RequestClose(int exportID, bool selectedOnly)
        //---------------------------------------------------------------------
        {
            _viewModelExport.RequestClose -= _viewModelExport_RequestClose;
            _windowExport.Close();

            // If the user selected an export format then
            // perform the export using that format.
            if (exportID != -1)
            {
                DispatcherHelper.DoEvents();
                Export(exportID, selectedOnly);
            }
        }

        /// <summary>
        /// Exports the combined GIS and database data using the
        /// specified export format.
        /// </summary>
        /// <param name="userExportId">The export format selected by the user.</param>
        /// <param name="selectedOnly">If set to <c>true</c> export only selected incids/features.</param>
        /// <exception cref="System.Exception">
        /// Failed to find a table alias that does not match a table name in the HLU dataset
        /// or
        /// No export fields are defined for format 'x'
        /// or
        /// Export query did not retrieve any rows
        /// </exception>
        private void Export(int userExportId, bool selectedOnly)
        {
            string tempPath = null;

            try
            {
                _viewModelMain.ChangeCursor(Cursors.Wait, "Creating export table ...");

                // Create a new unique table name to export to.
                string tableAlias = GetTableAlias();
                if (tableAlias == null)
                    throw new Exception("Failed to find a table alias that does not match a table name in the HLU dataset");

                // Retrieve the export fields for the export format
                // selected by the user from the database.
                _viewModelMain.HluTableAdapterManager.exports_fieldsTableAdapter.ClearBeforeFill = true;
                _viewModelMain.HluTableAdapterManager.exports_fieldsTableAdapter.Fill(
                    _viewModelMain.HluDataset.exports_fields, String.Format("{0} = {1} ORDER BY {2}, {3}",
                    _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.exports_fields.export_idColumn.ColumnName),
                    userExportId,
                    _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.exports_fields.table_nameColumn.ColumnName),
                    _viewModelMain.DataBase.QuoteIdentifier(_viewModelMain.HluDataset.exports_fields.field_ordinalColumn.ColumnName)));

                // Exit if there are no export fields for this format
                // (this should not be possible).
                if (_viewModelMain.HluDataset.exports_fields.Count == 0)
                    throw new Exception(String.Format("No export fields are defined for format '{0}'",
                        _viewModelMain.HluDataset.exports.FindByexport_id(userExportId).export_name));

                // Exit if there is no incid field for this format.
                if (!_viewModelMain.HluDataset.exports_fields.Any(f => f.column_name == _viewModelMain.IncidTable.incidColumn.ColumnName))
                    throw new Exception(String.Format("The export format '{0}' does not contain the column 'incid'",
                        _viewModelMain.HluDataset.exports.FindByexport_id(userExportId).export_name));

                // Build a new export data table and also determine the
                // number of output fields, a string of the output field
                // names, the from SQL clause, and the incid
                // field ordinal.
                DataTable exportTable;
                int[][] fieldMapTemplate;
                StringBuilder targetList;
                StringBuilder fromClause;
                int[] sortOrdinals;
                int[] conditionOrdinals;
                int[] matrixOrdinals;
                int[] formationOrdinals;
                int[] managementOrdinals;
                int[] complexOrdinals;
                int[] bapOrdinals;
                int[] sourceOrdinals;
                List<ExportField> exportFields = [];
                ExportJoins(tableAlias, ref exportFields, out exportTable,
                    out fieldMapTemplate, out targetList, out fromClause, out sortOrdinals, out conditionOrdinals,
                    out matrixOrdinals, out formationOrdinals, out managementOrdinals, out complexOrdinals,
                    out bapOrdinals, out sourceOrdinals);

                // Add the length of the GIS layer fields to the length of
                // field attributes exported from the database (excluding
                // the incid field which is already included).
                _attributesLength += 387;

                // Set the export filter conditions, depending if all the
                // records are to be exported or only the selected features.
                List<List<SqlFilterCondition>> exportFilter = null;
                if (selectedOnly)
                {
                    // If the where clause is not already set then get it
                    // using the GIS selection.
                    if ((_viewModelMain.IncidSelectionWhereClause == null) &&
                        (_viewModelMain.GisSelection != null) && (_viewModelMain.GisSelection.Rows.Count > 0))
                    {
                        // Get the incid column ordinal
                        int incidOrd = _viewModelMain.IncidTable.incidColumn.Ordinal;

                        // Get a unique list of incids from the selected GIS features
                        IEnumerable<string> incidsSelected = _viewModelMain.GisSelection.AsEnumerable()
                        .GroupBy(r => r.Field<string>(_viewModelMain.GisSelection.Columns[0].ColumnName)).Select(g => g.Key).OrderBy(s => s);

                        // Set the where clause to match the list of selected incids.
                        _viewModelMain.IncidSelectionWhereClause = ViewModelWindowMainHelpers.IncidSelectionToWhereClause(
                            250, incidOrd, _viewModelMain.IncidTable, incidsSelected);

                        // Set the export filter to the where clause.
                        exportFilter = _viewModelMain.IncidSelectionWhereClause;
                    }
                    else
                    {
                        // Combine all the where clauses into a single list (so
                        // that it can be re-chunked later into larger chunks
                        // than the standard chunk based on the IncidPageSize.
                        exportFilter = [_viewModelMain.IncidSelectionWhereClause.SelectMany(l => l).ToList()];
                    }
                }
                else
                {
                    SqlFilterCondition cond = new("AND",
                        _viewModelMain.IncidTable, _viewModelMain.IncidTable.incidColumn, null)
                    {
                        Operator = "IS NOT NULL"
                    };
                    exportFilter = new List<List<SqlFilterCondition>>([
                        new List<SqlFilterCondition>([cond]) ]);
                }

                // Warn the user when the export will be very large.
                //
                // Count the number of incids to be exported.
                int rowCount = 0;
                if (selectedOnly)
                    rowCount = _viewModelMain.IncidsSelectedMapCount;
                else
                    rowCount = _viewModelMain.IncidRowCount(false);

                // Warn the user if the export is VERY large.
                if (rowCount > 50000)
                {
                    MessageBoxResult userResponse = MessageBoxResult.No;
                    userResponse = MessageBox.Show("This export operation may take some time.\n\nDo you wish to proceed?", "HLU: Export",
                        MessageBoxButton.YesNo, MessageBoxImage.Question);

                    // Cancel the export.
                    if (userResponse != MessageBoxResult.Yes)
                        return;
                }

                // Create a temporary database containing an empty attribute data table.
                tempPath = ExportEmptyMdb(exportTable);

                // Exit if there was an error creating the database.
                if (String.IsNullOrEmpty(tempPath))
                    return;

                // Call the GIS application export prompt method to prompt
                // the user for the name and location of the new GIS layer.
                bool exportReady = false;
                exportReady = _viewModelMain.GISApplication.ExportPrompt(tempPath, exportTable.TableName, _attributesLength, selectedOnly);

                // Exit if no export file was selected or the export exceeds the max size.
                if (!exportReady)
                    return;

                _viewModelMain.ChangeCursor(Cursors.Wait, "Exporting to temporary table ...");

                // Export the attribute data to a temporary database.
                int exportRowCount;
                exportRowCount = ExportMdb(tempPath, targetList.ToString(), fromClause.ToString(), exportFilter,
                    _viewModelMain.DataBase, exportFields, exportTable, sortOrdinals, conditionOrdinals,
                    matrixOrdinals, formationOrdinals, managementOrdinals, complexOrdinals, bapOrdinals, sourceOrdinals,
                    fieldMapTemplate);

                // Exit if the database is empty.
                if (exportRowCount == 0)
                    return;

                _viewModelMain.ChangeCursor(Cursors.Wait, "Exporting from GIS ...");

                // Call the GIS application export method to join the
                // temporary attribute data to the GIS feature layer
                // and save them as a new GIS layer.
                _viewModelMain.GISApplication.Export(tempPath, exportTable.TableName, selectedOnly);

                // Remove the current record filter.
                //_viewModelMain.IncidSelection = null;
                //_viewModelMain.GisSelection = null;
                //_viewModelMain.OnPropertyChanged(nameof(IsFiltered));
                //_viewModelMain.OnPropertyChanged(nameof(StatusIncid));
            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Export failed. The error message was:\n\n{0}.",
                    ex.Message), "HLU: Export", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Try and delete the temporary database or, if that fails,
                // make a note of it to delete it later.
                if (!String.IsNullOrEmpty(tempPath))
                {
                    string[] tempFiles = Directory.GetFiles(Path.GetDirectoryName(tempPath),
                        Path.GetFileNameWithoutExtension(tempPath) + ".*");
                    foreach (string fName in tempFiles)
                    {
                        try { File.Delete(fName); }
                        catch { _viewModelMain.ExportMdbs.Add(fName); }
                    }
                }

                _viewModelMain.ChangeCursor(Cursors.Arrow, null);
            }
        }

        private void ExportJoins(string tableAlias, ref List<ExportField> exportFields, out DataTable exportTable,
            out int[][] fieldMapTemplate, out StringBuilder targetList, out StringBuilder fromClause,
            out int[] sortOrdinals, out int[] conditionOrdinals,
            out int[] matrixOrdinals, out int[] formationOrdinals,
            out int[] managementOrdinals, out int[] complexOrdinals,
            out int[] bapOrdinals, out int[] sourceOrdinals)
        {
            exportTable = new("HluExport");
            targetList = new();
            List<string> fromList = [];
            List<string> leftJoined = [];
            fromClause = new();
            sortOrdinals = null;
            conditionOrdinals = null;
            matrixOrdinals = null;
            formationOrdinals = null;
            managementOrdinals = null;
            complexOrdinals = null;
            bapOrdinals = null;
            sourceOrdinals = null;

            int tableAliasNum = 1;
            bool firstJoin = true;
            _lastTableName = null;
            _fieldCount = 0;
            int fieldLength = 0;
            _attributesLength = 0;
            _tableCount = 0;

            //
            foreach (HluDataSet.exports_fieldsRow r in
                _viewModelMain.HluDataset.exports_fields.OrderBy(r => r.field_ordinal))
            {
                // Get the field length of the input table/column.
                fieldLength = GetFieldLength(r.table_name, r.column_ordinal);

                // Enable new 'empty' fields to be included in exports.
                if (r.table_name.Equals("<none>", StringComparison.CurrentCultureIgnoreCase))
                {
                    // Override the input field length(s) if an export
                    // field length has been set.
                    if (!r.IsNull(_viewModelMain.HluDataset.exports_fields.field_lengthColumn) &&
                        r.field_length > 0)
                        fieldLength = r.field_length;

                    AddExportColumn(0, r.table_name, r.column_name, r.field_name,
                        r.field_type, fieldLength, !r.IsNull(_viewModelMain.HluDataset.exports_fields.field_formatColumn) ? r.field_format : null,
                        ref exportFields);
                    continue;
                }

                // Determine if this field is to be output multiple times,
                // once for each row in the relevant table up to the
                // maximum fields_count value.
                bool multipleFields = false;
                if (!r.IsNull(_viewModelMain.HluDataset.exports_fields.fields_countColumn))
                    multipleFields = true;

                // Add the required table to the list of sql tables in
                // the from clause.
                string currTable = _viewModelMain.DataBase.QualifyTableName(r.table_name);
                if (!fromList.Contains(currTable))
                {
                    fromList.Add(currTable);

                    var incidRelation = _viewModelMain.HluDataset.incid.ChildRelations.Cast<DataRelation>()
                        .Where(dr => dr.ChildTable.TableName == r.table_name);

                    if (!incidRelation.Any())
                    {
                        fromClause.Append(currTable);
                    }
                    else
                    {
                        DataRelation incidRel = incidRelation.ElementAt(0);
                        if (firstJoin)
                            firstJoin = false;
                        else
                            fromClause.Insert(0, "(").Append(')');
                        fromClause.Append(RelationJoinClause("LEFT", currTable, true,
                            _viewModelMain.DataBase.QuoteIdentifier(
                            incidRel.ParentTable.TableName), incidRel, fromList));
                        leftJoined.Add(currTable);
                    }
                }

                // Get the relationships for the table/column if a
                // value from a lookup table is required.
                string fieldFormat = !r.IsNull(_viewModelMain.HluDataset.exports_fields.field_formatColumn) ? r.field_format : null;
                //---------------------------------------------------------------------
                // CHANGED: CR14 (Exporting IHS codes or descriptions)
                // Enable users to specify if individual fields should be
                // exported with descriptions in the exports_fields table.
                //
                var relations = ((fieldFormat != null) && (fieldFormat.Equals("both", StringComparison.CurrentCultureIgnoreCase)
                    || fieldFormat.Equals("lookup", StringComparison.CurrentCultureIgnoreCase))) ? _viewModelMain.HluDataRelations.Where(rel =>
                    rel.ChildTable.TableName == r.table_name && rel.ChildColumns
                    .Count(ch => ch.ColumnName == r.column_name) == 1) : [];
                //---------------------------------------------------------------------

                switch (relations.Count())
                {
                    case 0:     // If this field does not have any related lookup tables.

                        // Add the field to the sql target list.
                        targetList.Append(String.Format(",{0}.{1} AS {2}", currTable,
                            _viewModelMain.DataBase.QuoteIdentifier(r.column_name), r.field_name.Replace("<no>", "")));

                        // Enable text field lengths to be specified in
                        // the export format.
                        //
                        // Override the input field length(s) if an export
                        // field length has been set.
                        if (!r.IsNull(_viewModelMain.HluDataset.exports_fields.field_lengthColumn) &&
                            r.field_length > 0)
                            fieldLength = r.field_length;

                        // Add the field to the sql list of export table columns.
                        AddExportColumn(multipleFields ? r.fields_count : 0, r.table_name, r.column_name, r.field_name,
                            r.field_type, fieldLength,
                            !r.IsNull(_viewModelMain.HluDataset.exports_fields.field_formatColumn) ? r.field_format : String.Empty,
                            ref exportFields);
                        break;
                    case 1:     // If this field has a related lookup table.

                        DataRelation lutRelation = relations.ElementAt(0);
                        string parentTable = _viewModelMain.DataBase.QualifyTableName(lutRelation.ParentTable.TableName);

                        string parentTableAlias = tableAlias + tableAliasNum++;
                        fromList.Add(parentTable);

                        // Determine the related lookup table field name and
                        // field ordinal.
                        string lutFieldName;
                        int lutFieldOrdinal;
                        if ((r.table_name == _viewModelMain.HluDataset.incid_sources.TableName) && (IdSuffixRegex().IsMatch(r.column_name)))
                        {
                            string lutSourceFieldName = Settings.Default.LutSourceFieldName;
                            int lutSourceFieldOrdinal = Settings.Default.LutSourceFieldOrdinal;

                            lutFieldName = lutSourceFieldName;
                            lutFieldOrdinal = lutSourceFieldOrdinal - 1;
                        }
                        else if ((r.table_name == _viewModelMain.HluDataset.incid.TableName) && (UseridSuffixRegex().IsMatch(r.column_name)))
                        {
                            string lutUserFieldName = Settings.Default.LutUserFieldName;
                            int lutUserFieldOrdinal = Settings.Default.LutUserFieldOrdinal;
                            lutFieldName = lutUserFieldName;
                            lutFieldOrdinal = lutUserFieldOrdinal - 1;
                        }
                        else
                        {
                            string lutDescriptionFieldName = Settings.Default.LutDescriptionFieldName;
                            int lutDescriptionFieldOrdinal = Settings.Default.LutDescriptionFieldOrdinal;

                            lutFieldName = lutDescriptionFieldName;
                            lutFieldOrdinal = lutDescriptionFieldOrdinal - 1;
                        }

                        // Get the list of columns for the lookup table.
                        DataColumn[] lutColumns = new DataColumn[lutRelation.ParentTable.Columns.Count];
                        lutRelation.ParentTable.Columns.CopyTo(lutColumns, 0);

                        // If the lookup table contains the required field name.
                        if (lutRelation.ParentTable.Columns.Contains(lutFieldName))
                        {
                            //---------------------------------------------------------------------
                            // CHANGED: CR15 (Concatenate IHS codes and descriptions)
                            // Enable users to specify if individual fields should be
                            // exported with both codes and descriptions concatenated
                            // together.
                            //
                            // If both the original field and it's corresponding lookup
                            // table field are required then add them both to the sql
                            // target list.
                            if ((fieldFormat != null) && (fieldFormat.Equals("both", StringComparison.CurrentCultureIgnoreCase)))
                            {
                                // Add the corresponding lookup table field to the sql
                                // target list.
                                targetList.Append(String.Format(",{0}.{1} {5} {6} {5} {2}.{3} AS {4}",
                                    currTable,
                                    _viewModelMain.DataBase.QuoteIdentifier(r.column_name),
                                    parentTableAlias,
                                    _viewModelMain.DataBase.QuoteIdentifier(lutFieldName),
                                    r.field_name.Replace("<no>", ""),
                                    _viewModelMain.DataBase.ConcatenateOperator,
                                    _viewModelMain.DataBase.QuoteValue(" : ")));

                                // Set the field length of the export field to the input
                                // field length plus the lookup table field length plus 3
                                // for the concatenation string length.
                                fieldLength += lutColumns.First(c => c.ColumnName == lutFieldName).MaxLength + 3;
                            }
                            //---------------------------------------------------------------------
                            else
                            {
                                // Add the corresponding lookup table field to the sql
                                // target list.
                                targetList.Append(String.Format(",{0}.{1} AS {2}",
                                    parentTableAlias,
                                    _viewModelMain.DataBase.QuoteIdentifier(lutFieldName),
                                    r.field_name.Replace("<no>", "")));

                                // Set the field length of the lookup table field.
                                fieldLength = lutColumns.First(c => c.ColumnName == lutFieldName).MaxLength;
                            }

                            // Enable text field lengths to be specified in
                            // the export format.
                            //
                            // Override the input field length(s) if an export
                            // field length has been set.
                            if (!r.IsNull(_viewModelMain.HluDataset.exports_fields.field_lengthColumn) &&
                                r.field_length > 0)
                                fieldLength = r.field_length;

                            // Add the field to the sql list of export table columns.
                            AddExportColumn(multipleFields ? r.fields_count : 0, r.table_name, r.column_name, r.field_name,
                                r.field_type, fieldLength, !r.IsNull(_viewModelMain.HluDataset.exports_fields.field_formatColumn) ? r.field_format : String.Empty,
                                ref exportFields);
                        }
                        // If the lookup table does not contains the required field
                        // name, but does contain the required field ordinal.
                        else if (lutRelation.ParentTable.Columns.Count >= lutFieldOrdinal)
                        {
                            //---------------------------------------------------------------------
                            // CHANGED: CR15 (Concatenate IHS codes and descriptions)
                            // Enable users to specify if individual fields should be
                            // exported with both codes and descriptions concatenated
                            // together.
                            //
                            // If both the original field and it's corresponding lookup
                            // table field are required then add them both to the sql
                            // target list.
                            if ((fieldFormat != null) && (fieldFormat.Equals("both", StringComparison.CurrentCultureIgnoreCase)))
                            {
                                // Add the corresponding lookup table field to the sql
                                // target list.
                                targetList.Append(String.Format(",{0}.{1} {5} {6} {5} {2}.{3} AS {4}",
                                    currTable,
                                    _viewModelMain.DataBase.QuoteIdentifier(r.column_name),
                                    parentTableAlias,
                                    _viewModelMain.DataBase.QuoteIdentifier(lutRelation.ParentTable.Columns[lutFieldOrdinal].ColumnName),
                                    r.field_name.Replace("<no>", ""),
                                    _viewModelMain.DataBase.ConcatenateOperator,
                                    _viewModelMain.DataBase.QuoteValue(" : ")));

                                // Set the field length of the lookup table field.
                                fieldLength = lutColumns.First(c => c.ColumnName == lutFieldName).MaxLength;
                            }
                            //---------------------------------------------------------------------
                            else
                            {
                                // Add the corresponding lookup table field to the sql
                                // target list.
                                targetList.Append(String.Format(",{0}.{1} AS {2}",
                                    parentTableAlias,
                                    _viewModelMain.DataBase.QuoteIdentifier(lutRelation.ParentTable.Columns[lutFieldOrdinal].ColumnName),
                                    r.field_name.Replace("<no>", "")));

                                // Set the field length of the lookup table field.
                                fieldLength = lutColumns.First(c => c.ColumnName == lutFieldName).MaxLength;
                            }

                            // Enable text field lengths to be specified in
                            // the export format.
                            //
                            // Override the input field length(s) if an export
                            // field length has been set.
                            if (!r.IsNull(_viewModelMain.HluDataset.exports_fields.field_lengthColumn) &&
                                r.field_length > 0)
                                fieldLength = r.field_length;

                            // Add the field to the sql list of export table columns.
                            AddExportColumn(multipleFields ? r.fields_count : 0, r.table_name, r.column_name, r.field_name,
                                r.field_type, fieldLength, !r.IsNull(_viewModelMain.HluDataset.exports_fields.field_formatColumn) ? r.field_format : null,
                                ref exportFields);
                        }
                        else
                        {
                            continue;
                        }


                        // Make all joins LEFT joins for simplicity and to allow for null
                        // foreign key values.
                        string joinType = "LEFT";
                        leftJoined.Add(parentTableAlias);

                        if (firstJoin)
                            firstJoin = false;
                        else
                            fromClause.Insert(0, "(").Append(')');

                        fromClause.Append(RelationJoinClause(joinType, currTable,
                            false, parentTableAlias, lutRelation, fromList));

                        break;
                }
            }

            // Interweave multiple record fields from the same
            // table together.
            //
            // Create a new field map template with as many items
            // as there are input fields.
            fieldMapTemplate = new int[exportFields.Max(e => e.FieldOrdinal) + 1][];

            // Loop through all the export fields, adding them as columns
            // in the export table and adding them to the field map template.
            int fieldTotal = 0;
            int primaryKeyOrdinal = -1;
            _incidOrdinal = -1;
            _conditionIdOrdinal = -1;
            _conditionDateStartOrdinal = -1;
            _conditionDateEndOrdinal = -1;
            _conditionDateTypeOrdinal = -1;
            _matrixIdOrdinal = -1;
            _formationIdOrdinal = -1;
            _managementIdOrdinal = -1;
            _complexIdOrdinal = -1;
            _bapIdOrdinal = -1;
            _bapTypeOrdinal = -1;
            _bapQualityOrdinal = -1;
            _sourceIdOrdinal = -1;
            _sourceDateStartOrdinals = [];
            _sourceDateEndOrdinals = [];
            _sourceDateTypeOrdinals = [];
            int sourceSortOrderOrdinal = -1;
            List<int> sortFields = [];
            List<int> conditionFields = [];
            List<int> matrixFields = [];
            List<int> formationFields = [];
            List<int> managementFields = [];
            List<int> complexFields = [];
            List<int> bapFields = [];
            List<int> sourceFields = [];
            foreach (ExportField f in exportFields.OrderBy(f => f.FieldOrder))
            {
                // Create a new data column for the field.
                DataColumn c = new(f.FieldName, f.FieldType);

                // If the field is an autonumber set the relevant
                // auto increment properties.
                if (f.AutoNum == true) c.AutoIncrement = true;

                // If the field is a text field and has a maximum length
                // then set the maximum length property.
                if ((f.FieldType == System.Type.GetType("System.String")) &&
                    (f.FieldLength > 0)) c.MaxLength = f.FieldLength;

                // Add the field as a new column in the export table.
                exportTable.Columns.Add(c);

                // If the field will not be sourced from the database.
                if (f.FieldOrdinal == -1)
                {
                    // Increment the total number of fields to be exported.
                    fieldTotal += 1;

                    // Skip adding the field to the field map template.
                    continue;
                }

                // If the field is not repeated and refers to the incid column
                // in the incid table.
                if ((f.FieldsCount == 0) && ((f.TableName == _viewModelMain.HluDataset.incid.TableName) &&
                    (f.ColumnName == _viewModelMain.HluDataset.incid.incidColumn.ColumnName)))
                {
                    // Store the input field position for use later 
                    // when exporting the data.
                    _incidOrdinal = f.FieldOrdinal;

                    // Add the input field position to the list of fields
                    // that will be used to sort the input records.
                    sortFields.Add(f.FieldOrdinal + 1);

                    // Store the output field position for use later 
                    // as the primary index field ordinal.
                    primaryKeyOrdinal = fieldTotal;
                }

                // If the table is the incid_condition table.
                if (f.TableName == _viewModelMain.HluDataset.incid_condition.TableName)
                {
                    // Add the output field position to the list of fields
                    // that are from the condition table.
                    conditionFields.Add(fieldTotal);

                    // If the field refers to the condition_id column then store
                    // the input field ordinal for use later as the unique
                    // incid_condition field ordinal.
                    if (f.ColumnName == _viewModelMain.HluDataset.incid_condition.incid_condition_idColumn.ColumnName)
                        _conditionIdOrdinal = f.FieldOrdinal;
                    // If the field refers to the condition_date_start column then
                    // store the input field ordinal for use later.
                    else if (f.ColumnName == _viewModelMain.HluDataset.incid_condition.condition_date_startColumn.ColumnName)
                        _conditionDateStartOrdinal = f.FieldOrdinal;
                    // If the field refers to the condition_date_end column then
                    // store the input field ordinal for use later.
                    else if (f.ColumnName == _viewModelMain.HluDataset.incid_condition.condition_date_endColumn.ColumnName)
                        _conditionDateEndOrdinal = f.FieldOrdinal;
                    // If the field refers to the condition_date_type column then
                    // store the input field ordinal for use later.
                    else if (f.ColumnName == _viewModelMain.HluDataset.incid_condition.condition_date_typeColumn.ColumnName)
                        _conditionDateTypeOrdinal = f.FieldOrdinal;
                }
                // If the table is the incid_ihs_matrix table.
                else if (f.TableName == _viewModelMain.HluDataset.incid_ihs_matrix.TableName)
                {
                    // Add the output field position to the list of fields
                    // that are from the matrix table.
                    matrixFields.Add(fieldTotal);

                    // If the field refers to the matrix_id column then store
                    // the input field ordinal for use later as the unique
                    // incid_ihs_matrix field ordinal.
                    if (f.ColumnName == _viewModelMain.HluDataset.incid_ihs_matrix.matrix_idColumn.ColumnName)
                        _matrixIdOrdinal = f.FieldOrdinal;
                }
                // If the table is the incid_ihs_formation table.
                else if (f.TableName == _viewModelMain.HluDataset.incid_ihs_formation.TableName)
                {
                    // Add the output field position to the list of fields
                    // that are from the formation table.
                    formationFields.Add(fieldTotal);

                    // If the field refers to the formation_id column then store
                    // the input field ordinal for use later as the unique
                    // incid_ihs_formation field ordinal.
                    if (f.ColumnName == _viewModelMain.HluDataset.incid_ihs_formation.formation_idColumn.ColumnName)
                        _formationIdOrdinal = f.FieldOrdinal;
                }
                // If the table is the incid_ihs_management table.
                else if (f.TableName == _viewModelMain.HluDataset.incid_ihs_management.TableName)
                {
                    // Add the output field position to the list of fields
                    // that are from the management table.
                    managementFields.Add(fieldTotal);

                    // If the field refers to the management_id column then store
                    // the input field ordinal for use later as the unique
                    // incid_ihs_management field ordinal.
                    if (f.ColumnName == _viewModelMain.HluDataset.incid_ihs_management.management_idColumn.ColumnName)
                        _managementIdOrdinal = f.FieldOrdinal;
                }
                // If the table is the incid_ihs_complex table.
                else if (f.TableName == _viewModelMain.HluDataset.incid_ihs_complex.TableName)
                {
                    // Add the output field position to the list of fields
                    // that are from the complex table.
                    complexFields.Add(fieldTotal);

                    // If the field refers to the complex_id column then store
                    // the input field ordinal for use later as the unique
                    // incid_ihs_complex field ordinal.
                    if (f.ColumnName == _viewModelMain.HluDataset.incid_ihs_complex.complex_idColumn.ColumnName)
                        _complexIdOrdinal = f.FieldOrdinal;
                }
                // If the table is the incid_bap table.
                else if (f.TableName == _viewModelMain.HluDataset.incid_bap.TableName)
                {
                    // Add the output field position to the list of fields
                    // that are from the bap table.
                    bapFields.Add(fieldTotal);

                    // If the field refers to the bap_id column then store
                    // the input field ordinal for use later as the unique
                    // incid_bap field ordinal.
                    if (f.ColumnName == _viewModelMain.HluDataset.incid_bap.bap_idColumn.ColumnName)
                        _bapIdOrdinal = f.FieldOrdinal;
                }
                // If the table is the incid_sources table.
                else if (f.TableName == _viewModelMain.HluDataset.incid_sources.TableName)
                {
                    // Add the output field position to the list of fields
                    // that are from the sources table.
                    sourceFields.Add(fieldTotal);

                    // If the field refers to the source_id column and is
                    // retrieved in it's 'raw' integer state then store
                    // the input field ordinal for use later as the unique
                    // incid_source field ordinal.
                    if ((f.ColumnName == _viewModelMain.HluDataset.incid_sources.source_idColumn.ColumnName) &&
                        ((string.IsNullOrEmpty(f.FieldFormat)) || (f.FieldFormat.Equals("code", StringComparison.CurrentCultureIgnoreCase))))
                        _sourceIdOrdinal = f.FieldOrdinal;
                    // If the field refers to the source_sort_order column then
                    // store the input field ordinal for use later.
                    else if (f.ColumnName == _viewModelMain.HluDataset.incid_sources.sort_orderColumn.ColumnName)
                        sourceSortOrderOrdinal = f.FieldOrdinal;
                    // If the field refers to the source_date_start column then
                    // store the input field ordinal for use later.
                    else if (f.ColumnName == _viewModelMain.HluDataset.incid_sources.source_date_startColumn.ColumnName)
                        _sourceDateStartOrdinals.Add(f.FieldOrdinal);
                    // If the field refers to the source_date_end column then
                    // store the input field ordinal for use later.
                    else if (f.ColumnName == _viewModelMain.HluDataset.incid_sources.source_date_endColumn.ColumnName)
                        _sourceDateEndOrdinals.Add(f.FieldOrdinal);
                    // If the field refers to the source_date_type column then
                    // store the input field ordinal for use later.
                    else if (f.ColumnName == _viewModelMain.HluDataset.incid_sources.source_date_typeColumn.ColumnName)
                        _sourceDateTypeOrdinals.Add(f.FieldOrdinal);
                }

                // Set the field mapping for the current field ordinal.
                List<int> fieldMap;
                if ((fieldMapTemplate[f.FieldOrdinal] != null) && (f.AutoNum != true))
                    fieldMap = fieldMapTemplate[f.FieldOrdinal].ToList();
                else
                {
                    fieldMap = [];
                    fieldMap.Add(f.FieldOrdinal);
                }

                // Add the current field number to the field map for
                // this field ordinal.
                fieldMap.Add(fieldTotal);

                // Update the field map template for this field ordinal.
                fieldMapTemplate[f.FieldOrdinal] = fieldMap.ToArray();

                // Increment the total number of fields to be exported.
                fieldTotal += 1;
            }

            // Get the last input field ordinal.
            int lastFieldOrdinal = exportFields.Max(e => e.FieldOrdinal);

            // If any incid_condition fields are in the export file.
            if ((exportFields.Any(f => f.TableName == _viewModelMain.HluDataset.incid_condition.TableName)))
            {
                // If the incid_condition_id column is not included then add
                // it so that different conditions can be identified.
                if (_conditionIdOrdinal == -1)
                {
                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}", _viewModelMain.HluDataset.incid_condition.TableName,
                        _viewModelMain.HluDataset.incid_condition.incid_condition_idColumn.ColumnName, _viewModelMain.HluDataset.incid_condition.incid_condition_idColumn.ColumnName));

                    // Store the input field ordinal for use
                    // later as the unique incid_condition field ordinal.
                    _conditionIdOrdinal = lastFieldOrdinal += 1;
                }

                // Store all of the condition date fields for use later when
                // formatting the attribute data.
                //
                // If the condition_date_start column is not included then add
                // it for use later.
                if (_conditionDateStartOrdinal == -1)
                {
                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}", _viewModelMain.HluDataset.incid_condition.TableName,
                        _viewModelMain.HluDataset.incid_condition.condition_date_startColumn.ColumnName, _viewModelMain.HluDataset.incid_condition.condition_date_startColumn.ColumnName));

                    // Store the input field ordinal for use later.
                    _conditionDateStartOrdinal = lastFieldOrdinal += 1;
                }

                // If the condition_date_end column is not included then add
                // it for use later.
                if (_conditionDateEndOrdinal == -1)
                {
                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}", _viewModelMain.HluDataset.incid_condition.TableName,
                        _viewModelMain.HluDataset.incid_condition.condition_date_endColumn.ColumnName, _viewModelMain.HluDataset.incid_condition.condition_date_endColumn.ColumnName));

                    // Store the input field ordinal for use later.
                    _conditionDateEndOrdinal = lastFieldOrdinal += 1;
                }

                // If the condition_date_type column is not included then add
                // it for use later.
                if (_conditionDateTypeOrdinal == -1)
                {
                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}", _viewModelMain.HluDataset.incid_condition.TableName,
                        _viewModelMain.HluDataset.incid_condition.condition_date_typeColumn.ColumnName, _viewModelMain.HluDataset.incid_condition.condition_date_typeColumn.ColumnName));

                    // Store the input field ordinal for use later.
                    _conditionDateTypeOrdinal = lastFieldOrdinal += 1;
                }

                // Add the input field position to the list of fields
                // that will be used to sort the input records. Multiply
                // by minus 1 to indicate descending order).
                sortFields.Add((_conditionIdOrdinal + 1) * -1);
            }

            // If any incid_ihs_matrix fields are in the export file.
            if ((exportFields.Any(f => f.TableName == _viewModelMain.HluDataset.incid_ihs_matrix.TableName)))
            {
                // If the matrix_id column is not included then add
                // it so that different matrixs can be identified.
                if (_matrixIdOrdinal == -1)
                {
                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}", _viewModelMain.HluDataset.incid_ihs_matrix.TableName,
                        _viewModelMain.HluDataset.incid_ihs_matrix.matrix_idColumn.ColumnName, _viewModelMain.HluDataset.incid_ihs_matrix.matrix_idColumn.ColumnName));

                    // Store the input field ordinal for use
                    // later as the unique incid_ihs_matrix field ordinal.
                    _matrixIdOrdinal = lastFieldOrdinal += 1;
                }

                //---------------------------------------------------------------------
                // CHANGED: CR43 (Sort multiple fields in exports)
                //
                // Add the input field position to the list of fields
                // that will be used to sort the input records.
                sortFields.Add(_matrixIdOrdinal + 1);
                //---------------------------------------------------------------------
            }

            // If any incid_ihs_formation fields are in the export file.
            if ((exportFields.Any(f => f.TableName == _viewModelMain.HluDataset.incid_ihs_formation.TableName)))
            {
                // If the formation_id column is not included then add
                // it so that different formations can be identified.
                if (_formationIdOrdinal == -1)
                {
                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}", _viewModelMain.HluDataset.incid_ihs_formation.TableName,
                        _viewModelMain.HluDataset.incid_ihs_formation.formation_idColumn.ColumnName, _viewModelMain.HluDataset.incid_ihs_formation.formation_idColumn.ColumnName));

                    // Store the input field ordinal for use
                    // later as the unique incid_ihs_formation field ordinal.
                    _formationIdOrdinal = lastFieldOrdinal += 1;
                }

                //---------------------------------------------------------------------
                // CHANGED: CR43 (Sort multiple fields in exports)
                //
                // Add the input field position to the list of fields
                // that will be used to sort the input records.
                sortFields.Add(_formationIdOrdinal + 1);
                //---------------------------------------------------------------------
            }

            // If any incid_ihs_management fields are in the export file.
            if ((exportFields.Any(f => f.TableName == _viewModelMain.HluDataset.incid_ihs_management.TableName)))
            {
                // If the management_id column is not included then add
                // it so that different managements can be identified.
                if (_managementIdOrdinal == -1)
                {
                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}", _viewModelMain.HluDataset.incid_ihs_management.TableName,
                        _viewModelMain.HluDataset.incid_ihs_management.management_idColumn.ColumnName, _viewModelMain.HluDataset.incid_ihs_management.management_idColumn.ColumnName));

                    // Store the input field ordinal for use
                    // later as the unique incid_ihs_management field ordinal.
                    _managementIdOrdinal = lastFieldOrdinal += 1;
                }

                //---------------------------------------------------------------------
                // CHANGED: CR43 (Sort multiple fields in exports)
                //
                // Add the input field position to the list of fields
                // that will be used to sort the input records.
                sortFields.Add(_managementIdOrdinal + 1);
                //---------------------------------------------------------------------
            }

            // If any incid_ihs_complex fields are in the export file.
            if ((exportFields.Any(f => f.TableName == _viewModelMain.HluDataset.incid_ihs_complex.TableName)))
            {
                // If the complex_id column is not included then add
                // it so that different complexs can be identified.
                if (_complexIdOrdinal == -1)
                {
                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}", _viewModelMain.HluDataset.incid_ihs_complex.TableName,
                        _viewModelMain.HluDataset.incid_ihs_complex.complex_idColumn.ColumnName, _viewModelMain.HluDataset.incid_ihs_complex.complex_idColumn.ColumnName));

                    // Store the input field ordinal for use
                    // later as the unique incid_ihs_complex field ordinal.
                    _complexIdOrdinal = lastFieldOrdinal += 1;
                }

                //---------------------------------------------------------------------
                // CHANGED: CR43 (Sort multiple fields in exports)
                //
                // Add the input field position to the list of fields
                // that will be used to sort the input records.
                sortFields.Add(_complexIdOrdinal + 1);
                //---------------------------------------------------------------------
            }

            // If any incid_bap fields are in the export file.
            if ((exportFields.Any(f => f.TableName == _viewModelMain.HluDataset.incid_bap.TableName)))
            {
                // If the bap_habitat_quality column is not included then
                // add it so that the baps can be sorted by quality.
                if (_bapQualityOrdinal == -1)
                {
                    // Add a field to the input table to get the determination
                    // quality of the bap habitat so that 'not present' habitats
                    // are listed after 'present' habitats.
                    if ((DbFactory.ConnectionType.ToString().Equals("access", StringComparison.CurrentCultureIgnoreCase)) ||
                        (DbFactory.Backend.ToString().Equals("access", StringComparison.CurrentCultureIgnoreCase)))
                        targetList.Append(String.Format(", IIF({0}.{1} = {2}, 2, IIF({0}.{1} = {3}, 1, 0)) AS {4}",
                            _viewModelMain.HluDataset.incid_bap.TableName,
                            _viewModelMain.HluDataset.incid_bap.quality_determinationColumn.ColumnName,
                            _viewModelMain.DataBase.QuoteValue(Settings.Default.BAPDeterminationQualityUserAdded),
                            _viewModelMain.DataBase.QuoteValue(Settings.Default.BAPDeterminationQualityPrevious),
                            "bap_habitat_quality"));
                    else
                        targetList.Append(String.Format(", CASE {0}.{1} WHEN {2} THEN 2 WHEN {3} THEN 1 ELSE 0 END AS {4}",
                            _viewModelMain.HluDataset.incid_bap.TableName,
                            _viewModelMain.HluDataset.incid_bap.quality_determinationColumn.ColumnName,
                            _viewModelMain.DataBase.QuoteValue(Settings.Default.BAPDeterminationQualityUserAdded),
                            _viewModelMain.DataBase.QuoteValue(Settings.Default.BAPDeterminationQualityPrevious),
                            "bap_habitat_quality"));
                    //// Add the field to the input table.
                    //targetList.Append(String.Format(",{0}.{1} AS {2}", _viewModelMain.HluDataset.incid_bap.TableName,
                    //    _viewModelMain.HluDataset.incid_bap.quality_determinationColumn.ColumnName, _viewModelMain.HluDataset.incid_bap.quality_determinationColumn.ColumnName));

                    // Store the input field ordinal for use later.
                    _bapQualityOrdinal = lastFieldOrdinal += 1;

                    // Add the input field position to the list of fields
                    // that will be used to sort the input records.
                    sortFields.Add(_bapQualityOrdinal + 1);
                }

                // If the bap_habitat_type column is not included then
                // add it so that the baps can be sorted by type.
                if (_bapTypeOrdinal == -1)
                {
                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}", _viewModelMain.HluDataset.incid_bap.TableName,
                        _viewModelMain.HluDataset.incid_bap.bap_habitatColumn.ColumnName, _viewModelMain.HluDataset.incid_bap.bap_habitatColumn.ColumnName));

                    // Store the input field ordinal for use later.
                    _bapTypeOrdinal = lastFieldOrdinal += 1;

                    // Add the input field position to the list of fields
                    // that will be used to sort the input records.
                    sortFields.Add(_bapTypeOrdinal + 1);
                }

                // If the bap_id column is not included then add
                // it so that different baps can be identified.
                if (_bapIdOrdinal == -1)
                {
                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}", _viewModelMain.HluDataset.incid_bap.TableName,
                        _viewModelMain.HluDataset.incid_bap.bap_idColumn.ColumnName, _viewModelMain.HluDataset.incid_bap.bap_idColumn.ColumnName));

                    // Store the input field ordinal for use
                    // later as the unique incid_bap field ordinal.
                    _bapIdOrdinal = lastFieldOrdinal += 1;

                    // Add the input field position to the list of fields
                    // that will be used to sort the input records.
                    sortFields.Add(_bapIdOrdinal + 1);
                }
            }

            // If any incid_source fields are in the export file.
            if ((exportFields.Any(f => f.TableName == _viewModelMain.HluDataset.incid_sources.TableName)))
            {
                // If the source_id column is not included then add
                // it so that different sources can be identified.
                if (_sourceIdOrdinal == -1)
                {
                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}", _viewModelMain.HluDataset.incid_sources.TableName,
                        _viewModelMain.HluDataset.incid_sources.source_idColumn.ColumnName, _viewModelMain.HluDataset.incid_sources.source_idColumn.ColumnName));

                    // Store the input field ordinal for use
                    // later as the unique incid_source field ordinal.
                    _sourceIdOrdinal = lastFieldOrdinal += 1;
                }

                //---------------------------------------------------------------------
                // CHANGED: CR43 (Sort multiple fields in exports)
                //
                // If the sort_order column is not included then add
                // it so that the sources can be sorted.
                if (sourceSortOrderOrdinal == -1)
                {
                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}", _viewModelMain.HluDataset.incid_sources.TableName,
                        _viewModelMain.HluDataset.incid_sources.sort_orderColumn.ColumnName, _viewModelMain.HluDataset.incid_sources.sort_orderColumn.ColumnName));

                    // Store the input field ordinal for use
                    // later as the unique incid_source field ordinal.
                    sourceSortOrderOrdinal = lastFieldOrdinal += 1;
                }

                // Add the input field position to the list of fields
                // that will be used to sort the input records.
                sortFields.Add(sourceSortOrderOrdinal + 1);
                //---------------------------------------------------------------------

                //---------------------------------------------------------------------
                // CHANGED: CR17 (Exporting date fields)
                // Store all of the source date fields for use later when
                // formatting the attribute data.
                //
                // If the source_date_start column is not included then add
                // it for use later.
                if ((_sourceDateStartOrdinals == null) || (_sourceDateStartOrdinals.Count == 0))
                {
                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}", _viewModelMain.HluDataset.incid_sources.TableName,
                        _viewModelMain.HluDataset.incid_sources.source_date_startColumn.ColumnName, _viewModelMain.HluDataset.incid_sources.source_date_startColumn.ColumnName));

                    // Store the input field ordinal for use later.
                    _sourceDateStartOrdinals.Add(lastFieldOrdinal += 1);
                }

                // If the source_date_end column is not included then add
                // it for use later.
                if ((_sourceDateEndOrdinals == null) || (_sourceDateEndOrdinals.Count == 0))
                {
                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}", _viewModelMain.HluDataset.incid_sources.TableName,
                        _viewModelMain.HluDataset.incid_sources.source_date_endColumn.ColumnName, _viewModelMain.HluDataset.incid_sources.source_date_endColumn.ColumnName));

                    // Store the input field ordinal for use later.
                    _sourceDateEndOrdinals.Add(lastFieldOrdinal += 1);
                }

                // If the source_date_type column is not included then add
                // it for use later.
                if ((_sourceDateTypeOrdinals == null) || (_sourceDateTypeOrdinals.Count == 0))
                {
                    // Add the field to the input table.
                    targetList.Append(String.Format(",{0}.{1} AS {2}", _viewModelMain.HluDataset.incid_sources.TableName,
                        _viewModelMain.HluDataset.incid_sources.source_date_typeColumn.ColumnName, _viewModelMain.HluDataset.incid_sources.source_date_typeColumn.ColumnName));

                    // Store the input field ordinal for use later.
                    _sourceDateTypeOrdinals.Add(lastFieldOrdinal += 1);
                }
                //---------------------------------------------------------------------
            }

            //---------------------------------------------------------------------
            // CHANGED: CR43 (Sort multiple fields in exports)
            //
            // Store which export fields will be used to sort the
            // input records.
            sortOrdinals = sortFields.ToArray();
            //---------------------------------------------------------------------

            // Store the field ordinals for all the fields for
            // every child table.
            conditionOrdinals = conditionFields.ToArray();
            matrixOrdinals = matrixFields.ToArray();
            formationOrdinals = formationFields.ToArray();
            managementOrdinals = managementFields.ToArray();
            complexOrdinals = complexFields.ToArray();
            bapOrdinals = bapFields.ToArray();
            sourceOrdinals = sourceFields.ToArray();

            // Set the incid field as the primary key to the table.
            if (primaryKeyOrdinal != -1)
                exportTable.PrimaryKey = [exportTable.Columns[primaryKeyOrdinal]];

            // Remove the leading comma from the target list of fields.
            if (targetList.Length > 1) targetList.Remove(0, 1);
        }

        private string ExportEmptyMdb(DataTable exportTable)
        {
            DbOleDb dbOut = null;

            string tempPath = String.Empty;
            try { tempPath = Path.GetTempPath(); }
            catch { tempPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); }
            tempPath = Path.Combine(tempPath, Path.GetFileNameWithoutExtension(Path.GetRandomFileName()) + ".mdb");

            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
                OdbcCP32 odbc = new();
                odbc.CreateDatabase(tempPath);
                string connString = String.Format(@"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={0};", tempPath);
                string defaultSchema = "";
                bool promptPwd = false;
                dbOut = new DbOleDb(ref connString, ref defaultSchema, ref promptPwd,
                    Settings.Default.PasswordMaskString, Settings.Default.UseAutomaticCommandBuilders,
                    true, Settings.Default.DbIsUnicode, Settings.Default.DbUseTimeZone, 255,
                    Settings.Default.DbBinaryLength, Settings.Default.DbTimePrecision,
                    Settings.Default.DbNumericPrecision, Settings.Default.DbNumericScale, _viewModelMain.DbConnectionTimeout);

                // Throw an error if the table cannot be created.
                if (!dbOut.CreateTable(exportTable))
                    throw new Exception("Error creating the temporary export table");

                DataSet datasetOut = new("Export");

                IDbDataAdapter adapterOut = dbOut.CreateAdapter(exportTable);
                adapterOut.MissingSchemaAction = MissingSchemaAction.AddWithKey;
                adapterOut.Fill(datasetOut);
                //int[] pkOrdinals = exportTable.PrimaryKey.Select(c => c.Ordinal).ToArray();
                //exportTable.PrimaryKey = pkOrdinals.Select(o => exportTable.Columns[o]).ToArray();
                //adapterOut.TableMappings.Clear();
                //adapterOut.TableMappings.Add(exportTable.TableName, datasetOut.Tables[0].TableName);

                //exportTable = datasetOut.Tables[0];

                //DataRow exportRow = exportTable.NewRow();

                //exportTable.Rows.Add(exportRow);

                //exportRow[exportColumn] = outValue;

                // Commit the output.
                //adapterOut.Update(datasetOut);

                return tempPath;

            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Export failed. The error message was:\n\n{0}.",
                    ex.Message), "HLU: Export", MessageBoxButton.OK, MessageBoxImage.Error);

                // Delete the temporary database if it was created.
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); }
                    catch { _viewModelMain.ExportMdbs.Add(tempPath); }
                }

                // Return a null database path as the export didn't finish.
                return null;
            }
            finally
            {
                if ((dbOut != null) && (dbOut.Connection.State != ConnectionState.Closed))
                {
                    try { dbOut.Connection.Close(); }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Exports the attribute data to a temporary access database. Uses DAO instead
        /// of a DataAdapter to improve performance.
        /// </summary>
        /// <param name="tempPath">Path and name of the temporary database.</param>
        /// <param name="targetListStr">The SELECT clause containing the list of output fields from the source database.</param>
        /// <param name="fromClauseStr">The FROM clause to output the fields from the source database.</param>
        /// <param name="exportFilter">The WHERE clause to apply for the export.</param>
        /// <param name="dataBase">The source database type.</param>
        /// <param name="exportFields">The list of export fields to create in the temporary database.</param>
        /// <param name="exportTable">The DataTable of the export table.</param>
        /// <param name="sortOrdinals">The column ordinals of the sort columns.</param>
        /// <param name="conditionOrdinals">The column ordinals of the condition columns.</param>
        /// <param name="matrixOrdinals">The column ordinals of the matrix columns.</param>
        /// <param name="formationOrdinals">The column ordinals of the formation columns.</param>
        /// <param name="managementOrdinals">The column ordinals of the management columns.</param>
        /// <param name="complexOrdinals">The column ordinals of the complex columns.</param>
        /// <param name="bapOrdinals">The column ordinals of the priority habitat columns.</param>
        /// <param name="sourceOrdinals">The column ordinals of the source columns.</param>
        /// <param name="fieldMap">The field mapping from the input source to the output table.</param>
        /// <returns></returns>
        private int ExportMdb(string tempPath, string targetListStr, string fromClauseStr, List<List<SqlFilterCondition>> exportFilter,
            DbBase dataBase, List<ExportField> exportFields, DataTable exportTable, int[] sortOrdinals, int[] conditionOrdinals,
            int[] matrixOrdinals, int[] formationOrdinals, int[] managementOrdinals, int[] complexOrdinals, int[] bapOrdinals, int[] sourceOrdinals,
            int[][] fieldMap)
        {
            int outputRowCount = 0;

            DBEngine dbEngine = new();

            try
            {

            	Database db = dbEngine.OpenDatabase(tempPath);
            	Recordset AccesssRecordset = db.OpenRecordset(exportTable.TableName);
            	Field[] AccesssFields;

                // If there is only one long list then chunk
                // it up into smaller lists.
                if (exportFilter.Count == 1)
                {
                    try
                    {
                        List<SqlFilterCondition> whereCond = [];
                        whereCond = exportFilter[0];
                        exportFilter = whereCond.ChunkClause(240).ToList();
                    }
                    catch { }
                }

                // Break exporting attributes into chunks to avoid errors
                // with excessive sql lengths.
                outputRowCount = 0;

                // Set the field map indexes to the start of the array.
                int[] fieldMapIndex = new int[fieldMap.Length];
                for (int k = 0; k < fieldMap.Length; k++)
                {
                    fieldMapIndex[k] = 1;
                }

                for (int j = 0; j < exportFilter.Count; j++)
                {
                    AccesssFields = new Field[exportTable.Columns.Count];
                	AccesssRecordset.AddNew();
                    bool rowAdded = false;

                    // Union the constituent parts of the export query
                    // together into a single SQL string.
                    string sql = ScratchDb.UnionQuery(targetListStr, fromClauseStr,
                        sortOrdinals, exportFilter[j], dataBase);

                    // Execute the sql to retrieve the records.
                    // Apply user's option database connection timeout.
                    using (IDataReader reader = _viewModelMain.DataBase.ExecuteReader(sql,
                        _viewModelMain.DbConnectionTimeout, CommandType.Text))
                    {
                        // Exit if no records were exported.
                        if ((reader == null))
                            throw new Exception("Export query failed or timed out");

                        string currIncid = String.Empty;
                        string prevIncid = String.Empty;
                        int currConditionId = -1;
                        int currConditionDateStart = 0;
                        int currConditionDateEnd = 0;
                        string currConditionDateType = String.Empty;
                        int currMatrixId = -1;
                        int currFormationId = -1;
                        int currManagementId = -1;
                        int currComplexId = -1;
                        int currBapId = -1;
                        int currSourceId = -1;
                        int currSourceDateStart = 0;
                        int currSourceDateEnd = 0;
                        string currSourceDateType = String.Empty;
                        List<int> conditionIds = null;
                        List<int> matrixIds = null;
                        List<int> formationIds = null;
                        List<int> managementIds = null;
                        List<int> complexIds = null;
                        List<int> bapIds = null;
                        List<int> sourceIds = null;
                        int exportColumn;

                        // Read each record and process the contents.
                        while (reader.Read())
                        {
                            // Get the current incid.
                            currIncid = reader.GetString(_incidOrdinal);

                            // Get the current condition id (or equivalent lookup table field).
                            if (_conditionIdOrdinal != -1)
                            {
                                object conditionIdValue = reader.GetValue(_conditionIdOrdinal);
                                if (conditionIdValue != DBNull.Value)
                                    currConditionId = (int)conditionIdValue;
                                else
                                    currConditionId = -1;
                            }

                            // Get the current matrix id.
                            if (_matrixIdOrdinal != -1)
                            {
                                object matrixIdValue = reader.GetValue(_matrixIdOrdinal);
                                if (matrixIdValue != DBNull.Value)
                                    currMatrixId = (int)matrixIdValue;
                                else
                                    currMatrixId = -1;
                            }

                            // Get the current formation id.
                            if (_formationIdOrdinal != -1)
                            {
                                object FormationIdValue = reader.GetValue(_formationIdOrdinal);
                                if (FormationIdValue != DBNull.Value)
                                    currFormationId = (int)FormationIdValue;
                                else
                                    currFormationId = -1;
                            }

                            // Get the current Management id.
                            if (_managementIdOrdinal != -1)
                            {
                                object ManagementIdValue = reader.GetValue(_managementIdOrdinal);
                                if (ManagementIdValue != DBNull.Value)
                                    currManagementId = (int)ManagementIdValue;
                                else
                                    currManagementId = -1;
                            }

                            // Get the current Complex id.
                            if (_complexIdOrdinal != -1)
                            {
                                object ComplexIdValue = reader.GetValue(_complexIdOrdinal);
                                if (ComplexIdValue != DBNull.Value)
                                    currComplexId = (int)ComplexIdValue;
                                else
                                    currComplexId = -1;
                            }

                            // Get the current bap id (or equivalent lookup table field).
                            if (_bapIdOrdinal != -1)
                            {
                                object bapIdValue = reader.GetValue(_bapIdOrdinal);
                                if (bapIdValue != DBNull.Value)
                                    currBapId = (int)bapIdValue;
                                else
                                    currBapId = -1;
                            }

                            // Get the current source id (or equivalent lookup table field).
                            if (_sourceIdOrdinal != -1)
                            {
                                object sourceIdValue = reader.GetValue(_sourceIdOrdinal);
                                if (sourceIdValue != DBNull.Value)
                                    currSourceId = (int)sourceIdValue;
                                else
                                    currSourceId = -1;
                            }

                            //---------------------------------------------------------------------
                            // CHANGED: CR17 (Exporting date fields)
                            // Store all of the source date fields for use later when
                            // formatting the attribute data.
                            //
                            // Get the current source date start.
                            if ((_sourceDateStartOrdinals.Count != 0) &&
                                !reader.IsDBNull(_sourceDateStartOrdinals[0]))
                                currSourceDateStart = reader.GetInt32(_sourceDateStartOrdinals[0]);

                            // Get the current source date end.
                            if ((_sourceDateEndOrdinals.Count != 0) &&
                                !reader.IsDBNull(_sourceDateEndOrdinals[0]))
                                currSourceDateEnd = reader.GetInt32(_sourceDateEndOrdinals[0]);

                            // Get the current source date type.
                            if ((_sourceDateTypeOrdinals.Count != 0) &&
                                !reader.IsDBNull(_sourceDateTypeOrdinals[0]))
                                currSourceDateType = reader.GetString(_sourceDateTypeOrdinals[0]);

                            // Store all of the condition date fields for use later when
                            // formatting the attribute data.
                            //
                            // Get the current condition date start.
                            if ((_conditionDateStartOrdinal != -1) &&
                                !reader.IsDBNull(_conditionDateStartOrdinal))
                                currConditionDateStart = reader.GetInt32(_conditionDateStartOrdinal);

                            // Get the current source date end.
                            if ((_conditionDateEndOrdinal != -1) &&
                                !reader.IsDBNull(_conditionDateEndOrdinal))
                                currConditionDateEnd = reader.GetInt32(_conditionDateEndOrdinal);

                            // Get the current condition date type.
                            if ((_conditionDateTypeOrdinal != -1) &&
                                !reader.IsDBNull(_conditionDateTypeOrdinal))
                                currConditionDateType = reader.GetString(_conditionDateTypeOrdinal);
                            //---------------------------------------------------------------------

                            // If this incid is different to the last record's incid
                            // then process all the fields.
                            if (currIncid != prevIncid)
                            {
                                // If the last export row has not been added then
                                // add it now.
                                if ((AccesssFields[0] != null) && (AccesssFields[0].Value != null))
                                {
						            AccesssRecordset.Update();
                                    rowAdded = true;

                                    // Increment the output row count.
                                    outputRowCount += 1;
                                }

                                // Store the last incid.
                                prevIncid = currIncid;

                                conditionIds = [];
                                matrixIds = [];
                                formationIds = [];
                                managementIds = [];
                                complexIds = [];
                                bapIds = [];
                                sourceIds = [];

                                // Reset the field map indexes to the start of the array.
                                for (int k = 0; k < fieldMap.Length; k++)
                                {
                                    fieldMapIndex[k] = 1;
                                }

                                // Create a new export row ready for the next values.
                            	AccesssRecordset.AddNew();
                                rowAdded = false;

                                // Loop through all the fields in the field map
                                // to transfer the values from the input reader
                                // to the correct field in the export row.
                                for (int i = 0; i < fieldMap.GetLength(0); i++)
                                {
                                    // Set the export column ordinal from the current
                                    // field map index for this field.
                                    exportColumn = fieldMap[i][fieldMapIndex[i]];

                                    // Increment the field map index for this field.
                                    fieldMapIndex[i] += 1;

                                    // If this field is not mapped from the input reader
                                    // set the export table value to null.
                                    if (fieldMap[i][0] == -1)
                                        continue;

                                    // For the first time... setup the field name.
                                    if (AccesssFields[exportColumn] == null)
                                        AccesssFields[exportColumn] = AccesssRecordset.Fields[exportTable.Columns[exportColumn].ColumnName];

                                    // Store the input value of the current column.
                                    object inValue = reader.GetValue(fieldMap[i][0]);

                                    // If the value is null then skip this field.
                                    if (inValue == DBNull.Value)
                                        continue;

                                    // Get the properties for the current export field.
                                    ExportField exportField = exportFields.Find(f => f.FieldOrdinal == i);

                                    // Enable fields to be exported using a different data type.
                                    // Convert the input value to the output value data type and format.
                                    object outValue;
                                    outValue = ConvertInput(fieldMap[i][0], inValue, reader.GetFieldType(fieldMap[i][0]),
                                        exportTable.Columns[exportColumn].DataType,
                                        exportField?.FieldFormat,
                                        currSourceDateStart, currSourceDateEnd, currSourceDateType,
                                        currConditionDateStart, currConditionDateEnd, currConditionDateType);

                                    // If the value is not null.
                                    if (outValue != null)
                                    {
                                        // Get the maximum length of the column.
                                        int fieldLength = exportTable.Columns[exportColumn].MaxLength;

                                        // If the maximum length of the column is shorter
                                        // than the value then truncate the value as it
                                        // is transferred  to the export row.
                                        if ((fieldLength != -1) && (fieldLength < outValue.ToString().Length))
											AccesssFields[exportColumn].Value = outValue.ToString().Substring(0, fieldLength);
                                        else
                                            AccesssFields[exportColumn].Value = outValue;
                                    }
                                }
                            }
                            else
                            {
                                // Loop through all the fields in the field map
                                // to transfer the values from the input reader
                                // to the correct field in the export row.
                                for (int i = 0; i < fieldMap.GetLength(0); i++)
                                {
                                    // Only process fields that have multiple outputs
                                    // specified in the field map.
                                    if (fieldMapIndex[i] < fieldMap[i].Length)
                                    {
                                        // Set the export column ordinal from the current
                                        // field map index for this field.
                                        exportColumn = fieldMap[i][fieldMapIndex[i]];

                                        // If the value is not null and the string value is different
                                        // to the last string value for this incid, or, the column is
                                        // allowed to have duplicates and the condition, bap or source is different
                                        // to the last condition, bap or source, then output the value.
                                        if (Array.IndexOf(conditionOrdinals, exportColumn) != -1)
                                        {
                                            if (conditionIds.Contains(currConditionId))
                                                continue;
                                        }
                                        else if (Array.IndexOf(matrixOrdinals, exportColumn) != -1)
                                        {
                                            if (matrixIds.Contains(currMatrixId))
                                                continue;
                                        }
                                        else if (Array.IndexOf(formationOrdinals, exportColumn) != -1)
                                        {
                                            if (formationIds.Contains(currFormationId))
                                                continue;
                                        }
                                        else if (Array.IndexOf(managementOrdinals, exportColumn) != -1)
                                        {
                                            if (managementIds.Contains(currManagementId))
                                                continue;
                                        }
                                        else if (Array.IndexOf(complexOrdinals, exportColumn) != -1)
                                        {
                                            if (complexIds.Contains(currComplexId))
                                                continue;
                                        }
                                        else if (Array.IndexOf(bapOrdinals, exportColumn) != -1)
                                        {
                                            if (bapIds.Contains(currBapId))
                                                continue;
                                        }
                                        else if (Array.IndexOf(sourceOrdinals, exportColumn) != -1)
                                        {
                                            if (sourceIds.Contains(currSourceId))
                                                continue;
                                        }

                                        // Increment the field map index for this field.
                                        fieldMapIndex[i] += 1;

                                        // If this field is not mapped from the input reader
                                        // set the export table value to null.
                                        if (fieldMap[i][0] == -1)
                                            continue;

                                        // For the first time... setup the field name.
                                        if (AccesssFields[exportColumn] == null)
                                            AccesssFields[exportColumn] = AccesssRecordset.Fields[exportTable.Columns[exportColumn].ColumnName];

                                        // Store the input value of the current column.
                                        object inValue = reader.GetValue(fieldMap[i][0]);

                                        // If the value is null then skip this field.
                                        if (inValue == DBNull.Value)
                                            continue;

                                        // Get the properties for the current export field.
                                        ExportField exportField = exportFields.Find(f => f.FieldOrdinal == i);

                                        // Convert the input value to the output value data type and format.
                                        object outValue;
                                        outValue = ConvertInput(fieldMap[i][0], inValue, reader.GetFieldType(fieldMap[i][0]),
                                            exportTable.Columns[exportColumn].DataType,
                                            exportField?.FieldFormat,
                                            currSourceDateStart, currSourceDateEnd, currSourceDateType,
                                            currConditionDateStart, currConditionDateEnd, currConditionDateType);

                                        // If the value is not null.
                                        if (outValue != null)
                                        {
                                            // Get the maximum length of the output column.
                                            int fieldLength = exportTable.Columns[exportColumn].MaxLength;

                                            // If the maximum length of the column is shorter
    	                                    // than the value then truncate the value as it
        	                                // is transferred  to the export row.
            	                            if ((fieldLength != -1) && (fieldLength < outValue.ToString().Length))
												AccesssFields[exportColumn].Value = outValue.ToString().Substring(0, fieldLength);
                    	                    else
                        	                    AccesssFields[exportColumn].Value = outValue;
                                        }
                                    }
                                }
                            }

                            // Store the current ids so that they are not output again.
                            conditionIds.Add(currConditionId);
                            matrixIds.Add(currMatrixId);
                            formationIds.Add(currFormationId);
                            managementIds.Add(currManagementId);
                            complexIds.Add(currComplexId);
                            bapIds.Add(currBapId);
                            sourceIds.Add(currSourceId);
                        }
                    }

                    // If the last export row has not been saved then
                    // save it now.
                    if (!rowAdded && (AccesssFields[0] != null) && (AccesssFields[0].Value != null))
                    {
			            AccesssRecordset.Update();

                        // Increment the output row count.
                        outputRowCount += 1;
                    }
                }

                // Exit if no records were exported.
                if (outputRowCount < 1)
                    throw new Exception("Export query did not retrieve any rows");

		        AccesssRecordset.Close();
            	db.Close();

                return outputRowCount;

            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Export failed. The error message was:\n\n{0}.",
                    ex.Message), "HLU: Export", MessageBoxButton.OK, MessageBoxImage.Error);

                // Delete the temporary database if it was created.
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); }
                    catch { _viewModelMain.ExportMdbs.Add(tempPath); }
                }

                // Return a zero export row count as the export didn't finish.
                return 0;
            }
            finally
            {

                System.Runtime.InteropServices.Marshal.ReleaseComObject(dbEngine);
    	        dbEngine = null;

            }
        }

        /// <summary>
        /// Gets the length of the original input field.
        /// </summary>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="columnOrdinal">The column ordinal.</param>
        /// <returns></returns>
        private int GetFieldLength(string tableName, int columnOrdinal)
        {
            int fieldLength = 0;
            var relations = _viewModelMain.HluDataRelations.Where(rel =>
                    rel.ParentTable.TableName == HluDatasetStatic.incid.TableName);

            // Get a list of all the incid related tables (including the
            // incid table itself.
            List<DataTable> tables;
            tables = _viewModelMain.HluDataset.incid.ChildRelations
                .Cast<DataRelation>().Select(r => r.ChildTable).ToList();
            tables.Add(_viewModelMain.HluDataset.incid);

            foreach (DataTable t in tables)
            {
                if (t.TableName == tableName)
                {
                    DataColumn[] columns = new DataColumn[t.Columns.Count];
                    t.Columns.CopyTo(columns, 0);

                    // Get the field length.
                    fieldLength = columns[columnOrdinal - 1].MaxLength;
                    break;
                }
            }

            return fieldLength;
        }

        /// <summary>
        /// Converts the input field into the output field, applying any
        /// required formatting as appropriate.
        /// </summary>
        /// <param name="inOrdinal">The input field ordinal.</param>
        /// <param name="inValue">The input field value.</param>
        /// <param name="inType">Data type of the input field.</param>
        /// <param name="outType">Date type of the output field.</param>
        /// <param name="outFormat">The required output field format.</param>
        /// <param name="sourceDateStart">The source date start.</param>
        /// <param name="sourceDateEnd">The source date end.</param>
        /// <param name="sourceDateType">The source date type.</param>
        /// <param name="conditionDateStart">The condition date start.</param>
        /// <param name="conditionDateEnd">The condition date end.</param>
        /// <param name="conditionDateType">The condition date type.</param>
        /// <returns></returns>
        private object ConvertInput(int inOrdinal, object inValue, System.Type inType,
            System.Type outType, string outFormat, int sourceDateStart, int sourceDateEnd, string sourceDateType,
            int conditionDateStart, int conditionDateEnd, string conditionDateType)
        {
            //---------------------------------------------------------------------
            // If the output field is a DateTime.
            //---------------------------------------------------------------------
            if (outType == System.Type.GetType("System.DateTime"))
            {
                // If the input field is also a DateTime
                if (inType == System.Type.GetType("System.DateTime"))
                {
                    // Returns the value as a DateTime.
                    if (inValue is DateTime)
                        return inValue;
                    else
                        return null;
                }
                // If the input field is an integer and is part of
                // the source date.
                else if ((inType == System.Type.GetType("System.Int32")) &&
                    (_sourceDateStartOrdinals.Contains(inOrdinal) || _sourceDateEndOrdinals.Contains(inOrdinal)))
                {
                    // Convert the value to an integer.
                    int inInt = (int)inValue;

                    // Convert the value to a vague date instance.
                    string vt = "D";
                    VagueDateInstance vd = new(inInt, inInt, vt);

                    // If the vague date is invalid then return null.
                    if ((vd == null) || (vd.IsBad) || (vd.IsUnknown))
                        return null;
                    else
                    {
                        // If the vague date is valid then parse it into
                        // a date format.
                        string itemStr = VagueDate.FromVagueDateInstance(vd, VagueDate.DateType.Vague);
                        DateTime inDate;
                        if (DateTime.TryParseExact(itemStr, "dd/MM/yyyy",
                            null, DateTimeStyles.None, out inDate))
                            return inDate;
                        else
                            return null;
                    }
                }
                // If the input field is an integer and is part of
                // the condition date.
                else if ((inType == System.Type.GetType("System.Int32")) &&
                    (_conditionDateStartOrdinal == inOrdinal || _conditionDateEndOrdinal == inOrdinal))
                {
                    // Convert the value to an integer.
                    int inInt = (int)inValue;

                    // Convert the value to a vague date instance.
                    string vt = "D";
                    VagueDateInstance vd = new(inInt, inInt, vt);

                    // If the vague date is invalid then return null.
                    if ((vd == null) || (vd.IsBad) || (vd.IsUnknown))
                        return null;
                    else
                    {
                        // If the vague date is valid then parse it into
                        // a date format.
                        string itemStr = VagueDate.FromVagueDateInstance(vd, VagueDate.DateType.Vague);
                        DateTime inDate;
                        if (DateTime.TryParseExact(itemStr, "dd/MM/yyyy",
                            null, DateTimeStyles.None, out inDate))
                            return inDate;
                        else
                            return null;
                    }
                }
                else
                {
                    // Otherwise, try and parse the input value as
                    // if it was a date string and return the date value
                    // if it is valid, or the raw value if not.
                    string inStr = inValue.ToString();
                    DateTime inDate;
                    if (DateTime.TryParseExact(inStr, "dd/MM/yyyy",
                        null, DateTimeStyles.None, out inDate))
                        return inDate;
                    else
                        return null;
                }
            }
            //---------------------------------------------------------------------
            // If the output field is a string and there is
            // a required output format.
            //---------------------------------------------------------------------
            else if ((outType == System.Type.GetType("System.String")) &&
                (outFormat != null))
            {
                //---------------------------------------------------------------------
                // If the input field is a DateTime field.
                //---------------------------------------------------------------------
                if (inType == System.Type.GetType("System.DateTime"))
                {
                    // If the input value is a valid DateTime then
                    // convert it to a string of the required format.
                    if (inValue is DateTime inDate)
                    {
                        string inStr = inDate.ToString(outFormat);
                        if (inStr != null)
                            return inStr;
                        else
                            return null;

                        //// If the date string formats exactly then return
                        //// the formatted value.
                        //if (DateTime.TryParseExact(inDate.ToString(), outFormat,
                        //    null, DateTimeStyles.None, out inDate))
                        //    return inDate.ToString();
                        //else
                        //    return null;
                    }
                    else
                        return null;
                }
                //---------------------------------------------------------------------
                // CHANGED: CR17 (Exporting date fields)
                // Convert source dates into a text field with the required
                // date format.
                //
                // If the input field is an integer and is part of
                // the source date.
                //---------------------------------------------------------------------
                else if ((inType == System.Type.GetType("System.Int32")) &&
                    (_sourceDateStartOrdinals.Contains(inOrdinal) || _sourceDateEndOrdinals.Contains(inOrdinal)))
                {
                    // Convert the value to an integer.
                    int inInt = (int)inValue;

                    // Convert the value to a vague date instance.
                    VagueDateInstance vd = new(sourceDateStart, sourceDateEnd, sourceDateType);

                    // If the vague date is invalid then set the output
                    // field to null.
                    if ((vd == null) || (vd.IsBad))
                        return null;
                    else if (vd.IsUnknown)
                        return VagueDate.VagueDateTypes.Unknown.ToString();
                    else
                    {
                        // If the output format is 'v' or 'V' then format the dates according
                        // to the source date type.
                        if (outFormat.Equals("v", StringComparison.CurrentCultureIgnoreCase))
                        {
                            return VagueDate.FromVagueDateInstance(vd, VagueDate.DateType.Vague);
                        }
                        // If the output format is blank then format the start or end date
                        // according to the source date type.
                        else if (String.IsNullOrEmpty(outFormat))
                        {
                            if (_sourceDateStartOrdinals.Contains(inOrdinal))
                            {
                                return VagueDate.FromVagueDateInstance(vd, VagueDate.DateType.Start);
                            }
                            else if (_sourceDateEndOrdinals.Contains(inOrdinal))
                            {
                                return VagueDate.FromVagueDateInstance(vd, VagueDate.DateType.End);
                            }
                            else
                                return null;
                        }
                        // If the output format is a vague date type then format it
                        // like that.
                        else if (VagueDate.FromCode(outFormat) != VagueDate.VagueDateTypes.Unknown)
                        {
                            // If the field is a start date then use the first character of
                            // the vague date type.
                            if (_sourceDateStartOrdinals.Contains(inOrdinal))
                            {
                                // Set the date type applicable to the source date.
                                string dateType = outFormat.Substring(0, 1);

                                return VagueDate.FromVagueDateInstance(new VagueDateInstance(sourceDateStart, sourceDateEnd, dateType),
                                    VagueDate.DateType.Start);
                            }
                            // If the field is an end date then use the last character of
                            // the vague date type.
                            else if (_sourceDateEndOrdinals.Contains(inOrdinal))
                            {
                                // Set the date type applicable to the source date.
                                vd.DateType = outFormat.Length == 1 ? outFormat + outFormat : outFormat;

                                return VagueDate.FromVagueDateInstance(vd, VagueDate.DateType.End);
                            }
                            else
                                return null;
                        }
                        // If the output format is not a vague date type then format it
                        // as if it is a standard date format (e.g. dd/MM/yyyy).
                        else
                        {
                            // Parse the date into a date format using the output format.
                            VagueDate.DateType dateType = VagueDate.DateType.Vague;
                            if (_sourceDateStartOrdinals.Contains(inOrdinal))
                                dateType = VagueDate.DateType.Start;
                            //---------------------------------------------------------------------
                            // FIX: 109 Fix bug exporting source dates.
                            //
                            else if (_sourceDateEndOrdinals.Contains(inOrdinal))
                                dateType = VagueDate.DateType.End;
                            //---------------------------------------------------------------------

                            string inStr = VagueDate.FromVagueDateInstance(new VagueDateInstance(sourceDateStart, sourceDateEnd, "D"), dateType);
                            DateTime inDate;
                            if (!DateTime.TryParseExact(inStr, "dd/MM/yyyy", null, DateTimeStyles.None, out inDate))
                                return null;

                            // Convert the DateTime to a string of the output format.
                            string outDate;
                            outDate = inDate.ToString(outFormat);

                            //---------------------------------------------------------------------
                            // FIX: 109 Fix bug exporting source dates.
                            //
                            //// Parse the formatted date back into a date using the
                            //// output format to check it is a valid date.
                            //DateTime inDateAgain;
                            //if (!DateTime.TryParseExact(outDate, outFormat, null, DateTimeStyles.None, out inDateAgain) ||
                            //    (inDate != inDateAgain))
                            //    return null;
                            //else
                            return outDate;
                            //---------------------------------------------------------------------
                        }
                    }
                }
                //---------------------------------------------------------------------
                // If the input field is an integer and is part of
                // the condition date.
                //---------------------------------------------------------------------
                else if ((inType == System.Type.GetType("System.Int32")) &&
                    (_conditionDateStartOrdinal == inOrdinal || _conditionDateEndOrdinal == inOrdinal))
                {
                    // Convert the value to an integer.
                    int inInt = (int)inValue;

                    // Convert the value to a vague date instance.
                    VagueDateInstance vd = new(conditionDateStart, conditionDateEnd, conditionDateType);

                    // If the vague date is invalid then set the output
                    // field to null.
                    if ((vd == null) || (vd.IsBad))
                        return null;
                    else if (vd.IsUnknown)
                        return VagueDate.VagueDateTypes.Unknown.ToString();
                    else
                    {
                        // If the output format is 'v' or 'V' then format the dates according
                        // to the condition date type.
                        if (outFormat.Equals("v", StringComparison.CurrentCultureIgnoreCase))
                        {
                            return VagueDate.FromVagueDateInstance(vd, VagueDate.DateType.Vague);
                        }
                        // If the output format is blank then format the start or end date
                        // according to the condition date type.
                        else if (String.IsNullOrEmpty(outFormat))
                        {
                            if (_conditionDateStartOrdinal == inOrdinal)
                            {
                                return VagueDate.FromVagueDateInstance(vd, VagueDate.DateType.Start);
                            }
                            else if (_conditionDateEndOrdinal == inOrdinal)
                            {
                                return VagueDate.FromVagueDateInstance(vd, VagueDate.DateType.End);
                            }
                            else
                                return null;
                        }
                        // If the output format is a vague date type then format it
                        // like that.
                        else if (VagueDate.FromCode(outFormat) != VagueDate.VagueDateTypes.Unknown)
                        {
                            // If the field is a start date then use the first character of
                            // the vague date type.
                            if (_conditionDateStartOrdinal == inOrdinal)
                            {
                                // Set the date type applicable to the condition date.
                                string dateType = outFormat.Substring(0, 1);

                                return VagueDate.FromVagueDateInstance(new VagueDateInstance(conditionDateStart, conditionDateEnd, dateType),
                                    VagueDate.DateType.Start);
                            }
                            // If the field is an end date then use the last character of
                            // the vague date type.
                            else if (_conditionDateEndOrdinal == inOrdinal)
                            {
                                // Set the date type applicable to the condition date.
                                vd.DateType = outFormat.Length == 1 ? outFormat + outFormat : outFormat;

                                return VagueDate.FromVagueDateInstance(vd, VagueDate.DateType.End);
                            }
                            else
                                return null;
                        }
                        // If the output format is not a vague date type then format it
                        // as if it is a standard date format (e.g. dd/MM/yyyy).
                        else
                        {
                            // Parse the date into a date format using the output format.
                            VagueDate.DateType dateType = VagueDate.DateType.Vague;
                            if (_conditionDateStartOrdinal == inOrdinal)
                                dateType = VagueDate.DateType.Start;
                            else if (_conditionDateEndOrdinal == inOrdinal)
                                dateType = VagueDate.DateType.End;

                            string inStr = VagueDate.FromVagueDateInstance(new VagueDateInstance(conditionDateStart, conditionDateEnd, "D"), dateType);
                            DateTime inDate;
                            if (!DateTime.TryParseExact(inStr, "dd/MM/yyyy", null, DateTimeStyles.None, out inDate))
                                return null;

                            // Convert the DateTime to a string of the output format.
                            string outDate;
                            outDate = inDate.ToString(outFormat);

                            return outDate;
                        }
                    }
                }
                else
                {
                    // Otherwise, try and parse the input value as
                    // if it was a date string and return the value
                    // as a string if it is valid.
                    string inStr = inValue.ToString();

                    DateTime inDate;
                    if (DateTime.TryParse(inStr, null, DateTimeStyles.None, out inDate))
                        return inDate.ToString();
                    else
                    {
                        // If the input is an IHS code that is blank (i.e. only the
                        // _separator character is retrieved) then return null.
                        if (inValue.ToString() == " : ")
                            return null;
                        else
                            return inValue;
                    }
                }
            }
            else
                return inValue;

        }

        /// <summary>
        /// Adds the export column to the export table.
        /// </summary>
        /// <param name="numFields">The number of occurrences of this field.</param>
        /// <param name="columnName">The name of the exported column.</param>
        /// <param name="dataType">The data type of the column.</param>
        /// <param name="maxLength">The maximum length of the column.</param>
        /// <param name="exportTable">The export table.</param>
        /// <param name="lutDescrColOrdinals">The lut description col ordinals.</param>
        private void AddExportColumn(int numFields, string tableName, string columnName, string fieldName, int fieldType, int maxLength,
            string fieldFormat, ref List<ExportField> exportFields)
        {
            Type dataType = null;
            int fieldLength = 0;
            bool autoNum = false;
            int attributeLength = 0;

            // Increment each time a different table is referenced.
            if (tableName != _lastTableName)
                _tableCount += 1;

            // Enable fields to be exported using a different
            // data type.
            switch (fieldType)
            {
                case 3:     // Integer
                    dataType = System.Type.GetType("System.Int32");
                    attributeLength = 2;
                    break;
                case 6:     // Single
                    dataType = System.Type.GetType("System.Single");
                    attributeLength = 4;
                    break;
                case 7:     // Double
                    dataType = System.Type.GetType("System.Double");
                    attributeLength = 8;
                    break;
                case 8:     // Date/Time
                    dataType = System.Type.GetType("System.DateTime");
                    attributeLength = 8;
                    break;
                case 10:    // Text
                    dataType = System.Type.GetType("System.String");
                    if (maxLength > 0)
                    {
                        fieldLength = Math.Min(maxLength, 254);
                        attributeLength = fieldLength;
                    }
                    else
                    {
                        fieldLength = 254;
                        attributeLength = fieldLength;
                    }
                    break;
                case 99:    // Autonumber
                    dataType = System.Type.GetType("System.Int32");
                    autoNum = true;
                    attributeLength = 4;
                    break;
                default:
                    dataType = System.Type.GetType("System.String");
                    fieldLength = maxLength;
                    attributeLength = maxLength;
                    break;
            }

            // If this field has multiple occurrences.
            if (numFields > 0)
            {
                int fieldCount = exportFields.Count + 1;

                for (int i = 1; i <= numFields; i++)
                {
                    ExportField fld = new();

                    // Enable new 'empty' fields to be included in exports.
                    if (tableName.Equals("<none>", StringComparison.CurrentCultureIgnoreCase))
                        fld.FieldOrdinal = -1;
                    else
                        fld.FieldOrdinal = _fieldCount;

                    fld.TableName = tableName;
                    fld.ColumnName = columnName;

                    // Include the occurrence counter in the field name, either
                    // where the user chooses or at the end.
                    if (OccurrenceCounterRegex().IsMatch(fieldName))
                        fld.FieldName = fieldName.Replace("<no>", i.ToString());
                    else
                    {
                        if (numFields == 1)
                            fld.FieldName = fieldName;
                        else
                            fld.FieldName = String.Format("{0}_{1}", fieldName, i);
                    }

                    fld.FieldType = dataType;

                    // Interweave multiple record fields from the same table together.
                    fld.FieldOrder = (_tableCount * 1000) + (i * 100) + fieldCount;

                    fld.FieldLength = fieldLength;
                    fld.FieldsCount = numFields;
                    fld.FieldFormat = fieldFormat;
                    fld.AutoNum = autoNum;

                    exportFields.Add(fld);

                    // Add the field attribute length to the running total.
                    _attributesLength += attributeLength;
                }
            }
            else
            {
                ExportField fld = new();

                // Enable new 'empty' fields to be included in exports.
                if (tableName.Equals("<none>", StringComparison.CurrentCultureIgnoreCase))
                    fld.FieldOrdinal = -1;
                else
                    fld.FieldOrdinal = _fieldCount;

                fld.TableName = tableName;
                fld.ColumnName = columnName;
                fld.FieldName = fieldName;
                fld.FieldType = dataType;

                // Interweave multiple record fields from the same table together.
                fld.FieldOrder = (_tableCount * 1000) + exportFields.Count + 1;

                fld.FieldLength = fieldLength;
                fld.FieldsCount = numFields;
                fld.FieldFormat = fieldFormat;
                fld.AutoNum = autoNum;

                exportFields.Add(fld);

                // Add the field attribute length to the running total.
                _attributesLength += attributeLength;
            }

            // Store the last table referenced.
            _lastTableName = tableName;

            // Increment the field counter.
            if (!tableName.Equals("<none>", StringComparison.CurrentCultureIgnoreCase))
                _fieldCount += 1;

        }

        private string RelationJoinClause(string joinType, string currTable, bool parentLeft,
            string parentTableAlias, DataRelation rel, List<string> fromList)
        {
            StringBuilder joinClausePart = new();

            for (int i = 0; i < rel.ParentColumns.Length; i++)
            {
                joinClausePart.Append(String.Format(" AND {0}.{2} = {1}.{3}", parentTableAlias, 
                    currTable, _viewModelMain.DataBase.QuoteIdentifier(rel.ParentColumns[i].ColumnName),
                    _viewModelMain.DataBase.QuoteIdentifier(rel.ChildColumns[i].ColumnName)));
            }

            if (parentTableAlias == _viewModelMain.DataBase.QuoteIdentifier(rel.ParentTable.TableName))
                parentTableAlias = String.Empty;
            else
                parentTableAlias = " " + parentTableAlias;

            string leftTable = String.Empty;
            string rightTable = string.Empty;
            if (parentLeft)
            {
                leftTable = _viewModelMain.DataBase.QuoteIdentifier(rel.ParentTable.TableName) + parentTableAlias;
                rightTable = currTable;
            }
            else
            {
                leftTable = currTable;
                rightTable = _viewModelMain.DataBase.QuoteIdentifier(rel.ParentTable.TableName) + parentTableAlias;
            }

            if (!fromList.Contains(currTable))
                return joinClausePart.Remove(0, 5).Insert(0, String.Format(" {0} {1} JOIN {2} ON ",
                    leftTable, joinType, rightTable)).ToString();
            else
                return joinClausePart.Remove(0, 5).Insert(0, String.Format(" {0} JOIN {1} ON ",
                    joinType, rightTable)).ToString();
        }

        private string GetTableAlias()
        {
            for (int i = 1; i < 5; i++)
            {
                for (int j = 122; j > 96; j--)
                {
                    char[] testCharArray = new char[i];
                    for (int k = 0; k < i; k++)
                        testCharArray[k] = (char)j;
                    string testString = new(testCharArray);
                    if (!_viewModelMain.HluDataset.Tables.Cast<DataTable>().Any(t => Regex.IsMatch(t.TableName,
                        testString + "[0-9]+", RegexOptions.IgnoreCase)))
                    {
                        return testString;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Defines a compiled case-insensitive regular expression that matches the suffix "_id".
        /// </summary>
        /// <remarks>
        /// - The pattern `(_id)` matches the exact string "_id".
        /// - The `RegexOptions.IgnoreCase` flag ensures that matching is case-insensitive.
        /// - The "en-GB" culture is specified to ensure consistent behavior in a UK English locale.
        /// - The `[GeneratedRegex]` attribute ensures that the regex is compiled at compile-time,
        ///   improving performance.
        /// </remarks>
        /// <returns>A <see cref="Regex"/> instance that can be used to match an "_id" suffix.</returns>
        [GeneratedRegex(@"(_id)", RegexOptions.IgnoreCase, "en-GB")]
        private static partial Regex IdSuffixRegex();

        /// <summary>
        /// Defines a compiled case-insensitive regular expression that matches the suffix "_user_id".
        /// </summary>
        /// <remarks>
        /// - The pattern `(_user_id)` matches the exact string "_user_id".
        /// - The `RegexOptions.IgnoreCase` flag ensures that matching is case-insensitive.
        /// - The "en-GB" culture is specified to ensure consistent behavior in a UK English locale.
        /// - The `[GeneratedRegex]` attribute ensures that the regex is compiled at compile-time,
        ///   improving performance.
        /// </remarks>
        /// <returns>A <see cref="Regex"/> instance that can be used to match a "_user_id" suffix.</returns>
        [GeneratedRegex(@"(_user_id)", RegexOptions.IgnoreCase, "en-GB")]
        private static partial Regex UseridSuffixRegex();

        /// <summary>
        /// Defines a compiled case-insensitive regular expression that matches the string "<no>".
        /// </summary>
        /// <remarks>
        /// - The pattern `(<no>)` matches the exact string "<no>", including the angle brackets.
        /// - The `RegexOptions.IgnoreCase` flag ensures that matching is case-insensitive.
        /// - The "en-GB" culture is specified to ensure consistent behavior in a UK English locale.
        /// - The `[GeneratedRegex]` attribute ensures that the regex is compiled at compile-time,
        ///   improving performance.
        /// </remarks>
        /// <returns>A <see cref="Regex"/> instance that can be used to match the string "<no>".</returns>
        [GeneratedRegex(@"(<no>)", RegexOptions.IgnoreCase, "en-GB")]
        private static partial Regex OccurrenceCounterRegex();

    }
}
