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
using System.IO;
using System.Windows;
using HLU.GISApplication.ArcGIS;
//DONE: MapInfo
//using HLU.GISApplication.MapInfo;
using HLU.Properties;
using HLU.UI.View;
using HLU.UI.ViewModel;
using Microsoft.Win32;

namespace HLU.GISApplication
{
    public enum GISApplications
    {
        None,
        ArcGIS
    };

    class GISAppFactory
    {
        private static GISApplications _gisApp;

        public static GISApp CreateGisApp()
        {
            try
            {
                _gisApp = GISApplications.None;

                if (Enum.IsDefined(typeof(GISApplications), Settings.Default.PreferredGis))
                    _gisApp = (GISApplications)Settings.Default.PreferredGis;

                if (_gisApp == GISApplications.None)
                {
                    _gisApp = GISApplications.ArcGIS;

                    Settings.Default.PreferredGis = (int)_gisApp;
                }

                if (_gisApp == GISApplications.None)
                    throw new ArgumentException("Could not find GIS application.");
                else
                    Settings.Default.Save();

                return _gisApp switch
                {
                    GISApplications.ArcGIS => new ArcMapApp(Settings.Default.MapPath),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "HLU: Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        public static bool ClearSettings()
        {
            try
            {
                Settings.Default.PreferredGis = (int)GISApplications.None;
                Settings.Default.MapPath = String.Empty;
                Settings.Default.Save();

                return true;
            }
            catch { return false; }
        }
    }
}
