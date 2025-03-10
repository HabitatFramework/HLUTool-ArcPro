// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2014, 2016 Thames Valley Environmental Records Centre
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
using System.Linq;
using System.Text;

namespace HLU.Data
{
    #region Enums

    /// <summary>
    /// Geometry types.
    /// </summary>
    public enum GeometryTypes { Point, Line, Polygon, Unknown };

    #endregion

    //---------------------------------------------------------------------
    // CHANGED: CR31 (Switching between GIS layers)
    // Enable the user to switch between different HLU layers, where
    // there is more than one valid layer in the current document.
    //
    // Contains details of each valid HLU layer (map/window number,
    // map/window name, layer number and layer name.
    //---------------------------------------------------------------------
    public class HLULayer
    {
        #region Fields

        private string _layerName;
        private bool _isEditable = false;

        #endregion

        #region Constructor

        public HLULayer()
        {
        }

        public HLULayer(string layerName)
        {
            _layerName = layerName;
        }

        public HLULayer(string layerName, bool isEditable)
        {
            _layerName = layerName;
            _isEditable = isEditable;
        }

        #endregion Constructor

        #region Properties

        public string LayerName
        {
            get { return _layerName; }
            set { _layerName = value; }
        }

        public bool IsEditable
        {
            get { return _isEditable; }
            set { _isEditable = value; }
        }

        public string DisplayName
        {
            get { return _layerName; }
        }

        #endregion Properties

        #region Methods

        public override string ToString()
        {
            return "Layer: " + _layerName;
        }

        public override int GetHashCode()
        {
            return this.LayerName.GetHashCode();
        }

        public virtual bool Equals(HLULayer other)
        {
            if (other == null) return false;

            return (this._layerName == other._layerName);
        }

        public override bool Equals(object obj)
        {
            if (this.GetType() != obj.GetType()) return false;

            return Equals(obj as HLULayer);
        }

        #endregion Methods

    }
}