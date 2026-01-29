// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2013 Thames Valley Environmental Records Centre
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
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows.Input;
using HLU.Data.Model;
using HLU.GISApplication;
using HLU.UI.View;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// Contains the view model for the Merge Features window.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="R"></typeparam>
    class ViewModelWindowMergeFeatures<T, R> : ViewModelBase, IDataErrorInfo
        where T : DataTable
        where R : DataRow
    {
        #region Fields

        private ICommand _okCommand;
        private ICommand _cancelCommand;
        private ICommand _flashFeatureCommand;
        private string _displayName = "Merge Features";
        private T _selectedFeatures;
        private R _resultFeature;
        private HluDataSet.incid_mm_polygonsRow[] _childRows;
        private HluDataSet.incid_mm_polygonsRow[] _currChildRows;
        private int _incidOrdinal;
        private int _selectedIndex = -1;
        private int[] _keyOrdinals;
        private ArcProApp _gisApp;

        #endregion

        #region Constructor

        public ViewModelWindowMergeFeatures(T selectedFeatures, int[] keyOrdinals, int incidOrdinal,
            HluDataSet.incid_mm_polygonsRow[] childRows, ArcProApp gisApp)
        {
            _selectedFeatures = selectedFeatures;
            _keyOrdinals = keyOrdinals;
            _childRows = childRows;
            _incidOrdinal = incidOrdinal;
            _gisApp = gisApp;

            // Sort the view by INCID so the window shows a consistent order.
            if ((_selectedFeatures != null) &&
                (_incidOrdinal >= 0) &&
                (_incidOrdinal < _selectedFeatures.Columns.Count))
            {
                string incidColumnName = _selectedFeatures.Columns[_incidOrdinal].ColumnName;
                _selectedFeatures.DefaultView.Sort = String.Format("{0} ASC", incidColumnName);
            }
        }

        #endregion Constructor

        #region ViewModelBase Members

        public override string DisplayName
        {
            get { return _displayName; }
            set { _displayName = value; }
        }

        public override string WindowTitle { get { return DisplayName; } }

        #endregion ViewModelBase Members

        #region RequestClose

        public delegate void RequestCloseEventHandler(int resultFeatureIndex);

        public event RequestCloseEventHandler RequestClose;

        #endregion RequestClose

        #region Ok Command

        /// <summary>
        /// Create Ok button command.
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
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

        /// <summary>
        /// Handles event when Ok button is clicked
        /// </summary>
        /// <param name="param"></param>
        /// <remarks></remarks>
        private void OkCommandClick(object param)
        {
            RequestClose?.Invoke(_selectedIndex);
        }

        /// <summary>
        /// Whether we can click the Ok button.
        /// </summary>
        private bool CanOk { get { return String.IsNullOrEmpty(this.Error); } }

        #endregion Ok Command

        #region Cancel Command

        /// <summary>
        /// Create Cancel button command.
        /// </summary>
        /// <value></value>
        /// <returns></returns>
        /// <remarks></remarks>
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
        /// Handles event when Cancel button is clicked
        /// </summary>
        /// <param name="param"></param>
        /// <remarks></remarks>
        private void CancelCommandClick(object param)
        {
            RequestClose?.Invoke(-1);
        }

        #endregion Cancel Command

        #region Flash Feature Command

        /// <summary>
        /// Create Flash Feature command.
        /// </summary>
        public ICommand FlashFeatureCommand
        {
            get
            {
                if (_flashFeatureCommand == null)
                {
                    Action<object> flashFeatureAction = new(this.FlashFeature);
                    _flashFeatureCommand = new RelayCommand(flashFeatureAction, param => this.CanFlashFeature);
                }

                return _flashFeatureCommand;
            }
        }

        /// <summary>
        /// Determines whether we can flash the selected feature(s) on the map.
        /// </summary>
        private bool CanFlashFeature
        {
            get
            {
                // Check that we have a result feature.
                return _resultFeature != null && (_resultFeature is HluDataSet.incid_mm_polygonsRow ||
                    ((_currChildRows != null) && (_currChildRows.Length > 0)));
            }
        }

        /// <summary>
        /// Flashes the selected feature(s) on the map.
        /// </summary>
        /// <param name="param"></param>
        private void FlashFeature(object param)
        {
            // Check that we have a GIS application instance.
            if (_gisApp == null) return;

            // Check that we have a result feature.
            if (_resultFeature == null) return;

            // If the result feature is a polygon feature then flash it alone.
            if (_resultFeature is HluDataSet.incid_mm_polygonsRow)
            {
                // Build the where clause for the selected feature.
                List<List<SqlFilterCondition>> whereClause =
                    ViewModelWindowMainHelpers.GisSelectionToWhereClause([_resultFeature],
                    _keyOrdinals, 10, _selectedFeatures);

                // Flash all the features relating to the where clause together.
                if (whereClause.Count == 1)
                    _gisApp.FlashSelectedFeature(whereClause[0]);

                return;
            }

            // If we have child rows then flash them.
            if ((_currChildRows != null) && (_currChildRows.Length > 0))
            {
                // Build the where clauses for the selected rows.
                List<List<SqlFilterCondition>> whereClauses =
                    ViewModelWindowMainHelpers.GisSelectionToWhereClause(_currChildRows,
                    _keyOrdinals, 100, _selectedFeatures);

                // Flash all the features relating to the where clause together
                // or in groups if there are too many features to fit within a single
                // item in the where clauses list.
                if (whereClauses.Count == 1)
                    _gisApp.FlashSelectedFeature(whereClauses[0]);
                else
                    _gisApp.FlashSelectedFeatures(whereClauses);
            }
        }

        #endregion Flash Feature Command

        #region Merge Features

        /// <summary>
        /// Gets the features that are to be merged.
        /// </summary>
        public T MergeFeatures
        {
            get { return _selectedFeatures; }
        }

        /// <summary>
        /// Gets or sets the feature that is currently selected.
        /// </summary>
        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                _selectedIndex = value;

                // Guard against invalid selections.
                if ((_selectedFeatures == null) ||
                    (_selectedIndex < 0) ||
                    (_selectedIndex >= _selectedFeatures.DefaultView.Count))
                {
                    _resultFeature = null;
                    _currChildRows = null;
                    return;
                }

                // IMPORTANT: resolve the selected row from the sorted view, not DataTable.Rows.
                DataRowView rowView = _selectedFeatures.DefaultView[_selectedIndex];
                _resultFeature = (R)rowView.Row;

                // If we have child rows then get those that relate to the selected feature.
                if ((_resultFeature is HluDataSet.incidRow) && (_childRows != null))
                {
                    string incid = _resultFeature.Field<string>(_incidOrdinal);
                    _currChildRows = _childRows
                        .Where(r => r.incid == incid)
                        .ToArray();
                }

                // Flash the selected feature on the map.
                FlashFeature(null);
            }
        }

        #endregion Merge Features

        #region IDataErrorInfo Members

        /// <summary>
        /// Gets the error message for the object.
        /// </summary>
        public string Error
        {
            get
            {
                string error = String.Empty;

                if ((_resultFeature == null) || (_selectedIndex < 0) || (_selectedIndex >= _selectedFeatures.DefaultView.Count))
                    error = "Error: You must select the feature whose attributes will be retained.";

                return error;
            }
        }

        /// <summary>
        /// Gets the error message for the property with the given name.
        /// </summary>
        /// <param name="columnName"></param>
        /// <returns></returns>
        public string this[string columnName]
        {
            get
            {
                string error = String.Empty;

                switch (columnName)
                {
                    case "SelectedIndex":
                        if ((_selectedIndex < 0) || (_selectedIndex >= _selectedFeatures.DefaultView.Count))
                            error = "Error: You must select the feature whose attributes will be retained.";
                        break;
                    case "ResultFeature":
                        if (_resultFeature == null)
                            error = "Error: You must select the feature whose attributes will be retained.";
                        break;
                }

                // dirty commands registered with CommandManager so they are queried to see if they can execute now
                CommandManager.InvalidateRequerySuggested();

                return error;
            }
        }

        #endregion IDataErrorInfo Members
    }
}