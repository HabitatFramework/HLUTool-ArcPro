// The DataTools are a suite of ArcGIS Pro addins used to extract, sync
// and manage biodiversity information from ArcGIS Pro and SQL Server
// based on pre-defined or user specified criteria.
//
// Copyright © 2024 Andy Foy Consulting.
//
// This file is part of DataTools suite of programs..
//
// DataTools are free software: you can redistribute it and/or modify
// them under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// DataTools are distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with with program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Windows;
using System.Xml;
using MessageBox = ArcGIS.Desktop.Framework.Dialogs.MessageBox;

//This configuration file reader loads all of the variables to
// be used by the tool. Some are mandatory, the remainder optional.

namespace HLU
{
    /// <summary>
    /// This class reads the config XML file and stores the results.
    /// </summary>
    internal class DataSyncConfig
    {
        #region Fields

        private static string _toolName;

        // Initialise component to read XML
        private readonly XmlElement _xmlDataSync;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Load the XML profile and read the variables.
        /// </summary>
        /// <param name="xmlFile"></param>
        /// <param name="toolName"></param>
        /// <param name="msgErrors"></param>
        public DataSyncConfig(string xmlFile, string toolName, bool msgErrors)
        {
            _toolName = toolName;

            // The user has specified the xmlFile and we've checked it exists.
            _xmlFound = true;
            _xmlLoaded = true;

            // Load the XML file into memory.
            XmlDocument xmlConfig = new();
            try
            {
                xmlConfig.Load(xmlFile);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading XML file. " + ex.Message, _toolName, MessageBoxButton.OK, MessageBoxImage.Error);
                _xmlLoaded = false;
                return;
            }

            // Get the InitialConfig node (the first node).
            XmlNode currNode = xmlConfig.DocumentElement.FirstChild;
            _xmlDataSync = (XmlElement)currNode;

            if (_xmlDataSync == null)
            {
                MessageBox.Show("Error loading XML file.", _toolName, MessageBoxButton.OK, MessageBoxImage.Error);
                _xmlLoaded = false;
                return;
            }

            // Get the mandatory variables.
            try
            {
                if (!GetMandatoryVariables())
                    return;
            }
            catch (Exception ex)
            {
                // Only report message if user was prompted for the XML
                // file (i.e. the user interface has already loaded).
                if (msgErrors)
                    MessageBox.Show("Error loading XML file. " + ex.Message, _toolName, MessageBoxButton.OK, MessageBoxImage.Error);
                _xmlLoaded = false;
                return;
            }
        }

        #endregion Constructor

        #region Get Mandatory Variables

        /// <summary>
        /// Get the mandatory variables from the XML file.
        /// </summary>
        /// <returns></returns>
        public bool GetMandatoryVariables()
        {
            string rawText;

            // The existing file location where log files will be saved with output messages.
            try
            {
                _logFilePath = _xmlDataSync["LogFilePath"].InnerText;
            }
            catch
            {
                throw new("Could not locate item 'LogFilePath' in the XML profile.");
            }

            // The location of the SDE file that specifies which SQL Server database to connect to.
            try
            {
                _sdeFile = _xmlDataSync["SDEFile"].InnerText;
            }
            catch
            {
                throw new("Could not locate item 'SDEFile' in the XML profile.");
            }

            // The schema used in the SQL Server database.
            try
            {
                _databaseSchema = _xmlDataSync["DatabaseSchema"].InnerText;
            }
            catch
            {
                throw new("Could not locate item 'DatabaseSchema' in the XML profile.");
            }

            // The stored procedure to compare the local layer and remote table in SQL Server.
            try
            {
                _compareStoredProcedure = _xmlDataSync["CompareStoredProcedure"].InnerText;
            }
            catch
            {
                throw new("Could not locate item 'CompareStoredProcedure' in the XML profile.");
            }

            // The stored procedure to update the remote table in SQL Server.
            try
            {
                _updateStoredProcedure = _xmlDataSync["UpdateStoredProcedure"].InnerText;
            }
            catch
            {
                throw new("Could not locate item 'UpdateStoredProcedure' in the XML profile.");
            }

            // The stored procedure to clear the temporary tables in SQL Server.
            try
            {
                _clearStoredProcedure = _xmlDataSync["ClearStoredProcedure"].InnerText;
            }
            catch
            {
                throw new("Could not locate item 'ClearStoredProcedure' in the XML profile.");
            }

            // The name of the local layer in GIS containing the features.
            try
            {
                _localLayer = _xmlDataSync["LocalLayer"].InnerText;
            }
            catch
            {
                throw new("Could not locate item 'LocalLayer' in the XML profile.");
            }

            // The local layer selection where clause (if required).
            try
            {
                _localClause = _xmlDataSync["LocalClause"].InnerText;
            }
            catch
            {
                throw new("Could not locate item 'LocalClause' in the XML profile.");
            }

            // The name of the remote table in SQL Server containing the remote features.
            try
            {
                _remoteTable = _xmlDataSync["RemoteTable"].InnerText;
            }
            catch
            {
                throw new("Could not locate item 'RemoteTable' in the XML profile.");
            }

            // The remote table selection where clause (if required).
            try
            {
                _remoteClause = _xmlDataSync["RemoteClause"].InnerText;
            }
            catch
            {
                throw new("Could not locate item 'RemoteClause' in the XML profile.");
            }

            // The name of the layer in GIS displaying the remote features from SQL Server.
            try
            {
                _remoteLayer = _xmlDataSync["RemoteLayer"].InnerText;
            }
            catch
            {
                throw new("Could not locate item 'RemoteLayer' in the XML profile.");
            }

            // The name of the key column in the local layer and remote table.
            try
            {
                _keyColumn = _xmlDataSync["KeyColumn"].InnerText;
            }
            catch
            {
                throw new("Could not locate item 'KeyColumn' in the XML profile.");
            }

            // The name of the spatial column in the local layer and remote table.
            try
            {
                _spatialColumn = _xmlDataSync["SpatialColumn"].InnerText;
            }
            catch
            {
                throw new("Could not locate item 'SpatialColumn' in the XML profile.");
            }

            // By default, should an existing log file be cleared?
            try
            {
                _defaultClearLogFile = false;
                rawText = _xmlDataSync["DefaultClearLogFile"].InnerText;
                if (rawText.ToLower(System.Globalization.CultureInfo.CurrentCulture) is "yes" or "y")
                    _defaultClearLogFile = true;
            }
            catch
            {
                // This is an optional node
                _defaultClearLogFile = false;
            }

            // By default, should the log file be opened after running?
            try
            {
                _defaultOpenLogFile = false;
                rawText = _xmlDataSync["DefaultOpenLogFile"].InnerText;
                if (rawText.ToLower(System.Globalization.CultureInfo.CurrentCulture) is "yes" or "y")
                    _defaultOpenLogFile = true;
            }
            catch
            {
                // This is an optional node
                _defaultOpenLogFile = false;
            }

            // All mandatory variables were loaded successfully.
            return true;
        }

        #endregion Get Mandatory Variables

        #region Members

        private readonly bool _xmlFound;

        /// <summary>
        /// Has the XML file been found.
        /// </summary>
        public bool XMLFound
        {
            get
            {
                return _xmlFound;
            }
        }

        private readonly bool _xmlLoaded;

        /// <summary>
        ///  Has the XML file been loaded.
        /// </summary>
        public bool XMLLoaded
        {
            get
            {
                return _xmlLoaded;
            }
        }

        #endregion Members

        #region Variables

        private string _logFilePath;

        public string LogFilePath
        {
            get { return _logFilePath; }
        }

        private string _sdeFile;

        public string SDEFile
        {
            get { return _sdeFile; }
        }

        private string _databaseSchema;

        public string DatabaseSchema
        {
            get { return _databaseSchema; }
        }

        private string _compareStoredProcedure;

        public string CompareStoredProcedure
        {
            get { return _compareStoredProcedure; }
        }

        private string _updateStoredProcedure;

        public string UpdateStoredProcedure
        {
            get { return _updateStoredProcedure; }
        }

        private string _clearStoredProcedure;

        public string ClearStoredProcedure
        {
            get { return _clearStoredProcedure; }
        }

        private string _localLayer;

        public string LocalLayer
        {
            get { return _localLayer; }
        }

        private string _localClause;

        public string LocalClause
        {
            get { return _localClause; }
        }

        private string _remoteTable;

        public string RemoteTable
        {
            get { return _remoteTable; }
        }

        private string _remoteClause;

        public string RemoteClause
        {
            get { return _remoteClause; }
        }

        private string _remoteLayer;

        public string RemoteLayer
        {
            get { return _remoteLayer; }
        }

        private string _keyColumn;

        public string KeyColumn
        {
            get { return _keyColumn; }
        }

        private string _spatialColumn;

        public string SpatialColumn
        {
            get { return _spatialColumn; }
        }

        private bool _defaultClearLogFile;

        public bool DefaultClearLogFile
        {
            get { return _defaultClearLogFile; }
        }

        private bool _defaultOpenLogFile;

        public bool DefaultOpenLogFile
        {
            get { return _defaultOpenLogFile; }
        }

        #endregion Variables
    }
}