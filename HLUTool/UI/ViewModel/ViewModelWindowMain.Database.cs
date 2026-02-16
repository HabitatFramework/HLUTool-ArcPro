// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2014 Sussex Biodiversity Record Centre
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

using HLU.Data;
using HLU.Data.Connection;
using HLU.Data.Model;
using HLU.Data.Model.HluDataSetTableAdapters;
using HLU.Date;
using HLU.Helpers;
using HLU.Properties;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// Database operations partial for ViewModelWindowMain.
    /// Contains: All database transactions, queries, table adapter operations, data persistence.
    /// </summary>
    partial class ViewModelWindowMain
    {
        #region Fields

        #region Fields - Database

        private DbBase _db;
        private Dictionary<Type, List<SqlFilterCondition>> _childRowFilterDict;
        private Dictionary<Type, string> _childRowOrderByDict;

        #endregion Fields - Database

        #region Fields - Selection Filters

        private DataTable _gisSelection;
        private List<List<SqlFilterCondition>> _incidSelectionWhereClause;
        private string _osmmUpdateWhereClause;
        private SqlFilterCondition _incidMMPolygonsIncidFilter;

        #endregion Fields - Selection Filters

        #region Fields - Validation Lists

        private List<string> _habitatWarnings = [];
        private List<string> _priorityWarnings = [];
        private List<string> _detailsWarnings = [];
        private List<string[]> _conditionWarnings = null;
        private List<string[]> _source1Warnings = null;
        private List<string[]> _source2Warnings = null;
        private List<string[]> _source3Warnings = null;
        private List<string> _habitatErrors = [];
        private List<string> _priorityErrors = [];
        private List<string> _detailsErrors = [];
        private List<string[]> _conditionErrors = null;
        private List<string[]> _source1Errors = null;
        private List<string[]> _source2Errors = null;
        private List<string[]> _source3Errors = null;

        #endregion Fields - Validation Lists

        #endregion Fields

        #region Properties

        #region Properties - Selection State

        internal DataTable GisSelection
        {
            get { return _gisSelection; }
            set { _gisSelection = value; }
        }

        internal DataTable IncidSelection
        {
            get { return _incidSelection; }
            set { _incidSelection = value; }
        }

        internal List<List<SqlFilterCondition>> IncidSelectionWhereClause
        {
            get { return _incidSelectionWhereClause; }
            set { _incidSelectionWhereClause = value; }
        }

        internal string OSMMUpdateWhereClause
        {
            get { return _osmmUpdateWhereClause; }
            set { _osmmUpdateWhereClause = value; }
        }

        #endregion Properties - Selection State

        #region Properties - Filter State

        /// <summary>
        /// Are there any filters applied to the Incid table and
        /// is the tool currently not in bulk update mode?
        /// </summary>
        public bool IsFiltered
        {
            get
            {
                return (IsNotBulkMode || (IsBulkMode && IsOsmmBulkMode))
                    && _incidSelection != null
                    && _incidSelection.Rows.Count > 0;
            }
        }

        #endregion Properties - Filter State

        #region Properties - Validation

        internal List<string> HabitatWarnings
        {
            get { return _habitatWarnings; }
            set { _habitatWarnings = value; }
        }

        internal List<string> PriorityWarnings
        {
            get { return _priorityWarnings; }
            set { _priorityWarnings = value; }
        }

        internal List<string[]> ConditionWarnings
        {
            get { return _conditionWarnings; }
            set { _conditionWarnings = value; }
        }

        internal List<string> DetailsWarnings
        {
            get { return _detailsWarnings; }
            set { _detailsWarnings = value; }
        }

        internal List<string[]> Source1Warnings
        {
            get { return _source1Warnings; }
            set { _source1Warnings = value; }
        }

        internal List<string[]> Source2Warnings
        {
            get { return _source2Warnings; }
            set { _source2Warnings = value; }
        }

        internal List<string[]> Source3Warnings
        {
            get { return _source3Warnings; }
            set { _source3Warnings = value; }
        }

        internal List<string> HabitatErrors
        {
            get { return _habitatErrors; }
            set { _habitatErrors = value; }
        }

        internal List<string> PriorityErrors
        {
            get { return _priorityErrors; }
            set { _priorityErrors = value; }
        }

        internal List<string[]> ConditionErrors
        {
            get { return _conditionErrors; }
            set { _conditionErrors = value; }
        }

        internal List<string> DetailsErrors
        {
            get { return _detailsErrors; }
            set { _detailsErrors = value; }
        }

        internal List<string[]> Source1Errors
        {
            get { return _source1Errors; }
            set { _source1Errors = value; }
        }

        internal List<string[]> Source2Errors
        {
            get { return _source2Errors; }
            set { _source2Errors = value; }
        }

        internal List<string[]> Source3Errors
        {
            get { return _source3Errors; }
            set { _source3Errors = value; }
        }

        #endregion Properties - Validation

        #endregion Properties

        #region Methods

        #region Validation - Data Integrity

        /// <summary>
        /// Check if the GIS and database are in sync for the selected features.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="itemType"></param>
        /// <param name="itemTypes"></param>
        /// <returns></returns>
        public bool CheckInSync(string action, string itemType, string itemTypes = "", bool showMessage = true)
        {
            // Set plural item types if not provided.
            if (itemTypes == "")
                itemTypes = itemType;

            // Check if the GIS features have been physically split by the user but not processed in HLU yet.
            //if (((_gisSelection != null) && (_gisSelection.Rows.Count > 1)) &&
            //   ((SelectedIncidsInGISCount == 1) && (SelectedToidsInGISCount == 1) && (SelectedFragsInGISCount == 1)))
            if ((_currentIncidToidsInGISCount == _currentIncidToidsInDBCount) &&
               (_currentIncidFragsInGISCount > _currentIncidFragsInDBCount))
            {
                if (showMessage)
                {
                    MessageBox.Show(string.Format("{0} features may have been physically split in GIS but not processed in HLU yet.", itemTypes), string.Format("HLU: {0}", action),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            // Check if the GIS and database are in sync.
            else if ((_currentIncidToidsInGISCount > _currentIncidToidsInDBCount) ||
               (_currentIncidFragsInGISCount > _currentIncidFragsInDBCount))
            {
                if (showMessage)
                {
                    if (_currentIncidFragsInGISCount == 1)
                        MessageBox.Show(string.Format("{0} feature not found in database.", itemType), string.Format("HLU: {0}", action),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    else
                        MessageBox.Show(string.Format("{0} features not found in database.", itemTypes), string.Format("HLU: {0}", action),
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if there are any valid ihs matrix rows.
        /// </summary>
        /// <returns></returns>
        private bool CheckIhsMatrix()
        {
            if (_hluDS == null) return false;

            if (IsBulkMode) return true;

            if (_incidIhsMatrixRows == null)
            {
                HluDataSet.incid_ihs_matrixDataTable ihsMatrixTable = _hluDS.incid_ihs_matrix;
                GetIncidChildRowsDb([Incid],
                    _hluTableAdapterMgr.incid_ihs_matrixTableAdapter, ref ihsMatrixTable);
            }

            return _incidIhsMatrixRows != null;
            //return true;
        }

        /// <summary>
        /// Removes the incid ihs matrix rows.
        /// </summary>
        public void RemoveIncidIhsMatrixRows()
        {
            // Check if there are any valid ihs matrix rows.
            if (CheckIhsMatrix())
            {
                for (int i = 0; i < _incidIhsMatrixRows.Length; i++)
                {
                    if (_incidIhsMatrixRows[i].RowState != DataRowState.Detached)
                        _incidIhsMatrixRows[i].Delete();
                    _incidIhsMatrixRows[i] = null;
                }
            }
        }

        /// <summary>
        /// Checks if there are any valid ihs formation rows.
        /// </summary>
        /// <returns></returns>
        private bool CheckIhsFormation()
        {
            if (_hluDS == null) return false;

            if (IsBulkMode) return true;

            if (_incidIhsFormationRows == null)
            {
                HluDataSet.incid_ihs_formationDataTable ihsFormationTable = _hluDS.incid_ihs_formation;
                GetIncidChildRowsDb([Incid],
                    _hluTableAdapterMgr.incid_ihs_formationTableAdapter, ref ihsFormationTable);
            }
            return _incidIhsFormationRows != null;
        }

        /// <summary>
        /// Removes the incid ihs formation rows.
        /// </summary>
        public void RemoveIncidIhsFormationRows()
        {
            // Check if there are any valid ihs formation rows.
            if (CheckIhsFormation())
            {
                for (int i = 0; i < _incidIhsFormationRows.Length; i++)
                {
                    if (_incidIhsFormationRows[i].RowState != DataRowState.Detached)
                        _incidIhsFormationRows[i].Delete();
                    _incidIhsFormationRows[i] = null;
                }
            }
        }

        /// <summary>
        /// Checks if there are any valid ihs management rows.
        /// </summary>
        /// <returns></returns>
        private bool CheckIhsManagement()
        {
            if (_hluDS == null) return false;

            if (IsBulkMode) return true;

            if (_incidIhsManagementRows == null)
            {
                HluDataSet.incid_ihs_managementDataTable ihsManagementTable = _hluDS.incid_ihs_management;
                GetIncidChildRowsDb([Incid],
                    _hluTableAdapterMgr.incid_ihs_managementTableAdapter, ref ihsManagementTable);
            }

            return _incidIhsManagementRows != null;
        }

        /// <summary>
        /// Removes the incid ihs management rows.
        /// </summary>
        public void RemoveIncidIhsManagementRows()
        {
            // Check if there are any valid ihs management rows.
            if (CheckIhsManagement())
            {
                for (int i = 0; i < _incidIhsManagementRows.Length; i++)
                {
                    if (_incidIhsManagementRows[i].RowState != DataRowState.Detached)
                        _incidIhsManagementRows[i].Delete();
                    _incidIhsManagementRows[i] = null;
                }
            }
        }

        /// <summary>
        /// Checks if there are any valid ihs complex rows.
        /// </summary>
        /// <returns></returns>
        private bool CheckIhsComplex()
        {
            if (_hluDS == null) return false;

            if (IsBulkMode) return true;

            if (_incidIhsComplexRows == null)
            {
                HluDataSet.incid_ihs_complexDataTable ihsComplexTable = _hluDS.incid_ihs_complex;
                GetIncidChildRowsDb([Incid],
                    _hluTableAdapterMgr.incid_ihs_complexTableAdapter, ref ihsComplexTable);
            }

            return _incidIhsManagementRows != null;
        }

        /// <summary>
        /// Removes the incid ihs complex rows.
        /// </summary>
        public void RemoveIncidIhsComplexRows()
        {
            // Check if there are any valid ihs complex rows.
            if (CheckIhsComplex())
            {
                for (int i = 0; i < _incidIhsComplexRows.Length; i++)
                {
                    if (_incidIhsComplexRows[i].RowState != DataRowState.Detached)
                        _incidIhsComplexRows[i].Delete();
                    _incidIhsComplexRows[i] = null;
                }
            }
        }

        #endregion Validation - Data Integrity

        #region Dirty Checks

        /// <summary>
        /// Determines whether any of the incid tables are dirty].
        /// </summary>
        /// <returns>
        ///   <c>true</c> if dirty otherwise, <c>false</c>.
        /// </returns>
        internal bool IsDirtyIncid()
        {
            // If anything has changed in any of the data tables
            return ((_incidCurrentRow != null) && (_incidCurrentRow.RowState != DataRowState.Detached) &&
                ((_incidCurrentRow.Ishabitat_primaryNull() && !String.IsNullOrEmpty(_incidPrimary)) ||
                (!_incidCurrentRow.Ishabitat_primaryNull() && String.IsNullOrEmpty(_incidPrimary)) ||
                (_incidPrimary != _incidCurrentRow.habitat_primary) ||
                !CompareIncidCurrentRowClone()));
        }

        /// <summary>
        /// Determines whether the incid ihs matrix table is dirty.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if dirty otherwise, <c>false</c>.
        /// </returns>
        internal bool IsDirtyIncidIhsMatrix()
        {
            if (_incidIhsMatrixRows != null)
            {
                if (_incidIhsMatrixRows.Count(r => r != null) != _origIncidIhsMatrixCount) return true;

                foreach (DataRow r in _incidIhsMatrixRows)
                    if (ViewModelWindowMainHelpers.RowIsDirty(r)) return true;

                return false;
            }
            return _origIncidIhsMatrixCount != 0;
        }

        /// <summary>
        /// Determines whether the incid ihs formation table is dirty.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if dirty otherwise, <c>false</c>.
        /// </returns>
        internal bool IsDirtyIncidIhsFormation()
        {
            if (_incidIhsFormationRows != null)
            {
                if (_incidIhsFormationRows.Count(r => r != null) != _origIncidIhsFormationCount) return true;

                foreach (DataRow r in _incidIhsFormationRows)
                    if (ViewModelWindowMainHelpers.RowIsDirty(r)) return true;

                return false;
            }
            return _origIncidIhsFormationCount != 0;
        }

        /// <summary>
        /// Determines whether the incid ihs management table is dirty.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if dirty otherwise, <c>false</c>.
        /// </returns>
        internal bool IsDirtyIncidIhsManagement()
        {
            if (_incidIhsManagementRows != null)
            {
                if (_incidIhsManagementRows.Count(r => r != null) != _origIncidIhsManagementCount) return true;

                foreach (DataRow r in _incidIhsManagementRows)
                    if (ViewModelWindowMainHelpers.RowIsDirty(r)) return true;

                return false;
            }
            return _origIncidIhsManagementCount != 0;
        }

        /// <summary>
        /// Determines whether the incid ihs complex table is dirty.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if dirty otherwise, <c>false</c>.
        /// </returns>
        internal bool IsDirtyIncidIhsComplex()
        {
            if (_incidIhsComplexRows != null)
            {
                if (_incidIhsComplexRows.Count(r => r != null) != _origIncidIhsComplexCount) return true;

                foreach (DataRow r in _incidIhsComplexRows)
                    if (ViewModelWindowMainHelpers.RowIsDirty(r)) return true;

                return false;
            }
            return _origIncidIhsComplexCount != 0;
        }

        /// <summary>
        /// Determines whether the incid secondary table is dirty.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if dirty otherwise, <c>false</c>.
        /// </returns>
        internal bool IsDirtyIncidSecondary()
        {
            //TODO: Check this works/is needed
            if (_incidSecondaryHabitats == null)
                return false;

            if (_incidSecondaryRows.Any(r => r.RowState == DataRowState.Deleted)) return true;

            if (_incidSecondaryHabitats != null)
            {
                if (_incidSecondaryHabitats.Any(sh => IncidSecondaryRowDirty(sh))) return true;
            }

            if ((_incidSecondaryRows != null) && (_incidSecondaryHabitats.Count !=
                _incidSecondaryRows.Length)) return true;

            if (_incidSecondaryRows != null)
            {
                foreach (DataRow r in _incidSecondaryRows)
                    if (ViewModelWindowMainHelpers.RowIsDirty(r)) return true;
            }
            return false;
        }

        /// <summary>
        /// Determines whether the incid condition table is dirty.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if dirty otherwise, <c>false</c>.
        /// </returns>
        internal bool IsDirtyIncidCondition()
        {
            if (_incidConditionRows != null)
            {
                if (_incidConditionRows.Count(r => r != null) != _origIncidConditionCount) return true;

                foreach (DataRow r in _incidConditionRows)
                    if (ViewModelWindowMainHelpers.RowIsDirty(r)) return true;

                return false;
            }
            return _origIncidConditionCount != 0;
        }

        /// <summary>
        /// Determines whether the incid bap table is dirty.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if dirty otherwise, <c>false</c>.
        /// </returns>
        internal bool IsDirtyIncidBap()
        {
            //TODO: Check if works/is needed
            if (_incidBapRowsAuto == null)
                return false;

            if (_incidBapRows.Any(r => r.RowState == DataRowState.Deleted)) return true;

            int incidBapRowsAutoNum = 0;
            if (_incidBapRowsAuto != null)
            {
                incidBapRowsAutoNum = _incidBapRowsAuto.Count;
                if (_incidBapRowsAuto.Any(be => IncidBapRowDirty(be))) return true;
            }
            int incidBapRowsAutoUserNum = 0;
            if (_incidBapRowsUser != null)
            {
                incidBapRowsAutoUserNum = _incidBapRowsUser.Count;
                if (_incidBapRowsUser.Any(be => IncidBapRowDirty(be))) return true;
            }

            if ((_incidBapRows != null) && (incidBapRowsAutoNum + incidBapRowsAutoUserNum !=
                _incidBapRows.Length)) return true;

            if (_incidBapRows != null)
            {
                foreach (DataRow r in _incidBapRows)
                    if (ViewModelWindowMainHelpers.RowIsDirty(r)) return true;
            }
            return false;
        }

        /// <summary>
        /// Determines whether the incid sources table is dirty.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if dirty otherwise, <c>false</c>.
        /// </returns>
        private bool IsDirtyIncidSources()
        {
            if (_incidSourcesRows != null)
            {
                if (_incidSourcesRows.Count(r => r != null) != _origIncidSourcesCount) return true;

                foreach (DataRow r in _incidSourcesRows)
                    if (ViewModelWindowMainHelpers.RowIsDirty(r)) return true;

                return false;
            }
            return _origIncidSourcesCount != 0;
        }

        /// <summary>
        /// Determines whether the incid osmm updates table is dirty.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if dirty otherwise, <c>false</c>.
        /// </returns>
        internal bool IsDirtyIncidOSMMUpdates()
        {
            if (_incidOSMMUpdatesRows != null)
            {
                foreach (DataRow r in _incidOSMMUpdatesRows)
                    if (ViewModelWindowMainHelpers.RowIsDirty(r)) return true;
            }
            return false;
        }

        private bool IncidSecondaryRowDirty(SecondaryHabitat sh)
        {
            // deleted secondary habitat row
            var q = _incidSecondaryRows.Where(r => r.RowState != DataRowState.Deleted && r.secondary_id == sh.secondary_id);
            switch (q.Count())
            {
                case 0:
                    return true; // new row;
                case 1:
                    if (!sh.IsValid() && sh.IsAdded) return true;

                    HluDataSet.incid_secondaryRow oldRow = q.ElementAt(0);
                    object[] itemArray = sh.ToItemArray();
                    for (int i = 0; i < itemArray.Length; i++)
                    {
                        if (oldRow.IsNull(i))
                        {
                            if (itemArray[i] != null) return true;
                        }
                        else if (!oldRow[i].Equals(itemArray[i]))
                        {
                            return true;
                        }
                    }
                    return false;
                default:
                    return true; // duplicate row must be new or altered
            }
        }

        private bool IncidBapRowDirty(BapEnvironment be)
        {
            // deleted user BAP row
            var q = _incidBapRows.Where(r => r.RowState != DataRowState.Deleted && r.bap_id == be.bap_id);
            switch (q.Count())
            {
                case 0:
                    return true; // new row;
                case 1:
                    // Only flag an incid_bap row that is invalid as dirty if it has
                    // been added by the user. This allows existing records to be
                    // viewed in the user interface without warning the user that
                    // the data has changed.
                    if (!be.IsValid() && be.IsAdded) return true;

                    HluDataSet.incid_bapRow oldRow = q.ElementAt(0);
                    object[] itemArray = be.ToItemArray();
                    for (int i = 0; i < itemArray.Length; i++)
                    {
                        if (oldRow.IsNull(i))
                        {
                            if (itemArray[i] != null) return true;
                        }
                        else if (!oldRow[i].Equals(itemArray[i]))
                        {
                            return true;
                        }
                    }
                    return false;
                default:
                    return true; // duplicate row must be new or altered
            }
        }

        #endregion Dirty Checks

        #region Validation

        /// <summary>
        /// Determines whether the specified DataRow contains non-null values for all columns that do not allow nulls.
        /// </summary>
        /// <remarks>Columns that allow null values are not considered when determining completeness. If
        /// the DataRow is null, the method returns false.</remarks>
        /// <param name="r">The DataRow to evaluate for completeness. Cannot be null.</param>
        /// <returns>true if all columns in the row that do not allow nulls contain non-null values; otherwise, false.</returns>
        internal bool IsCompleteRow(DataRow r)
        {
            if (r == null) return false;

            foreach (DataColumn c in r.Table.Columns)
            {
                if (!c.AllowDBNull && r.IsNull(c)) return false;
            }

            return true;
        }

        /// <summary>
        /// Validates the condition fields and returns a list of errors for missing or invalid values.
        /// </summary>
        /// <remarks>Each error entry identifies a specific field and describes the validation issue. This
        /// method checks for required relationships between the condition, its qualifier, and its date, and reports
        /// errors if any required field is missing or invalid.</remarks>
        /// <returns>A list of string arrays, where each array contains the field name and the corresponding error message. The
        /// list is empty if all condition fields are valid.</returns>
        private List<string[]> ValidateCondition()
        {
            List<string[]> errors = [];

            // Validate the condition fields if no condition has been entered
            if (IncidCondition == null)
            {
                if (IncidConditionQualifier != null)
                    errors.Add(["IncidConditionQualifier", "Error: Condition qualifier is not valid without a condition"]);
                if (IncidConditionDate != null)
                    errors.Add(["IncidConditionDate", "Error: Condition date is not valid without a condition"]);
            }
            else
            {
                // Check the condition fields if a condition has been entered
                if (IncidConditionQualifier == null)
                    errors.Add(["IncidConditionQualifier", "Error: Condition qualifier is mandatory for a condition"]);
                if (IncidConditionDate == null)
                    errors.Add(["IncidConditionDate", "Error: Condition date is mandatory for a condition"]);
                else if (IncidConditionDate.IsBad)
                    errors.Add(["IncidConditionDate", "Error: Invalid condition vague date"]);
            }

            return errors;
        }

        /// <summary>
        /// Validates the primary source fields and returns a list of validation errors, if any are found.
        /// </summary>
        /// <remarks>This method checks for required fields, valid values, and logical consistency among
        /// the primary source fields. It also enforces special rules when operating in OSMM Bulk Update mode. Each
        /// error is represented as a two-element array: the first element is the field name, and the second is the
        /// error message.</remarks>
        /// <returns>A list of string arrays, where each array contains the field name and the corresponding error message. The
        /// list is empty if no validation errors are found.</returns>
        private List<string[]> ValidateSource1()
        {
            List<string[]> errors = [];

            // Validate the source if it is real
            if (IncidSource1Id != null && IncidSource1Id != Int32.MinValue)
            {
                // Check the source id is found in the lookup table
                EnumerableRowCollection<HluDataSet.lut_sourcesRow> rows =
                    HluDataset.lut_sources.Where(r => r.source_id == IncidSource1Id);
                if (!rows.Any())
                    errors.Add(["IncidSource1Id", "Error: Source name is mandatory for each source"]);
                if (IncidSource1Date == null)
                    errors.Add(["IncidSource1Date", "Error: Date is mandatory for each source"]);
                else if (IncidSource1Date.IsBad)
                    errors.Add(["IncidSource1Date", "Error: Invalid vague date"]);
                if (String.IsNullOrEmpty(IncidSource1HabitatClass))
                    errors.Add(["IncidSource1HabitatClass", "Error: Habitat class is mandatory for each source"]);
                else if ((IncidSource1HabitatClass.Equals("none", StringComparison.CurrentCultureIgnoreCase)) != String.IsNullOrEmpty(IncidSource1HabitatType))
                    errors.Add(["IncidSource1HabitatType", "Error: Habitat type is mandatory if habitat class is filled in"]);

                // Use the skip value from settings
                string skipVal = Settings.Default.SourceImportanceSkip;
                if (String.IsNullOrEmpty(IncidSource1BoundaryImportance))
                {
                    errors.Add(["IncidSource1BoundaryImportance", "Error: Boundary importance is mandatory for each source"]);
                }
                else
                {
                    ValidateSourceDuplicates(@"IncidSource\d+BoundaryImportance", @"\d+", "1", skipVal, errors);

                    // Validates the source importances by ensuring that boundary and habitat importance
                    // values are applied in order (as specified in the settings).
                    ValidateSourceImportances(@"IncidSource\d+BoundaryImportance", @"\d+", "1", errors);
                }
                if (String.IsNullOrEmpty(IncidSource1HabitatImportance))
                {
                    errors.Add([ "IncidSource1HabitatImportance",
                        "Error: Habitat importance is mandatory for each source" ]);
                }
                else
                {
                    ValidateSourceDuplicates(@"IncidSource\d+HabitatImportance", @"\d+", "1", skipVal, errors);

                    // Validates the source importances by ensuring that boundary and habitat importance
                    // values are applied in order (as specified in the settings).
                    ValidateSourceImportances(@"IncidSource\d+HabitatImportance", @"\d+", "1", errors);
                }
            }
            else
            {
                // Validation for OSMM Bulk Update mode.
                if ((IsOsmmBulkMode) &&
                    (IncidSource2Id == null || IncidSource2Id == Int32.MinValue) &&
                    (IncidSource3Id == null || IncidSource3Id == Int32.MinValue))
                    errors.Add([ "IncidSource1Id",
                        "Error: At least one source must be specified" ]);
                if (IncidSource1Date != null)
                    errors.Add([ "IncidSource1Date",
                        "Error: Date cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource1HabitatClass))
                    errors.Add([ "IncidSource1HabitatClass",
                        "Error: Habitat class cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource1HabitatType))
                    errors.Add([ "IncidSource1HabitatType",
                        "Error: Habitat type cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource1BoundaryImportance))
                    errors.Add([ "IncidSource1BoundaryImportance",
                        "Error: Boundary importance cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource1HabitatImportance))
                    errors.Add([ "IncidSource1HabitatImportance",
                        "Error: Habitat importance cannot be filled in if no source has been specified" ]);
            }

            return errors;
        }

        /// <summary>
        /// Validates the secondary source fields and returns a list of validation errors, if any are found.
        /// </summary>
        /// <remarks>Each validation error is represented as a two-element array: the first element is the
        /// name of the field with the error, and the second element is a descriptive error message. This method checks
        /// for required fields, valid values, and logical consistency among related fields for the secondary source. It
        /// should be called when validating user input or data integrity before processing or saving the secondary
        /// source information.</remarks>
        /// <returns>A list of string arrays, where each array contains the field name and the corresponding error message. The
        /// list is empty if no validation errors are found.</returns>
        private List<string[]> ValidateSource2()
        {
            List<string[]> errors = [];

            // Validate the source if it is real
            if (IncidSource2Id != null && IncidSource2Id != Int32.MinValue)
            {
                // Check the source id is found in the lookup table
                EnumerableRowCollection<HluDataSet.lut_sourcesRow> rows =
                    HluDataset.lut_sources.Where(r => r.source_id == IncidSource2Id);
                if (!rows.Any())
                    errors.Add(["IncidSource1Id", "Error: Source name is mandatory for each source"]);
                if (IncidSource2Date == null)
                    errors.Add(["IncidSource2Date", "Error: Date is mandatory for each source"]);
                else if (IncidSource2Date.IsBad)
                    errors.Add(["IncidSource2Date", "Error: Invalid vague date"]);
                if (String.IsNullOrEmpty(IncidSource2HabitatClass))
                    errors.Add(["IncidSource2HabitatClass", "Error: Habitat class is mandatory for each source"]);
                else if ((IncidSource2HabitatClass.Equals("none", StringComparison.CurrentCultureIgnoreCase)) != String.IsNullOrEmpty(IncidSource2HabitatType))
                    errors.Add(["IncidSource2HabitatType", "Error: Habitat type is mandatory if habitat class is filled in"]);

                // Use the skip value from settings
                string skipVal = Settings.Default.SourceImportanceSkip;
                if (String.IsNullOrEmpty(IncidSource2BoundaryImportance))
                {
                    errors.Add(["IncidSource2BoundaryImportance", "Error: Boundary importance is mandatory for each source"]);
                }
                else
                {
                    ValidateSourceDuplicates(@"IncidSource\d+BoundaryImportance", @"\d+", "2", skipVal, errors);

                    // Validates the source importances by ensuring that boundary and habitat importance
                    // values are applied in order (as specified in the settings).
                    ValidateSourceImportances(@"IncidSource\d+BoundaryImportance", @"\d+", "2", errors);
                }
                if (String.IsNullOrEmpty(IncidSource2HabitatImportance))
                {
                    errors.Add([ "IncidSource2HabitatImportance",
                        "Error: Habitat importance is mandatory for each source" ]);
                }
                else
                {
                    ValidateSourceDuplicates(@"IncidSource\d+HabitatImportance", @"\d+", "2", skipVal, errors);

                    // Validates the source importances by ensuring that boundary and habitat importance
                    // values are applied in order (as specified in the settings).
                    ValidateSourceImportances(@"IncidSource\d+HabitatImportance", @"\d+", "2", errors);
                }
            }
            else
            {
                if (IncidSource2Date != null)
                    errors.Add([ "IncidSource2Date",
                        "Error: Date cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource2HabitatClass))
                    errors.Add([ "IncidSource2HabitatClass",
                        "Error: Habitat class cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource2HabitatType))
                    errors.Add([ "IncidSource2HabitatType",
                        "Error: Habitat type cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource2BoundaryImportance))
                    errors.Add([ "IncidSource2BoundaryImportance",
                        "Error: Boundary importance cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource2HabitatImportance))
                    errors.Add([ "IncidSource2HabitatImportance",
                        "Error: Habitat importance cannot be filled in if no source has been specified" ]);
            }

            return errors;
        }

        /// <summary>
        /// Validates the third source's data fields and returns a list of validation errors, if any are found.
        /// </summary>
        /// <remarks>Each validation error is represented as a two-element string array: the first element
        /// is the name of the field with the error, and the second element is a descriptive error message. Validation
        /// checks include required fields, lookup table existence, and logical consistency between related fields. This
        /// method does not throw exceptions for validation failures; all issues are reported in the returned
        /// list.</remarks>
        /// <returns>A list of string arrays, where each array contains the field name and the corresponding error message. The
        /// list is empty if no validation errors are found.</returns>
        private List<string[]> ValidateSource3()
        {
            List<string[]> errors = [];

            // Validate the source if it is real
            if (IncidSource3Id != null && IncidSource3Id != Int32.MinValue)
            {
                // Check the source id is found in the lookup table
                EnumerableRowCollection<HluDataSet.lut_sourcesRow> rows =
                    HluDataset.lut_sources.Where(r => r.source_id == IncidSource3Id);
                if (!rows.Any())
                    errors.Add(["IncidSource1Id", "Error: Source name is mandatory for each source"]);
                if (IncidSource3Date == null)
                    errors.Add(["IncidSource3Date", "Error: Date is mandatory for each source"]);
                else if (IncidSource3Date.IsBad)
                    errors.Add(["IncidSource3Date", "Error: Invalid vague date"]);
                if (String.IsNullOrEmpty(IncidSource3HabitatClass))
                    errors.Add(["IncidSource3HabitatClass", "Error: Habitat class is mandatory for each source"]);
                else if ((IncidSource3HabitatClass.Equals("none", StringComparison.CurrentCultureIgnoreCase)) != String.IsNullOrEmpty(IncidSource3HabitatType))
                    errors.Add(["IncidSource3HabitatType", "Error: Habitat type is mandatory if habitat class is filled in"]);

                // Use the skip value from settings
                string skipVal = Settings.Default.SourceImportanceSkip;
                if (String.IsNullOrEmpty(IncidSource3BoundaryImportance))
                {
                    errors.Add(["IncidSource3BoundaryImportance", "Error: Boundary importance is mandatory for each source"]);
                }
                else
                {
                    ValidateSourceDuplicates(@"IncidSource\d+BoundaryImportance", @"\d+", "3", skipVal, errors);

                    // Validates the source importances by ensuring that boundary and habitat importance
                    // values are applied in order (as specified in the settings).
                    ValidateSourceImportances(@"IncidSource\d+BoundaryImportance", @"\d+", "3", errors);
                }
                if (String.IsNullOrEmpty(IncidSource3HabitatImportance))
                {
                    errors.Add([ "IncidSource3HabitatImportance",
                        "Error: Habitat importance is mandatory for each source" ]);
                }
                else
                {
                    ValidateSourceDuplicates(@"IncidSource\d+HabitatImportance", @"\d+", "3", skipVal, errors);

                    // Validates the source importances by ensuring that boundary and habitat importance
                    // values are applied in order (as specified in the settings).
                    ValidateSourceImportances(@"IncidSource\d+HabitatImportance", @"\d+", "3", errors);
                }
            }
            else
            {
                if (IncidSource3Date != null)
                    errors.Add([ "IncidSource3Date",
                        "Error: Date cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource3HabitatClass))
                    errors.Add([ "IncidSource3HabitatClass",
                        "Error: Habitat class cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource3HabitatType))
                    errors.Add([ "IncidSource3HabitatType",
                        "Error: Habitat type cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource3BoundaryImportance))
                    errors.Add([ "IncidSource3BoundaryImportance",
                        "Error: Boundary importance cannot be filled in if no source has been specified" ]);
                if (!String.IsNullOrEmpty(IncidSource3HabitatImportance))
                    errors.Add([ "IncidSource3HabitatImportance",
                        "Error: Habitat importance cannot be filled in if no source has been specified" ]);
            }

            return errors;
        }

        /// <summary>
        /// Validates the source importances by ensuring that boundary and habitat importance
        /// values are applied in order (as specified in the settings).
        /// </summary>
        /// <param name="propNamePat">Pattern for regular expression match of property name in current class.</param>
        /// <param name="propNamePatWildcard">Wildcard element in propNamePat.</param>
        /// <param name="propNameWildcardValCurrProp">Value of propNamePatWildcard for current property to be validated.</param>
        /// <param name="errors">List of errors, composed of name of property in error and error message.
        /// The error message is built by splitting propNamePat on propNamePatWildcard and prepending the
        /// last element of the split array with blanks added in front of capital letters to the string
        /// "must be applied in the order ...".</param>
        private void ValidateSourceImportances(string propNamePat, string propNamePatWildcard,
            string propNameWildcardValCurrProp, List<string[]> errors)
        {
            string propNameCheck = propNamePat.Replace(propNamePatWildcard, propNameWildcardValCurrProp);
            PropertyInfo propInf = this.GetType().GetProperty(propNameCheck);
            if (propInf == null) return;

            string skipVal = Settings.Default.SourceImportanceSkip;
            string ord1val = Settings.Default.SourceImportanceApply1;
            string ord2val = Settings.Default.SourceImportanceApply2;
            string ord3val = Settings.Default.SourceImportanceApply3;

            object checkVal = propInf.GetValue(this, null);
            if ((checkVal == null) || checkVal.Equals(skipVal)) return;

            string[] split = propNamePat.Split([propNamePatWildcard], StringSplitOptions.None);
            string errMsg = String.Format("Error: {0}", split[split.Length - 1]);

            errMsg = string.Join(" ", StringHelper.GetCapitalisedRegex().Matches(errMsg).Cast<Match>().Select(m => errMsg.Substring(m.Index, m.Length)
                .Concat(string.Format(" must be applied in the order {0}, {1} then {2}", ord1val, ord2val, ord3val))));

            if (!String.IsNullOrEmpty(ord1val))
            {
                int ord1Sources = 0;
                foreach (PropertyInfo pi in this.GetType().GetProperties().Where(pn => Regex.IsMatch(pn.Name, propNamePat)))
                {
                    object compVal = pi.GetValue(this, null);
                    if ((compVal != null) && !compVal.Equals(skipVal) && compVal.Equals(ord1val))
                    {
                        ord1Sources += 1;
                    }
                }
                if (ord1Sources == 0 && checkVal.Equals(ord2val))
                    errors.Add([propNameCheck, errMsg]);
            }

            if (!String.IsNullOrEmpty(ord2val))
            {
                int ord2Sources = 0;
                foreach (PropertyInfo pi in this.GetType().GetProperties().Where(pn => Regex.IsMatch(pn.Name, propNamePat)))
                {
                    object compVal = pi.GetValue(this, null);
                    if ((compVal != null) && !compVal.Equals(skipVal) && compVal.Equals(ord2val))
                    {
                        ord2Sources += 1;
                    }
                }
                if (ord2Sources == 0 && checkVal.Equals(ord3val))
                    errors.Add([propNameCheck, errMsg]);
            }

        }

        /// <summary>
        /// Checks all properties of current class whose names follow a specified pattern for duplicate values.
        /// </summary>
        /// <param name="propNamePat">Pattern for regular expression match of property name in current class.</param>
        /// <param name="propNamePatWildcard">Wildcard element in propNamePat.</param>
        /// <param name="propNameWildcardValCurrProp">Value of propNamePatWildcard for current property to be validated.</param>
        /// <param name="skipVal">Value that may occur repeatedly (e.g. "none").</param>
        /// <param name="errors">List of errors, composed of name of property in error and error message.
        /// The error message is built by splitting propNamePat on propNamePatWildcard and prepending the
        /// last element of the split array with blanks added in front of capital letters to the string
        /// "of two sources cannot be equal for the same INCID".</param>
        private void ValidateSourceDuplicates(string propNamePat, string propNamePatWildcard,
            string propNameWildcardValCurrProp, object skipVal, List<string[]> errors)
        {
            string propNameCheck = propNamePat.Replace(propNamePatWildcard, propNameWildcardValCurrProp);
            PropertyInfo propInf = this.GetType().GetProperty(propNameCheck);
            if (propInf == null) return;

            object checkVal = propInf.GetValue(this, null);
            if ((checkVal == null) || checkVal.Equals(skipVal)) return;

            string[] split = propNamePat.Split([propNamePatWildcard], StringSplitOptions.None);
            string errMsg = String.Format("Error: {0}", split[split.Length - 1]);

            errMsg = string.Join(" ", StringHelper.GetCapitalisedRegex().Matches(errMsg).Cast<Match>().Select(m => errMsg.Substring(m.Index, m.Length)
                .Concat(" of two sources cannot be equal for the same INCID")));

            foreach (PropertyInfo pi in this.GetType().GetProperties().Where(pn => pn.Name != propNameCheck && Regex.IsMatch(pn.Name, propNamePat)))
            {
                if (pi.Name == propNameCheck) continue;

                object compVal = pi.GetValue(this, null);
                if ((compVal != null) && !compVal.Equals(skipVal) && compVal.Equals(checkVal))
                {
                    errors.Add([propNameCheck, errMsg]);
                    errors.Add([pi.Name, errMsg]);
                }
            }
        }

        #endregion Validation

        #region Error Messages

        /// <summary>
        /// Gets the error message for the property with the given column name.
        /// </summary>
        /// <param name="columnName"></param>
        /// <param name="errorList"></param>
        /// <returns></returns>
        private string ErrorMessage(string columnName, List<string[]> errorList)
        {
            if (errorList != null)
            {
                IEnumerable<string[]> err = errorList.Where(s => s[0] == columnName);
                if (err.Any()) return err.ElementAt(0)[1];
            }
            return null;
        }

        /// <summary>
        /// Gets a list of all of the errors.
        /// </summary>
        /// <param name="errors"></param>
        /// <returns></returns>
        private string ErrorMessageList(List<string[]> errors)
        {
            if ((errors == null) || (errors.Count == 0)) return null;

            StringBuilder sbMsg = new();

            foreach (string[] e in errors)
            {
                if ((e.Length == 2) && (!String.IsNullOrEmpty(e[1])))
                    sbMsg.Append(Environment.NewLine).Append(e[1]);
            }

            if (sbMsg.Length > 0)
            {
                sbMsg.Remove(0, 1);
                return sbMsg.ToString();
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Adds a column name to the list of errors if it is not already in the list.
        /// </summary>
        /// <param name="errorList"></param>
        /// <param name="columnName"></param>
        public void AddToErrorList(List<string> errorList, string columnName)
        {
            if (errorList.Contains(columnName))
                return;

            errorList.Add(columnName);
        }

        /// <summary>
        /// Removes a column name from the list of errors.
        /// </summary>
        /// <param name="errorList"></param>
        /// <param name="columnName"></param>
        public void RemoveFromErrorList(List<string> errorList, string columnName)
        {
            errorList.Remove(columnName);
        }

        /// <summary>
        /// Resets all warnings and errors.
        /// </summary>
        public void ResetWarningsErrors()
        {
            _habitatWarnings = [];
            _habitatErrors = [];
            _priorityWarnings = [];
            _priorityErrors = [];
            _detailsWarnings = [];
            _detailsErrors = [];
            _conditionWarnings = null;
            _conditionErrors = null;
            _source1Warnings = null;
            _source2Warnings = null;
            _source3Warnings = null;
            _source1Errors = null;
            _source2Errors = null;
            _source3Errors = null;
        }

        #endregion Error Messages

        #region Habitat Logic

        /// <summary>
        /// Actions when the primary code has been changed.
        /// </summary>
        /// <param name="incidPrimary">The incid primary.</param>
        private void NewPrimaryHabitat(string incidPrimary)
        {
            if (incidPrimary != null)
            {
                // Set the primary habitat category.
                _incidPrimaryCategory = _lutPrimary.Where(p => p.code == incidPrimary).ElementAt(0).category;

                // Set NVC codes based on current primary habitat
                _incidNVCCodes = null;
                if (_primaryCodes != null)
                {
                    var q = _primaryCodes.Where(h => h.code == _incidPrimary);
                    if (q.Any())
                        _incidNVCCodes = q.ElementAt(0).nvc_codes;
                }

                // Store all secondary habitat codes that are flagged as local for
                // all secondary groups that relate to the primary habitat category.
                _secondaryCodesValid = (from s in SecondaryHabitatCodesAll
                                        join ps in _lutPrimarySecondary on s.code equals ps.code_secondary
                                        where ((ps.code_primary == _incidPrimary) || (ps.code_primary.EndsWith('*') && Regex.IsMatch(_incidPrimary, @"\A" + ps.code_primary.TrimEnd('*') + @"") == true))
                                        select s).OrderBy(r => r.sort_order).ThenBy(r => r.description).ToArray();

                // Store the list of valid secondary codes.
                SecondaryHabitat.ValidSecondaryCodes = _secondaryCodesValid.Select(s => s.code);
            }
            else
            {
                _incidPrimaryCategory = null;
                _incidNVCCodes = null;
                _secondaryCodesValid = null;

                // Clear the list of valid secondary codes.
                SecondaryHabitat.ValidSecondaryCodes = null;
            }

            // Refresh the related fields
            OnPropertyChanged(nameof(NvcCodes));

            OnPropertyChanged(nameof(SecondaryGroupCodes));
            OnPropertyChanged(nameof(SecondaryGroupEnabled));
            SecondaryGroup = _preferredSecondaryGroup;
            OnPropertyChanged(nameof(SecondaryGroup));

            OnPropertyChanged(nameof(CanAddSecondaryHabitat));
            OnPropertyChanged(nameof(CanAddSecondaryHabitatList));

            // Refresh secondary table to re-trigger the validation.
            RefreshSecondaryHabitats();
        }

        /// <summary>
        /// Gets the secondary habitats.
        /// </summary>
        public void GetSecondaryHabitats()
        {
            // Remove any existing handlers before assigning a new collection.
            if (_incidSecondaryHabitats != null)
                _incidSecondaryHabitats.CollectionChanged -= _incidSecondaryHabitats_CollectionChanged;

            // Identify any secondary habitat rows that have not been marked as deleted.
            IEnumerable<HluDataSet.incid_secondaryRow> incidSecondaryRowsUndel =
                _incidSecondaryRows.Where(r => r.RowState != DataRowState.Deleted);

            // If there are any rows not marked as deleted add them to the collection.
            if (incidSecondaryRowsUndel != null)
            {
                // Order the secondary codes as required
                _incidSecondaryHabitats = _secondaryCodeOrder switch
                {
                    "As entered" => new ObservableCollection<SecondaryHabitat>(
                           incidSecondaryRowsUndel.OrderBy(r => r.secondary_id).Select(r => new SecondaryHabitat(IsBulkMode, r))),
                    "By group then code" => new ObservableCollection<SecondaryHabitat>(
                           incidSecondaryRowsUndel.OrderBy(r => r.secondary_group).ThenBy(r => r.secondary).Select(r => new SecondaryHabitat(IsBulkMode, r))),
                    "By code" => new ObservableCollection<SecondaryHabitat>(
                           incidSecondaryRowsUndel.OrderBy(r => r.secondary).Select(r => new SecondaryHabitat(IsBulkMode, r))),
                    _ => new ObservableCollection<SecondaryHabitat>(
                           incidSecondaryRowsUndel.OrderBy(r => r.secondary_id).Select(r => new SecondaryHabitat(IsBulkMode, r)))
                };
            }
            else
            {
                // Otherwise there can't be any secondary habitat rows so
                // set a new collection.
                _incidSecondaryHabitats = [];
            }

            // Track any changes to the user rows collection.
            _incidSecondaryHabitats.CollectionChanged += _incidSecondaryHabitats_CollectionChanged;

            // Set the new list of secondary habitat rows for the class.
            SecondaryHabitat.SecondaryHabitatList = _incidSecondaryHabitats;

            // Set the validation option in the secondary habitat environment.
            SecondaryHabitat.PrimarySecondaryCodeValidation = _primarySecondaryCodeValidation;

            // Check if there are any errors in the secondary habitat records to see
            // if the Habitats tab label should be flagged as also in error.
            if (_incidSecondaryHabitats != null && _incidSecondaryHabitats.Count > 0)
            {
                int countInvalid = _incidSecondaryHabitats.Count(sh => !sh.IsValid());
                if (countInvalid > 0)
                    AddToErrorList(_habitatErrors, "SecondaryHabitat");
                else
                    RemoveFromErrorList(_habitatErrors, "SecondaryHabitat");
            }
            else
                RemoveFromErrorList(_habitatErrors, "SecondaryHabitat");

            OnPropertyChanged(nameof(IncidSecondaryHabitats));
            OnPropertyChanged(nameof(HabitatSecondariesMandatory));
            OnPropertyChanged(nameof(HabitatTabLabel));
        }

        /// <summary>
        /// Add a secondary habitat.
        /// </summary>
        public bool AddSecondaryHabitat(bool bulkUpdateMode, int secondary_id, string incid, string secondary_habitat, string secondary_group)
        {
            // Store old secondary habitats list
            ObservableCollection<SecondaryHabitat> oldSecondaryHabs = _incidSecondaryHabitats;

            // Remove any existing handlers before assigning a new collection.
            if (_incidSecondaryHabitats != null)
                _incidSecondaryHabitats.CollectionChanged -= _incidSecondaryHabitats_CollectionChanged;

            // If there are any existing rows add the new row the collection
            // and then sort them.
            if (_incidSecondaryHabitats != null)
                _incidSecondaryHabitats.Add(new SecondaryHabitat(bulkUpdateMode, -1, Incid, secondary_habitat, secondary_group));
            else
            {
                // Otherwise there can't be any secondary habitat rows so
                // just create a new collection and add the new row.
                _incidSecondaryHabitats = [new SecondaryHabitat(bulkUpdateMode, -1, Incid, secondary_habitat, secondary_group)];
            }

            // Track any changes to the user rows collection.
            _incidSecondaryHabitats.CollectionChanged += _incidSecondaryHabitats_CollectionChanged;

            // Set the new list of secondary habitat rows for the class.
            SecondaryHabitat.SecondaryHabitatList = _incidSecondaryHabitats;

            // Track when the secondary habitat records have changed so that the apply
            // button will appear.
            Changed = true;

            return (_incidSecondaryHabitats == null || (oldSecondaryHabs != null && _incidSecondaryHabitats != oldSecondaryHabs));
        }

        /// <summary>
        /// Refresh the secondary habitat table.
        /// </summary>
        public void RefreshSecondaryHabitats()
        {
            // If there are any existing rows then (re)sort them.
            if (_incidSecondaryHabitats != null)
            {
                // Remove any existing handlers before assigning a new collection.
                _incidSecondaryHabitats.CollectionChanged -= _incidSecondaryHabitats_CollectionChanged;

                // Order the secondary codes as required
                _incidSecondaryHabitats = _secondaryCodeOrder switch
                {
                    "As entered" => new ObservableCollection<SecondaryHabitat>(
                            _incidSecondaryHabitats.OrderBy(r => r.secondary_id)),
                    "By group then code" => new ObservableCollection<SecondaryHabitat>(
                            _incidSecondaryHabitats.OrderBy(r => r.secondary_group).ThenBy(r => r.secondary_habitat_int)),
                    "By code" => new ObservableCollection<SecondaryHabitat>(
                            _incidSecondaryHabitats.OrderBy(r => r.secondary_habitat_int)),
                    _ => new ObservableCollection<SecondaryHabitat>(
                            _incidSecondaryHabitats.OrderBy(r => r.secondary_id))
                };

                // Track any changes to the user rows collection.
                _incidSecondaryHabitats.CollectionChanged += _incidSecondaryHabitats_CollectionChanged;

                // Check if there are any errors in the secondary habitat records to see
                // if the Habitats tab label should be flagged as also in error.
                if (_incidSecondaryHabitats.Count > 0)
                {
                    int countInvalid = _incidSecondaryHabitats.Count(sh => !sh.IsValid());
                    if (countInvalid > 0)
                        AddToErrorList(_habitatErrors, "SecondaryHabitat");
                    else
                        RemoveFromErrorList(_habitatErrors, "SecondaryHabitat");
                }
                else
                    RemoveFromErrorList(_habitatErrors, "SecondaryHabitat");

                OnPropertyChanged(nameof(IncidSecondaryHabitats));
                OnPropertyChanged(nameof(HabitatSecondariesMandatory));
                OnPropertyChanged(nameof(HabitatTabLabel));
            }
        }

        private void _incidSecondaryHabitats_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Error));

            // Track when the secondary habitat records have changed so that the apply
            // button will appear.
            Changed = true;

            // Check if there are any errors in the secondary habitat records to see
            // if the Habitats tab label should be flagged as also in error.
            if (_incidSecondaryHabitats != null && _incidSecondaryHabitats.Count > 0)
            {
                int countInvalid = _incidSecondaryHabitats.Count(sh => !sh.IsValid());
                if (countInvalid > 0)
                    AddToErrorList(_habitatErrors, "SecondaryHabitat");
                else
                    RemoveFromErrorList(_habitatErrors, "SecondaryHabitat");
            }
            else
            {
                RemoveFromErrorList(_habitatErrors, "SecondaryHabitat");
            }

            // Update the list of secondary habitat rows for the class.
            SecondaryHabitat.SecondaryHabitatList = _incidSecondaryHabitats;

            // Refresh secondary table and summary.
            RefreshSecondaryHabitats();
            OnPropertyChanged(nameof(IncidSecondarySummary));
            OnPropertyChanged(nameof(HabitatSecondariesMandatory));

            // Refresh the BAP habitat environments (in case secondary codes
            // are, or should be, reflected).
            GetBapEnvironments();
            OnPropertyChanged(nameof(IncidBapHabitatsAuto));
            OnPropertyChanged(nameof(IncidBapHabitatsUser));
            OnPropertyChanged(nameof(BapHabitatsAutoEnabled));
            OnPropertyChanged(nameof(BapHabitatsUserEnabled));

            OnPropertyChanged(nameof(HabitatTabLabel));
        }

        #endregion Habitat Logic

        #region Priority Habitat Logic

        /// <summary>
        /// Gets all of the automatically assigned and user assigned bap environments.
        /// </summary>
        public void GetBapEnvironments()
        {
            // Remove any existing handlers before assigning a new collection.
            if (_incidBapRowsAuto != null)
                _incidBapRowsAuto.CollectionChanged -= _incidBapRowsAuto_CollectionChanged;
            if (_incidBapRowsUser != null)
                _incidBapRowsUser.CollectionChanged -= _incidBapRowsUser_CollectionChanged;

            IEnumerable<string> mandatoryBap = null;
            IEnumerable<HluDataSet.incid_bapRow> incidBapRowsUndel = null;
            if (IncidPrimary != null)
            {
                // Identify which primary BAP rows there should be from the
                // primary and secondary codes.
                mandatoryBap = MandatoryBapEnvironments(IncidPrimary, IncidSecondaryHabitats);

                // Identify any BAP rows (both auto generated and user added) that
                // have not been marked as deleted.
                incidBapRowsUndel = _incidBapRows.Where(r => r.RowState != DataRowState.Deleted);
            }

            // If there are any undeleted rows and the IHS codes indicate
            // that there should be some primary BAP (auto) rows then sort out
            // which of the undeleted rows are the auto rows.
            if ((incidBapRowsUndel != null) && (mandatoryBap != null))
            {
                // primary BAP environments from DB (real bap_id) and new (bap_id = -1)
                IEnumerable<BapEnvironment> prevBapRowsAuto = null;
                IEnumerable<BapEnvironment> newBapRowsAuto = null;
                if (incidBapRowsUndel == null)
                {
                    prevBapRowsAuto = Array.Empty<BapEnvironment>().AsEnumerable();
                    newBapRowsAuto = Array.Empty<BapEnvironment>().AsEnumerable();
                }
                else
                {
                    // Which of the undeleted rows are auto rows that
                    // already existed.
                    prevBapRowsAuto = from r in incidBapRowsUndel
                                      join pot in mandatoryBap on r.bap_habitat equals pot
                                      where _incidCurrentRow.incid != null && r.incid == _incidCurrentRow.incid
                                      select new BapEnvironment(false, false, r);

                    // Which of the undeleted rows were previously user
                    // added rows but should now be promoted to auto
                    // rows as a result of changes to the IHS codes.
                    newBapRowsAuto = from r in incidBapRowsUndel
                                     join pot in mandatoryBap on r.bap_habitat equals pot
                                     where !prevBapRowsAuto.Any(p => p.bap_habitat == r.bap_habitat)
                                     select new BapEnvironment(false, false, r);
                }

                // Determine if there are any potential BAP rows that should
                // be added as a result of changes to the IHS codes.
                var potBap = from p in mandatoryBap
                             where !prevBapRowsAuto.Any(a => a.bap_habitat == p)
                             where !incidBapRowsUndel.Any(row => row.bap_habitat == p)
                             select new BapEnvironment(false, false, -1, Incid, p, null, null, null);

                // Remove any existing handlers before assigning a new collection.
                if (_incidBapRowsAuto != null)
                {
                    foreach (BapEnvironment be in _incidBapRowsAuto)
                    {
                        be.DataChanged -= _incidBapRowsUser_DataChanged;
                    }
                }

                // Concatenate the previous auto rows, the newly promoted auto
                // rows and the potential BAP rows.
                _incidBapRowsAuto = new ObservableCollection<BapEnvironment>(
                    prevBapRowsAuto.Concat(newBapRowsAuto).Concat(potBap));
            }
            else if (incidBapRowsUndel != null)
            {
                // Remove any existing handlers before assigning a new collection.
                if (_incidBapRowsAuto != null)
                {
                    foreach (BapEnvironment be in _incidBapRowsAuto)
                    {
                        be.DataChanged -= _incidBapRowsUser_DataChanged;
                    }
                }

                // As there should be no primary BAP rows according to the
                // IHS codes then the auto rows should be blank (because any
                // undeleted rows must therefore now be considered as user rows.
                _incidBapRowsAuto = [];
            }
            else if ((mandatoryBap != null) && (mandatoryBap.Any()))
            {
                // Remove any existing handlers before assigning a new collection.
                if (_incidBapRowsAuto != null)
                {
                    foreach (BapEnvironment be in _incidBapRowsAuto)
                    {
                        be.DataChanged -= _incidBapRowsUser_DataChanged;
                    }
                }

                // If there should be some primary BAP rows according to the
                // IHS codes, but there are no existing undeleted rows, then
                // all the primrary BAP codes must become new auto rows.
                _incidBapRowsAuto = new ObservableCollection<BapEnvironment>(
                    mandatoryBap.Select(p => new BapEnvironment(false, false, -1, Incid, p, null, null, null)));
            }
            else
            {
                // Remove any existing handlers before assigning a new collection.
                if (_incidBapRowsAuto != null)
                {
                    foreach (BapEnvironment be in _incidBapRowsAuto)
                    {
                        be.DataChanged -= _incidBapRowsUser_DataChanged;
                    }
                }

                // There shouldn't be any primary BAP rows according to the IHS
                // codes, and there are no existing undeleted rows, so there are
                // no auto rows.
                _incidBapRowsAuto = [];
            }

            // Track any changes to the auto rows collection.
            _incidBapRowsAuto.CollectionChanged += _incidBapRowsAuto_CollectionChanged;

            // Track when the auto data has been changed so that the apply button
            // will appear.
            foreach (BapEnvironment be in _incidBapRowsAuto)
            {
                be.DataChanged += _incidBapRowsAuto_DataChanged;
            }
            ;

            // Check if there are any errors in the auto BAP records to see
            // if the Priority tab label should be flagged as also in error.
            if (_incidBapRowsAuto != null && _incidBapRowsAuto.Count > 0)
            {
                int countInvalid = _incidBapRowsAuto.Count(be => !be.IsValid());
                if (countInvalid > 0)
                    AddToErrorList(_priorityErrors, "BapAuto");
                else
                    RemoveFromErrorList(_priorityErrors, "BapAuto");
            }
            else
                RemoveFromErrorList(_priorityErrors, "BapAuto");

            OnPropertyChanged(nameof(IncidBapHabitatsAuto));

            // If there are undeleted rows and there are some auto rows
            // then sort them out to determine which of the undeleted rows
            // are considered as user added.
            if ((incidBapRowsUndel != null) && (_incidBapRowsAuto != null))
            {
                List<BapEnvironment> prevBapRowsUser = null;
                // If there were no user added rows before then there
                // are no previous user added rows.
                if (_incidBapRowsUser == null)
                {
                    prevBapRowsUser = [];
                }
                else
                {
                    // If there were user added rows before then determine
                    // which of them have not been promoted to auto rows.
                    prevBapRowsUser = (from r in _incidBapRowsUser
                                       where _incidCurrentRow.incid != null && r.incid == _incidCurrentRow.incid
                                       where !_incidBapRowsAuto.Any(row => row.bap_habitat == r.bap_habitat)
                                       select r).ToList();
                    prevBapRowsUser.ForEach(delegate (BapEnvironment be)
                    {
                        // Don't overwrite the determination quality value loaded from the
                        // database with 'Not present but close to definition' as other
                        // values may be valid and will be validated later.
                        //
                        //be.quality_determination = BapEnvironment.BAPDetQltyUserAdded;
                        be.BulkUpdateMode = IsBulkMode;
                    });
                }

                // Remove any existing handlers before assigning a new collection.
                if (_incidBapRowsUser != null)
                {
                    foreach (BapEnvironment be in _incidBapRowsUser)
                    {
                        be.DataChanged -= _incidBapRowsUser_DataChanged;
                    }
                }

                // Concatenate the previous user added rows with any remaining
                // undeleted rows that are not auto rows.
                _incidBapRowsUser = new ObservableCollection<BapEnvironment>(prevBapRowsUser.Concat(
                    from r in incidBapRowsUndel
                    where !_incidBapRowsAuto.Any(a => a.bap_habitat == r.bap_habitat)
                    where !prevBapRowsUser.Any(p => p.bap_habitat == r.bap_habitat)
                    select new BapEnvironment(IsBulkMode, true, r)));
            }
            // If thereare undeleted rows but no auto rows then all the
            // undeleted rows must be considered user added rows.
            else if (incidBapRowsUndel != null)
            {
                // Remove any existing handlers before assigning a new collection.
                if (_incidBapRowsUser != null)
                {
                    foreach (BapEnvironment be in _incidBapRowsUser)
                    {
                        be.DataChanged -= _incidBapRowsUser_DataChanged;
                    }
                }

                _incidBapRowsUser = new ObservableCollection<BapEnvironment>(
                   incidBapRowsUndel.Select(r => new BapEnvironment(IsBulkMode, true, r)));
            }
            else
            {
                // Remove any existing handlers before assigning a new collection.
                if (_incidBapRowsUser != null)
                {
                    foreach (BapEnvironment be in _incidBapRowsUser)
                    {
                        be.DataChanged -= _incidBapRowsUser_DataChanged;
                    }
                }

                // Otherwise there can't be any user added rows.
                _incidBapRowsUser = [];
            }

            // Track any changes to the user rows collection.
            _incidBapRowsUser.CollectionChanged += _incidBapRowsUser_CollectionChanged;

            // Track when the user data has been changed so that the apply button
            // will appear.
            foreach (BapEnvironment be in _incidBapRowsUser)
            {
                be.DataChanged += _incidBapRowsUser_DataChanged;
            }
            ;

            // Check if there are any errors in the user BAP records to see
            // if the Priority tab label should be flagged as also in error.
            if (_incidBapRowsUser != null && _incidBapRowsUser.Count > 0)
            {
                int countInvalid = _incidBapRowsUser.Count(be => !be.IsValid());
                if (countInvalid > 0)
                    AddToErrorList(_priorityErrors, "BapUser");
                else
                    RemoveFromErrorList(_priorityErrors, "BapUser");

                // Check if there are any duplicates between the primary and
                // secondary BAP records.
                if (_incidBapRowsAuto != null && _incidBapRowsAuto.Count > 0)
                {
                    List<string> beDups = (from be in _incidBapRowsAuto.Concat(_incidBapRowsUser)
                                           group be by be.bap_habitat into g
                                           where g.Count() > 1
                                           select g.Key).ToList();

                    if (beDups.Count > 2)
                        AddToErrorList(_priorityErrors, "BapUserDup");
                    else
                        RemoveFromErrorList(_priorityErrors, "BapUserDup");
                }
            }
            else
                RemoveFromErrorList(_priorityErrors, "BapUser");

            OnPropertyChanged(nameof(IncidBapHabitatsUser));

            // Concatenate the auto rows and the user rows to become the new list
            // of BAP rows.
            BapEnvironment.BapEnvironmentList = _incidBapRowsAuto.Concat(_incidBapRowsUser);

            OnPropertyChanged(nameof(PriorityTabLabel));
        }

        /// <summary>
        /// Track when the BAP primary records have changed so that the apply
        /// button will appear.
        /// </summary>
        private void _incidBapRowsAuto_DataChanged(bool BapChanged)
        {
            Changed = true;

            // Check if there are any errors in the primary BAP records to see
            // if the Priority tab label should be flagged as also in error.
            if (_incidBapRowsAuto != null && _incidBapRowsAuto.Count > 0)
            {
                int countInvalid = _incidBapRowsAuto.Count(be => !be.IsValid());
                if (countInvalid > 0)
                    AddToErrorList(_priorityErrors, "BapAuto");
                else
                    RemoveFromErrorList(_priorityErrors, "BapAuto");
            }
            OnPropertyChanged(nameof(PriorityTabLabel));
        }

        /// <summary>
        /// Track when the BAP secondary records have changed so that the apply
        /// button will appear.
        /// </summary>
        private void _incidBapRowsUser_DataChanged(bool BapChanged)
        {
            Changed = true;

            // Check if there are any errors in the secondary BAP records to see
            // if the Priority tab label should be flagged as also in error.
            if (_incidBapRowsUser != null && _incidBapRowsUser.Count > 0)
            {
                int countInvalid = _incidBapRowsUser.Count(be => !be.IsValid());
                if (countInvalid > 0)
                    AddToErrorList(_priorityErrors, "BapUser");
                else
                    RemoveFromErrorList(_priorityErrors, "BapUser");

                // Check if there are any duplicates between the primary and
                // secondary BAP records.
                if (_incidBapRowsAuto != null && _incidBapRowsAuto.Count > 0)
                {
                    List<string> beDups = (from be in _incidBapRowsAuto.Concat(_incidBapRowsUser)
                                           group be by be.bap_habitat into g
                                           where g.Count() > 1
                                           select g.Key).ToList();

                    if (beDups.Count > 2)
                        AddToErrorList(_priorityErrors, "BapUserDup");
                    else
                        RemoveFromErrorList(_priorityErrors, "BapUserDup");
                }
            }
            OnPropertyChanged(nameof(PriorityTabLabel));
        }

        /// <summary>
        /// Build a enumerable of the mandatory bap habitats
        /// based on the primary habitat and all the secondary habitats.
        /// </summary>
        /// <param name="primaryHabitat">The primary habitat.</param>
        /// <param name="secondaryHabitats">The secondary habitats.</param>
        /// <returns></returns>
        internal IEnumerable<string> MandatoryBapEnvironments(string primaryHabitat, ObservableCollection<SecondaryHabitat> secondaryHabitats)
        {
            IEnumerable<string> primaryBap = null;
            IEnumerable<string> secondaryBap = null;
            string[] q = null;

            // Get the BAP habitats associated with the primary habitat
            if (!String.IsNullOrEmpty(primaryHabitat))
            {
                try
                {
                    // Enable multiple priority habitat types (from the same or different
                    // classifications) to be assigned
                    q = (from pb in _lutPrimaryBapHabitat
                         where pb.code_primary == primaryHabitat
                         select pb.bap_habitat).ToArray();

                    // If any primary bap habitats have been found
                    primaryBap = null;
                    if ((q != null) && (q.Length != 0))
                        primaryBap = q;
                }
                catch { }
            }

            // Get the BAP habitats associated with all of the secondary habitats
            if (secondaryHabitats != null)
            {
                try
                {
                    q = (from sb in _lutSecondaryBapHabitat
                         join s in secondaryHabitats on sb.code_secondary equals s.secondary_habitat
                         select sb.bap_habitat).ToArray();

                    // If any secondary bap habitats have been found
                    secondaryBap = null;
                    if ((q != null) && (q.Length != 0))
                        secondaryBap = q;
                }
                catch { }
            }

            IEnumerable<string> allBap = null;
            allBap = primaryBap != null ? secondaryBap != null ? primaryBap.Concat(secondaryBap) : primaryBap : secondaryBap;
            if (allBap != null)
                return allBap.Distinct();
            else
                return [];
        }

        /// <summary>
        /// Track when the BAP primary records have changed so that the apply
        /// button will appear.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _incidBapRowsAuto_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Error));

            // Track when the BAP primary records have changed so that the apply
            // button will appear.
            Changed = true;

            // Check if there are any errors in the primary BAP records to see
            // if the Priority tab label should be flagged as also in error.
            if (_incidBapRowsAuto != null && _incidBapRowsAuto.Count > 0)
            {
                int countInvalid = _incidBapRowsAuto.Count(be => !be.IsValid());
                if (countInvalid > 0)
                    AddToErrorList(_priorityErrors, "BapAuto");
                else
                    RemoveFromErrorList(_priorityErrors, "BapAuto");
            }
            OnPropertyChanged(nameof(PriorityTabLabel));
        }

        /// <summary>
        /// Track when the BAP secondary records have changed so that the apply
        /// button will appear.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _incidBapRowsUser_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Error));

            // Flag that the current record has changed so that the apply button will appear.
            Changed = true;

            // Check if there are any errors in the secondary BAP records to see
            // if the Priority tab label should be flagged as also in error.
            if (_incidBapRowsUser != null && _incidBapRowsUser.Count > 0)
            {
                int countInvalid = _incidBapRowsUser.Count(be => !be.IsValid());
                if (countInvalid > 0)
                    AddToErrorList(_priorityErrors, "BapUser");
                else
                    RemoveFromErrorList(_priorityErrors, "BapUser");

                // Check if there are any duplicates between the primary and
                // secondary BAP records.
                if (_incidBapRowsAuto != null && _incidBapRowsAuto.Count > 0)
                {
                    List<string> beDups = (from be in _incidBapRowsAuto.Concat(_incidBapRowsUser)
                                           group be by be.bap_habitat into g
                                           where g.Count() > 1
                                           select g.Key).ToList();

                    if (beDups.Count > 2)
                        AddToErrorList(_priorityErrors, "BapUserDup");
                    else
                        RemoveFromErrorList(_priorityErrors, "BapUserDup");
                }
            }
            else
            {
                RemoveFromErrorList(_priorityErrors, "BapUser");
            }

            OnPropertyChanged(nameof(PriorityTabLabel));

            // Track when the BAP secondary records have changed so that the apply button will appear.
            foreach (BapEnvironment be in _incidBapRowsUser)
            {
                if (be == null)
                    be.DataChanged -= _incidBapRowsUser_DataChanged;
                else if (be.bap_id == -1)
                    be.DataChanged += _incidBapRowsUser_DataChanged;
            }
        }

        #endregion Priority Habitat Logic

        #region Condition Logic

        /// <summary>
        /// Check if there are any valid condition rows.
        /// </summary>
        /// <returns></returns>
        private bool CheckCondition()
        {
            if (_hluDS == null) return false;

            if (IsBulkMode) return true;

            if (_incidConditionRows == null)
            {
                HluDataSet.incid_conditionDataTable incidConditionTable = _hluDS.incid_condition;
                _incidConditionRows = GetIncidChildRowsDb([Incid],
                    _hluTableAdapterMgr.incid_conditionTableAdapter, ref incidConditionTable);
            }

            return _incidConditionRows != null;
        }

        /// <summary>
        /// Updates the incid condition row.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rowNumber">The row number.</param>
        /// <param name="columnOrdinal">The column ordinal.</param>
        /// <param name="newValue">The new value.</param>
        private void UpdateIncidConditionRow<T>(int rowNumber, int columnOrdinal, T newValue)
        {
            try
            {
                if (_incidConditionRows == null) return;

                // If the row is blank
                if (_incidConditionRows[rowNumber] == null)
                {
                    if ((columnOrdinal == HluDataset.incid_condition.conditionColumn.Ordinal) && newValue != null)
                    {
                        // Set the row id
                        HluDataSet.incid_conditionRow newRow = IncidConditionTable.Newincid_conditionRow();
                        newRow.incid_condition_id = NextIncidConditionId;
                        if (IsNotBulkMode)
                            newRow.incid = IncidCurrentRow.incid;
                        _incidConditionRows[rowNumber] = newRow;
                    }
                    else
                    {
                        return;
                    }
                }
                // If the new condition is null
                else if ((columnOrdinal == HluDataset.incid_condition.conditionColumn.Ordinal) && (newValue == null))
                {
                    // Delete the row unless during a bulk update
                    if (IsNotBulkMode)
                    {
                        if (_incidConditionRows[rowNumber].RowState != DataRowState.Detached)
                            _incidConditionRows[rowNumber].Delete();
                        _incidConditionRows[rowNumber] = null;
                    }
                    else
                    {
                        _incidConditionRows[rowNumber] = IncidConditionTable.Newincid_conditionRow();
                        IncidConditionRows[rowNumber].incid_condition_id = rowNumber;
                        IncidConditionRows[rowNumber].condition = null;
                        IncidConditionRows[rowNumber].incid = RecIDs.CurrentIncid;
                    }
                    return;
                }

                // Update the date columns
                if ((columnOrdinal == HluDataset.incid_condition.condition_date_startColumn.Ordinal) ||
                    (columnOrdinal == HluDataset.incid_condition.condition_date_endColumn.Ordinal))
                {
                    if (newValue is Date.VagueDateInstance vd)
                    {
                        _incidConditionRows[rowNumber].condition_date_start = vd.StartDate;
                        _incidConditionRows[rowNumber].condition_date_end = vd.EndDate;
                        _incidConditionRows[rowNumber].condition_date_type = vd.DateType;
                    }
                    else
                    {
                        _incidConditionRows[rowNumber].condition_date_start = VagueDate.DateUnknown;
                        _incidConditionRows[rowNumber].condition_date_end = VagueDate.DateUnknown;
                        _incidConditionRows[rowNumber].condition_date_type = null;
                    }
                }
                // Update all other columns if they have changed
                else if ((((_incidConditionRows[rowNumber].IsNull(columnOrdinal) ^ (newValue == null)) ||
                    ((!_incidConditionRows[rowNumber].IsNull(columnOrdinal) && (newValue != null)))) &&
                    !_incidConditionRows[rowNumber][columnOrdinal].Equals(newValue)))
                {
                    _incidConditionRows[rowNumber][columnOrdinal] = newValue;
                }

                if ((_incidConditionRows[rowNumber].RowState == DataRowState.Detached) &&
                    IsCompleteRow(_incidConditionRows[rowNumber]))
                {
                    IncidConditionTable.Addincid_conditionRow(_incidConditionRows[rowNumber]);
                }
            }
            catch { }
        }

        #endregion Condition Logic

        #region Sources Logic

        /// <summary>
        /// Checks if there are any valid source rows.
        /// </summary>
        /// <returns></returns>
        private bool CheckSources()
        {
            if (_hluDS == null) return false;

            if (IsBulkMode) return true;

            if (_incidSourcesRows == null)
            {
                HluDataSet.incid_sourcesDataTable incidSourcesTable = _hluDS.incid_sources;
                _incidSourcesRows = GetIncidChildRowsDb([Incid],
                    _hluTableAdapterMgr.incid_sourcesTableAdapter, ref incidSourcesTable);
            }
            return _incidSourcesRows != null;
        }

        /// <summary>
        /// Returns the default date for a given source.
        /// </summary>
        /// <param name="currentDate">The current date.</param>
        /// <param name="sourceID">The source identifier.</param>
        /// <returns></returns>
        public Date.VagueDateInstance DefaultSourceDate(Date.VagueDateInstance currentDate, Nullable<int> sourceID)
        {
            if ((HluDataset == null) || (HluDataset.lut_sources == null)) return currentDate;

            EnumerableRowCollection<HluDataSet.lut_sourcesRow> rows =
                HluDataset.lut_sources.Where(r => r.source_id == sourceID &&
                    !r.IsNull(HluDataset.lut_sources.source_date_defaultColumn));

            if (rows.Any())
            {
                string defaultDate;
                string dateType = VagueDate.GetType(rows.ElementAt(0).source_date_default, out defaultDate);
                int startDate = VagueDate.ToTimeSpanDays(defaultDate, dateType, VagueDate.DateType.Start);
                int endDate = VagueDate.ToTimeSpanDays(defaultDate, dateType, VagueDate.DateType.End);
                return new Date.VagueDateInstance(startDate, endDate, dateType);
            }

            return currentDate;
        }

        /// <summary>
        /// Updates the incid sources row.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="rowNumber">The row number.</param>
        /// <param name="columnOrdinal">The column ordinal.</param>
        /// <param name="newValue">The new value.</param>
        private void UpdateIncidSourcesRow<T>(int rowNumber, int columnOrdinal, T newValue)
        {
            try
            {
                if (_incidSourcesRows == null) return;

                // If the row is blank
                if (_incidSourcesRows[rowNumber] == null)
                {
                    if (columnOrdinal == HluDataset.incid_sources.source_idColumn.Ordinal)
                    {
                        // Set the row id
                        HluDataSet.incid_sourcesRow newRow = IncidSourcesTable.Newincid_sourcesRow();
                        newRow.incid_source_id = NextIncidSourcesId;
                        newRow.incid = IncidCurrentRow.incid;
                        newRow.sort_order = rowNumber + 1;
                        _incidSourcesRows[rowNumber] = newRow;
                    }
                    else
                    {
                        return;
                    }
                }
                // If the new source_id is null
                else if ((columnOrdinal == HluDataset.incid_sources.source_idColumn.Ordinal) && (newValue == null))
                {
                    // Delete the row unless during a bulk update
                    if (IsNotBulkMode)
                    {
                        if (_incidSourcesRows[rowNumber].RowState != DataRowState.Detached)
                            _incidSourcesRows[rowNumber].Delete();
                        _incidSourcesRows[rowNumber] = null;
                    }
                    else
                    {
                        _incidSourcesRows[rowNumber] = IncidSourcesTable.Newincid_sourcesRow();
                        IncidSourcesRows[rowNumber].incid_source_id = rowNumber;
                        IncidSourcesRows[rowNumber].source_id = Int32.MinValue;
                        IncidSourcesRows[rowNumber].incid = RecIDs.CurrentIncid;
                    }
                    return;
                }

                // Update the date columns
                if ((columnOrdinal == HluDataset.incid_sources.source_date_startColumn.Ordinal) ||
                    (columnOrdinal == HluDataset.incid_sources.source_date_endColumn.Ordinal))
                {
                    if (newValue is Date.VagueDateInstance vd)
                    {
                        _incidSourcesRows[rowNumber].source_date_start = vd.StartDate;
                        _incidSourcesRows[rowNumber].source_date_end = vd.EndDate;
                        _incidSourcesRows[rowNumber].source_date_type = vd.DateType;
                    }
                    else
                    {
                        _incidSourcesRows[rowNumber].source_date_start = VagueDate.DateUnknown;
                        _incidSourcesRows[rowNumber].source_date_end = VagueDate.DateUnknown;
                        _incidSourcesRows[rowNumber].source_date_type = null;
                    }
                }
                // Update all other columns if they have changed
                else if ((((_incidSourcesRows[rowNumber].IsNull(columnOrdinal) ^ (newValue == null)) ||
                    ((!_incidSourcesRows[rowNumber].IsNull(columnOrdinal) && (newValue != null)))) &&
                    !_incidSourcesRows[rowNumber][columnOrdinal].Equals(newValue)))
                {
                    _incidSourcesRows[rowNumber][columnOrdinal] = newValue;
                }

                // If updating the source_id get the default date
                if (columnOrdinal == HluDataset.incid_sources.source_idColumn.Ordinal)
                {
                    try
                    {
                        HluDataSet.lut_sourcesRow lutRow =
                            HluDataset.lut_sources.Single(r => r.source_id == _incidSourcesRows[rowNumber].source_id);
                        if (!String.IsNullOrEmpty(lutRow.source_date_default))
                        {
                            string defaultDateString;
                            string formatString = VagueDate.GetType(lutRow.source_date_default, out defaultDateString);
                            int defaultStartDate = VagueDate.ToTimeSpanDays(defaultDateString,
                                formatString, VagueDate.DateType.Start);
                            int defaultEndDate = VagueDate.ToTimeSpanDays(defaultDateString,
                                formatString, VagueDate.DateType.End);
                            _incidSourcesRows[rowNumber].source_date_start = defaultStartDate;
                            _incidSourcesRows[rowNumber].source_date_end = defaultEndDate;
                        }
                    }
                    catch { }
                }

                if ((_incidSourcesRows[rowNumber].RowState == DataRowState.Detached) &&
                    IsCompleteRow(_incidSourcesRows[rowNumber]))
                {
                    _incidSourcesRows[rowNumber].sort_order = rowNumber + 1;
                    IncidSourcesTable.Addincid_sourcesRow(_incidSourcesRows[rowNumber]);
                }
            }
            catch { }
        }

        #endregion Sources Logic

        #region Incid MM Polygons Operations

        /// <summary>
        /// Populates the specified incid_mm_polygons data table with rows that match the provided filter conditions.
        /// </summary>
        /// <remarks>If the whereClause parameter is null or empty, the method does not modify the table.
        /// The method initializes the table adapter if it has not been created.</remarks>
        /// <param name="whereClause">A list of filter conditions used to select rows from the incid_mm_polygons table. Must not be null or empty
        /// to perform the operation.</param>
        /// <param name="table">A reference to the incid_mm_polygons data table to be filled with the filtered rows.</param>
        internal void GetIncidMMPolygonRows(List<List<SqlFilterCondition>> whereClause,
            ref HluDataSet.incid_mm_polygonsDataTable table)
        {
            if ((whereClause != null) && (whereClause.Count > 0))
            {
                if (_hluTableAdapterMgr.incid_mm_polygonsTableAdapter == null)
                    _hluTableAdapterMgr.incid_mm_polygonsTableAdapter =
                        new HluTableAdapter<HluDataSet.incid_mm_polygonsDataTable,
                            HluDataSet.incid_mm_polygonsRow>(_db);

                _hluTableAdapterMgr.incid_mm_polygonsTableAdapter.Fill(table, whereClause);
            }
        }

        /// <summary>
        /// Populates the specified incid_osmm_updatesDataTable with rows that match the provided filter conditions.
        /// </summary>
        /// <remarks>If no filter conditions are provided, the method does not modify the data table. The
        /// method only operates when at least one filter group is specified.</remarks>
        /// <param name="whereClause">A collection of filter conditions used to select which rows are retrieved. Each inner list represents a
        /// group of conditions combined with logical AND; multiple groups are combined with logical OR. Cannot be null
        /// or empty.</param>
        /// <param name="table">A reference to the incid_osmm_updatesDataTable that will be filled with the matching rows. Must be a valid,
        /// initialized data table.</param>
        internal void GetIncidOSMMUpdatesRows(List<List<SqlFilterCondition>> whereClause,
            ref HluDataSet.incid_osmm_updatesDataTable table)
        {
            if ((whereClause != null) && (whereClause.Count > 0))
            {
                if (_hluTableAdapterMgr.incid_osmm_updatesTableAdapter == null)
                    _hluTableAdapterMgr.incid_osmm_updatesTableAdapter =
                        new HluTableAdapter<HluDataSet.incid_osmm_updatesDataTable,
                            HluDataSet.incid_osmm_updatesRow>(_db);

                _hluTableAdapterMgr.incid_osmm_updatesTableAdapter.Fill(table, whereClause);
            }
        }

        #endregion Incid MM Polygons Operations

        #region OSMM Updates Operation

        /// <summary>
        /// Queries the database OSMM updates <see cref="AnyOSMMUpdates"/>.
        /// <paramref name="cancellationToken"/>
        /// </summary>
        internal async Task CheckAnyOSMMUpdatesAsync(CancellationToken cancellationToken = default)
        {
            // Count the number of OSMM updates.
            int rowCount = await CountOSMMUpdatesAsync(cancellationToken).ConfigureAwait(false);

            // Set the property to true if there are any, and false otherwise.
            _anyOSMMUpdates = rowCount > 0;
            OnPropertyChanged(nameof(AnyOSMMUpdates));
        }

        /// <summary>
        /// Counts incid OSMM updates in the database
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        internal async Task<int> CountOSMMUpdatesAsync(CancellationToken cancellationToken = default)
        {
            object result = await _db.ExecuteScalarAsync(String.Format(
                "SELECT COUNT(*) FROM {0}", _db.QualifyTableName(_hluDS.incid_osmm_updates.TableName)),
                _db.Connection.ConnectionTimeout,
                CommandType.Text,
                cancellationToken);

            // COUNT(*) can come back as int or long depending on provider.
            return Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }

        #endregion OSMM Updates Operation

        #region Filter by Incid Operation

        /// <summary>
        /// Select the required incid.
        /// </summary>
        /// <param name="queryIncid">The query incid.</param>
        public async Task FilterByIncidAsync(String queryIncid)
        {
            if (String.IsNullOrEmpty(queryIncid))
                return;

            try
            {
                ChangeCursor(Cursors.Wait, "Validating ...");

                //TODO: Needed?
                // Let WPF render the cursor/message before heavy work begins.
                //await Dispatcher.Yield(DispatcherPriority.Background);

                // Select only the incid database table to use in the query.
                List<DataTable> whereTables = [];
                whereTables.Add(IncidTable);

                // Replace any connection type specific qualifiers and delimiters.
                string newWhereClause = null;

                // Ensure predicted count of toids/fragment selected works with
                // any query.
                string sqlWhereClause = String.Format("[incid].incid = '{0}'", queryIncid);

                newWhereClause = ReplaceStringQualifiers(sqlWhereClause);

                // Create a selection DataTable of PK values of IncidTable.
                _incidSelection = _db.SqlSelect(true, IncidTable.PrimaryKey, whereTables, newWhereClause);

                // Get a list of all the incids in the selection.
                _incidsSelectedMap = _incidSelection.AsEnumerable()
                    .GroupBy(r => r.Field<string>(_incidSelection.Columns[0].ColumnName)).Select(g => g.Key).OrderBy(s => s);

                // Retrospectively set the where clause to match the list
                // of selected incids (for possible use later).
                _incidSelectionWhereClause = ViewModelWindowMainHelpers.IncidSelectionToWhereClause(
                    IncidPageSize, IncidTable.incidColumn.Ordinal, IncidTable, _incidsSelectedMap);

                // Backup the current selection (filter).
                DataTable incidSelectionBackup = _incidSelection;

                // If there are any records in the selection (and the tool is
                // not currently in bulk update mode).
                if (IsFiltered)
                {
                    // Find the expected number of features to be selected in GIS.
                    _selectedFragsInDBCount = await ExpectedSelectionFeatures(whereTables, newWhereClause);

                    // Store the number of incids found in the database
                    _selectedIncidsInDBCount = _incidSelection != null ? _incidSelection.Rows.Count : 0;

                    ChangeCursor(Cursors.Wait, "Filtering ...");

                    // Select the required incid(s) in GIS.
                    if (await PerformGisSelectionAsync(true, _selectedFragsInDBCount, _selectedIncidsInDBCount))
                    {
                        // Analyse the results of the GIS selection by counting the number of
                        // incids, toids and fragments selected.
                        AnalyzeGisSelectionSet(true);

                        // Indicate the selection didn't come from the map.
                        _filteredByMap = false;

                        // Set the filter back to the first incid.
                        await SetFilterAsync();

                        // Reset the cursor back to normal.
                        ChangeCursor(Cursors.Arrow, null);

                        // Check if the GIS and database are in sync.
                        if ((_currentIncidToidsInGISCount > _currentIncidToidsInDBCount) ||
                            (_currentIncidFragsInGISCount > _currentIncidFragsInDBCount))
                        {
                            if (_currentIncidFragsInGISCount == 1)
                                MessageBox.Show("Selected feature not found in database.", "HLU: Selection",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            else
                                MessageBox.Show("Not all selected features found in database.", "HLU: Selection",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        // Check if the counts returned are less than those expected.
                        else if (_currentIncidFragsInGISCount < _selectedFragsInDBCount)
                        {
                            MessageBox.Show("Not all selected features found in active layer.", "HLU: Selection",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    else
                    {
                        // Restore the previous selection (filter).
                        //_incidSelection = incidSelectionBackup;

                        // Clear the selection (filter).
                        _incidSelection = null;

                        // Indicate the selection didn't come from the map.
                        _filteredByMap = false;

                        // Set the filter back to the first incid.
                        await SetFilterAsync();

                        // Reset the cursor back to normal.
                        ChangeCursor(Cursors.Arrow, null);

                        // Warn the user that no records were found.
                        MessageBox.Show("Map feature not found in active layer.", "HLU: Apply Query",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    // Restore the previous selection (filter).
                    //_incidSelection = incidSelectionBackup;

                    // Clear the selection (filter).
                    _incidSelection = null;

                    // Indicate the selection didn't come from the map.
                    _filteredByMap = false;

                    // Set the filter back to the first incid.
                    await SetFilterAsync();

                    // Reset the cursor back to normal
                    ChangeCursor(Cursors.Arrow, null);

                    // Warn the user that the record was not found
                    MessageBox.Show("Record not found in database.", "HLU: Apply Query",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _incidSelection = null;
                ChangeCursor(Cursors.Arrow, null);
                MessageBox.Show(ex.Message, "HLU: Apply Query",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Refreshes the status-related properties and notifies listeners of property changes.
                RefreshStatus();
            }
        }

        #endregion Filter by Incid Operation

        #region OSMM Filter Operation

        /// <summary>
        /// Applies the OSMM updates filter.
        /// </summary>
        /// <param name="processFlag">The process flag.</param>
        /// <param name="spatialFlag">The spatial flag.</param>
        /// <param name="changeFlag">The change flag.</param>
        /// <param name="status">The status.</param>
        public async Task ApplyOSMMUpdatesFilterAsync(string processFlag, string spatialFlag, string changeFlag, string status)
        {
            try
            {
                ChangeCursor(Cursors.Wait, "Validating ...");

                //TODO: Needed?
                // Let WPF render the cursor/message before heavy work begins.
                //await Dispatcher.Yield(DispatcherPriority.Background);

                // Select only the incid_osmm_updates database table to use in the query.
                List<DataTable> whereTables = [];
                whereTables.Add(IncidOSMMUpdatesTable);

                // Always filter out applied updates
                string sqlWhereClause;
                sqlWhereClause = "[incid_osmm_updates].status <> -1";

                // Add any other filter criteria.
                if (processFlag != null && processFlag != _codeAnyRow)
                    sqlWhereClause = String.Format("{0} AND [incid_osmm_updates].process_flag = {1}", sqlWhereClause, processFlag);

                if (spatialFlag != null && spatialFlag != _codeAnyRow)
                    sqlWhereClause = String.Format("{0} AND [incid_osmm_updates].spatial_flag = '{1}'", sqlWhereClause, spatialFlag);

                if (changeFlag != null && changeFlag != _codeAnyRow)
                    sqlWhereClause = String.Format("{0} AND [incid_osmm_updates].change_flag = '{1}'", sqlWhereClause, changeFlag);

                if (status != null && status != _codeAnyRow)
                {
                    int newStatus = status switch
                    {
                        "Rejected" => -99,
                        "Ignored" => -2,
                        "Applied" => -1,
                        "Pending" => 0,
                        "Proposed" => 1,
                        _ => -999
                    };

                    if (newStatus == 1)
                        sqlWhereClause = String.Format("{0} AND [incid_osmm_updates].status > 0", sqlWhereClause);
                    else
                        sqlWhereClause = String.Format("{0} AND [incid_osmm_updates].status = {1}", sqlWhereClause, newStatus);
                }

                // Don't show pending or applied updates when no status filter is applied
                if (status == null || status == _codeAnyRow)
                    sqlWhereClause = String.Format("{0} AND [incid_osmm_updates].status <> 0  AND [incid_osmm_updates].status <> -1", sqlWhereClause);

                // Replace any connection type specific qualifiers and delimiters.
                string newWhereClause = null;
                newWhereClause = ReplaceStringQualifiers(sqlWhereClause);

                // Store the where clause for updating the OSMM updates later.
                _osmmUpdateWhereClause = newWhereClause;

                // Create a selection DataTable of PK values of IncidTable.
                _incidSelection = _db.SqlSelect(true, IncidTable.PrimaryKey, whereTables, newWhereClause);

                // Get a list of all the incids in the selection.
                _incidsSelectedMap = _incidSelection.AsEnumerable()
                    .GroupBy(r => r.Field<string>(_incidSelection.Columns[0].ColumnName)).Select(g => g.Key).OrderBy(s => s);

                // Retrospectively set the where clause to match the list
                // of selected incids (for possible use later).
                _incidSelectionWhereClause = ViewModelWindowMainHelpers.IncidSelectionToWhereClause(
                    IncidPageSize, IncidTable.incidColumn.Ordinal, IncidTable, _incidsSelectedMap);

                // Backup the current selection (filter).
                DataTable incidSelectionBackup = _incidSelection;

                // If there are any records in the selection (and the tool is
                // not currently in bulk update mode).
                if (IsFiltered)
                {
                    // Find the expected number of features to be selected in GIS.
                    _selectedFragsInDBCount = await ExpectedSelectionFeatures(whereTables, newWhereClause);

                    // Store the number of incids found in the database
                    _selectedIncidsInDBCount = _incidSelection != null ? _incidSelection.Rows.Count : 0;

                    ChangeCursor(Cursors.Wait, "Filtering ...");

                    // Select the required incid(s) in GIS.
                    if (await PerformGisSelectionAsync(true, _selectedFragsInDBCount, _selectedIncidsInDBCount))
                    {
                        // Analyse the results, set the filter and reset the cursor AFTER
                        // returning from performing the GIS selection so that other calls
                        // to the PerformGisSelection method can control if/when these things
                        // are done.
                        //
                        // Analyse the results of the GIS selection by counting the number of
                        // incids, toids and fragments selected.
                        AnalyzeGisSelectionSet(true);

                        // Indicate the selection didn't come from the map.
                        _filteredByMap = false;

                        if (IsNotOsmmBulkMode)
                        {
                            // Indicate there are more OSMM updates to review.
                            _osmmUpdatesEmpty = false;

                            // Set the filter to the first incid.
                            await SetFilterAsync();

                            OnPropertyChanged(nameof(CanOSMMAccept));
                            OnPropertyChanged(nameof(CanOSMMSkip));
                        }

                        // Refresh all the controls
                        RefreshAll();

                        // Reset the cursor back to normal.
                        ChangeCursor(Cursors.Arrow, null);
                    }
                    else
                    {
                        if (IsNotOsmmBulkMode)
                        {
                            // Clear the selection (filter).
                            _incidSelection = null;

                            // Indicate the selection didn't come from the map.
                            _filteredByMap = false;

                            // Indicate there are no more OSMM updates to review.
                            if (IsNotOsmmBulkMode)
                                _osmmUpdatesEmpty = true;

                            // Clear all the form fields (except the habitat class
                            // and habitat type).
                            ClearForm();

                            // Clear the map selection.
                            await _gisApp.ClearMapSelectionAsync();

                            // Reset the map counters
                            _selectedIncidsInGISCount = 0;
                            _selectedToidsInGISCount = 0;
                            _selectedFragsInGISCount = 0;

                            // Refresh all the controls
                            RefreshAll();
                        }

                        // Reset the cursor back to normal.
                        ChangeCursor(Cursors.Arrow, null);

                        // Warn the user that no records were found.
                        MessageBox.Show("No map features found in active layer.", "HLU: Apply Query",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    if (IsNotOsmmBulkMode)
                    {
                        // Clear the selection (filter).
                        _incidSelection = null;

                        // Indicate the selection didn't come from the map.
                        _filteredByMap = false;

                        // Indicate there are no more OSMM updates to review.
                        _osmmUpdatesEmpty = true;

                        // Clear all the form fields (except the habitat class
                        // and habitat type).
                        ClearForm();

                        // Clear the map selection.
                        await _gisApp.ClearMapSelectionAsync();

                        // Reset the map counters
                        _selectedIncidsInGISCount = 0;
                        _selectedToidsInGISCount = 0;
                        _selectedFragsInGISCount = 0;

                        // Refresh all the controls
                        RefreshAll();
                    }
                    else
                    {
                        // Restore the previous selection (filter).
                        _incidSelection = incidSelectionBackup;
                    }

                    // Reset the cursor back to normal.
                    ChangeCursor(Cursors.Arrow, null);

                    // Warn the user that no records were found.
                    MessageBox.Show("No records found in database.", "HLU: Apply Query",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _incidSelection = null;

                ChangeCursor(Cursors.Arrow, null);

                MessageBox.Show(ex.Message, "HLU: Apply Query",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Refreshes the status-related properties and notifies listeners of property changes.
                RefreshStatus();
            }
        }

        #endregion OSMM Filter Operation

        #region Clear Filter Operation

        /// <summary>
        /// Clears any active incid filter and optionally moves to the first incid in the index.
        /// </summary>
        /// <param name="resetRowIndex">If set to <c>true</c> the first incid in the index is loaded.</param>
        public async Task ClearFilterAsync(bool resetRowIndex)
        {
            // Reset the OSMM Updates filter when in OSMM Update mode.
            if (IsOsmmReviewMode)
            {
                await ApplyOSMMUpdatesFilterAsync(null, null, null, null);
                return;
            }
            else if (IsOsmmBulkMode)
            {
                await ApplyOSMMUpdatesFilterAsync(null, null, null, "Pending");
                return;
            }

            ChangeCursor(Cursors.Wait, "Clearing filter ...");

            _incidSelection = null;
            _incidSelectionWhereClause = null;
            _gisSelection = null;
            _selectedIncidsInDBCount = 0;
            _selectedFragsInDBCount = 0;
            _selectedIncidsInGISCount = 0;
            _selectedToidsInGISCount = 0;
            _selectedFragsInGISCount = 0;
            _incidPageRowNoMax = -1;

            // Only move to the first incid in the index if required, to save
            // changing the index here and then again immediately after from
            // the calling method.
            if (resetRowIndex)
            {
                // Show the wait cursor and processing message in the status area
                // whilst moving to the new Incid.
                //ChangeCursor(Cursors.Wait, "Processing ...");

                _incidCurrentRowIndex = 1;
                //IncidCurrentRowIndex = 1;

                //ChangeCursor(Cursors.Arrow, null);
            }

            // Suggest the selection came from the map so that
            // the map doesn't auto zoom to the first incid.
            _filteredByMap = true;

            // Re-retrieve the current record (which includes counting the number of
            // toids and fragments for the current incid selected in the GIS and
            // in the database).
            if (resetRowIndex)
                await MoveIncidCurrentRowIndexAsync(_incidCurrentRowIndex);
            else
                // Count the number of toids and fragments for the current incid
                // selected in the GIS and in the database.
                CountCurrentIncidToidFrags();

            // Indicate the selection didn't come from the map.
            _filteredByMap = false;

            // Refresh all the status type fields.
            RefreshStatus();

            // Reset the cursor back to normal.
            ChangeCursor(Cursors.Arrow, null);
        }

        #endregion Clear Filter Operation

        #region Selection Analysis

        /// <summary>
        /// Count how many incids, toids and fragments are selected in GIS.
        /// </summary>
        private void AnalyzeGisSelectionSet(bool updateIncidSelection)
        {
            _selectedIncidsInGISCount = 0;
            _selectedToidsInGISCount = 0;
            _selectedFragsInGISCount = 0;

            if ((_gisSelection != null) && (_gisSelection.Rows.Count > 0))
            {
                switch (_gisSelection.Columns.Count)
                {
                    case 3:
                        // Get the unique fragments selected in GIS.
                        _fragsSelectedMap = from r in _gisSelection.AsEnumerable()
                                            group r by new
                                            {
                                                incid = r.Field<string>(0),
                                                toid = r.Field<string>(1),
                                                fragment = r.Field<string>(2)
                                            }
                                                into g
                                            select g.Key.fragment;

                        // Count the number of fragments selected in GIS.
                        _selectedFragsInGISCount = _fragsSelectedMap.Count();
                        goto case 2;
                    case 2:
                        // Get the unique toids selected in GIS.
                        _toidsSelectedMap = _gisSelection.AsEnumerable()
                            .GroupBy(r => r.Field<string>(_gisIDColumns[1].ColumnName)).Select(g => g.Key);

                        // Count the number of toids selected in GIS.
                        _selectedToidsInGISCount = _toidsSelectedMap.Count();
                        goto case 1;
                    case 1:
                        // Get the unique incids selected in GIS (ordered so that the filter
                        // is sorted in incid order).
                        _incidsSelectedMap = _gisSelection.AsEnumerable()
                            .GroupBy(r => r.Field<string>(_gisIDColumns[0].ColumnName)).Select(g => g.Key).OrderBy(s => s);

                        // Count the number of incids selected in GIS.
                        _selectedIncidsInGISCount = _incidsSelectedMap.Count();
                        break;
                }

                // Update the database Incid selection only if required.
                if ((updateIncidSelection) && (_selectedIncidsInGISCount > 0))
                {
                    // Set the Incid selection where clause to match the list of
                    // selected incids (for possible use later).
                    if (_incidSelectionWhereClause == null)
                        _incidSelectionWhereClause = ViewModelWindowMainHelpers.IncidSelectionToWhereClause(
                            IncidPageSize, IncidTable.incidColumn.Ordinal, IncidTable, _incidsSelectedMap);

                    // Update the database Incid selection to the Incids selected in the map.
                    GisToDbSelection();
                }
            }
            else
            {
                // Clear the sekection.
                if (updateIncidSelection)
                {
                    _incidSelection = null;
                    _incidSelectionWhereClause = null;

                    // Reset filter when no map features selected.
                    _incidPageRowNoMax = -1;
                }
            }
        }

        /// <summary>
        /// Set the database Incid selection based on the Incids selected in the map.
        /// </summary>
        private void GisToDbSelection()
        {
            _incidSelection = NewIncidSelectionTable();
            foreach (string s in _incidsSelectedMap)
                _incidSelection.Rows.Add([s]);
        }

        #endregion Selection Analysis

        #region Selection Tables

        private DataTable NewIncidSelectionTable()
        {
            DataTable outTable = new();
            outTable.Columns.Add(new DataColumn(IncidTable.incidColumn.ColumnName, IncidTable.incidColumn.DataType));
            outTable.DefaultView.Sort = IncidTable.incidColumn.ColumnName;
            return outTable;
        }

        #endregion Selection Tables

        #region Filter Management

        /// <summary>
        /// Updates the current filter state and moves the selection to the appropriate incid row
        /// based on the current filtering and splitting conditions.
        /// </summary>
        /// <remarks>This method should be called when the filter state changes or when a new incid is
        /// created during a split operation. The method determines the correct incid row to select
        /// based on whether filtering is enabled and whether a split is in progress.</remarks>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task SetFilterAsync()
        {
            try
            {
                // If filtered, and there are selected incids in the map or not connected to GIS
                // or in OSMM Update mode.
                if (IsFiltered && (((_selectedIncidsInGISCount > 0) || (_gisApp == null)) || IsOsmmReviewMode))
                    // If currently splitting a feature then go to the last incid
                    // in the filter (which will be the new incid).
                    if (_splitting)
                    {
                        // Move to the last incid in the selection.
                        await MoveIncidCurrentRowIndexAsync(IsFiltered ? _incidSelection.Rows.Count : _incidRowCount);
                    }
                    else
                    {
                        // Move to the first incid in the selection.
                        await MoveIncidCurrentRowIndexAsync(1);
                    }
            }
            catch { }
        }

        #endregion Filter Management

        #region Incid Row Count

        /// <summary>
        /// Counts the rows in the Incid table.
        /// </summary>
        /// <param name="recount">if set to <c>true</c> [recount].</param>
        /// <returns></returns>
        public int IncidRowCount(bool recount)
        {
            if (recount || (_incidRowCount <= 0))
            {
                try
                {
                    _incidRowCount = (int)_db.ExecuteScalar(String.Format(
                        "SELECT COUNT(*) FROM {0}", _db.QualifyTableName(_hluDS.incid.TableName)),
                        _db.Connection.ConnectionTimeout, CommandType.Text);

                    // Refresh the status fields
                    RefreshStatus();
                }
                catch { return -1; }
            }
            return _incidRowCount;
        }

        #endregion Incid Row Count

        #region New Row

        /// <summary>
        /// Initiates all the necessary actions when moving to another incid row.
        /// </summary>
        private async Task NewIncidCurrentRowAsync()
        {
            //TODO: Check if the selection is already being read so it
            // doesn't repeat itself.
            //// Re-check GIS selection in case it has changed.
            //if (_gisApp != null)
            //{
            //    // Initialise the GIS selection table.
            //    _gisSelection = NewGisSelectionTable();

            //    // Recheck the selected features in GIS (passing a new GIS
            //    // selection table so that it knows the columns to return.
            //    try
            //    {
            //        _gisSelection = await _gisApp.ReadMapSelectionAsync(_gisSelection);
            //    }
            //    catch (HLUToolException ex)
            //    {
            //        // Preserve stack trace and wrap in a meaningful type
            //        MessageBox.Show(ex.Message, "HLU Tool", MessageBoxButton.OK, MessageBoxImage.Warning);
            //        return;
            //    }

            //    _incidSelectionWhereClause = null;

            //    AnalyzeGisSelectionSet(false);
            //}

            bool canMove = false;
            if (!IsFiltered)
            {
                //TODO: Bug here sometimes?
                int newRowIndex = SeekIncid(_incidCurrentRowIndex);
                if ((canMove = newRowIndex != -1))
                    _incidCurrentRow = _hluDS.incid[newRowIndex];
            }
            else
            {
                if ((canMove = (_incidCurrentRowIndex != -1) &&
                    (_incidCurrentRowIndex <= _incidSelection.Rows.Count)))
                    _incidCurrentRow = await SeekIncidFiltered(_incidCurrentRowIndex);
            }

            if (canMove)
            {
                // Clone the current row to use to check for changes later
                CloneIncidCurrentRow();

                _incidArea = -1;
                _incidLength = -1;

                // Flag that the current record has not been changed yet so that the
                // apply button does not appear.
                Changed = false;

                // Clear the habitat type.
                HabitatType = null;
                OnPropertyChanged(nameof(HabitatType));

                // Get the incid table values
                IncidCurrentRowDerivedValuesRetrieve();
                OnPropertyChanged(nameof(IncidPrimary));

                // Get the incid child rows
                GetIncidChildRows(IncidCurrentRow);

                // If there are any OSMM Updates for this incid then store the values.
                if (_incidOSMMUpdatesRows.Length > 0)
                {
                    _incidOSMMUpdatesOSMMXref = _incidOSMMUpdatesRows[0].osmm_xref_id;
                    _incidOSMMUpdatesProcessFlag = _incidOSMMUpdatesRows[0].process_flag;
                    _incidOSMMUpdatesSpatialFlag = _incidOSMMUpdatesRows[0].Isspatial_flagNull() ? null : _incidOSMMUpdatesRows[0].spatial_flag;
                    _incidOSMMUpdatesChangeFlag = _incidOSMMUpdatesRows[0].Ischange_flagNull() ? null : _incidOSMMUpdatesRows[0].change_flag;
                    _incidOSMMUpdatesStatus = _incidOSMMUpdatesRows[0].status;
                }
                else
                {
                    _incidOSMMUpdatesOSMMXref = 0;
                    _incidOSMMUpdatesProcessFlag = 0;
                    _incidOSMMUpdatesSpatialFlag = null;
                    _incidOSMMUpdatesChangeFlag = null;
                    _incidOSMMUpdatesStatus = null;
                }

                // If auto select of features on change of incid is enabled.
                if (_autoSelectOnGis && IsNotBulkMode && !_filteredByMap)
                {
                    // Select the current incid record on the Map.
                    await SelectOnMapAsync(false);
                }

                // Count the number of toids and fragments for the current incid
                // selected in the GIS and in the database.
                CountCurrentIncidToidFrags();

                OnPropertyChanged(nameof(IncidCurrentRowIndex));
                OnPropertyChanged(nameof(OSMMIncidCurrentRowIndex));
                OnPropertyChanged(nameof(IncidCurrentRow));

                // Refresh all statuses, headers and fields
                RefreshStatus();
                RefreshHeader();
                RefreshOSMMUpdate();
                RefreshHabitatTab();
                RefreshIHSTab();
                RefreshPriorityTab();
                RefreshDetailsTab();
                RefreshSource1();
                RefreshSource2();
                RefreshSource3();
                RefreshHistory();
            }

            // Update the editing control state
            CheckEditingControlState();
        }

        #endregion New Row

        #region Helper Methods

        /// <summary>
        /// Replaces any string or date delimiters with connection type specific
        /// versions and qualifies any table names.
        /// </summary>
        /// <param name="words">The words.</param>
        /// <returns></returns>
        internal String ReplaceStringQualifiers(String sqlcmd)
        {
            // Check if a table name (delimited by '[]' characters) is found
            // in the sql command.
            int i1 = 0;
            int i2 = 0;
            String start = String.Empty;
            String end = String.Empty;

            while ((i1 != -1) && (i2 != -1))
            {
                i1 = sqlcmd.IndexOf('[', i2);
                if (i1 != -1)
                {
                    i2 = sqlcmd.IndexOf(']', i1 + 1);
                    if (i2 != -1)
                    {
                        // Strip out the table name.
                        string table = sqlcmd.Substring(i1 + 1, i2 - i1 - 1);

                        // Split the table name from the rest of the sql command.
                        if (i1 == 0)
                            start = String.Empty;
                        else
                            start = sqlcmd.Substring(0, i1);

                        if (i2 == sqlcmd.Length - 1)
                            end = String.Empty;
                        else
                            end = sqlcmd.Substring(i2 + 1);

                        // Replace the table name with a qualified table name.
                        sqlcmd = start + _db.QualifyTableName(table) + end;

                        // Reposition the last index.
                        i2 = sqlcmd.Length - end.Length;
                    }
                }
            }

            // Check if any strings are found (delimited by single quotes)
            // in the sql command.
            i1 = 0;
            i2 = 0;

            while ((i1 != -1) && (i2 != -1))
            {
                i1 = sqlcmd.IndexOf('\'', i2);
                if (i1 != -1)
                {
                    i2 = sqlcmd.IndexOf('\'', i1 + 1);
                    if (i2 != -1)
                    {
                        // Strip out the text string.
                        string text = sqlcmd.Substring(i1 + 1, i2 - i1 - 1);

                        // Split the text string from the rest of the sql command.
                        if (i1 == 0)
                            start = String.Empty;
                        else
                            start = sqlcmd.Substring(0, i1);

                        if (i2 == sqlcmd.Length - 1)
                            end = String.Empty;
                        else
                            end = sqlcmd.Substring(i2 + 1);

                        // Replace any wild characters found in the text.
                        if (start.TrimEnd().EndsWith(" LIKE"))
                        {
                            text = text.Replace("_", _db.WildcardSingleMatch);
                            text = text.Replace("%", _db.WildcardManyMatch);
                        }

                        // Replace the text delimiters with the correct delimiters.
                        sqlcmd = start + _db.QuoteValue(text) + end;

                        // Reposition the last index.
                        i2 = sqlcmd.Length - end.Length;
                    }
                }
            }

            // Check if any dates are found (delimited by '#' characters)
            // in the sql command.
            i1 = 0;
            i2 = 0;

            while ((i1 != -1) && (i2 != -1))
            {
                i1 = sqlcmd.IndexOf('#', i2);
                if (i1 != -1)
                {
                    i2 = sqlcmd.IndexOf('#', i1 + 1);
                    if (i2 != -1)
                    {
                        // Strip out the date string.
                        DateTime dt;
                        if (DateTime.TryParse(sqlcmd.AsSpan(i1 + 1, i2 - i1 - 1), out dt))
                        {
                            // Split the date string from the rest of the sql command.
                            if (i1 == 0)
                                start = String.Empty;
                            else
                                start = sqlcmd.Substring(0, i1);

                            if (i2 == sqlcmd.Length - 1)
                                end = String.Empty;
                            else
                                end = sqlcmd.Substring(i2 + 1);

                            // Replace the date delimiters with the correct delimiters.
                            sqlcmd = start + _db.QuoteValue(dt) + end;

                            // Reposition the last index.
                            i2 = sqlcmd.Length - end.Length;
                        }
                    }
                }
            }
            return sqlcmd;
        }

        #endregion Helper Methods

        #endregion Methods

    }
}