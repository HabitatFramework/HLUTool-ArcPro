// HLUTool is used to view and maintain habitat and land use GIS data.
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

namespace HLU.Enums
{
    #region Enums

    /// <summary>
    /// An enumeration of the different options for what to do when
    /// attempting to update a subset of features for an incid.
    /// </summary>
    public enum SubsetUpdateActions
    {
        Prompt,
        Split,
        All
    };

    /// <summary>
    /// An enumeration of the different options for whether
    /// to auto zoom to the GIS selection.
    /// </summary>
    public enum AutoZoomToSelection
    {
        Off,
        When,
        Always
    };

    /// <summary>
    /// An enumeration of the different options for whether
    /// to validate secondary codes against the habitat type
    /// mandatory codes.
    /// </summary>
    public enum HabitatSecondaryCodeValidationOptions
    {
        Ignore,
        Warning,
        Error
    };

    /// <summary>
    /// An enumeration of the different options for whether
    /// to validate secondary codes against the primary code.
    /// </summary>
    public enum PrimarySecondaryCodeValidationOptions
    {
        Ignore,
        Error
    };

    /// <summary>
    /// An enumeration of the different options for whether
    /// to validate quality determination and interpretation.
    /// </summary>
    public enum QualityValidationOptions
    {
        Optional,
        Mandatory
    };

    /// <summary>
    /// An enumeration of the different options for whether
    /// to validate potential priority habitat quality determination.
    /// </summary>
    public enum PotentialPriorityDetermQtyValidationOptions
    {
        Ignore,
        Error
    };

    #endregion Enums
}