// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2014, 2016 Thames Valley Environmental Records Centre
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

    /// <summary>
    /// Enable the user to switch between different HLU layers, where
    /// there is more than one valid layer in the current document.
    /// Contains details of each valid HLU layer (map/window number,
    /// map/window name, layer number and layer name.
    /// </summary>
    public class HLULayer
    {
        #region Fields

        private string _layerName;
        private bool _isEditable = false;

        #endregion

        #region Constructor

        /// <summary>
        /// Initialise a new instance of the HLULayer class.
        /// </summary>
        public HLULayer()
        {
        }

        /// <summary>
        /// Initialise a new instance of the HLULayer class with the specified layer name.
        /// </summary>
        /// <param name="layerName">The name of the layer.</param>
        public HLULayer(string layerName)
        {
            _layerName = layerName;
        }

        /// <summary>
        /// Initialise a new instance of the HLULayer class with the specified layer name and editability.
        /// </summary>
        /// <param name="layerName">The name of the layer.</param>
        /// <param name="isEditable">A value indicating whether the layer is editable.</param>
        public HLULayer(string layerName, bool isEditable)
        {
            _layerName = layerName;
            _isEditable = isEditable;
        }

        #endregion Constructor

        #region Properties

        /// <summary>
        /// Gets or sets the name of the layer.
        /// </summary>
        /// <value>The name of the layer.</value>
        public string LayerName
        {
            get { return _layerName; }
            set { _layerName = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the layer is editable.
        /// </summary>
        /// <value><c>true</c> if the layer is editable; otherwise, <c>false</c>.</value>
        public bool IsEditable
        {
            get { return _isEditable; }
            set { _isEditable = value; }
        }

        /// <summary>
        /// Gets the display name of the layer, which is currently the same as the layer name.
        /// </summary>
        /// <value>The display name of the layer.</value>
        public string DisplayName
        {
            get { return _layerName; }
        }

        #endregion Properties

        #region Overrides

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

        #endregion Overrides

    }
}