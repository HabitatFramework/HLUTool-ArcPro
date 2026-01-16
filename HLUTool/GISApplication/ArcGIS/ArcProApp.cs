// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2013-2014, 2016 Thames Valley Environmental Records Centre
// Copyright © 2014, 2018 Sussex Biodiversity Record Centre
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

using ArcGIS.Core.CIM;
using ArcGIS.Core.Data;
using ArcGIS.Core.Data.Analyst3D;
using ArcGIS.Core.Geometry;
using ArcGIS.Core.Internal.CIM;
using ArcGIS.Desktop.Editing;
using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Threading.Tasks;
using ArcGIS.Desktop.Internal.GeoProcessing.ModelBuilder;
using ArcGIS.Desktop.Layouts;
using ArcGIS.Desktop.Mapping;
using HLU.Data;
using HLU.Data.Model;
using HLU.Properties;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Envelope = ArcGIS.Core.Geometry.Envelope;
using Field = ArcGIS.Core.Data.Field;
using LinearUnit = ArcGIS.Core.Geometry.LinearUnit;
using QueryFilter = ArcGIS.Core.Data.QueryFilter;
using SpatialReference = ArcGIS.Core.Geometry.SpatialReference;
//using ArcGIS.Core.Internal.CIM;

namespace HLU.GISApplication
{
    /// <summary>
    /// Provides ArcGIS Pro application-specific functionality.
    /// </summary>
    internal partial class ArcProApp : SqlBuilder
    {
        #region Enums

        public enum DistanceUnits
        {
            Chains, Centimeters, Feet, Inches, Kilometers, Links, Meters,
            Miles, Millimeters, NauticalMiles, Rods, SurveyFeet, Yards
        }

        public enum AreaUnits
        {
            Acres, Hectares, Perches, Roods, SquareChains, SquareCentimeters,
            SquareFeet, SquareInches, SquareKilometers, SquareLinks, SquareMeters,
            SquareMiles, SquareMillimeters, SquareRods, SquareSurveyFeet, SquareYards
        }

        #endregion Enums

        #region Fields

        /// <summary>
        /// Workspace-dependent prefix added to date values in SQL queries.
        /// </summary>
        private string _dateLiteralPrefix;

        /// <summary>
        /// Workspace-dependent suffix added to date values in SQL queries.
        /// </summary>
        private string _dateLiteralSuffix;

        /// <summary>
        /// Workspace-dependent format string passed to the ToString() method when adding date values to SQL queries.
        /// </summary>
        private string _dateFormatString;

        /// <summary>
        /// Number format to the ToString() method when adding floating point numbers to SQL queries.
        /// ArcGIS expect a decimal point regardless of regional settings.
        /// </summary>
        private NumberFormatInfo _numberFormatInfo;

        //TODO: ArcGIS
        ///// <summary>
        ///// Dictionay of ESRI SQL predicates and their string equivalents.
        ///// </summary>
        //private Dictionary<String, esriSQLPredicates> _sqlPredicates;

        /// <summary>
        /// Template of the HLU layer's data structure.
        /// </summary>
        private HluGISLayer.incid_mm_polygonsDataTable _hluLayerStructure;

        //TODO: ArcGIS
        ///// <summary>
        ///// The workspace of the feature class of the HLU layer.
        ///// </summary>
        //private IFeatureWorkspace _hluWS;

        //TODO: ArcPro
        /// <summary>
        /// The HLU map layer.
        /// </summary>
        private FeatureLayer _hluLayer;


        /// <summary>
        /// The name of the feature class.
        /// </summary>
        private string _hluTableName;

        /// <summary>
        /// The list of valid HLU map layer names in the document.
        /// </summary>
        private List<string> _hluLayerNamesList;

        /// <summary>
        /// The current valid HLU map layer in the document.
        /// </summary>
        private HLULayer _hluCurrentLayer;

        //TODO: ArcGIS
        ///// <summary>
        ///// Persisted HLU layer that is cloned every time the application starts.
        ///// </summary>
        //private IGeoFeatureLayer _templateLayer;

        //TODO: ArcPro
        /// <summary>
        /// The feature class of the HLU layer.
        /// </summary>
        private FeatureClass _hluFeatureClass;

        //TODO: ArcPro
        /// <summary>
        /// The map of the HLU layer cast as IActiveView.
        /// </summary>
        private MapView _hluView;

        //TODO: ArcGIS
        ///// <summary>
        ///// SQL syntax supported by the HLU workspace.
        ///// </summary>
        //private ISQLSyntax _hluWSSqlSyntax;

        //TODO: Is _hluFeatureClass Needed?
        /// <summary>
        /// Maps the _hluFeatureClass data structure onto _hluLayerStructure.
        /// This is required by shapefiles with potentially truncated field names.
        /// The positions in this array correspond to the ordinals of columns in _hluLayerStructure;
        /// the value at each position to the ordinal of the correspoding field of _hluFeatureClass.
        /// </summary>
        private int[] _hluFieldMap;

        /// <summary>
        /// Field names of the HLU feature class, in the same order as in _hluFieldMap
        /// </summary>
        private string[] _hluFieldNames;

        /// <summary>
        /// Area unit of measurement (currently unused).
        /// </summary>
        private int _unitArea;

        /// <summary>
        /// Distance unit of measurement (currently unused.)
        /// </summary>
        private int _unitDistance;

        /// <summary>
        /// Maximum (nominal) allowable length of a SQL query.
        /// </summary>
        private int _maxSqlLength = Settings.Default.MaxSqlLengthArcGIS;

        #endregion Fields

        #region Implementation of SqlBuilder

        /// <summary>
        /// Quotes a string literal for SQL where clauses.
        /// </summary>
        /// <param name="value">The raw string.</param>
        /// <returns>A single-quoted and escaped literal.</returns>
        private static string QuoteStringLiteral(string value)
        {
            // Escape single quotes by doubling them.
            string escaped = (value ?? string.Empty).Replace("'", "''");
            return $"'{escaped}'";
        }

        /// <summary>
        /// Quotes an identifier using the configured quote prefix/suffix.
        /// </summary>
        /// <param name="identifier">The field or table name.</param>
        /// <returns>The quoted identifier.</returns>
        public override string QuoteIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return identifier;

            var prefix = QuotePrefix;
            var suffix = QuoteSuffix;

            if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(suffix))
                return identifier;

            if (!identifier.StartsWith(prefix, StringComparison.Ordinal))
                identifier = prefix + identifier;

            if (!identifier.EndsWith(suffix, StringComparison.Ordinal))
                identifier += suffix;

            return identifier;
        }

        /// <summary>
        /// Get the quote prefix for ArcGIS Pro.
        /// </summary>
        public override string QuotePrefix
        {
            get
            {
                return GetIdentifierQuoteDelimiters().Prefix;
            }
        }

        /// <summary>
        /// Get the quote suffix for ArcGIS Pro.
        /// </summary>
        public override string QuoteSuffix
        {
            get
            {
                return GetIdentifierQuoteDelimiters().Suffix;
            }
        }

        /// <summary>
        /// Gets identifier quote delimiters suitable for the active layer's datastore.
        /// </summary>
        /// <remarks>
        /// Shapefiles and file geodatabases typically do not require identifier delimiters,
        /// so empty strings are returned for those sources.
        /// </remarks>
        private (string Prefix, string Suffix) GetIdentifierQuoteDelimiters()
        {
            return QueuedTask.Run(() =>
            {
                var map = MapView.Active?.Map;
                if (map == null)
                    return (string.Empty, string.Empty);

                // Pick a representative feature layer.
                // If you know the specific layer you're querying, pass it in instead.
                var layer = map.Layers.OfType<FeatureLayer>().FirstOrDefault();
                if (layer == null)
                    return (string.Empty, string.Empty);

                using var table = layer.GetTable();
                var datastore = table.GetDatastore();

                // Shapefiles / folders (FileSystemDatastore) and file-based sources generally don't need quoting.
                // This covers shapefiles and other folder-based vector sources.
                if (datastore is ArcGIS.Core.Data.FileSystemDatastore)
                    return (string.Empty, string.Empty);

                // Geodatabases: could be file GDB, mobile GDB, or enterprise.
                if (datastore is ArcGIS.Core.Data.Geodatabase geodatabase)
                {
                    // Fast path: treat local GDBs as no-quote.
                    // We detect enterprise by looking for telltale connection-string markers.
                    var cs = SafeGetConnectionString(datastore);

                    if (IsSqlServer(cs))
                        return ("[", "]");

                    if (IsOracle(cs) || IsPostgreSql(cs))
                        return ("\"", "\"");

                    return (string.Empty, string.Empty);
                }

                // Default: don't quote.
                return (string.Empty, string.Empty);
            }).Result;
        }

        /// <summary>
        /// Safely returns a datastore connection string, or an empty string.
        /// </summary>
        private static string SafeGetConnectionString(ArcGIS.Core.Data.Datastore datastore)
        {
            try
            {
                return datastore.GetConnectionString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Returns true if the connection string appears to be SQL Server.
        /// </summary>
        private static bool IsSqlServer(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return false;

            // Common markers seen in Pro connection strings for SQL Server / SDE.
            return connectionString.Contains("SQLServer", StringComparison.OrdinalIgnoreCase) ||
                   connectionString.Contains("DBCLIENT=sqlserver", StringComparison.OrdinalIgnoreCase) ||
                   connectionString.Contains("INSTANCE=", StringComparison.OrdinalIgnoreCase) ||
                   connectionString.Contains("SERVER=", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns true if the connection string appears to be Oracle.
        /// </summary>
        private static bool IsOracle(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return false;

            return connectionString.Contains("Oracle", StringComparison.OrdinalIgnoreCase) ||
                   connectionString.Contains("DBCLIENT=oracle", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns true if the connection string appears to be PostgreSQL.
        /// </summary>
        private static bool IsPostgreSql(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return false;

            return connectionString.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
                   connectionString.Contains("DBCLIENT=postgresql", StringComparison.OrdinalIgnoreCase);
        }


        /// <summary>
        /// Get the string literal delimiter for ArcGIS Pro.
        /// </summary>
        public override string StringLiteralDelimiter { get { return "'"; } }

        /// <summary>
        /// Get the date literal prefix for ArcGIS Pro.
        /// </summary>
        public override string DateLiteralPrefix { get { return _dateLiteralPrefix; ; } }

        /// <summary>
        /// Get the date literal suffix for ArcGIS Pro.
        /// </summary>
        public override string DateLiteralSuffix { get { return _dateLiteralSuffix; } }

        /// <summary>
        ///  Get the wildcard single match character for ArcGIS Pro.
        /// </summary>
        public override string WildcardSingleMatch
        {
            get
            {
                //TODO: ArcGIS
                //return SQLSyntax.GetSpecialCharacter(esriSQLSpecialCharacters.esriSQL_WildcardSingleMatch);
                return null;
            }
        }

        /// <summary>
        /// Get the wildcard many match character for ArcGIS Pro.
        /// </summary>
        public override string WildcardManyMatch
        {
            get
            {
                //TODO: ArcGIS
                //return SQLSyntax.GetSpecialCharacter(esriSQLSpecialCharacters.esriSQL_WildcardManyMatch);
                return null;
            }
        }

        /// <summary>
        /// Get the concatenate operator for ArcGIS Pro.
        /// </summary>
        public override string ConcatenateOperator { get { return "&"; } }

        ///// <summary>
        ///// The the quote character for ArcGIS Pro.
        ///// </summary>
        ///// <param name="identifier"></param>
        ///// <returns></returns>
        //public override string QuoteIdentifier(string identifier)
        //{
        //    if (!String.IsNullOrEmpty(identifier))
        //    {
        //        if (!identifier.StartsWith(QuotePrefix)) identifier = identifier.Insert(0, QuotePrefix);
        //        if (!identifier.EndsWith(QuoteSuffix)) identifier += QuoteSuffix;
        //    }
        //    return identifier;
        //}

        /// <summary>
        /// Does not escape string delimiter or other special characters.
        /// Does check if value is already quoted.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public override string QuoteValue(object value)
        {
            if (value == null) return "NULL";
            int colType;
            if (_typeMapSystemToSQL.TryGetValue(value.GetType(), out colType))
            {
                string s;
                switch ((esriFieldType)colType)
                {
                    case esriFieldType.esriFieldTypeString:
                        s = value.ToString();
                        if (s.Length == 0) return StringLiteralDelimiter + StringLiteralDelimiter;
                        if (!s.StartsWith(StringLiteralDelimiter)) s = StringLiteralDelimiter + s;
                        if (!s.EndsWith(StringLiteralDelimiter)) s += StringLiteralDelimiter;
                        return s;
                    case esriFieldType.esriFieldTypeDate:
                        s = value is System.DateTime ? FormatDate((DateTime)value) : value.ToString();
                        if (s.Length == 0) return DateLiteralPrefix + DateLiteralSuffix;
                        if (!s.StartsWith(DateLiteralPrefix)) s = DateLiteralPrefix + s;
                        if (!s.EndsWith(DateLiteralSuffix)) s += DateLiteralSuffix;
                        return s;
                    case esriFieldType.esriFieldTypeSingle:
                        return FormatNumber((float)value).ToString();
                    case esriFieldType.esriFieldTypeDouble:
                        return FormatNumber((double)value).ToString();
                    default:
                        return value.ToString();
                }
            }
            else
            {
                return value.ToString();
            }
        }

        /// <summary>
        /// Get the field name alis of a supplied data column.
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public override string ColumnAlias(DataColumn c)
        {
            if (c == null)
                return String.Empty;
            else
                return ColumnAlias(c.Table.TableName, c.ColumnName);
        }

        /// <summary>
        /// Get the field name alias of a supplied table name and column name.
        /// </summary>
        /// <param name="tableName"></param>
        /// <param name="columnName"></param>
        /// <returns></returns>
        public override string ColumnAlias(string tableName, string columnName)
        {
            if (String.IsNullOrEmpty(columnName))
                return String.Empty;
            else if (String.IsNullOrEmpty(tableName))
                return columnName;
            else
                return tableName + "." + columnName;
        }

        /// <summary>
        /// Qualify the column names of the supplied data columns.
        /// </summary>
        /// <param name="targetColumns"></param>
        /// <returns></returns>
        public override bool QualifyColumnNames(DataColumn[] targetColumns)
        {
            if ((targetColumns == null) || (targetColumns.Length == 0)) return false;
            return targetColumns.Any(c => GetFieldName(_hluLayerStructure.Columns[c.ColumnName].Ordinal) == null);
        }

        /// <summary>
        /// Get the target list of data columns as a comma-seperated string.
        /// </summary>
        /// <param name="targetColumns"></param>
        /// <param name="quoteIdentifiers"></param>
        /// <param name="checkQualify"></param>
        /// <param name="qualifyColumns"></param>
        /// <param name="resultTable"></param>
        /// <returns></returns>
        public override string TargetList(DataColumn[] targetColumns, bool quoteIdentifiers,
            bool checkQualify, ref bool qualifyColumns, out DataTable resultTable)
        {
            resultTable = new();

            if ((targetColumns == null) || (targetColumns.Length == 0)) return String.Empty; ;

            StringBuilder sbTargetList = new();

            try
            {
                if (checkQualify) qualifyColumns = QualifyColumnNames(targetColumns);

                string fieldName;
                string columnAlias;
                foreach (DataColumn c in targetColumns)
                {
                    fieldName = GetFieldName(_hluLayerStructure.Columns[c.ColumnName].Ordinal);
                    if (qualifyColumns)
                    {
                        columnAlias = ColumnAlias(c);
                        if (quoteIdentifiers)
                            sbTargetList.Append(String.Format(",{0}.{1}", QuoteIdentifier(c.Table.TableName),
                                QuoteIdentifier(fieldName)));
                        else
                            sbTargetList.Append(String.Format(",{0}", columnAlias));
                        resultTable.Columns.Add(new DataColumn(columnAlias, c.DataType));
                    }
                    else
                    {
                        if (quoteIdentifiers)
                            sbTargetList.Append(String.Format(",{0}", QuoteIdentifier(fieldName)));
                        else
                            sbTargetList.Append(String.Format(",{0}", fieldName));
                        resultTable.Columns.Add(new DataColumn(c.ColumnName, c.DataType));
                    }
                }
                sbTargetList.Remove(0, 1);
            }
            catch { }

            return sbTargetList.ToString();
        }

        /// <summary>
        /// Joins are supported using WHERE syntax.
        /// Column names must be qualified with table name if multiple tables are joined.
        /// </summary>
        /// <param name="selectDistinct">If set to true a 'DISTINCT' clause is added to the SQL statement.</param>
        /// <param name="targetList">The target list of data columns.</param>
        /// <param name="whereConds">The SQL WHERE conditions.</param>
        /// <returns></returns>
        public override DataTable SqlSelect(bool selectDistinct,
            DataColumn[] targetList, List<SqlFilterCondition> whereConds)
        {
            //TODO: _arcMap
            if ((_hluLayer == null) || (_hluView == null) ||
                (targetList == null) || (targetList.Length == 0)) return new();

            try
            {
                DataTable resultTable;
                bool qualifyColumns = false;
                bool additionalTables;
                string subFields = TargetList(targetList, false, true, ref qualifyColumns, out resultTable);
                string fromList = qualifyColumns ?
                    FromList(false, targetList, false, ref whereConds, out additionalTables) : _hluLayer.Name;

                SqlSelectShared(fromList, whereConds, ref resultTable, qualifyColumns, subFields);
                return resultTable;
            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Map selection failed. ArcMap returned the following error message:\n\n{0}",
                    ex.Message), "HLU: Selection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new();
            }
        }

        /// <summary>
        /// Joins are supported using WHERE syntax.
        /// Column names must be qualified with table name if multiple tables are joined.
        /// </summary>
        /// <param name="selectDistinct">If set to true a 'DISTINCT' clause is added to the SQL statement.</param>
        /// <param name="addGeometryInfo">If set to true the geometry fields will be added to the returned data table.</param>
        /// <param name="targetList">The target list of data columns.</param>
        /// <param name="whereConds">The SQL WHERE conditions.</param>
        /// <returns></returns>
        public DataTable SqlSelect(bool selectDistinct, bool addGeometryInfo,
            DataColumn[] targetList, List<SqlFilterCondition> whereConds)
        {
            //TODO: _arcMap
            if ((_hluLayer == null) || (_hluView == null) ||
                (targetList == null) || (targetList.Length == 0)) return new();

            try
            {
                DataTable resultTable;
                bool qualifyColumns = false;
                bool additionalTables;
                string subFields = TargetList(targetList, false, true, ref qualifyColumns, out resultTable);
                string fromList = qualifyColumns ?
                    FromList(false, targetList, false, ref whereConds, out additionalTables) : _hluLayer.Name;

                SqlSelectShared(fromList, whereConds, ref resultTable, qualifyColumns, subFields);
                return resultTable;
            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Map selection failed. ArcMap returned the following error message:\n\n{0}",
                    ex.Message), "HLU: Selection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="selectDistinct"></param>
        /// <param name="targetTables"></param>
        /// <param name="whereConds"></param>
        /// <returns></returns>
        public override DataTable SqlSelect(bool selectDistinct,
            DataTable[] targetTables, List<SqlFilterCondition> whereConds)
        {
            //TODO: _arcMap
            if ((_hluLayer == null) || (_hluView == null) || (targetTables == null) ||
                (targetTables.Length == 0) || (targetTables[0].Columns.Count == 0)) return new();

            try
            {
                DataTable resultTable;
                bool qualifyColumns = false;
                string subFields = TargetList(targetTables, false, ref qualifyColumns, out resultTable);
                bool additionalTables;
                string fromList = FromList(false, false, targetTables, ref whereConds, out additionalTables);

                SqlSelectShared(fromList, whereConds, ref resultTable, qualifyColumns, subFields);
                return resultTable;
            }
            catch (Exception ex)
            {
                MessageBox.Show(String.Format("Map selection failed. ArcMap returned the following error message:\n\n{0}",
                    ex.Message), "HLU: Selection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new();
            }
        }

        private void SqlSelectShared(string fromList, List<SqlFilterCondition> whereConds,
            ref DataTable resultTable, bool qualifyColumns, string subFields)
        {
            List<string> selectionList = [];

            if (qualifyColumns) // joined tables
            {
                //TODO: ArcPro
                //string oidColumnAlias = ColumnAlias(((IDataset)_hluLayer.FeatureClass).Name,
                //    _hluLayer.FeatureClass.OIDFieldName);

                //int oidOrdinalTable = resultTable.Columns.Contains(oidColumnAlias) ?
                //    resultTable.Columns[oidColumnAlias].Ordinal : -1;

                //if (oidOrdinalTable != -1)
                //{
                //    selectionList = IpcArcMap([ "qd", fromList, subFields,
                //        WhereClause(false, false, true, MapWhereClauseFields(_hluLayerStructure, whereConds)),
                //        oidColumnAlias, "false" ]);
                //}
            }
            else // single table
            {
                //    selectionList = IpcArcMap([ "qf", subFields,
                //        WhereClause(false, false, false, MapWhereClauseFields(_hluLayerStructure, whereConds)), "false" ]);

                //    try
                //    {
                //        CreateSelectionFieldList(_pipeData[1]);
                //        IQueryFilter queryFilter = new QueryFilterClass();
                //        queryFilter.SubFields = String.Join(",", _selectFields);
                //        queryFilter.WhereClause = _pipeData[2];
                //        _sendColumnHeaders = _pipeData[3] == "true";
                //        _pipeData.Clear();

                //        _dummyControl.Invoke(_selByQFilterDel, new object[] { queryFilter });
                //    }
                //    catch (Exception ex) { PipeException(ex); }
            }

            //ThrowPipeError(selectionList);

            //foreach (string s in selectionList)
            //{
            //    string[] items = s.Split(PipeFieldDelimiter);
            //    resultTable.Rows.Add(items);
            //}
        }

        #endregion

        public static readonly string HistoryAdditionalFieldsDelimiter = Settings.Default.HistoryAdditionalFieldsDelimiter;

        public DataTable SqlSelect(string scratchMdbPath,
            string selectionTableName, DataColumn[] targetColumns)
        {
            //try
            //{
            //    bool qualifyColumns = false;
            //    DataTable resultTable;
            //    string subFields = TargetList(targetColumns, false, true,
            //        ref qualifyColumns, out resultTable);

            //    List<string> selectionList = IpcArcMap([ "sj", scratchMdbPath,
            //        selectionTableName, subFields, "false" ]);

            //    ThrowPipeError(selectionList);

            //    foreach (string s in selectionList)
            //    {
            //        string[] items = s.Split(PipeFieldDelimiter);
            //        resultTable.Rows.Add(items);
            //    }

            //    return resultTable;
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show(String.Format("Map selection failed. " +
            //        "ArcMap returned the following error message:\n\n{0}", ex.Message),
            //        "HLU: Selection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            //    return null;
            //}
            return null;
        }

        /// <summary>
        /// Calculate the approximate length of the SQL statement that will be
        /// used in GIS so that it can be determined if the selection can be
        /// performed using a direct query or if a table join is needed.
        /// </summary>
        /// <param name="targetList">The target list of data columns.</param>
        /// <param name="whereConds">The SQL WHERE conditions.</param>
        /// <returns>Integer of the approximate length of the SQL statement that will
        /// meet the where conditions.</returns>
        public int SqlLength(DataColumn[] targetList, List<SqlFilterCondition> whereConds)
        {
            //TODO: _arcMap
            if ((_hluLayer == null) || (_hluView == null) ||
                (targetList == null) || (targetList.Length == 0))
                return 0;

            try
            {
                bool qualifyColumns = false;
                bool additionalTables;
                DataTable resultTable = null;

                string subFields = TargetList(targetList, false, true, ref qualifyColumns, out resultTable);
                string fromList = qualifyColumns ?
                    FromList(false, targetList, false, ref whereConds, out additionalTables) : _hluLayer.Name;

                int sqlLen = WhereClause(false, false, true, MapWhereClauseFields(_hluLayerStructure, whereConds)).Length;

                return sqlLen;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Asynchronous method to read the map selection and populate the DataTable.
        /// </summary>
        /// <param name="resultTable">
        /// A DataTable defining the schema of the rows to be returned from GIS.
        /// </param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<DataTable> ReadMapSelectionAsync(DataTable resultTable)
        {
            // Return if the supplied resultTable is null
            if (resultTable == null)
                return resultTable;

            // Capture the current layer once (prevents confusing mid-call changes)
            FeatureLayer hluLayer = _hluLayer;

            // Return if the active layer hasn't been set yet.
            if (hluLayer == null)
                throw new GisSelectionException("No active HLU layer is set.");

            // Execute the query on the main CIM thread
            await QueuedTask.Run(() =>
            {
                try
                {
                    // Get the selection from the FeatureLayer
                    var selection = _hluLayer.GetSelection();

                    // Return if there are no selected features
                    if (selection.GetCount() == 0)
                        return;

                    // Validate that required columns exist on the layer
                    // so you don't blow up halfway through filling the DataTable.
                    var table = hluLayer.GetTable();
                    var def = table.GetDefinition();

                    var missing = resultTable.Columns
                        .Cast<DataColumn>()
                        .Select(c => c.ColumnName)
                        .Where(colName => def.FindField(colName) < 0)
                        .ToList();

                    if (missing.Count > 0)
                        throw new MissingLayerFieldsException(missing);

                    // Use a RowCursor to iterate through the selected features
                    using RowCursor rowCursor = selection.Search();

                    // Loop through the selected features until there are no more
                    while (rowCursor.MoveNext())
                    {
                        // Get the current feature
                        using Feature feature = (Feature)rowCursor.Current;

                        // Create a new DataRow
                        DataRow dataRow = resultTable.NewRow();

                        // Populate the DataRow with feature attributes
                        foreach (DataColumn c in resultTable.Columns)
                        {
                            dataRow[c.ColumnName] = feature[c.ColumnName];
                        }

                        //// Populate the DataRow with feature attributes
                        //for (int i = 0; i < resultTable.Columns.Count; i++)
                        //{
                        //    dataRow[i] = feature[i];
                        //}

                        // Add the DataRow to the DataTable
                        resultTable.Rows.Add(dataRow);
                    }
                }
                catch (HLUToolException)
                {
                    // Preserve meaning + stack trace for our own exceptions
                    throw;
                }
                catch (Exception ex)
                {
                    // Preserve stack trace and wrap in a meaningful type
                    throw new HLUToolException("Error reading map selection.", ex);
                }
            });

            // Return the resultTable
            return resultTable;
        }

        #region Selection

        /// <summary>
        /// Selects the feature(s) for a single incid in the active HLU layer and returns the selected IDs.
        /// </summary>
        /// <param name="incid">The incid to select.</param>
        /// <param name="resultTable">
        /// A DataTable defining the columns to return (typically incid/toid/toidfragid).
        /// </param>
        /// <returns>The populated selection table.</returns>
        /// <exception cref="GisSelectionException">Thrown if no active HLU layer is set.</exception>
        public async Task<bool> SelectIncidOnMapAsync(string incid)
        {
            if (string.IsNullOrWhiteSpace(incid))
                return false;

            if (_hluLayer == null)
                throw new GisSelectionException("No active HLU layer is set.");

            // Build a where clause using the actual layer field name for incid.
            // Assumes incid is a text field in the layer, as per legacy behaviour.
            string incidFieldName = GetFieldName(_hluLayerStructure.incidColumn.Ordinal);

            if (string.IsNullOrWhiteSpace(incidFieldName))
                throw new GisSelectionException("Could not resolve the incid field name for the active HLU layer.");

            string whereClause = $"{QuoteIdentifier(incidFieldName)} = {QuoteStringLiteral(incid)}";

            await SelectByWhereClauseAsync(whereClause, SelectionCombinationMethod.New).ConfigureAwait(false);

            return true;
        }

        /// <summary>
        /// Selects all INCIDs from a supplied selection table on the active HLU layer and
        /// returns the selected features into the supplied result table schema.
        /// </summary>
        /// <param name="incidSelection">
        /// A DataTable containing the INCID values that should be selected in GIS.
        /// </param>
        /// <returns>
        /// The populated result table containing the selected GIS features.
        /// </returns>
        public async Task<bool> SelectIncidsOnMapAsync(DataTable incidSelection)
        {
            // Nothing to select – return false.
            if (incidSelection == null || incidSelection.Rows.Count == 0)
                return false;

            // The active HLU layer must already be known (as with SelectCurrentOnMapAsync).
            if (_hluLayer == null)
                throw new GisSelectionException("No active HLU layer is set.");

            // Determine the ordinal of the INCID column from the layer structure.
            // This keeps the logic consistent with the rest of the tool.
            int incidOrdinal = _hluLayerStructure.incidColumn.Ordinal;

            // Resolve the actual GIS field name for the INCID column.
            string incidFieldName = GetFieldName(incidOrdinal);

            // If we can't resolve the field name, we can't proceed.
            if (string.IsNullOrWhiteSpace(incidFieldName))
                throw new GisSelectionException(
                    "Could not resolve the incid field name for the active HLU layer.");

            // Extract all distinct INCID values from the selection table, and:
            //  - convert to string (INCID is stored as text),
            //  - ignore null/empty values,
            //  - remove duplicates (important for SQL length and performance).
            var incids = incidSelection.Rows
                .Cast<DataRow>()
                .Select(r => r[incidOrdinal]?.ToString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // If no valid INCIDs were found, there is nothing to select.
            if (incids.Count == 0)
                return false;

            // Build one or more WHERE clauses of the form:
            //   <incidField> IN ('A','B','C',...)
            //
            // The clauses are chunked to avoid provider SQL length limits
            // (FGDB, SDE, shapefile, etc.).
            IEnumerable<string> whereClauses = BuildInWhereClauses(
                QuoteIdentifier(incidFieldName),
                incids,
                maxClauseLength: 8000);

            // First selection replaces the current map selection,
            // subsequent clauses are added to it.
            bool first = true;

            foreach (string whereClause in whereClauses)
            {
                await SelectByWhereClauseAsync(
                    whereClause,
                    first
                        ? SelectionCombinationMethod.New
                        : SelectionCombinationMethod.Add).ConfigureAwait(false);

                first = false;
            }

            return true;
        }

        /// <summary>
        /// Calculates the expected number of TOIDs and features for a given INCID selection
        /// by querying the active HLU layer directly.
        /// </summary>
        /// <param name="incidSelection">
        /// A DataTable containing INCID values to test.
        /// </param>
        /// <returns>
        /// A tuple containing:
        ///  - the expected number of distinct TOIDs,
        ///  - the expected number of feature rows.
        /// </returns>
        public async Task<(int ExpectedNumToids, int ExpectedNumFeatures)>
            ExpectedSelectionFeaturesAsync(DataTable incidSelection)
        {
            // No selection supplied – nothing expected.
            if (incidSelection == null || incidSelection.Rows.Count == 0)
                return (0, 0);

            // The active HLU layer must already be known.
            if (_hluLayer == null)
                throw new GisSelectionException("No active HLU layer is set.");

            // Resolve column ordinals from the layer structure.
            int incidOrdinal = _hluLayerStructure.incidColumn.Ordinal;
            int toidOrdinal = _hluLayerStructure.toidColumn.Ordinal;

            // Resolve GIS field names.
            string incidFieldName = GetFieldName(incidOrdinal);
            string toidFieldName = GetFieldName(toidOrdinal);

            if (string.IsNullOrWhiteSpace(incidFieldName))
                throw new GisSelectionException(
                    "Could not resolve the incid field name for the active HLU layer.");

            if (string.IsNullOrWhiteSpace(toidFieldName))
                throw new GisSelectionException(
                    "Could not resolve the toid field name for the active HLU layer.");

            // Extract distinct INCIDs from the selection table.
            var incids = incidSelection.Rows
                .Cast<DataRow>()
                .Select(r => r[incidOrdinal]?.ToString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (incids.Count == 0)
                return (0, 0);

            // Build chunked WHERE clauses for querying the layer.
            IEnumerable<string> whereClauses = BuildInWhereClauses(
                QuoteIdentifier(incidFieldName),
                incids,
                maxClauseLength: 8000);

            // Run the counting logic on the MCT.
            return await QueuedTask.Run(() =>
            {
                int featureCount = 0;

                // Track unique TOIDs explicitly.
                var distinctToids =
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (string whereClause in whereClauses)
                {
                    // Limit returned fields for performance.
                    QueryFilter qf = new()
                    {
                        WhereClause = whereClause,
                        SubFields = QuoteIdentifier(toidFieldName)
                    };

                    using RowCursor cursor = _hluLayer.Search(qf);
                    while (cursor.MoveNext())
                    {
                        using Row row = cursor.Current;

                        // Every row corresponds to a GIS feature.
                        featureCount++;

                        // Collect unique TOIDs.
                        object toidObj = row[toidFieldName];
                        string toid = toidObj?.ToString();

                        if (!string.IsNullOrWhiteSpace(toid))
                            distinctToids.Add(toid);
                    }
                }

                return (distinctToids.Count, featureCount);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Builds a series of SQL "field IN (...)" clauses, chunked to stay under an approximate maximum length.
        /// </summary>
        /// <param name="fieldExpression">The (already quoted) field name or expression.</param>
        /// <param name="values">The values to include.</param>
        /// <param name="maxClauseLength">Maximum clause length.</param>
        /// <returns>An enumerable of where clauses.</returns>
        private IEnumerable<string> BuildInWhereClauses(
            string fieldExpression,
            IEnumerable<string> values,
            int maxClauseLength)
        {
            if (string.IsNullOrWhiteSpace(fieldExpression))
                yield break;

            var quotedValues = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(QuoteStringLiteral)
                .ToList();

            if (quotedValues.Count == 0)
                yield break;

            string prefix = $"{fieldExpression} IN (";
            string suffix = ")";

            var current = new List<string>();
            int currentLength = prefix.Length + suffix.Length;

            foreach (string v in quotedValues)
            {
                int extra = (current.Count == 0 ? 0 : 1) + v.Length;

                if (current.Count > 0 && currentLength + extra > maxClauseLength)
                {
                    yield return prefix + string.Join(",", current) + suffix;
                    current.Clear();
                    currentLength = prefix.Length + suffix.Length;
                    extra = v.Length;
                }

                current.Add(v);
                currentLength += extra;
            }

            if (current.Count > 0)
                yield return prefix + string.Join(",", current) + suffix;
        }

        /// <summary>
        /// Selects features in the active HLU layer using a where clause.
        /// </summary>
        /// <param name="whereClause">The SQL where clause.</param>
        /// <param name="selectionMethod">The selection combination method.</param>
        /// <exception cref="GisSelectionException">Thrown if no active HLU layer is set.</exception>
        public async Task SelectByWhereClauseAsync(
            string whereClause,
            SelectionCombinationMethod selectionMethod)
        {
            if (_hluLayer == null)
                throw new GisSelectionException("No active HLU layer is set.");

            if (string.IsNullOrWhiteSpace(whereClause))
                return;

            await QueuedTask.Run(() =>
            {
                QueryFilter filter = new()
                {
                    WhereClause = whereClause
                };

                _hluLayer.Select(filter, selectionMethod);
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Clears the selection on the active HLU layer.
        /// </summary>
        public async Task ClearMapSelectionAsync()
        {
            if (_hluLayer == null)
                return;

            await QueuedTask.Run(() =>
            {
                _hluLayer.ClearSelection();
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Counts the selected features on the active HLU layer.
        /// </summary>
        /// <returns>The selection count, or 0 if no layer/selection.</returns>
        public async Task<int> CountMapSelectionAsync()
        {
            if (_hluLayer == null)
                return 0;

            return await QueuedTask.Run(() =>
            {
                return _hluLayer.SelectionCount;
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks whether the selected rows are unique by (incid,toid,toidfragid) in the active HLU layer.
        /// </summary>
        /// <remarks>
        /// This mirrors the legacy "SelectedRowsUnique" intent to protect against integrity issues.
        /// </remarks>
        /// <returns>True if unique or empty selection; false if duplicates found.</returns>
        public async Task<bool> SelectedRowsUniqueAsync()
        {
            if (_hluLayer == null)
                return true;

            return await QueuedTask.Run(() =>
            {
                var selection = _hluLayer.GetSelection();
                if (selection == null || selection.GetCount() == 0)
                    return true;

                // Resolve field names from structure ordinals.
                string incidField = GetFieldName(_hluLayerStructure.incidColumn.Ordinal);
                string toidField = GetFieldName(_hluLayerStructure.toidColumn.Ordinal);
                string fragField = GetFieldName(_hluLayerStructure.toidfragidColumn.Ordinal);

                if (string.IsNullOrWhiteSpace(incidField) ||
                    string.IsNullOrWhiteSpace(toidField) ||
                    string.IsNullOrWhiteSpace(fragField))
                    return true;

                HashSet<string> keys = new(StringComparer.Ordinal);

                using RowCursor cursor = selection.Search();
                while (cursor.MoveNext())
                {
                    using Row row = cursor.Current;

                    string incid = Convert.ToString(row[incidField]) ?? string.Empty;
                    string toid = Convert.ToString(row[toidField]) ?? string.Empty;
                    string frag = Convert.ToString(row[fragField]) ?? string.Empty;

                    string key = $"{incid}|{toid}|{frag}";

                    if (!keys.Add(key))
                        return false;
                }

                return true;
            }).ConfigureAwait(false);
        }

        #endregion Selection

        #region Zoom

        //TODO: Remove minZoom and distUnits if not needed
        /// <summary>
        /// Zooms the active map view to the current selection in the active HLU layer,
        /// with ArcMap-compatible behaviour:
        /// - "always": always zoom to the selection.
        /// - "when": only zoom if the selection is not fully within the current view extent.
        /// </summary>
        /// <param name="minZoom">
        /// Minimum allowable scale (legacy meaning: do not remain more zoomed-in than this).
        /// Interpreted as a map scale (e.g. 10000).
        /// </param>
        /// <param name="autoZoomToSelection">
        /// 2 = always zoom, 1 = zoom only when selection is outside the visible area, 0 = do not zoom.
        /// </param>
        /// <param name="ratio">
        /// Optional zoom ratio applied after zooming, unless a valid scale list is provided.
        /// </param>
        /// <param name="validScales">
        /// Optional list of valid scales used to snap to the next scale up instead of applying the ratio directly.
        /// </param>
        public async Task ZoomSelectedAsync(
            int minZoom,
            int autoZoomToSelection,
            double? ratio = null,
            List<int> validScales = null)
        {
            // Respect caller setting: 0 means do not zoom.
            if (autoZoomToSelection == 0)
                return;

            if (_hluLayer == null)
                return;

            MapView view = MapView.Active;
            if (view == null)
                return;

            await QueuedTask.Run(async () =>
            {
                var selection = _hluLayer.GetSelection();
                if (selection == null || selection.GetCount() == 0)
                    return;

                // Determine whether we should zoom based on ArcMap-style behaviour.
                // autoZoomToSelection meanings:
                //   2 = always zoom to selection.
                //   1 = zoom only when the selection is not fully within the visible map extent.
                //   0 = do not zoom at all.
                bool shouldZoom = (autoZoomToSelection == 2);

                if (!shouldZoom && autoZoomToSelection == 1)
                {
                    // Get the current visible extent of the active map view.
                    // This represents what the user can currently see on screen.
                    Envelope viewExtent = view.Extent;

                    // Get the extent of the selected features in the HLU layer.
                    // QueryExtent(true) returns the envelope of the current selection only.
                    Envelope selectionExtent = GetSelectionExtent(_hluLayer);

                    // If either extent cannot be determined, default to zooming.
                    // This mirrors the ArcMap behaviour of "better to zoom than do nothing".
                    if (viewExtent == null || selectionExtent == null)
                    {
                        shouldZoom = true;
                    }
                    else
                    {
                        // Ensure both extents are in the map's spatial reference.
                        // Geometry containment tests are unreliable if spatial references differ.
                        SpatialReference mapSpatialReference = view.Map?.SpatialReference;

                        if (mapSpatialReference != null)
                        {
                            if (viewExtent.SpatialReference == null ||
                                !viewExtent.SpatialReference.IsEqual(mapSpatialReference))
                            {
                                viewExtent = (Envelope)GeometryEngine.Instance.Project(
                                    viewExtent,
                                    mapSpatialReference);
                            }

                            if (selectionExtent.SpatialReference == null ||
                                !selectionExtent.SpatialReference.IsEqual(mapSpatialReference))
                            {
                                selectionExtent = (Envelope)GeometryEngine.Instance.Project(
                                    selectionExtent,
                                    mapSpatialReference);
                            }
                        }

                        // Is the selection extent NOT fully contained
                        // within the current visible map extent.
                        shouldZoom = !IsEnvelopeContained(
                            outer: viewExtent,
                            inner: selectionExtent);
                    }
                }

                // If the selection extent is NOT fully contained
                // within the current visible map extent don't zoom.
                if (!shouldZoom)
                    return;

                // Zoom to the selection in this layer.
                await view.ZoomToAsync(_hluLayer, true).ConfigureAwait(false);

                // Apply additional scale logic (ported from ApplyZoomToMapFrame):
                // - If a ratio is provided, apply ratio or next scale up from validScales.
                // - Otherwise, enforce minZoom as a minimum allowed zoom-in (legacy safeguard).
                ApplyZoomToMapView(
                    view,
                    ratio,
                    null,
                    validScales);

                if (minZoom > 0 && (validScales == null || validScales.Count < 2) && !ratio.HasValue)
                {
                    Camera camera = view.Camera;

                    // Smaller scale = more zoomed-in. If we are closer than minZoom, zoom out to minZoom.
                    if (camera.Scale < minZoom)
                    {
                        camera.Scale = minZoom;

                        await view.ZoomToAsync(camera, duration: null).ConfigureAwait(false);
                    }
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the extent of the currently selected features in a feature layer
        /// by iterating the selection and unioning each feature's shape extent.
        /// </summary>
        /// <remarks>
        /// This avoids FeatureLayer.QueryExtent(true) which can return an unexpectedly
        /// large extent for some layer/data-source types (e.g. query layers/joins).
        /// Must be called on the MCT (i.e. from within QueuedTask.Run).
        /// </remarks>
        /// <param name="featureLayer">The feature layer whose selection extent is required.</param>
        /// <returns>
        /// The envelope of the selected features, or null if there is no selection
        /// or feature shapes cannot be read.
        /// </returns>
        private static Envelope GetSelectionExtent(
            FeatureLayer featureLayer)
        {
            if (featureLayer == null)
                return null;

            Selection selection = featureLayer.GetSelection();
            if (selection == null || selection.GetCount() == 0)
                return null;

            EnvelopeBuilderEx builder = null;

            // Search the selected rows only.
            using RowCursor cursor = selection.Search();

            while (cursor.MoveNext())
            {
                using Row row = cursor.Current;

                // Selected rows from a FeatureLayer should be Feature rows.
                if (row is not Feature feature)
                    continue;

                ArcGIS.Core.Geometry.Geometry shape = feature.GetShape();
                if (shape == null)
                    continue;

                Envelope env = shape.Extent;
                if (env == null)
                    continue;

                // Build up a unioned envelope across all selected features.
                if (builder == null)
                {
                    builder = new EnvelopeBuilderEx(env);
                }
                else
                {
                    builder.Union(env);
                }
            }

            return builder?.ToGeometry() as Envelope;
        }

        /// <summary>
        /// Determines whether one envelope is fully contained within another,
        /// using a small tolerance to avoid floating-point precision issues.
        /// </summary>
        /// <param name="outer">The envelope representing the visible map extent.</param>
        /// <param name="inner">The envelope representing the selection extent.</param>
        /// <returns>
        /// True if <paramref name="inner"/> is completely inside <paramref name="outer"/>;
        /// otherwise false.
        /// </returns>
        private static bool IsEnvelopeContained(
            Envelope outer,
            Envelope inner)
        {
            if (outer == null || inner == null)
                return false;

            // Tolerance helps prevent false negatives due to floating-point rounding.
            double tolerance = Math.Max(outer.Width, outer.Height) * 1e-9;

            return inner.XMin >= outer.XMin - tolerance &&
                   inner.YMin >= outer.YMin - tolerance &&
                   inner.XMax <= outer.XMax + tolerance &&
                   inner.YMax <= outer.YMax + tolerance;
        }

        #endregion Zoom

        #region OLD?

        //TODO: Replace calls with ClearMapSelectionAsync
        /// <summary>
        /// Clears the currently selected map features.
        /// </summary>
        public void ClearMapSelection()
        {
            //IpcArcMap(["cs"]);
        }

        //TODO: Replace calls with CountMapSelectionAsync
        /// <summary>
        /// Counts the currently selected map features.
        /// </summary>
        public void CountMapSelection(ref int fragCount)
        {
            //List<string> retList = IpcArcMap(["qs"]);
            //if (retList.Count > 0)
            //    fragCount = Convert.ToInt32(retList[0]);
            //else
            //    fragCount = 0;
        }

        //TODO: Replace calls with SelectedRowsUniqueAsync
        /// <summary>
        /// Check if all selected rows have unique keys to avoid
        /// any potential data integrity problems.
        /// </summary>
        /// <returns></returns>
        public bool SelectedRowsUnique()
        {
            //try
            //{
            //    List<string> retList = IpcArcMap(["su"]);
            //    if (retList.Count > 0)
            //        return Convert.ToBoolean(retList[0]);
            //    else
            //        return true;
            //}
            //catch { return true; }
            return false;
        }

        #endregion Selection

        #region Helpers

        /// <summary>
        /// Returns the next scale larger than the current scale from a list of valid scales.
        /// </summary>
        /// <param name="currentScale">
        /// The current map scale (e.g. 5000).
        /// </param>
        /// <param name="scaleList">
        /// A list of valid scales. Must contain at least two values.
        /// </param>
        /// <returns>
        /// The next scale up (i.e. more zoomed-out). If the current scale exceeds
        /// the largest value in the list, the method extrapolates using the last interval.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="scaleList"/> contains fewer than two values.
        /// </exception>
        private static double GetNextScaleUp(
            double currentScale,
            List<int> scaleList)
        {
            if (scaleList == null || scaleList.Count < 2)
                throw new ArgumentException("Scale list must contain at least two values.");

            // Ensure the list is ordered from smallest (most zoomed-in)
            // to largest (most zoomed-out).
            scaleList.Sort();

            // Find the first scale larger than the current scale.
            foreach (int scale in scaleList)
            {
                if (scale > currentScale)
                    return scale;
            }

            // If we reach here, the current scale is beyond the largest defined scale.
            // Extrapolate using the difference between the last two values.
            int count = scaleList.Count;
            int last = scaleList[count - 1];
            int secondLast = scaleList[count - 2];
            int gap = last - secondLast;

            double extrapolated = last;

            while (extrapolated <= currentScale)
            {
                extrapolated += gap;
            }

            return extrapolated;
        }

        /// <summary>
        /// Applies zoom logic to a map view by modifying its camera scale,
        /// based on either a ratio, a fixed scale, or a list of valid scales.
        /// </summary>
        /// <remarks>
        /// This is a MapView-based adaptation of ApplyZoomToMapFrame.
        /// Camera changes are applied using ZoomToAsync rather than direct assignment.
        /// </remarks>
        /// <param name="mapView">
        /// The active map view to apply the zoom to.
        /// </param>
        /// <param name="ratio">
        /// Optional zoom ratio to apply (e.g. 1.5).
        /// Ignored if <paramref name="scale"/> is provided.
        /// </param>
        /// <param name="scale">
        /// Optional fixed scale to apply directly (e.g. 10000).
        /// </param>
        /// <param name="validScales">
        /// Optional list of valid scales. If supplied and <paramref name="ratio"/> is set,
        /// the next scale up from the list is chosen instead of applying the ratio directly.
        /// </param>
        private static void ApplyZoomToMapView(
            MapView mapView,
            double? ratio,
            double? scale,
            List<int> validScales = null)
        {
            if (mapView == null)
                return;

            try
            {
                Camera camera = mapView.Camera;

                // Ratio-based zoom logic.
                if (ratio.HasValue)
                {
                    // If a scale list is supplied, choose the next valid scale up.
                    if (validScales != null && validScales.Count >= 2)
                    {
                        double currentScale = camera.Scale;
                        double nextScale = GetNextScaleUp(currentScale, validScales);

                        camera.Scale = nextScale;

                        // Apply the updated camera.
                        _ = mapView.ZoomToAsync(camera, duration: null);
                    }
                    else
                    {
                        // No scale list supplied: apply the ratio directly.
                        camera.Scale *= ratio.Value;

                        _ = mapView.ZoomToAsync(camera, duration: null);
                    }
                }
                // Fixed-scale zoom logic.
                else if (scale.HasValue && scale.Value > 0)
                {
                    camera.Scale = scale.Value;

                    _ = mapView.ZoomToAsync(camera, duration: null);
                }
            }
            catch (Exception ex)
            {
                // Zoom errors should never be fatal to the calling workflow.
                System.Diagnostics.Trace.WriteLine(
                    $"ApplyZoomToMapView error: {ex.Message}");
            }
        }

        #endregion Helpers

        public void FlashSelectedFeature(List<SqlFilterCondition> whereClause)
        {
            //List<string> resultList = IpcArcMap([ "fl",
            //    WhereClause(false, false, false, MapWhereClauseFields(_hluLayerStructure, whereClause)) ]);
        }

        public void FlashSelectedFeatures(List<List<SqlFilterCondition>> whereClauses)
        {
            //foreach (List<SqlFilterCondition> whereClause in whereClauses)
            //{
            //    List<string> resultList = IpcArcMap([ "fl",
            //        WhereClause(false, false, false, MapWhereClauseFields(_hluLayerStructure, whereClause)) ]);
            //}
        }

        public DataTable SplitFeature(string currentToidFragmentID, string lastToidFragmentID,
            List<SqlFilterCondition> selectionWhereClause, DataColumn[] historyColumns)
        {
            //return ResultTableFromList(IpcArcMap([ "sp",
            //    WhereClause(false, false, false, MapWhereClauseFields(_hluLayerStructure, selectionWhereClause)),
            //    lastToidFragmentID, String.Join(",", historyColumns.Select(c => c.ColumnName).ToArray()) ]));
            return null;
        }

        /// <summary>
        /// Split features logically by changing their incid number.
        /// Pass the old incid number together with the new incid number
        /// so that only features belonging to the old incid are
        /// updated.
        /// </summary>
        /// <param name="oldIncid"></param>
        /// <param name="newIncid"></param>
        /// <param name="historyColumns"></param>
        /// <returns></returns>
        public DataTable SplitFeaturesLogically(string oldIncid, string newIncid, DataColumn[] historyColumns)
        {
            //try
            //{
            //    string[] sendList =
            //    [
            //        "sl",
            //        oldIncid,
            //        newIncid,
            //        //DONE: Aggregate
            //        //historyColumns.Aggregate(new(), (sb, c) =>
            //        //    sb.Append("," + c.ColumnName)).Remove(0, 1).ToString(),
            //        string.Join(",", historyColumns.Select(c => c.ColumnName)),
            //    ];
            //    return ResultTableFromList(IpcArcMap(sendList));
            //}
            //catch { throw; }
            return null;
        }

        public DataTable MergeFeatures(string newToidFragmentID,
            List<SqlFilterCondition> resultWhereClause, DataColumn[] historyColumns)
        {
            //return ResultTableFromList(IpcArcMap([ "mg",
            //    WhereClause(false, false, false, MapWhereClauseFields(_hluLayerStructure, resultWhereClause)),
            //    newToidFragmentID, String.Join(",", historyColumns.Select(c => c.ColumnName).ToArray())]));
            return null;
        }

        public DataTable MergeFeaturesLogically(string keepIncid, DataColumn[] historyColumns)
        {
            //string[] sendList =
            //[
            //    "ml",
            //    keepIncid,
            //    String.Join(",", historyColumns.Select(c => c.ColumnName).ToArray()),
            //];
            //return ResultTableFromList(IpcArcMap(sendList));
            return null;
        }

        private DataTable ResultTableFromList(List<string> resultList)
        {
            //try
            //{
            //    if ((resultList != null) && (resultList.Count > 1))
            //    {
            //        // Create a new result table
            //        DataTable resultTable = new();

            //        // Define the result table by adding the columns
            //        int i = 0;
            //        string s;
            //        while ((i < resultList.Count) && ((s = resultList[i++]) != PipeTransmissionInterrupt))
            //        {
            //            string[] items = s.Split(PipeFieldDelimiter);
            //            resultTable.Columns.Add(new DataColumn(items[0], Type.GetType(items[1])));
            //        }

            //        // Add the values to the result table
            //        while (i < resultList.Count)
            //        {
            //            // Split the final resultlist string and trim spaces
            //            string[] items = resultList[i++].Split(PipeFieldDelimiter).Select(r => r.Trim()).ToArray();
            //            resultTable.Rows.Add(items);
            //        }

            //        return resultTable;
            //    }
            //}
            //catch { }

            return null;
        }

        public DataTable UpdateFeatures(DataColumn[] updateColumns, object[] updateValues,
            DataColumn[] historyColumns)
        {
            //try
            //{
            //    string delimiter = PipeFieldDelimiter.ToString();

            //    return ResultTableFromList(IpcArcMap(new string[] { "us" }
            //        .Concat(updateColumns.Select(c => c.ColumnName))
            //        .Concat([PipeTransmissionInterrupt])
            //        .Concat(updateValues.Select(o => o.ToString()))
            //        .Concat([PipeTransmissionInterrupt])
            //        .Concat(historyColumns.Select(c => c.ColumnName)).ToArray()));
            //}
            //catch { throw; }
            return null;
        }

        //public DataTable UpdateFeatures(DataColumn[] updateColumns, object[] updateValues,
        //    DataColumn[] historyColumns, List<SqlFilterCondition> selectionWhereClause)
        //{
        //    try
        //    {
        //        string delimiter = PipeFieldDelimiter.ToString();

        //        return ResultTableFromList(IpcArcMap(new string[] { "up",
        //            WhereClause(false, false, false, MapWhereClauseFields(_hluLayerStructure, selectionWhereClause)) }
        //            .Concat(updateColumns.Select(c => c.ColumnName))
        //            .Concat([PipeTransmissionInterrupt])
        //            .Concat(updateValues.Select(o => o.ToString()))
        //            .Concat([PipeTransmissionInterrupt])
        //            .Concat(historyColumns.Select(c => c.ColumnName)).ToArray()));
        //    }
        //    catch { throw; }
        //    return null;
        //}

        public async Task<DataTable> GetHistoryAsync(DataColumn[] historyColumns, List<SqlFilterCondition> selectionWhereClause)
        {
            //TODO: Needed?
            // Ensure selection event handlers do not interfere
            //_selectFieldOrdinals = null;

            // Create a query filter for selecting features
            QueryFilter queryFilter = new()
            {
                WhereClause = WhereClause(false, false, false, MapWhereClauseFields(_hluLayerStructure, selectionWhereClause))
            };

            // Extract history column names
            string[] historyColumnNames = historyColumns.Select(c => c.ColumnName).ToArray();

            // Create a DataTable to store history data
            DataTable historyTable = new();
            foreach (string columnName in historyColumnNames)
            {
                historyTable.Columns.Add(columnName);
            }

            // Get the history field indexes
            int[] historyFieldIndexes = HistorySchema(historyColumnNames);

            // Build the history data to return
            await QueuedTask.Run(() =>
            {
                try
                {
                    // Use a RowCursor to iterate through the selected features
                    using RowCursor rowCursor = _hluFeatureClass.Search(queryFilter, false);

                    // Loop through the selected features, until there are no more,
                    // to capture the history.
                    while (rowCursor.MoveNext())
                    {
                        // Get the current feature
                        using Row row = rowCursor.Current;

                        // Capture the history before modification
                        DataRow historyRow = historyTable.NewRow();
                        for (int i = 0; i < historyFieldIndexes.Length; i++)
                        {
                            historyRow[i] = row[historyFieldIndexes[i]] ?? DBNull.Value;
                        }
                        historyTable.Rows.Add(historyRow);
                    }
                }
                catch (Exception ex)
                {
                    // Preserve stack trace and wrap in a meaningful type
                    throw new HLUToolException("Error reading GIS features: " + ex.Message, ex);
                }
            });

            // Return the history table
            return historyTable;
        }

        public async Task UpdateFeaturesAsync(DataColumn[] updateColumns, object[] updateValues,
            DataColumn[] historyColumns, List<SqlFilterCondition> selectionWhereClause, EditOperation editOperation)
        {
            //TODO: Needed?
            // Ensure selection event handlers do not interfere
            //_selectFieldOrdinals = null;

            // Create a query filter for selecting features
            QueryFilter queryFilter = new()
            {
                WhereClause = WhereClause(false, false, false, MapWhereClauseFields(_hluLayerStructure, selectionWhereClause))
            };

            // Extract history column names
            string[] historyColumnNames = historyColumns.Select(c => c.ColumnName).ToArray();

            // Cast the feature class as a Table
            Table hluTable = _hluFeatureClass as Table;

            // Perform updates and return history data
            await QueuedTask.Run(() =>
            {
                try
                {
                    // Get update field indexes
                    List<int> updateFieldIndexes = updateColumns.Select(c => _hluFeatureClass.GetDefinition().FindField(c.ColumnName)).ToList();

                    // Execute within an EditOperation to support different data sources
                    editOperation.Callback(context =>
                    {
                        // Use a RowCursor to iterate through the selected features
                        using RowCursor rowCursor = _hluFeatureClass.Search(queryFilter, false);

                        // Loop through the selected features until there are no more
                        while (rowCursor.MoveNext())
                        {
                            // Get the current feature
                            using Row row = rowCursor.Current;

                            // Apply updates
                            for (int i = 0; i < updateFieldIndexes.Count; i++)
                            {
                                row[updateFieldIndexes[i]] = updateValues[i];
                            }

                            // Store the row.
                            row.Store();
                        }
                    }, hluTable);
                }
                catch (Exception ex)
                {
                    // Preserve stack trace and wrap in a meaningful type
                    throw new HLUToolException("Error updating GIS features: " + ex.Message, ex);
                }
            });
        }

        public DataTable UpdateFeatures(DataColumn[] updateColumns, object[] updateValues,
            DataColumn[] historyColumns, string tempMdbPathName, string selectionTableName)
        {
            //string delimiter = PipeFieldDelimiter.ToString();

            //try
            //{
            //    return ResultTableFromList(IpcArcMap(new string[] { "ub", tempMdbPathName, selectionTableName }
            //        .Concat(updateColumns.Select(c => c.ColumnName))
            //        .Concat([PipeTransmissionInterrupt])
            //        .Concat(updateValues.Select(o => o == null ? String.Empty : o.ToString()))
            //        .Concat([PipeTransmissionInterrupt])
            //        .Concat(historyColumns.Select(c => c.ColumnName)).ToArray()));
            //}
            //catch { throw; }
            return null;
        }

        #region History

        private int[] HistorySchema(string[] historyColumns)
        {
            int ix;
            var historyFields = from c in historyColumns
                                let ordinal = (ix = MapField(c)) != -1 ? ix : FuzzyFieldOrdinal(c)
                                where ordinal != -1
                                select new
                                {
                                    FieldOrdinal = ordinal,
                                    FieldName = c.Replace(HistoryAdditionalFieldsDelimiter, String.Empty)
                                };

            //for (int i = 0; i < historyFields.Count(); i++)
            //{
            //    var a = historyFields.ElementAt(i);
            //    _pipeData.Add(String.Format("{0}{1}{2}", a.FieldName, _pipeFieldDelimiter,
            //        _hluFieldSysTypeNames[a.FieldOrdinal]));
            //}

            //// GeometryColumn1: Length for polygons; length for polylines; X for points
            //_pipeData.Add(String.Format("{0}{1}System.Double", _historyGeometry1ColumnName, _pipeFieldDelimiter));

            //// GeometryColumn2: Area for polygons; empty for polylines; Y for points
            //_pipeData.Add(String.Format("{0}{1}System.Double", _historyGeometry2ColumnName, _pipeFieldDelimiter));

            //_pipeData.Add(_pipeTransmissionInterrupt);

            return historyFields.Select(hf => hf.FieldOrdinal).ToArray();
        }

        private string History(FeatureClass feature, int[] historyFieldOrdinals, string[] additionalValues)
        {
            StringBuilder history = new();

            //int j = 0;
            //foreach (int i in historyFieldOrdinals)
            //{
            //    history.Append(String.Format("{0}{1}", _pipeFieldDelimiter, i != -1 ?
            //        feature.get_Value(i) : additionalValues[j++]));
            //}

            //double geom1;
            //double geom2;
            //GetGeometryProperties(feature, out geom1, out geom2);

            //history.Append(String.Format("{0}{1}{0}{2}", _pipeFieldDelimiter,
            //    geom1 != -1 ? geom1.ToString() : String.Empty,
            //    geom2 != -1 ? geom2.ToString() : String.Empty));

            return history.Remove(0, 1).ToString();
        }

        private void GetGeometryProperties(FeatureClass feature, out double geom1, out double geom2)
        {
            geom1 = -1;
            geom2 = -1;
            //switch (feature.Shape.GeometryType)
            //{
            //    case esriGeometryType.esriGeometryPolygon:
            //        IArea area = feature.Shape as IArea;
            //        geom1 = ((IPolygon4)feature.Shape).Length;
            //        geom2 = area.Area;
            //        break;
            //    case esriGeometryType.esriGeometryPolyline:
            //        IPolyline5 pline = feature.Shape as IPolyline5;
            //        geom1 = pline.Length;
            //        break;
            //    case esriGeometryType.esriGeometryPoint:
            //        IPoint point = feature.Shape as IPoint;
            //        geom1 = point.X;
            //        geom2 = point.Y;
            //        break;
            //}
        }

        #endregion

        #region Fields

        private int MapField(string name)
        {
            name = name.Trim();
            int o;
            if ((o = FieldOrdinal(name)) != -1)
            {
                return o;
            }
            else if ((o = ColumnOrdinal(name)) != -1)
            {
                return FieldOrdinal(_hluLayerStructure.Columns[o].ColumnName);
            }
            return -1;
        }

        private int FieldOrdinal(string columnName)
        {
            int ordinal = -1;
            if ((_hluFieldMap != null) && (_hluLayerStructure != null) && !String.IsNullOrEmpty(columnName) &&
                ((ordinal = _hluLayerStructure.Columns.IndexOf(columnName.Trim())) != -1))

                return _hluFieldMap[ordinal];
            else
                return -1;
        }

        private int FieldOrdinal(int columnOrdinal)
        {
            if ((_hluFieldMap != null) && (columnOrdinal > -1) && (columnOrdinal < _hluFieldMap.Length))
                return _hluFieldMap[columnOrdinal];
            else
                return -1;
        }

        private int ColumnOrdinal(string fieldName)
        {
            if ((_hluFieldNames != null) && !String.IsNullOrEmpty((fieldName = fieldName.Trim())))
                return System.Array.IndexOf<string>(_hluFieldNames, fieldName);
            else
                return -1;
        }

        private int FuzzyFieldOrdinal(string fieldName)
        {
            return FieldOrdinal(FuzzyColumnOrdinal(fieldName));
        }

        private int FuzzyColumnOrdinal(string fieldName)
        {
            if ((_hluFieldNames != null) && !String.IsNullOrEmpty((fieldName = fieldName.Trim())))
            {
                var q = from c in _hluLayerStructure.Columns.Cast<DataColumn>()
                        join s in fieldName.Split([HistoryAdditionalFieldsDelimiter],
                            StringSplitOptions.RemoveEmptyEntries).Distinct() on c.ColumnName equals s
                        select c.Ordinal;
                if (q.Count() == 1) return q.First();
            }
            return -1;
        }

        #endregion Fields

        //TODO: Replace calls with ZoomSelectedAsync
        public void ZoomSelected(int minZoom, string distUnits, bool alwaysZoom)
        {
            //// Enable auto zoom when selecting features on map.
            //if (alwaysZoom)
            //    IpcArcMap(["zs", minZoom.ToString(), distUnits, "always"]);
            //else
            //    IpcArcMap(["zs", minZoom.ToString(), distUnits, "when"]);
        }

        /// <summary>
        /// Prompts the user for the export layer name.
        /// </summary>
        /// <param name="tempMdbPathName">Name of the temporary MDB path to save the
        /// temporary attribute data to.</param>
        /// <param name="attributeDatasetName">Name of the attribute dataset.</param>
        /// <param name="attributesLength">Length of the attribute data row.</param>
        /// <returns></returns>
        public bool ExportPrompt(string tempMdbPathName, string attributeDatasetName, int attributesLength, bool selectedOnly)
        {
            //List<string> returnList = IpcArcMap(
            //    ["ep", tempMdbPathName, attributeDatasetName]);

            //if ((returnList.Count > 0) && (returnList[0] == "cancelled"))
            //{
            //    // Display message if no output layer is entered by the user.
            //    MessageBox.Show("Export cancelled. No output table selected.",
            //        "HLU: Export", MessageBoxButton.OK, MessageBoxImage.Error);
            //    return false;
            //}
            //else if (returnList.Count > 0)
            //{
            //    MessageBox.Show(String.Format("The export operation failed. The Message returned was:\n\n{0}",
            //        returnList[0]), "HLU: Export", MessageBoxButton.OK, MessageBoxImage.Error);
            //    return false;
            //}
            //else
            //{
            //    return true;
            //}
            return false;
        }

        /// <summary>
        /// Exports the HLU features and attribute data to a new GIS layer file.
        /// </summary>
        /// <param name="tempMdbPathName">Name of the temporary MDB path containing the
        /// attribute data.</param>
        /// <param name="attributeDatasetName">Name of the attribute dataset.</param>
        /// <param name="selectedOnly">If set to <c>true</c> only selected features
        /// will be exported.</param>
        /// <returns></returns>
        public bool Export(string tempMdbPathName, string attributeDatasetName, bool selectedOnly)
        {
            //List<string> returnList = IpcArcMap(
            //    ["ex", tempMdbPathName, attributeDatasetName, (selectedOnly ? "true" : "false")]);

            //if ((returnList.Count > 0) && (returnList[0] == "cancelled"))
            //{
            //    // Display message if no output layer is entered by the user.
            //    MessageBox.Show("Export cancelled.", "HLU: Export",
            //        MessageBoxButton.OK, MessageBoxImage.Information);
            //    return true;
            //}
            //else if ((returnList.Count > 0) && (returnList[0] == "noselection"))
            //{
            //    // Display message if no selected features are found.
            //    MessageBox.Show("Export cancelled. No features selected.", "HLU: Export",
            //        MessageBoxButton.OK, MessageBoxImage.Exclamation);
            //    return true;
            //}
            //else if (returnList.Count > 0)
            //{
            //    MessageBox.Show(String.Format("The export operation failed. The Message returned was:\n\n{0}",
            //        returnList[0]), "HLU: Export", MessageBoxButton.OK, MessageBoxImage.Error);
            //    return false;
            //}
            //else
            //{
            //    return true;
            //}
            return false;
        }

        #region Private Methods

        //TODO: ArcGIS
        //private ITable CreateQueryTable(IWorkspace workspace, IQueryDef queryDef, String tableName)
        //{
        //    // create a reference to a TableQueryName object.
        //    IQueryName2 queryName2 = (IQueryName2)CreateArcObject<TableQueryNameClass>(Settings.Default.UseObjectFactory);
        //    queryName2.PrimaryKey = "";

        //    // specify the query definition.
        //    queryName2.QueryDef = queryDef;

        //    // get a name object for the workspace.
        //    IDataset dataset = (IDataset)workspace;
        //    IWorkspaceName workspaceName = (IWorkspaceName)dataset.FullName;

        //    // cast the TableQueryName object to the IDatasetName interface and open it.
        //    IDatasetName datasetName = (IDatasetName)queryName2;
        //    datasetName.WorkspaceName = workspaceName;
        //    datasetName.Name = tableName;
        //    IName name = (IName)datasetName;

        //    // open the name object and get a reference to a table object.
        //    ITable table = (ITable)name.Open();
        //    return table;
        //}

        private int[] OutputFieldOrdinals(DataTable resultTable)
        {
            int[] ordinals = new int[resultTable.Columns.Count];
            for (int i = 0; i < ordinals.Length; i++)
                ordinals[i] = GetFieldOrdinal(resultTable.Columns[i].ColumnName);
            return ordinals;
        }

        //TODO: ArcGIS
        //private void SelectionSetToTable(ISelectionSet selectionSet, ref DataTable resultTable)
        //{
        //    using (ComReleaser comReleaser = new())
        //    {
        //        ICursor resultCursor;
        //        selectionSet.Search(null, true, out resultCursor);
        //        comReleaser.ManageLifetime(resultCursor);
        //        int[] ordinals = OutputFieldOrdinals(resultTable);
        //        IRow selectRow;
        //        DataRow resultRow;
        //        while ((selectRow = resultCursor.NextRow()) != null)
        //        {
        //            resultRow = resultTable.NewRow();
        //            for (int i = 0; i < ordinals.Length; i++)
        //                resultRow[i] = selectRow.get_Value(ordinals[i]);
        //            resultTable.Rows.Add(resultRow);
        //            selectRow = resultCursor.NextRow();
        //        }
        //        resultCursor.Flush();
        //    }
        //}

        //TODO: ArcGIS
        //private void CursorToDataTable(ICursor cursor, ref DataTable resultTable)
        //{
        //    DataRow resultRow;
        //    IRow selectRow;
        //    using (ComReleaser comReleaser = new())
        //    {
        //        comReleaser.ManageLifetime(cursor);
        //        while ((selectRow = cursor.NextRow()) != null)
        //        {
        //            resultRow = resultTable.NewRow();
        //            for (int i = 0; i < selectRow.Fields.FieldCount; i++)
        //                resultRow[i] = selectRow.get_Value(i);
        //            resultTable.Rows.Add(resultRow);
        //            selectRow = cursor.NextRow();
        //        }
        //        cursor.Flush();
        //    }
        //}

        //TODO: ArcGIS
        //private void SelectedIDs(ISelectionSet selectionSet, ref DataTable resultTable)
        //{
        //    if ((selectionSet == null) || (selectionSet.Count == 0) || (resultTable == null) ||
        //        (resultTable.Columns[0].DataType != typeof(System.Int32))) return;

        //    DataRow resultRow;
        //    IEnumIDs selIDs = selectionSet.IDs;
        //    for (int i = 0; i < selectionSet.Count; i++)
        //    {
        //        resultRow = resultTable.NewRow();
        //        resultRow[0] = selIDs.Next();
        //        resultTable.Rows.Add(resultRow);
        //    }
        //}

        //TODO: ArcGIS
        //private DataTable SelectedIDsTable(ISelectionSet selectionSet)
        //{
        //    DataTable resultTable = new();

        //    if (selectionSet != null)
        //    {
        //        resultTable.Columns.Add(new DataColumn("OBJECTID", typeof(System.Int32)));
        //        SelectedIDs(selectionSet, ref resultTable);
        //    }
        //    return resultTable;
        //}

        //TODO: ArcGIS
        //private int[] SelectedIDs(ISelectionSet selectionSet)
        //{
        //    if ((selectionSet == null) || (!selectionSet.Any())) return [];

        //    int[] resultIDs = new int[selectionSet.Count()];
        //    IEnumIDs selIDs = selectionSet.IDs;
        //    for (int i = 0; i < resultIDs.Length; i++)
        //        resultIDs[i] = selIDs.Next();

        //    return resultIDs;
        //}

        //TODO: ArcGIS
        //private ISQLSyntax SQLSyntax
        //{
        //    get
        //    {
        //        if (_hluWSSqlSyntax != null)
        //        {
        //            return _hluWSSqlSyntax;
        //        }
        //        else if (_hluWS != null)
        //        {
        //            _hluWSSqlSyntax = (ISQLSyntax)_hluWS;
        //            return _hluWSSqlSyntax;
        //        }
        //        else
        //        {
        //            return null;
        //        }
        //    }
        //}

        //TODO: ArcGIS
        //private bool IsPredicateSupported(esriSQLPredicates predicate)
        //{
        //    if (SQLSyntax == null) return false;

        //    int supportedPredicates = SQLSyntax.GetSupportedPredicates();

        //    // cast the predicate value to an integer and use bitwise arithmetic to check for support.
        //    int predicateValue = (int)predicate;
        //    int supportedValue = predicateValue & supportedPredicates;

        //    return supportedValue > 0;
        //}

        //TODO: ArcGIS
        //private bool IsSQLClauseSupported(IWorkspace workspace, esriSQLClauses sqlClause)
        //{
        //    // cast workspace to the ISQLSyntax interface.
        //    ISQLSyntax sqlSyntax = (ISQLSyntax)workspace;

        //    // use a bitwise AND to check if the clause is supported.
        //    int supportedSQLClauses = sqlSyntax.GetSupportedClauses();
        //    int clauseCheck = supportedSQLClauses & (int)sqlClause;

        //    // if the result of a bitwise AND is greater than 0, the clause is supported.
        //    return (clauseCheck > 0);
        //}

        #endregion

        /// <summary>
        /// Units in which history reports polygon areas.  Defaults to squared linear unit of HLU layer.
        /// </summary>
        public AreaUnits AreaUnit
        {
            set
            {
                //TODO: ArcPro
                switch (value)
                {
                    case AreaUnits.SquareCentimeters:
                        //_unitArea = (int)esriSRUnit2Type.esriSRUnit_Centimeter;
                        _unitArea = ((int)LinearUnit.Centimeters.FactoryCode);
                        break;
                    //case AreaUnits.SquareChains:
                    //    _unitArea = (int)esriSRUnit2Type.esriSRUnit_InternationalChain;
                    //    break;
                    case AreaUnits.SquareFeet:
                        //_unitArea = (int)esriSRUnitType.esriSRUnit_Foot;
                        _unitArea = ((int)LinearUnit.Feet.FactoryCode);
                        break;
                    case AreaUnits.SquareInches:
                        //_unitArea = (int)esriSRUnit2Type.esriSRUnit_InternationalInch;
                        _unitArea = ((int)LinearUnit.Inches.FactoryCode);
                        break;
                    case AreaUnits.SquareKilometers:
                        //_unitArea = (int)esriSRUnitType.esriSRUnit_Kilometer;
                        _unitArea = ((int)LinearUnit.Kilometers.FactoryCode);
                        break;
                    //case AreaUnits.SquareLinks:
                    //    _unitArea = (int)esriSRUnit2Type.esriSRUnit_InternationalLink;
                    //    break;
                    case AreaUnits.SquareMeters:
                        //_unitArea = (int)esriSRUnitType.esriSRUnit_Meter;
                        _unitArea = ((int)LinearUnit.Meters.FactoryCode);
                        break;
                    case AreaUnits.SquareMiles:
                        //_unitArea = (int)esriSRUnit2Type.esriSRUnit_StatuteMile;
                        _unitArea = ((int)LinearUnit.Miles.FactoryCode);
                        break;
                    case AreaUnits.SquareMillimeters:
                        //_unitArea = (int)esriSRUnit2Type.esriSRUnit_Millimeter;
                        _unitArea = ((int)LinearUnit.Millimeters.FactoryCode);
                        break;
                    //case AreaUnits.SquareRods:
                    //    _unitArea = (int)esriSRUnit2Type.esriSRUnit_InternationalRod;
                    //    break;
                    //case AreaUnits.SquareSurveyFeet:
                    //    _unitArea = (int)esriSRUnitType.esriSRUnit_SurveyFoot;
                    //    break;
                    case AreaUnits.SquareYards:
                        //_unitArea = (int)esriSRUnit2Type.esriSRUnit_InternationalYard;
                        _unitArea = ((int)LinearUnit.Yards.FactoryCode);
                        break;
                }
            }
        }

        /// <summary>
        /// Units in which history reports polyline lengths and polygon perimeters. Defaults to linear unit of HLU layer.
        /// </summary>
        public DistanceUnits DistanceUnit
        {
            set
            {
                //TODO: ArcPro
                switch (value)
                {
                    case DistanceUnits.Centimeters:
                        //_unitDistance = (int)esriSRUnit2Type.esriSRUnit_Centimeter;
                        _unitDistance = ((int)LinearUnit.Centimeters.FactoryCode);
                        break;
                    //case DistanceUnits.Chains:
                    //    _unitDistance = (int)esriSRUnit2Type.esriSRUnit_InternationalChain;
                    //    break;
                    case DistanceUnits.Feet:
                        //_unitDistance = (int)esriSRUnitType.esriSRUnit_Foot;
                        _unitDistance = ((int)LinearUnit.Feet.FactoryCode);
                        break;
                    case DistanceUnits.Inches:
                        //_unitDistance = (int)esriSRUnit2Type.esriSRUnit_InternationalInch;
                        _unitDistance = ((int)LinearUnit.Inches.FactoryCode);
                        break;
                    case DistanceUnits.Kilometers:
                        //_unitDistance = (int)esriSRUnitType.esriSRUnit_Kilometer;
                        _unitDistance = ((int)LinearUnit.Kilometers.FactoryCode);
                        break;
                    //case DistanceUnits.Links:
                    //    _unitDistance = (int)esriSRUnit2Type.esriSRUnit_InternationalLink;
                    //    break;
                    case DistanceUnits.Meters:
                        //_unitDistance = (int)esriSRUnitType.esriSRUnit_Meter;
                        _unitDistance = ((int)LinearUnit.Meters.FactoryCode);
                        break;
                    case DistanceUnits.Miles:
                        //_unitDistance = (int)esriSRUnit2Type.esriSRUnit_StatuteMile;
                        _unitDistance = ((int)LinearUnit.Miles.FactoryCode);
                        break;
                    case DistanceUnits.Millimeters:
                        //_unitDistance = (int)esriSRUnit2Type.esriSRUnit_Millimeter;
                        _unitDistance = ((int)LinearUnit.Millimeters.FactoryCode);
                        break;
                    //case DistanceUnits.NauticalMiles:
                    //    _unitDistance = (int)esriSRUnitType.esriSRUnit_NauticalMile;
                    //    break;
                    //case DistanceUnits.SurveyFeet:
                    //    _unitDistance = (int)esriSRUnitType.esriSRUnit_SurveyFoot;
                    //    break;
                    case DistanceUnits.Yards:
                        //_unitDistance = (int)esriSRUnit2Type.esriSRUnit_InternationalYard;
                        _unitDistance = ((int)LinearUnit.Yards.FactoryCode);
                        break;
                }
            }
        }

        /// <summary>
        /// Maximum (nominal) allowable length of a SQL query.
        /// </summary>
        public int MaxSqlLength
        {
            get { return _maxSqlLength; }
        }

        public string HluLayerName
        {
            get { return _hluLayer?.Name; }
        }

        //TODO: ArcPro
        public async Task<string> IncidFieldNameAsync()
        {
            Field field = await GetFieldAsync(_hluLayerStructure.incidColumn.Ordinal);
            return field.Name;
        }

        /// <summary>
        /// The number of valid hlu layer namess.
        /// </summary>
        public int HluLayerCount
        {
            get { return _hluLayerNamesList?.Count ?? 0; }
        }

        /// <summary>
        /// The list of valid hlu layer names.
        /// </summary>
        public List<string> ValidHluLayerNames
        {
            get { return _hluLayerNamesList; }
        }

        /// <summary>
        /// The properties of the current hlu layer.
        /// </summary>
        public HLULayer CurrentHluLayer
        {
            get { return _hluCurrentLayer; }
        }

        //TODO: Is _hluFeatureClass Needed?
        /// <summary>
        /// Checks whether the current document contains an HLU layer. Also initializes the fields
        /// _hluView, _hluCurrentLayer, _hluLayer, _hluFeatureClass and _hluWS, and indirectly
        /// (by calling CreateFieldMap()), _hluFieldMap and _hluFieldNames.
        /// </summary>
        /// <returns>True if the current document contains a valid HLU layer, otherwise false.</returns>
        public async Task<bool> IsHluWorkspaceAsync(string currentLayerName)
        {
            // Backup current layer variables.
            FeatureLayer hluLayerBak = _hluLayer;
            string hluTableNameBak = _hluTableName;
            HLULayer hluCurrentLayerBak = _hluCurrentLayer;

            // Initialise or clear the list of valid layers.
            if (_hluLayerNamesList == null)
                _hluLayerNamesList = [];
            else
                _hluLayerNamesList.Clear();

            if (string.IsNullOrEmpty(currentLayerName))
                _hluLayer = null;

            //TODO: ArcGIS
            try
            {
                //TODO: Check whether QueuedTask is needed here
                // Get all of the feature layers in the active map view.
                //IEnumerable<FeatureLayer> featureLayers = GetFeatureLayers();
                IEnumerable<FeatureLayer> featureLayers = await QueuedTask.Run(() =>
                {
                    return GetFeatureLayers()?.ToList() ?? [];
                });

                // Loop through all of the feature layers.
                foreach(FeatureLayer layer in featureLayers)
                {
                    // Check if the feature layer a valid HLU layer.
                    if (await IsHluLayerAsync(layer, false))
                    {
                        // Add the layer to the list of valid layers.
                        string layerName = layer.Name;
                        _hluLayerNamesList.Add(layerName);

                        // Store the details of the first valid layer found
                        // if none already stored or there is no current
                        // layer.
                        if (_hluLayer == null)
                        {
                            //TODO: HLU variables
                            //_hluUidFieldOrdinals = hluUidFieldOrdinals;
                            //_hluFieldSysTypeNames = hluFieldSysTypeNames;
                            //_hluSqlSyntax = hluSqlSyntax;
                            //_quotePrefix = quotePrefix;
                            //_quoteSuffix = quoteSuffix;

                            //TODO: Workspace variables?
                            //_hluWS = (IFeatureWorkspace)((IDataset)_hluFeatureClass).Workspace;

                            //TODO: ArcGIS
                            //// Map the fields in the layer to the fields in the HLU layer structure.
                            //CreateFieldMap(7, 5, 3, retList);

                            _hluLayer = layer;
                            _hluTableName = layer.Name;
                            //TODO: Needed?
                            //_hluFeatureClass = _hluLayer.GetFeatureClass();
                            _hluCurrentLayer = new(layerName);
                        }

                        //break;
                    }
                }
            }
            catch { }

            // If the currentLayer is still found in the workspace
            // then restore the variables.
            if (!string.IsNullOrEmpty(currentLayerName) && _hluLayerNamesList.Contains(currentLayerName))
            {
                _hluLayer = hluLayerBak;
                _hluTableName = hluTableNameBak;
                _hluCurrentLayer = hluCurrentLayerBak;
            }

            if (_hluLayer != null)
            {
                return true;
            }
            else
            {
                DestroyHluLayer();
                return false;
            }
        }

        /// <summary>
        /// Determines which of the layers in all the maps are valid HLU layers
        /// and stores these in a list so the user can switch between them.
        /// Called before displaying the list of layers for the user to switch
        /// between.
        /// </summary>
        /// <returns>The number of valid HLU layers in the list</returns>
        public int ListHluLayers()
        {
            //if (_hluLayerStructure == null)
            //    _hluLayerStructure = new HluGISLayer.incid_mm_polygonsDataTable();

            //if (_hluLayerNamesList == null)
            //    _hluLayerNamesList = [];

            //try
            //{
            //    List<string> retList = IpcArcMap(["ll"]);
            //    if ((retList != null) && (retList.Count > 3))
            //    {
            //        if (Int32.Parse(retList[0]) > 0)
            //        {
            //            // Store the total number of map windows.
            //            _mapWindowsCount = Int32.Parse(retList[1]);

            //            // Split each layer into constituent parts and add them to the list
            //            // of valid layers.
            //            if (_hluLayerNamesList == null)
            //                _hluLayerNamesList = [];
            //            else
            //                _hluLayerNamesList.Clear();

            //            for (int i = 3; i < retList.Count; i++)
            //            {
            //                // Increment the map number by 1 so that it starts with 1 instead
            //                // of 0 to be more user-friendly when displayed.
            //                string[] layerParts = retList[i].ToString().Split(["::"], StringSplitOptions.None);
            //                _hluLayerNamesList.Add(new GISLayer(Int32.Parse(layerParts[0]) + 1, layerParts[1], Int32.Parse(layerParts[2]), layerParts[3]));
            //            }
            //        }
            //    }
            //    else
            //    {
            //        _hluCurrentLayer = null;
            //        return 0;
            //    }
            //}
            //catch { }

            //if (_hluCurrentLayer == null)
            //    _hluCurrentLayer = _hluLayerNamesList[0];
            //return _hluLayerNamesList.Count;
            return 0;
        }

        /// <summary>
        /// Populates field map from list returned by ArcMap through pipe.
        /// </summary>
        /// <param name="minLength">Minimum valid length of pipeReturnList (7 for workspace, 6 for layer).</param>
        /// <param name="skipElems">Number of elements of pipeReturnList to be skipped
        /// (5 for workspace, 4 for layer).</param>
        /// <param name="skipFirst">Number of elements to be skipped at the beginning of pipeReturnList
        /// (3 for workspace, 2 for layer).</param>
        /// <param name="pipeReturnList">List returned from pipe.</param>
        private void CreateFieldMap(int minLength, int skipElems, int skipFirst, List<string> pipeReturnList)
        {
            //TODO: ArcGIS
            //if ((pipeReturnList == null) || (pipeReturnList.Count < minLength) ||
            //    (pipeReturnList.Count % 2 != minLength % 2)) return;

            //int numFields = (pipeReturnList.Count - skipElems) / 2;

            //int limit = numFields + skipFirst + 1;

            //_hluFieldMap = pipeReturnList.Where((s, index) => index > skipFirst && index < limit )
            //    .Select(s => Int32.Parse(s)).ToArray();

            //_hluFieldNames = pipeReturnList.Where((s, index) => index > limit).ToArray();
        }

        private void DestroyHluLayer()
        {
            _hluFieldMap = null;
            _hluFieldNames = null;

            //TODO: Needed?
            //_hluFeatureClass = null;

            _hluLayer = null;
            _hluView = null;
        }

        //TODO: Is _hluFeatureClass Needed?
        /// <summary>
        /// Retrieves the name of the field of _hluFeatureClass that corresponds to the column of
        /// _hluLayerStructure whose ordinal is passed in.
        /// </summary>
        /// <param name="columnOrdinal">Ordinal of the column in _hluLayerStructure.</param>
        /// <returns>Name of the field of _hluFeatureClass corresponding to column _hluLayerStructure[columnOrdinal].</returns>
        protected string GetFieldName(int columnOrdinal)
        {
            if ((_hluFieldNames == null) || (_hluFieldMap == null) || (columnOrdinal < 0) ||
                (columnOrdinal > _hluFieldNames.Length - 1)) return null;
            else
                return _hluFieldNames[columnOrdinal];
        }

        //TODO: Is _hluFeatureClass Needed?
        /// <summary>
        /// Retrieves the ordinal of the field of _hluFeatureClass that corresponds to the column of
        /// _hluLayerStructure whose ordinal is passed in.
        /// </summary>
        /// <param name="columnName">Name of the column in _hluLayerStructure.</param>
        /// <returns>Ordinal of the field of _hluFeatureClass corresponding to column
        /// _hluLayerStructure.Columns[columnName].</returns>
        private int GetFieldOrdinal(string columnName)
        {
            if ((_hluFieldMap == null) || (_hluLayerStructure == null) ||
                String.IsNullOrEmpty(columnName)) return -1;
            int columnOrdinal = _hluLayerStructure.Columns[columnName.Trim()].Ordinal;
            if (columnOrdinal == -1)
                return -1;
            else
                return _hluFieldMap[columnOrdinal];
        }

        //TODO: Is _hluFeatureClass Needed?
        /// <summary>
        /// Retrieves the ordinal of the field of _hluFeatureClass that corresponds to the column of
        /// _hluLayerStructure whose ordinal is passed in.
        /// </summary>
        /// <param name="columnOrdinal">Ordinal of the column in _hluLayerStructure.</param>
        /// <returns>Ordinal of the field of _hluFeatureClass corresponding to column
        /// _hluLayerStructure.Columns[columnOrdinal].</returns>
        private int GetFieldOrdinal(int columnOrdinal)
        {
            if ((_hluFieldMap == null) || (_hluLayerStructure == null) ||
                (columnOrdinal < 0) || (columnOrdinal > _hluFieldMap.Length))
                return -1;
            else
                return _hluFieldMap[columnOrdinal];
        }

        //TODO: Is _hluFeatureClass Needed?
        /// <summary>
        /// Retrieves the field of _hluFeatureClass that corresponds to the column of _hluLayerStructure whose ordinal is passed in.
        /// </summary>
        /// <param name="columnOrdinal">Ordinal of the column in _hluLayerStructure.</param>
        /// <returns>The field of _hluFeatureClass corresponding to column _hluLayerStructure[columnOrdinal].</returns>
        private async Task<Field> GetFieldAsync(int columnOrdinal)
        {
            if ((_hluFeatureClass == null) || (_hluFieldMap == null) ||
                (columnOrdinal < 0) || (columnOrdinal >= _hluFieldMap.Length)) return null;
            int fieldOrdinal = _hluFieldMap[columnOrdinal];

            //TODO: ArcPro
            if (fieldOrdinal >= 0)
            {
                Field field = null;
                try
                {
                    await QueuedTask.Run(() =>
                    {
                        // Get the table definition of the table.
                        using FeatureClassDefinition hluFeatureClassDefinition = _hluFeatureClass.GetDefinition();

                        // Get the field count.
                        int fieldCnt = hluFeatureClassDefinition.GetFields().Count;

                        if (fieldOrdinal < fieldCnt)
                            field = hluFeatureClassDefinition.GetFields()[_hluFieldMap[columnOrdinal]];
                    });
                }
                catch
                {
                    return null;
                }

                return field;
                //return _hluFeatureClass.Fields.get_Field(_hluFieldMap[columnOrdinal]);
            }
            else
                return null;
        }

        /// <summary>
        /// Get a field position in a feature class by name.
        /// </summary>
        /// <param name="layerPath"></param>
        /// <param name="fieldName"></param>
        /// <returns>bool</returns>
        public async Task<int> GetFieldOrdinalAsync(string layerPath, string fieldName)
        {
            // Check there is an input feature featureLayer path.
            if (String.IsNullOrEmpty(layerPath))
                return -1;

            // Check there is an input field name.
            if (String.IsNullOrEmpty(fieldName))
                return -1;

            try
            {
                // Find the feature featureLayer by name if it exists. Only search existing layers.
                FeatureLayer featurelayer = await FindLayerAsync(layerPath);

                if (featurelayer == null)
                    return -1;

                int fieldOrd = -1;

                await QueuedTask.Run(() =>
                {
                    // Get the underlying feature class as a table.
                    using ArcGIS.Core.Data.Table table = featurelayer.GetTable();
                    if (table != null)
                    {
                        // Get the table definition of the table.
                        using TableDefinition tableDef = table.GetDefinition();

                        // Get the fields in the table.
                        IReadOnlyList<Field> fields = tableDef.GetFields();

                        // Loop through all fields looking for a name match.
                        int fieldNum = 0;
                        foreach (Field fld in fields)
                        {
                            if (fld.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase) ||
                                fld.AliasName.Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                            {
                                fieldOrd = fieldNum;
                                break;
                            }

                            fieldNum += 1;
                        }
                    }
                });

                return fieldOrd;
            }
            catch
            {
                // Handle Exception.
                return -1;
            }
        }

        /// <summary>
        /// Get a field position in a feature class by name, type and length.
        /// </summary>
        /// <param name="layerPath"></param>
        /// <param name="fieldName"></param>
        /// <returns>bool</returns>
        public async Task<int> GetFieldOrdinalAsync(FeatureLayer featurelayer, string fieldName, esriFieldType fieldType, int fieldMaxLength = 0)
        {
            // Check there is an input feature featureLayer.
            if (featurelayer == null)
                return -1;

            // Check there is an input field name.
            if (String.IsNullOrEmpty(fieldName))
                return -1;

            // Check there is an input field type.
            if (fieldType == 0)
                return -1;

            try
            {
                int fieldOrd = -1;

                await QueuedTask.Run(() =>
                {
                    // Get the underlying feature class as a table.
                    using ArcGIS.Core.Data.Table table = featurelayer.GetTable();
                    if (table != null)
                    {
                        // Get the table definition of the table.
                        using TableDefinition tableDef = table.GetDefinition();

                        // Get the fields in the table.
                        IReadOnlyList<Field> fields = tableDef.GetFields();

                        // Loop through all fields looking for a name match.
                        int fieldNum = 0;
                        foreach (Field fld in fields)
                        {
                            // Get the field names.
                            string fldName = fld.Name;
                            string fldAlias = fld.AliasName;

                            // Get the esri field type.
                            esriFieldType esriFldType = (esriFieldType)fld.FieldType;

                            // Get the field length.
                            int fldLength = 0;
                            if (fld.FieldType == FieldType.String)
                                fldLength = fld.Length;

                            if (((fldName.Equals(fieldName, StringComparison.OrdinalIgnoreCase) ||
                                fldAlias.Equals(fieldName, StringComparison.OrdinalIgnoreCase)))
                                && (esriFldType == fieldType)
                                && (fldLength == fieldMaxLength))
                            {
                                fieldOrd = fieldNum;
                                break;
                            }

                            fieldNum += 1;
                        }
                    }
                });

                return fieldOrd;
            }
            catch
            {
                // Handle Exception.
                return -1;
            }
        }

        //TODO: Is _hluFeatureClass Needed?
        /// <summary>
        /// Retrieves the column of _hluLayerStructure that corresponds to the field of _hluFeatureClass whose ordinal is passed in.
        /// </summary>
        /// <param name="fieldOrdinal">The ordinal of the field of _hluFeatureClass.</param>
        /// <returns>The column of _hluLayerStructure corresponding to the field with ordinal fieldOrdinal in _hluFeatureClass.</returns>
        private DataColumn GetColumn(int fieldOrdinal)
        {
            if ((_hluLayerStructure == null) || (_hluFieldMap == null) ||
                (fieldOrdinal <= 0) || (fieldOrdinal >= _hluFieldMap.Length)) return null;
            int columnOrdinal = System.Array.IndexOf(_hluFieldMap, fieldOrdinal);
            if (columnOrdinal != -1)
                return _hluLayerStructure.Columns[columnOrdinal];
            else
                return null;
        }

        //TODO: Is _hluFeatureClass Needed?
        //TODO: ArcPro
        /// <summary>
        /// Retrieves the column of _hluLayerStructure that corresponds to the field of _hluFeatureClass whose name is passed in.
        /// </summary>
        /// <param name="fieldName">The name of the field of _hluFeatureClass.</param>
        /// <returns>The column of _hluLayerStructure corresponding to the field named fieldName in _hluFeatureClass.</returns>
        private async Task<DataColumn> GetColumnAsync(string fieldName)
        {
            if ((_hluLayerStructure == null) || (_hluFieldMap == null) ||
                (_hluFeatureClass == null) || String.IsNullOrEmpty(fieldName)) return null;

            //TODO: ArcPro
            int fieldOrdinal = await GetFieldOrdinalAsync(_hluFeatureClass.GetName(), fieldName);
            //int fieldOrdinal = _hluFeatureClass.Fields.FindField(fieldName);

            if (fieldOrdinal == -1) return null;
            int columnOrdinal = System.Array.IndexOf(_hluFieldMap, fieldOrdinal);
            if ((columnOrdinal >= 0) && (columnOrdinal <= _hluLayerStructure.Columns.Count))
                return _hluLayerStructure.Columns[columnOrdinal];
            else
                return null;
        }

        private string FormatDate(DateTime value)
        {
            try
            {
                //DONE: Result is always false
                //if (value == null)
                //    return "NULL";
                //else
                return value.ToString(_dateFormatString);
            }
            catch { return value.ToString(); }
        }

        private string FormatNumber(double number)
        {
            return number.ToString(_numberFormatInfo);
        }

        private string FormatNumber(float number)
        {
            return number.ToString(_numberFormatInfo);
        }

        private void SetDefaults()
        {
            //TODO: ArcGIS
            //if (_hluWS == null) return;

            // ArcGIS expects decimal point regardless of regional settings
            _numberFormatInfo = new()
            {
                NumberDecimalSeparator = ".",
                NumberGroupSeparator = ""
            };

            //TODO: ArcGIS
            //IWorkspace ws = _hluWS as IWorkspace;

            //TODO: ArcGIS
            //switch (ws.WorkspaceFactory.GetClassID().Value.ToString())
            //{
            //    case "{DD48C96A-D92A-11D1-AA81-00C04FA33A15}":
            //        //[Datefield] = #mm-dd-yyyy hh:mm:ss# or [Datefield] = #mm-dd-yyyy# or [Datefield] = #yyyy/mm/dd#
            //        _dateLiteralPrefix = "#";
            //        _dateLiteralSuffix = "#";
            //        _dateFormatString = "yyyy-MM-dd HH:mm:ss"; // "MM-dd-yyyy HH:mm:ss";
            //        break;
            //    case "{71FE75F0-EA0C-4406-873E-B7D53748AE7E}":
            //        //"Datefield" = date 'yyyy-mm-dd hh:mm:ss' // File geodatabases support the use of a time in the date field
            //        _dateLiteralPrefix = "date '";
            //        _dateLiteralSuffix = "'";
            //        _dateFormatString = "yyyy-MM-dd HH:mm:ss";
            //        break;
            //    case "{A06ADB96-D95C-11D1-AA81-00C04FA33A15}":
            //        //"Datefield" = date 'yyyy-mm-dd' // Shapefiles and coverages do not support the use of time in a date field
            //        _dateLiteralPrefix = "date '";
            //        _dateLiteralSuffix = "'";
            //        _dateFormatString = "yyyy-MM-dd";
            //        break;
            //    case "{1D887452-D9F2-11D1-AA81-00C04FA33A15}":
            //        //"Datefield" = date 'yyyy-mm-dd' // Shapefiles and coverages do not support the use of time in a date field
            //        _dateLiteralPrefix = "date '";
            //        _dateLiteralSuffix = "'";
            //        _dateFormatString = "yyyy-MM-dd";
            //        break;
            //    case "{6DE812D2-9AB6-11D2-B0D7-0000F8780820}":
            //        //"Datefield" = date 'yyyy-mm-dd' // Shapefiles and coverages do not support the use of time in a date field
            //        _dateLiteralPrefix = "date '";
            //        _dateLiteralSuffix = "'";
            //        _dateFormatString = "yyyy-MM-dd";
            //        break;
            //    case "{D9B4FA40-D6D9-11D1-AA81-00C04FA33A15}":
            //        SetDefaultsSde(ws);
            //        break;
            //}
        }

        protected List<SqlFilterCondition> MapWhereClauseFields(
            HluGISLayer.incid_mm_polygonsDataTable _hluLayerStructure, List<SqlFilterCondition> whereClause)
        {
            List<SqlFilterCondition> outWhereClause = [];
            for (int i = 0; i < whereClause.Count; i++)
            {
                SqlFilterCondition cond = whereClause[i];
                if (!_hluLayerStructure.Columns.Contains(cond.Column.ColumnName))
                {
                    if ((!String.IsNullOrEmpty(cond.CloseParentheses)) && (outWhereClause.Count > 0))
                    {
                        SqlFilterCondition condPrev = outWhereClause[outWhereClause.Count - 1];
                        condPrev.CloseParentheses += cond.CloseParentheses;
                        outWhereClause[outWhereClause.Count - 1] = condPrev;
                    }
                    if ((!String.IsNullOrEmpty(cond.OpenParentheses)) && (i < whereClause.Count - 1))
                    {
                        SqlFilterCondition condNext = whereClause[i + 1];
                        condNext.OpenParentheses += cond.OpenParentheses;
                        whereClause[i + 1] = condNext;
                    }
                    continue;
                }
                string columnName = GetFieldName(_hluLayerStructure.Columns[cond.Column.ColumnName].Ordinal);
                if (!String.IsNullOrEmpty(columnName))
                {
                    cond.Column = new DataColumn(columnName, cond.Column.DataType);
                    outWhereClause.Add(cond);
                }
            }
            return outWhereClause;
        }

        #region SDE

        //TODO: ArcGIS
        //private void SetDefaultsSde(IWorkspace ws)
        //{
        //    Int32 SE_RETURN = 0;

        //    SdeDLL[] sdeLibs = null;

        //    try
        //    {
        //        IPropertySet propSet = ws.ConnectionProperties;
        //        object propNames, outPropVals;
        //        propSet.GetAllProperties(out propNames, out outPropVals);
        //        List<string> propNamesList = new((string[])propNames);
        //        object[] propValsArray = (object[])outPropVals;

        //        propNamesList.ForEach(delegate (string pn) { pn = pn.ToUpper(); });

        //        int ix = propNamesList.IndexOf("SERVER");
        //        string server = ix != -1 ? propValsArray[ix].ToString() : String.Empty;

        //        ix = propNamesList.IndexOf("INSTANCE");
        //        string instance = ix != -1 ? propValsArray[ix].ToString() : String.Empty;

        //        ix = propNamesList.IndexOf("DATABASE");
        //        string database = ix != -1 ? propValsArray[ix].ToString() : String.Empty;

        //        ix = propNamesList.IndexOf("USERNAME");
        //        if (ix == -1) ix = propNamesList.IndexOf("USER");
        //        string username = ix != -1 ? propValsArray[ix].ToString() : String.Empty;

        //        ix = propNamesList.IndexOf("PASSWORD");
        //        string password = ix != -1 ? propValsArray[ix].ToString() : String.Empty;

        //        sdeLibs = ExtractSDE();

        //        SE_Error seConnError = new();
        //        SE_Connection connection = new();
        //        if ((SE_RETURN = SE_connection_create(server, instance, database, username, password, ref seConnError,
        //            ref connection)) != SE_SUCCESS) throw (new Exception(Enum.GetName(typeof(sdeError), SE_RETURN)));

        //        Int32 dbms_id = Int32.MinValue;
        //        Int32 dbms_properties = Int32.MinValue;
        //        SE_connection_get_dbms_info(connection.handle, ref dbms_id, ref dbms_properties);

        //        SE_connection_free(connection.handle);

        //        switch ((SE_DBMS)dbms_id)
        //        {
        //            case SE_DBMS.SE_DBMS_IS_INFORMIX:
        //                // Datefield = 'yyyy-mm-dd hh:mm:ss' // hh:mm:ss part cannot be omitted even if it's equal to 00:00:00. 
        //                _dateLiteralPrefix = "'";
        //                _dateLiteralSuffix = "'";
        //                _dateFormatString = "yyyy-MM-dd HH:mm:ss";
        //                break;
        //            case SE_DBMS.SE_DBMS_IS_ORACLE:
        //                // Datefield = date 'yyyy-mm-dd' // this will not return records where the time is not null.
        //                // Datefield = TO_DATE('yyyy-mm-dd hh:mm:ss','YYYY-MM-DD HH24:MI:SS')
        //                // Datefield = TO_DATE('2003-01-08 14:35:00','YYYY-MM-DD HH24:MI:SS')
        //                // Datefield = TO_DATE('2003-11-18','YYYY-MM-DD') // this will not return records where the time is not null.
        //                _dateLiteralPrefix = " TO_DATE('";
        //                _dateLiteralSuffix = "','YYYY-MM-DD HH24:MI:SS')";
        //                _dateFormatString = "yyyy-MM-dd HH:mm:ss";
        //                break;
        //            case SE_DBMS.SE_DBMS_IS_SQLSERVER:
        //                // Datefield = 'yyyy-mm-dd hh:mm:ss' // hh:mm:ss part can be omitted when the time is not set in the records. 
        //                // Datefield = 'mm/dd/yyyy'
        //                _dateLiteralPrefix = "'";
        //                _dateLiteralSuffix = "'";
        //                _dateFormatString = "yyyy-MM-dd HH:mm:ss";
        //                break;
        //            case SE_DBMS.SE_DBMS_IS_DB2:
        //            case SE_DBMS.SE_DBMS_IS_DB2_EXT:
        //                // Datefield = TO_DATE('yyyy-mm-dd hh:mm:ss','YYYY-MM-DD HH24:MI:SS') // hh:mm:ss part cannot be omitted even if the time is equal to 00:00:00. 
        //                _dateLiteralPrefix = " TO_DATE('";
        //                _dateLiteralSuffix = "','YYYY-MM-DD HH24:MI:SS')"; // assumes 24h format, use CultureInfo ??
        //                _dateFormatString = "yyyy-MM-dd HH:mm:ss";
        //                break;
        //            case SE_DBMS.SE_DBMS_IS_OTHER: // guessing PostgreSQL
        //            case SE_DBMS.SE_DBMS_IS_UNKNOWN:
        //                //Datefield = TIMESTAMP 'YYYY-MM-DD HH24:MI:SS'
        //                //Datefield = TIMESTAMP 'YYYY-MM-DD' // must specify full time stamp when using "=" queries, not with "<" or ">".
        //                _dateLiteralPrefix = "TIMESTAMP '";
        //                _dateLiteralSuffix = "'";
        //                _dateFormatString = "yyyy-MM-dd HH:mm:ss";
        //                break;
        //            case SE_DBMS.SE_DBMS_IS_JET:
        //                //[Datefield] = #mm-dd-yyyy hh:mm:ss# or [Datefield] = #mm-dd-yyyy# or [Datefield] = #yyyy/mm/dd#
        //                _dateLiteralPrefix = "#";
        //                _dateLiteralSuffix = "#";
        //                _dateFormatString = "yyyy-MM-dd HH:mm:ss";
        //                break;
        //            default:
        //                //"Datefield" = date 'yyyy-mm-dd'
        //                _dateLiteralPrefix = " date '";
        //                _dateLiteralSuffix = "'";
        //                _dateFormatString = "yyyy-MM-dd";
        //                break;
        //        }
        //    }
        //    catch // (Exception ex)
        //    {
        //        _dateLiteralPrefix = "date '";
        //        _dateLiteralSuffix = "'";
        //        //MessageBox.Show(SE_RETURN != 0 ? String.Format("There was an error trying to obtain the correct date format from" +
        //        //    " the SDE server.{0}The error code returned from the server was:{0}{0}{1}.{0}{0}Using default SDE date format.",
        //        //    Environment.NewLine, ex.Message) : ex.Message, "SDE Error", MessageBoxButton.OK, MessageBoxImage.Error);
        //    }
        //    finally
        //    {
        //        if (sdeLibs != null)
        //        {
        //            for (int i = sdeLibs.Length - 1; i > -1; i--)
        //            {
        //                if ((sdeLibs[i].LibHandle != IntPtr.Zero) &&
        //                    WinAPI.FreeLibrary(sdeLibs[i].LibHandle) && File.Exists(sdeLibs[i].LibPath))
        //                {
        //                    File.Delete(sdeLibs[i].LibPath);
        //                }
        //            }
        //        }
        //    }
        //}

        //private SdeDLL[] ExtractSDE()
        //{
        //    string sdeLibPrefix = "HLU.GISApplication.lib";
        //    // sde DLLs in order of dependency, i.e., main DLL last
        //    SdeDLL[] sdeLibs =
        //    [
        //        new SdeDLL("pe.dll", sdeLibPrefix),
        //        new SdeDLL("sg.dll", sdeLibPrefix),
        //        new SdeDLL("sde.dll", sdeLibPrefix),
        //    ];
        //    try
        //    {
        //        Process p = Process.GetCurrentProcess();
        //        ProcessModule[] pms = null;
        //        if ((p != null) && ((pms = p.Modules.Cast<ProcessModule>().Where(pm => pm.ModuleName
        //            .Equals(sdeLibs[sdeLibs.Length - 1].LibName, StringComparison.CurrentCultureIgnoreCase)).ToArray()).Length > 0))
        //        {
        //            return null;
        //        }

        //        int pid;
        //        WinAPI.GetWindowThreadProcessId(_arcMapWindow, out pid);
        //        Process _arcProcess = Process.GetProcessById(pid);
        //        string arcDirName = System.IO.Path.GetDirectoryName(_arcProcess.MainModule.FileName);
        //        string tmpDirName = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HLUTool" +
        //            Assembly.GetExecutingAssembly().GetName().Version.ToString());

        //        for (int i = 0; i < sdeLibs.Length; i++)
        //        {
        //            if (!pms.Any(pm => pm.ModuleName.Equals(sdeLibs[i].LibName, StringComparison.CurrentCultureIgnoreCase)))
        //            {
        //                sdeLibs[i].LibPath = System.IO.Path.Combine(arcDirName, sdeLibs[i].LibName);
        //                if (!File.Exists(sdeLibs[i].LibPath))
        //                {
        //                    sdeLibs[i].LibPath = System.IO.Path.Combine(tmpDirName, sdeLibs[i].LibName);
        //                    if (!Directory.Exists(tmpDirName)) Directory.CreateDirectory(tmpDirName);
        //                    sdeLibs[i].LibPath = ExtractDLL(sdeLibs[i], tmpDirName);
        //                }
        //                sdeLibs[i].LibHandle = WinAPI.LoadLibrary(sdeLibs[i].LibPath);
        //            }
        //        }
        //    }
        //    catch { }
        //    return sdeLibs;
        //}

        //private struct SdeDLL
        //{
        //    public IntPtr LibHandle;
        //    public string ResourceName;
        //    public string LibName;
        //    public string LibPath;

        //    public SdeDLL(string dllName, string resourcePrefix)
        //    {
        //        LibHandle = IntPtr.Zero;
        //        ResourceName = (!String.IsNullOrEmpty(resourcePrefix) ? resourcePrefix +
        //            (!resourcePrefix.EndsWith('.') ? "." : String.Empty) : String.Empty) + dllName;
        //        LibName = dllName;
        //        LibPath = null;
        //    }
        //}

        //private static string ExtractDLL(SdeDLL lib, string extractDir)
        //{
        //    string dllPath = null;
        //    using (Stream sm = Assembly.GetExecutingAssembly().GetManifestResourceStream(lib.ResourceName))
        //    {
        //        try
        //        {
        //            dllPath = System.IO.Path.Combine(extractDir, lib.LibName);
        //            using (Stream outFile = File.Create(dllPath))
        //            {
        //                const int sz = 4096;
        //                byte[] buf = new byte[sz];
        //                while (true)
        //                {
        //                    int bytesRead = sm.Read(buf, 0, sz);
        //                    if (bytesRead < 1) break;
        //                    outFile.Write(buf, 0, bytesRead);
        //                }
        //            }
        //        }
        //        catch { }
        //    }
        //    return dllPath;
        //}

        //private enum SE_DBMS : int
        //{
        //    SE_DBMS_IS_UNKNOWN = -1,
        //    SE_DBMS_IS_OTHER = 0,
        //    SE_DBMS_IS_ORACLE = 1,
        //    SE_DBMS_IS_INFORMIX = 2,
        //    SE_DBMS_IS_SYBASE = 3,
        //    SE_DBMS_IS_DB2 = 4,
        //    SE_DBMS_IS_SQLSERVER = 5,
        //    SE_DBMS_IS_ARCINFO = 6,
        //    SE_DBMS_IS_IUS = 7,
        //    SE_DBMS_IS_DB2_EXT = 8,
        //    SE_DBMS_IS_ARCSERVER = 9,
        //    SE_DBMS_IS_JET = 10
        //};

        //private const Int32 SE_SUCCESS = 0;

        //[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        //public struct SE_Connection
        //{
        //    public Int32 handle;
        //}

        //[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        //public struct SE_Error
        //{
        //    public Int32 sde_error;
        //    public Int32 ext_error;
        //    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
        //    public char[] err_msg1;
        //    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4096)]
        //    public char[] err_msg2;
        //}

        //[DllImport("sde.dll")]
        //private static extern Int32 SE_connection_get_dbms_info(Int32 hSDE_Connection,
        //    ref Int32 dbms_id, ref Int32 dbms_properties);

        //[DllImport("sde.dll", SetLastError = true, ThrowOnUnmappableChar = true)]
        //public static extern Int32 SE_connection_create(string server, string instance,
        //    string database, string username, string password, ref SE_Error error, ref SE_Connection conn);

        //[DllImport("sde.dll")]
        //private static extern void SE_connection_free(Int32 hSDE_Connection);

        #endregion

        //TODO: ArcGIS
        //private bool OpenMapDocument(string path, string title)
        //{
        //    if (_arcMap == null) return false;

        //    try
        //    {
        //        if (!File.Exists(path))
        //        {
        //            OpenFileDialog openFileDlg = new()
        //            {
        //                Filter = "ESRI ArcMap Documents (*.mxd)|*.mxd",
        //                Title = title,
        //                CheckPathExists = true,
        //                CheckFileExists = true,
        //                ValidateNames = true,
        //                Multiselect = false,
        //                RestoreDirectory = false,
        //                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        //            };

        //            _arcMap.Visible = false;

        //            if (openFileDlg.ShowDialog() == true)
        //            {
        //                path = openFileDlg.FileName;
        //                Settings.Default.MapPath = path;

        //                // For some reason the HLU layer does not display in the map
        //                // window (although it appears in the contents list and the
        //                // attribute table can be opened) if the application is not set
        //                // to visible again before opening the document.
        //                _arcMap.Visible = true;
        //            }
        //            else
        //            {
        //                return false;
        //            }
        //        }

        //        _arcMap.OpenDocument(path);

        //        return true;
        //    }
        //    catch { return false; }
        //    finally { _arcMap.Visible = true; }
        //}

        //TODO: ArcGIS
        //private IEnumLayer Layers(IMap map)
        //{
        //    if (map == null) return null;

        //    UID uid = new UIDClass();
        //    uid.Value = typeof(IFeatureLayer).GUID.ToString("B");
        //    return map.get_Layers(uid, true);
        //}

        //TODO: ArcGIS
        //private IMaps Maps(IApplication app)
        //{
        //    if (app == null)
        //        return null;
        //    else
        //        return ((IMxDocument)app.Document).Maps;
        //}

        //TODO: ArcGIS
        //private object CreateArcObject<T>(bool useObjectFactory)
        //    where T : new()
        //{
        //    if (_arcMap == null) return default(T);

        //    if (useObjectFactory)
        //    {
        //        if (_objectFactory == null) _objectFactory = (IObjectFactory)_arcMap;
        //        string typeClsID = typeof(T).GUID.ToString("B");
        //        return _objectFactory.Create(typeClsID);
        //    }
        //    else
        //    {
        //        return new T();
        //    }
        //}

        //TODO: appROTEvent
        //private void appROTEvent_AppRemoved(AppRef app)
        //{
        //    if ((app is IMxApplication) && (new IntPtr(app.hWnd) == hWnd))
        //    {
        //        _objectFactory = null;
        //        _arcMap = null;
        //        _pipeName = null;
        //        DestroyHluLayer();

        //        MessageBoxResult userResponse = MessageBox.Show("ArcMap was unexpectedly closed.",
        //            "ArcMap Closed", MessageBoxButton.OK, MessageBoxImage.Exclamation);
        //    }
        //}

        //TODO: appROTEvent
        //private void appROTEvent_AppAdded(AppRef app)
        //{
        //    if ((app is IMxApplication) && (_arcMap == null))
        //    {
        //        _arcMap = (IApplication)app;
        //        _objectFactory = (IObjectFactory)_arcMap;
        //        _pipeName = String.Format("{0}.{1}", PipeBaseName, _arcMap.hWnd);
        //    }
        //}

        //TODO: Is _hluFeatureClass Needed?
        //TODO: ArcGIS
        ///// <summary>
        ///// Retrieves the field of _hluFeatureClass that corresponds to the column of _hluLayerStructure whose name is passed in.
        ///// </summary>
        ///// <param name="columnName">Name of the column of _hluLayerStructure.</param>
        ///// <returns>The field of _hluFeatureClass corresponding to column _hluLayerStructure[columnName].</returns>
        //public static IField GetField(string columnName, IFeatureClass _hluFeatureClass,
        //    HluGISLayer.incid_mm_polygonsDataTable _hluLayerStructure, int[] _hluFieldMap)
        //{
        //    if ((_hluLayerStructure == null) || (_hluFieldMap == null) ||
        //        (_hluFeatureClass == null) || String.IsNullOrEmpty(columnName)) return null;
        //    DataColumn c = _hluLayerStructure.Columns[columnName.Trim()];
        //    if ((c == null) || (c.Ordinal >= _hluFieldMap.Length)) return null;
        //    int fieldOrdinal = _hluFieldMap[c.Ordinal];
        //    if ((fieldOrdinal >= 0) && (fieldOrdinal <= _hluFieldMap.Length))
        //        return _hluFeatureClass.Fields.get_Field(fieldOrdinal);
        //    else
        //        return null;
        //}

        //TODO: ArcGIS
        //public static string WhereClauseFromCursor(int oidOrdinalCursor, string oidColumnAlias, ICursor cursor)
        //{
        //    StringBuilder sbIDs = new();
        //    StringBuilder sbBetween = new();
        //    string betweenTemplate = " OR (" + oidColumnAlias + " BETWEEN {0} AND {1})";
        //    int currOid = -1;
        //    int nextOid = -1;
        //    int countContinuous = 0;
        //    IRow row = cursor.NextRow();

        //    while (row != null)
        //    {
        //        currOid = (int)row.get_Value(oidOrdinalCursor);
        //        nextOid = currOid;
        //        countContinuous = 1;
        //        do
        //        {
        //            row = cursor.NextRow();
        //            if (row != null)
        //            {
        //                nextOid = (int)row.get_Value(oidOrdinalCursor);
        //                if (nextOid != currOid + countContinuous)
        //                    break;
        //                else
        //                    countContinuous++;
        //            }
        //            else
        //            {
        //                break;
        //            }
        //        }
        //        while (true);
        //        switch (countContinuous)
        //        {
        //            case 1:
        //                sbIDs.Append(',').Append(currOid);
        //                break;
        //            case 2:
        //                sbIDs.Append(',').Append(currOid);
        //                if (nextOid != currOid) sbIDs.Append(',').Append(nextOid);
        //                break;
        //            default:
        //                sbBetween.Append(String.Format(betweenTemplate, currOid, currOid + countContinuous - 1));
        //                break;
        //        }
        //    }

        //    if (sbIDs.Length > 1) sbIDs.Remove(0, 1).Insert(0, oidColumnAlias + " IN (").Append(')');
        //    return sbIDs.Append(sbBetween).ToString();
        //}

        //TODO: ArcGIS
        //public static IQueryDef CreateQueryDef(IFeatureWorkspace featureWorkspace,
        //    String tables, String subFields, String whereClause)
        //{
        //    // Create the query definition.
        //    IQueryDef queryDef = featureWorkspace.CreateQueryDef();

        //    // Provide a list of table(s) to join.
        //    queryDef.Tables = tables;

        //    // Declare the subfields to retrieve.
        //    queryDef.SubFields = subFields; // must be qualified if multiple tables !!

        //    // Assign a where clause to filter the results.
        //    queryDef.WhereClause = whereClause;

        //    return queryDef;
        //}

        //TODO: ArcGIS
        public static void GetTypeMaps(out Dictionary<Type, int> _typeMapSystemToSQL,
            out Dictionary<int, Type> _typeMapSQLToSystem)
        {
            _typeMapSystemToSQL = [];
            _typeMapSystemToSQL.Add(typeof(System.String), (int)esriFieldType.esriFieldTypeString);
            _typeMapSystemToSQL.Add(typeof(System.Decimal), (int)esriFieldType.esriFieldTypeSingle);
            _typeMapSystemToSQL.Add(typeof(System.Int64), (int)esriFieldType.esriFieldTypeInteger);
            _typeMapSystemToSQL.Add(typeof(System.Int32), (int)esriFieldType.esriFieldTypeSmallInteger);
            _typeMapSystemToSQL.Add(typeof(System.Int16), (int)esriFieldType.esriFieldTypeSmallInteger);
            _typeMapSystemToSQL.Add(typeof(System.Boolean), (int)esriFieldType.esriFieldTypeSmallInteger);
            _typeMapSystemToSQL.Add(typeof(System.Single), (int)esriFieldType.esriFieldTypeSingle);
            _typeMapSystemToSQL.Add(typeof(System.Double), (int)esriFieldType.esriFieldTypeDouble);
            _typeMapSystemToSQL.Add(typeof(System.DateTime), (int)esriFieldType.esriFieldTypeDate);

            _typeMapSQLToSystem = [];
            _typeMapSQLToSystem.Add((int)esriFieldType.esriFieldTypeBlob, typeof(System.Byte[]));
            _typeMapSQLToSystem.Add((int)esriFieldType.esriFieldTypeDate, typeof(System.DateTime));
            _typeMapSQLToSystem.Add((int)esriFieldType.esriFieldTypeDouble, typeof(System.Double));
            _typeMapSQLToSystem.Add((int)esriFieldType.esriFieldTypeGeometry, typeof(System.Byte[]));
            _typeMapSQLToSystem.Add((int)esriFieldType.esriFieldTypeGlobalID, typeof(System.Guid));
            _typeMapSQLToSystem.Add((int)esriFieldType.esriFieldTypeGUID, typeof(System.Guid));
            _typeMapSQLToSystem.Add((int)esriFieldType.esriFieldTypeInteger, typeof(System.Int64));
            _typeMapSQLToSystem.Add((int)esriFieldType.esriFieldTypeOID, typeof(System.Int64));
            _typeMapSQLToSystem.Add((int)esriFieldType.esriFieldTypeRaster, typeof(System.Byte[]));
            _typeMapSQLToSystem.Add((int)esriFieldType.esriFieldTypeSingle, typeof(System.Single));
            _typeMapSQLToSystem.Add((int)esriFieldType.esriFieldTypeSmallInteger, typeof(System.Int32));
            _typeMapSQLToSystem.Add((int)esriFieldType.esriFieldTypeString, typeof(System.String));
            _typeMapSQLToSystem.Add((int)esriFieldType.esriFieldTypeXML, typeof(System.String));
        }
    }
}
