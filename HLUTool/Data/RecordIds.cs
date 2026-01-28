// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
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
using System.Data;
using System.Linq;
using HLU.Data.Connection;
using HLU.Data.Model;
using HLU.Data.Model.HluDataSetTableAdapters;
using HLU.UI.ViewModel;

namespace HLU.Data
{
    class RecordIds
    {
        #region Fields

        private string _siteID;
        DbBase _db;
        HluDataSet _hluDataset;
        GeometryTypes _gisLayerType;
        TableAdapterManager _hluTableAdapterMgr;
        private string _habitatVersion;
        private int _incidCurrentNumber = -1;
        int _nextIncidSecondaryId = -1;
        int _nextIncidConditionId = -1;
        int _nextIncidBapId = -1;
        int _nextIncidSourcesId = -1;

        #endregion

        #region ctor

        /// <summary>
        /// Initializes a new instance of the <see cref="RecordIds"/> class.
        /// </summary>
        /// <param name="db"></param>
        /// <param name="hluDataset"></param>
        /// <param name="hluTableAdapterMgr"></param>
        /// <param name="gisLayerType"></param>
        /// <exception cref="ArgumentException"></exception>
        public RecordIds(DbBase db, HluDataSet hluDataset,
            TableAdapterManager hluTableAdapterMgr, GeometryTypes gisLayerType)
        {
            // Check parameters.
            _db = db ?? throw new ArgumentException("db is null", nameof(db));
            _hluDataset = hluDataset ?? throw new ArgumentException("hluDataset is null", nameof(hluDataset));
            _hluTableAdapterMgr = hluTableAdapterMgr ?? throw new ArgumentException("hluTableAdapterMgr is null", nameof(hluTableAdapterMgr));

            // Set GIS layer type.
            _gisLayerType = gisLayerType;
            if (_hluDataset.lut_last_incid.IsInitialized && _hluDataset.lut_last_incid.Count == 0)
            {
                if (_hluTableAdapterMgr.lut_last_incidTableAdapter == null)
                    _hluTableAdapterMgr.lut_last_incidTableAdapter =
                        new HluTableAdapter<HluDataSet.lut_last_incidDataTable, HluDataSet.lut_last_incidRow>(_db);
                _hluTableAdapterMgr.Fill(_hluDataset,
                    [typeof(HluDataSet.lut_last_incidDataTable)], false);
            }

            // Initialize current INCID number.
            _incidCurrentNumber = CurrentMaxIncidNumber(false);

            // Initialize INCID child record IDs.
            InitializeIncidChildRecordIds();
        }

        /// <summary>
        /// Initializes the next IDs for INCID child records from the database.
        /// </summary>
        public void InitializeIncidChildRecordIds()
        {
            object retVal;

            retVal = _db.ExecuteScalar(String.Format("SELECT MAX({0}) + 1 FROM {1}",
                _db.QuoteIdentifier(_hluDataset.incid_secondary.secondary_idColumn.ColumnName),
                _db.QualifyTableName(_hluDataset.incid_secondary.TableName)),
                _db.Connection.ConnectionTimeout, CommandType.Text);
            _nextIncidSecondaryId = retVal != DBNull.Value && retVal != null ? (int)retVal : 1;

            retVal = _db.ExecuteScalar(String.Format("SELECT MAX({0}) + 1 FROM {1}",
                _db.QuoteIdentifier(_hluDataset.incid_condition.incid_condition_idColumn.ColumnName),
                _db.QualifyTableName(_hluDataset.incid_condition.TableName)),
                _db.Connection.ConnectionTimeout, CommandType.Text);
            _nextIncidConditionId = retVal != DBNull.Value && retVal != null ? (int)retVal : 1;

            retVal = _db.ExecuteScalar(String.Format("SELECT MAX({0}) + 1 FROM {1}",
                _db.QuoteIdentifier(_hluDataset.incid_bap.bap_idColumn.ColumnName),
                _db.QualifyTableName(_hluDataset.incid_bap.TableName)),
                _db.Connection.ConnectionTimeout, CommandType.Text);
            _nextIncidBapId = retVal != DBNull.Value && retVal != null ? (int)retVal : 1;

            retVal = _db.ExecuteScalar(String.Format("SELECT MAX({0}) + 1 FROM {1}",
                _db.QuoteIdentifier(_hluDataset.incid_sources.incid_source_idColumn.ColumnName),
                _db.QualifyTableName(_hluDataset.incid_sources.TableName)),
                _db.Connection.ConnectionTimeout, CommandType.Text);
            _nextIncidSourcesId = retVal != DBNull.Value && retVal != null ? (int)retVal : 1;
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the habitat version from lut_version table.
        /// </summary>
        public string HabitatVersion
        {
            get
            {
                // Get the habitat_version from the lut_version table.
                if (String.IsNullOrEmpty(_habitatVersion))
                    if (_hluDataset.lut_version.Count > 0)
                        _habitatVersion = _hluDataset.lut_version.ElementAt(_hluDataset.lut_version.Count - 1).habitat_version;
                    else
                        _habitatVersion = "0";

                return _habitatVersion;
            }
        }

        /// <summary>
        /// Gets the SiteID from lut_site_id table based on GIS layer type.
        /// </summary>
        public string SiteID
        {
            get
            {
                if (String.IsNullOrEmpty(_siteID))
                {
                    if (_hluDataset.lut_site_id.Count > 0)
                    {
                        switch (_gisLayerType)
                        {
                            case GeometryTypes.Point:
                                _siteID = _hluDataset.lut_site_id.ElementAt(_hluDataset.lut_site_id.Count - 1).site_id_point;
                                break;
                            case GeometryTypes.Line:
                                _siteID = _hluDataset.lut_site_id.ElementAt(_hluDataset.lut_site_id.Count - 1).site_id_line;
                                break;
                            case GeometryTypes.Polygon:
                                _siteID = _hluDataset.lut_site_id.ElementAt(_hluDataset.lut_site_id.Count - 1).site_id_polygon;
                                break;
                        }
                    }
                    else
                    {
                        _siteID = "0000";
                    }
                }
                return _siteID;
            }
        }

        /// <summary>
        /// Gets the next available INCID, checking lut_last_incid and incid tables in DB.
        /// Increments the number and saves the new value back to lut_last_incid in DB.
        /// </summary>
        public string NextIncid
        {
            get
            {
                _incidCurrentNumber = CurrentMaxIncidNumber(true);
                return IncidString(_incidCurrentNumber);
            }
        }
        
        /// <summary>
        /// Gets the current INCID string.
        /// </summary>
        public string CurrentIncid
        {
            get { return SiteID + ":" + _incidCurrentNumber.ToString("D7"); }
        }
        
        /// <summary>
        /// Gets the current INCID Bap ID.
        /// </summary>
        public int CurrentIncidBapId
        {
            get
            {
                return NextID(_nextIncidBapId, _hluDataset.incid_bap,
                    _hluDataset.incid_bap.bap_idColumn.Ordinal) - 1;
            }
        }
        
        /// <summary>
        /// Gets the next available INCID Secondary ID.
        /// </summary>
        public int NextIncidSecondaryId
        {
            get
            {
                _nextIncidSecondaryId = NextID(_nextIncidSecondaryId, _hluDataset.incid_secondary,
                    _hluDataset.incid_secondary.secondary_idColumn.Ordinal);
                return _nextIncidSecondaryId;
            }
        }
        
        /// <summary>
        /// Gets the next available INCID Condition ID.
        /// </summary>
        public int NextIncidConditionId
        {
            get
            {
                _nextIncidConditionId = NextID(_nextIncidConditionId, _hluDataset.incid_condition,
                    _hluDataset.incid_condition.incid_condition_idColumn.Ordinal);
                return _nextIncidConditionId;
            }
        }
        
        /// <summary>
        /// Gets the next available INCID Bap ID.
        /// </summary>
        public int NextIncidBapId
        {
            get
            {
                _nextIncidBapId = NextID(_nextIncidBapId, _hluDataset.incid_bap,
                    _hluDataset.incid_bap.bap_idColumn.Ordinal);
                return _nextIncidBapId;
            }
        }
        
        /// <summary>
        /// Gets the next available INCID Sources ID.
        /// </summary>
        public int NextIncidSourcesId
        {
            get
            {
                _nextIncidSourcesId = NextID(_nextIncidSourcesId, _hluDataset.incid_sources, 
                    _hluDataset.incid_sources.incid_source_idColumn.Ordinal);
                return _nextIncidSourcesId;
            }
        }
        
        /// <summary>
        /// Gets the maximum INCID number.
        /// </summary>
        public int MaxIncidNumber
        {
            get { return (int)Math.Pow((double)10, (double)(IncidString(1).Length - SiteID.Length - 1)) - 1; }
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Parses the INCID number from an INCID string.
        /// </summary>
        /// <param name="incidString"></param>
        /// <returns></returns>
        public static int IncidNumber(string incidString)
        {
            try
            {
                // Split on colon and parse the second part.
                int i;
                if (Int32.TryParse(incidString.Split(':')[1], out i))
                    return i;
                else
                    return -1;
            }
            catch { return -1; }
        }

        /// <summary>
        /// Formats an INCID string from an INCID number.
        /// </summary>
        /// <param name="incidNumber"></param>
        /// <returns></returns>
        public string IncidString(int incidNumber)
        {
            // Concatenate SiteID and INCID number with leading zeros.
            return SiteID + ":" + incidNumber.ToString("D7");
        }

        /// <summary>
        /// Gets the maximum toid fragment ID for a given TOID.
        /// </summary>
        /// <param name="toid"></param>
        /// <returns></returns>
        public string MaxToidFragmentId(string toid)
        {
            // Check parameter.
            if (String.IsNullOrEmpty(toid))
                return null;

            try
            {
                object retVal = _db.ExecuteScalar(String.Format("SELECT MAX({0}) FROM {1} WHERE {2} = {3}",
                    _db.QuoteIdentifier(_hluDataset.incid_mm_polygons.toidfragidColumn.ColumnName),
                    _db.QualifyTableName(_hluDataset.incid_mm_polygons.TableName),
                    _db.QuoteIdentifier(_hluDataset.incid_mm_polygons.toidColumn.ColumnName),
                    _db.QuoteValue(toid)), _db.Connection.ConnectionTimeout, CommandType.Text);
                return retVal.ToString() ?? "00000";
            }
            catch { return null; }
        }

        #endregion

        #region Private

        /// <summary>
        /// Gets the current maximum INCID number from in-memory table, lut_last_incid table and DB incid table.
        /// </summary>
        /// <param name="increment"></param>
        /// <returns></returns>
        private int CurrentMaxIncidNumber(bool increment)
        {
            try
            {
                int maxIncidNumber = 0;

                // check in-memory incid table
                if (_hluDataset.incid.Count > 0)
                    maxIncidNumber = _hluDataset.incid.Max(r => IncidNumber(r.incid));

                // check lut_last_incid in DB
                _hluTableAdapterMgr.Fill(_hluDataset, typeof(HluDataSet.lut_last_incidDataTable), true);
                HluDataSet.lut_last_incidRow lastIncidRow = null;
                if (_hluDataset.lut_last_incid.Count > 0)
                {
                    lastIncidRow =
                        _hluDataset.lut_last_incid.ElementAt(_hluDataset.lut_last_incid.Count - 1);
                    if (lastIncidRow.last_incid > maxIncidNumber)
                        maxIncidNumber = lastIncidRow.last_incid;
                }

                // check DB incid table
                string sql = String.Format("SELECT MAX({0}) FROM {1}",
                    _db.QuoteIdentifier(_hluDataset.incid.incidColumn.ColumnName),
                    _db.QualifyTableName(_hluDataset.incid.TableName));
                object result = _db.ExecuteScalar(sql, _db.Connection.ConnectionTimeout, CommandType.Text);
                int dbMax;
                if ((result != DBNull.Value) && (result != null) &&
                    ((dbMax = IncidNumber(result.ToString())) > maxIncidNumber)) maxIncidNumber = dbMax;

                if (increment)
                {
                    // increment the greatest value found and save to lut_last_incid
                    maxIncidNumber++;
                    if (lastIncidRow != null)
                        lastIncidRow.last_incid = maxIncidNumber;
                    else
                        lastIncidRow = _hluDataset.lut_last_incid.Addlut_last_incidRow(maxIncidNumber);
                    _hluTableAdapterMgr.lut_last_incidTableAdapter.Update(lastIncidRow);
                }

                return maxIncidNumber;
            }
            catch { return -1; }
        }

        /// <summary>
        /// Gets the next ID for a given table and ID column ordinal.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="nextID"></param>
        /// <param name="table"></param>
        /// <param name="idColumnOrdinal"></param>
        /// <returns></returns>
        public int NextID<T>(int nextID, T table, int idColumnOrdinal)
            where T : DataTable
        {
            if ((nextID == -1) && (table.Rows.Count > 0) && (table != null))
                nextID = table.AsEnumerable().Max(r => r.Field<int>(idColumnOrdinal)) + 1;

            string sql = String.Format("SELECT MAX({0}) + 1 FROM {1}",
                _db.QuoteIdentifier(table.Columns[idColumnOrdinal].ColumnName), _db.QualifyTableName(table.TableName));
            object result = _db.ExecuteScalar(sql, _db.Connection.ConnectionTimeout, CommandType.Text);
            int dbMax;
            if ((result != DBNull.Value) && (result != null) && Int32.TryParse(result.ToString(), out dbMax) &&
                (nextID < dbMax)) nextID = dbMax;
            else
                nextID += 1;

            return nextID;
        }

        #endregion
    }
}
