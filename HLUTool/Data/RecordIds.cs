// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
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

using HLU.Data.Connection;
using HLU.Data.Model;
using HLU.Enums;
using System;
using System.Data;
using System.Linq;

namespace HLU.Data
{
    internal partial class RecordIds
    {
        #region Fields

        private string _siteID;
        private DbBase _db;
        private HluDataSet _hluDataset;
        private HluGeometryTypes _gisLayerType;
        private TableAdapterManager _hluTableAdapterMgr;
        private int _incidCurrentNumber = -1;
        private int _nextIncidSecondaryId = -1;
        private int _nextIncidConditionId = -1;
        private int _nextIncidBapId = -1;
        private int _nextIncidSourcesId = -1;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="RecordIds"/> class.
        /// </summary>
        /// <param name="db"></param>
        /// <param name="hluDataset"></param>
        /// <param name="hluTableAdapterMgr"></param>
        /// <param name="gisLayerType"></param>
        /// <exception cref="ArgumentException"></exception>
        public RecordIds(DbBase db, HluDataSet hluDataset,
            TableAdapterManager hluTableAdapterMgr, HluGeometryTypes gisLayerType)
        {
            // Check parameters.
            _db = db ?? throw new ArgumentException("db is null", nameof(db));
            _hluDataset = hluDataset ?? throw new ArgumentException("hluDataset is null", nameof(hluDataset));
            _hluTableAdapterMgr = hluTableAdapterMgr ?? throw new ArgumentException("hluTableAdapterMgr is null", nameof(hluTableAdapterMgr));

            // Set GIS layer type.
            _gisLayerType = gisLayerType;
            if (_hluDataset.lut_last_incid.IsInitialized && _hluDataset.lut_last_incid.Count == 0)
            {
                _hluTableAdapterMgr.lut_last_incidTableAdapter ??=
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

            // Get the next INCID Secondary ID from the database, starting with the maximum ID in the in-memory table.
            retVal = _db.ExecuteScalar(String.Format("SELECT MAX({0}) + 1 FROM {1}",
                _db.QuoteIdentifier(_hluDataset.incid_secondary.secondary_idColumn.ColumnName),
                _db.QualifyTableName(_hluDataset.incid_secondary.TableName)),
                _db.Connection.ConnectionTimeout, CommandType.Text);
            _nextIncidSecondaryId = retVal != DBNull.Value && retVal != null ? (int)retVal : 1;

            // Get the next INCID Condition ID from the database, starting with the maximum ID in the in-memory table.
            retVal = _db.ExecuteScalar(String.Format("SELECT MAX({0}) + 1 FROM {1}",
                _db.QuoteIdentifier(_hluDataset.incid_condition.incid_condition_idColumn.ColumnName),
                _db.QualifyTableName(_hluDataset.incid_condition.TableName)),
                _db.Connection.ConnectionTimeout, CommandType.Text);
            _nextIncidConditionId = retVal != DBNull.Value && retVal != null ? (int)retVal : 1;

            // Get the next INCID Bap ID from the database, starting with the maximum ID in the in-memory table.
            retVal = _db.ExecuteScalar(String.Format("SELECT MAX({0}) + 1 FROM {1}",
                _db.QuoteIdentifier(_hluDataset.incid_bap.bap_idColumn.ColumnName),
                _db.QualifyTableName(_hluDataset.incid_bap.TableName)),
                _db.Connection.ConnectionTimeout, CommandType.Text);
            _nextIncidBapId = retVal != DBNull.Value && retVal != null ? (int)retVal : 1;

            // Get the next INCID Sources ID from the database, starting with the maximum ID in the in-memory table.
            retVal = _db.ExecuteScalar(String.Format("SELECT MAX({0}) + 1 FROM {1}",
                _db.QuoteIdentifier(_hluDataset.incid_sources.incid_source_idColumn.ColumnName),
                _db.QualifyTableName(_hluDataset.incid_sources.TableName)),
                _db.Connection.ConnectionTimeout, CommandType.Text);
            _nextIncidSourcesId = retVal != DBNull.Value && retVal != null ? (int)retVal : 1;
        }

        #endregion Constructor

        #region Public Properties

        /// <summary>
        /// Gets the habitat version from the lut_habitat_class table for the given habitat class code.
        /// </summary>
        /// <param name="habitatClassCode">
        /// The habitat class code to look up (e.g. the code from lut_primary.habitat_class_code).
        /// </param>
        /// <returns>
        /// The habitat_version for the matching lut_habitat_class row, or "0" if not found.
        /// </returns>
        public string GetHabitatVersion(string habitatClassCode)
        {
            // Check parameter and return "0" if null or empty.
            if (String.IsNullOrEmpty(habitatClassCode))
                return "0";

            // Find the first row in lut_habitat_class where code matches habitatClassCode.
            var row = _hluDataset.lut_habitat_class
                .FirstOrDefault(r => r.code == habitatClassCode);

            // If no matching row is found or habitat_version is null, return "0".
            if (row == null || row.Ishabitat_versionNull())
                return "0";

            // Return the habitat_version from the matching row.
            return row.habitat_version;
        }

        /// <summary>
        /// Gets the SiteID from lut_site_id table based on GIS layer type.
        /// </summary>
        /// <value>The SiteID.</value>
        public string SiteID
        {
            get
            {
                // If SiteID is not set, get the last one from lut_site_id based on GIS layer type.
                // If lut_site_id is empty, default to "0000".
                if (String.IsNullOrEmpty(_siteID))
                {
                    if (_hluDataset.lut_site_id.Count > 0)
                    {
                        switch (_gisLayerType)
                        {
                            case HluGeometryTypes.Point:
                                _siteID = _hluDataset.lut_site_id.ElementAt(_hluDataset.lut_site_id.Count - 1).site_id_point;
                                break;

                            case HluGeometryTypes.Line:
                                _siteID = _hluDataset.lut_site_id.ElementAt(_hluDataset.lut_site_id.Count - 1).site_id_line;
                                break;

                            case HluGeometryTypes.Polygon:
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
        /// <value>The next INCID string.</value>
        public string NextIncid
        {
            get
            {
                // Get the current maximum INCID number from in-memory table, lut_last_incid table
                // and DB incid table, increment it and save back to lut_last_incid.
                _incidCurrentNumber = CurrentMaxIncidNumber(true);

                // Return the new INCID string based on the INCID number.
                return IncidString(_incidCurrentNumber);
            }
        }

        /// <summary>
        /// Gets the current INCID string.
        /// </summary>
        /// <value>The current INCID string.</value>
        public string CurrentIncid
        {
            get
            {
                // Return the current INCID string based on the current INCID number.
                return SiteID + ":" + _incidCurrentNumber.ToString("D7");
            }
        }

        /// <summary>
        /// Gets the current INCID Bap ID.
        /// </summary>
        /// <value>The current INCID Bap ID.</value>
        public int CurrentIncidBapId
        {
            get
            {
                // Return the current INCID Bap ID based on the next INCID Bap ID.
                return NextID(_nextIncidBapId, _hluDataset.incid_bap,
                    _hluDataset.incid_bap.bap_idColumn.Ordinal) - 1;
            }
        }

        /// <summary>
        /// Gets the next available INCID Secondary ID.
        /// </summary>
        /// <value>The next INCID Secondary ID.</value>
        public int NextIncidSecondaryId
        {
            get
            {
                // Get the next INCID Secondary ID based on the next INCID Secondary ID and the
                // maximum ID in the incid_secondary table.
                _nextIncidSecondaryId = NextID(_nextIncidSecondaryId, _hluDataset.incid_secondary,
                    _hluDataset.incid_secondary.secondary_idColumn.Ordinal);

                // Return the next INCID Secondary ID.
                return _nextIncidSecondaryId;
            }
        }

        /// <summary>
        /// Gets the next available INCID Condition ID.
        /// </summary>
        /// <value>The next INCID Condition ID.</value>
        public int NextIncidConditionId
        {
            get
            {
                // Get the next INCID Condition ID based on the next INCID Condition ID and the
                // maximum ID in the incid_condition table.
                _nextIncidConditionId = NextID(_nextIncidConditionId, _hluDataset.incid_condition,
                    _hluDataset.incid_condition.incid_condition_idColumn.Ordinal);

                // Return the next INCID Condition ID.
                return _nextIncidConditionId;
            }
        }

        /// <summary>
        /// Gets the next available INCID Bap ID.
        /// </summary>
        /// <value>The next INCID Bap ID.</value>
        public int NextIncidBapId
        {
            get
            {
                // Get the next INCID Bap ID based on the next INCID Bap ID and the maximum ID in
                // the incid_bap table.
                _nextIncidBapId = NextID(_nextIncidBapId, _hluDataset.incid_bap,
                    _hluDataset.incid_bap.bap_idColumn.Ordinal);

                // Return the next INCID Bap ID.
                return _nextIncidBapId;
            }
        }

        /// <summary>
        /// Gets the next available INCID Sources ID.
        /// </summary>
        /// <value>The next INCID Sources ID.</value>
        public int NextIncidSourcesId
        {
            get
            {
                // Get the next INCID Sources ID based on the next INCID Sources ID and the maximum ID in the incid_sources table.
                _nextIncidSourcesId = NextID(_nextIncidSourcesId, _hluDataset.incid_sources,
                    _hluDataset.incid_sources.incid_source_idColumn.Ordinal);

                // Return the next INCID Sources ID.
                return _nextIncidSourcesId;
            }
        }

        /// <summary>
        /// Gets the maximum INCID number.
        /// </summary>
        /// <value>The maximum INCID number.</value>
        public int MaxIncidNumber
        {
            get
            {
                // Get the maximum INCID number based on the length of the INCID string and SiteID.
                return (int)Math.Pow((double)10, (double)(IncidString(1).Length - SiteID.Length - 1)) - 1;
            }
        }

        #endregion Public Properties

        #region Public methods

        /// <summary>
        /// Parses the INCID number from an INCID string.
        /// </summary>
        /// <param name="incidString">The INCID string to parse.</param>
        /// <returns>The INCID number, or -1 if parsing fails.</returns>
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
        /// <param name="incidNumber">The INCID number to format.</param>
        /// <returns>The formatted INCID string.</returns>
        public string IncidString(int incidNumber)
        {
            // Concatenate SiteID and INCID number with leading zeros.
            return SiteID + ":" + incidNumber.ToString("D7");
        }

        /// <summary>
        /// Gets the maximum fragment ID for a given TOID.
        /// </summary>
        /// <param name="toid">The TOID to get the maximum fragment ID for.</param>
        /// <returns>The maximum fragment ID, or null if the TOID is invalid.</returns>
        public string MaxFragmentId(string toid)
        {
            // Check parameter.
            if (String.IsNullOrEmpty(toid))
                return null;

            try
            {
                // Build SQL to get the maximum fragment ID for the given TOID and execute it.
                object retVal = _db.ExecuteScalar(String.Format("SELECT MAX({0}) FROM {1} WHERE {2} = {3}",
                    _db.QuoteIdentifier(_hluDataset.incid_mm_polygons.fragidColumn.ColumnName),
                    _db.QualifyTableName(_hluDataset.incid_mm_polygons.TableName),
                    _db.QuoteIdentifier(_hluDataset.incid_mm_polygons.toidColumn.ColumnName),
                    _db.QuoteValue(toid)), _db.Connection.ConnectionTimeout, CommandType.Text);

                // Return the maximum fragment ID with leading zeros, or "00000" if null.
                return retVal.ToString() ?? "00000";
            }
            catch { return null; }
        }

        #endregion Public methods

        #region Private methods

        /// <summary>
        /// Gets the current maximum INCID number from in-memory table, lut_last_incid table and DB incid table.
        /// </summary>
        /// <param name="increment">Indicates whether to increment the maximum INCID number.</param>
        /// <returns>The current maximum INCID number, or -1 if an error occurs.</returns>
        private int CurrentMaxIncidNumber(bool increment)
        {
            try
            {
                int maxIncidNumber = 0;

                // Check the in-memory incid table
                if (_hluDataset.incid.Count > 0)
                    maxIncidNumber = _hluDataset.incid.Max(r => IncidNumber(r.incid));

                // Check the lut_last_incid table
                _hluTableAdapterMgr.Fill(_hluDataset, typeof(HluDataSet.lut_last_incidDataTable), true);
                HluDataSet.lut_last_incidRow lastIncidRow = null;
                if (_hluDataset.lut_last_incid.Count > 0)
                {
                    lastIncidRow =
                        _hluDataset.lut_last_incid.ElementAt(_hluDataset.lut_last_incid.Count - 1);
                    if (lastIncidRow.last_incid > maxIncidNumber)
                        maxIncidNumber = lastIncidRow.last_incid;
                }

                // Check the DB incid table
                string sql = String.Format("SELECT MAX({0}) FROM {1}",
                    _db.QuoteIdentifier(_hluDataset.incid.incidColumn.ColumnName),
                    _db.QualifyTableName(_hluDataset.incid.TableName));
                object result = _db.ExecuteScalar(sql, _db.Connection.ConnectionTimeout, CommandType.Text);
                int dbMax;
                if ((result != DBNull.Value) && (result != null) &&
                    ((dbMax = IncidNumber(result.ToString())) > maxIncidNumber))
                    maxIncidNumber = dbMax;

                // If increment is true, increment the greatest value found and save to lut_last_incid.
                if (increment)
                {
                    // Increment the greatest value found and save to lut_last_incid
                    maxIncidNumber++;
                    if (lastIncidRow != null)
                        lastIncidRow.last_incid = maxIncidNumber;
                    else
                        lastIncidRow = _hluDataset.lut_last_incid.Addlut_last_incidRow(maxIncidNumber);
                    _hluTableAdapterMgr.lut_last_incidTableAdapter.Update(lastIncidRow);
                }

                // Return the current maximum INCID number.
                return maxIncidNumber;
            }
            catch { return -1; }
        }

        /// <summary>
        /// Gets the next ID for a given table and ID column ordinal.
        /// </summary>
        /// <typeparam name="T">The type of the DataTable.</typeparam>
        /// <param name="nextID">The next ID to start from.</param>
        /// <param name="table">The DataTable to get the next ID for.</param>
        /// <param name="idColumnOrdinal">The ordinal of the ID column.</param>
        /// <returns>The next ID for the specified table and column.</returns>
        public int NextID<T>(int nextID, T table, int idColumnOrdinal)
            where T : DataTable
        {
            // If nextID is -1, get the maximum ID from the in-memory table.
            if ((nextID == -1) && (table.Rows.Count > 0) && (table != null))
                nextID = table.AsEnumerable().Max(r => r.Field<int>(idColumnOrdinal)) + 1;

            // Build SQL to get the maximum ID from the database table and execute it.
            string sql = String.Format("SELECT MAX({0}) + 1 FROM {1}",
                _db.QuoteIdentifier(table.Columns[idColumnOrdinal].ColumnName), _db.QualifyTableName(table.TableName));

            object result = _db.ExecuteScalar(sql, _db.Connection.ConnectionTimeout, CommandType.Text);

            // If the result is valid and greater than nextID, use it; otherwise, increment nextID by 1.
            int dbMax;
            if ((result != DBNull.Value) && (result != null) && Int32.TryParse(result.ToString(), out dbMax) &&
                (nextID < dbMax))
                nextID = dbMax;
            else
                nextID += 1;

            // Return the next ID.
            return nextID;
        }

        #endregion Private methods
    }
}