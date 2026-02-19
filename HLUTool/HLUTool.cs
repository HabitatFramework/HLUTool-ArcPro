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

using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using HLU.GISApplication;
using HLU.Properties;
using System.IO;

namespace HLU
{
    internal class HLUTool : Module
    {
        private static HLUTool _this = null;

        /// <summary>
        /// Stores the path to the working geodatabase for cleanup on exit
        /// </summary>
        private static string _workingGdbPath = null;

        /// <summary>
        /// Retrieve the singleton instance to this module here
        /// </summary>
        public static HLUTool Current => _this ??= (HLUTool)FrameworkApplication.FindModule("HLUTool_Module");

        /// <summary>
        /// Gets or sets the working geodatabase path for this module instance
        /// </summary>
        public static string WorkingGdbPath
        {
            get => _workingGdbPath;
            set => _workingGdbPath = value;
        }

        #region Overrides

        /// <summary>
        /// Called by Framework when ArcGIS Pro is closing
        /// </summary>
        /// <returns>False to prevent Pro from closing, otherwise True</returns>
        protected override bool CanUnload()
        {
            //return false to ~cancel~ Application close
            return true;
        }

        /// <summary>
        /// Called by Framework when the module is being unloaded
        /// </summary>
        protected override void Uninitialize()
        {
            // Clean up working GDB on shutdown
            if (!string.IsNullOrEmpty(_workingGdbPath) && Directory.Exists(_workingGdbPath))
            {
                // Attempt to delete the entire working GDB
                _ = ArcGISProHelpers.DeleteFileGeodatabaseAsync(_workingGdbPath);
            }

            base.Uninitialize();
        }

        #endregion Overrides
    }
}