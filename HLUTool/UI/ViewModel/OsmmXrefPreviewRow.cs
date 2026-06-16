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

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// Represents one row in the OSMM xref preview grid: a unique combination
    /// of OSMM attribute values found in the selected features, together with
    /// the feature count for that combination, the resolved habitat values from
    /// <c>lut_osmm_habitat_xref</c>, and a flag indicating whether a match was found.
    /// </summary>
    public sealed class OsmmXrefPreviewRow
    {
        /// <summary>Gets the OSMM <c>make</c> attribute value.</summary>
        public string Make { get; init; }

        /// <summary>Gets the OSMM <c>desc_group</c> attribute value.</summary>
        public string DescGroup { get; init; }

        /// <summary>Gets the OSMM <c>desc_term</c> attribute value.</summary>
        public string DescTerm { get; init; }

        /// <summary>Gets the OSMM <c>theme</c> attribute value.</summary>
        public string Theme { get; init; }

        /// <summary>Gets the OSMM <c>feat_code</c> attribute value.</summary>
        public string FeatCode { get; init; }

        /// <summary>Gets the number of selected features with this attribute combination.</summary>
        public int Count { get; init; }

        /// <summary>
        /// Gets the resolved <c>habitat_primary</c> value from
        /// <c>lut_osmm_habitat_xref</c>, or <c>null</c> when no match was found.
        /// </summary>
        public string HabitatPrimary { get; init; }

        /// <summary>
        /// Gets the resolved <c>habitat_secondaries</c> value from
        /// <c>lut_osmm_habitat_xref</c>, or <c>null</c> when no match was found.
        /// </summary>
        public string HabitatSecondaries { get; init; }

        /// <summary>
        /// Gets a value indicating whether this combination was found in
        /// <c>lut_osmm_habitat_xref</c>.
        /// </summary>
        public bool IsMatched { get; init; }

        /// <summary>
        /// Gets a value indicating whether the resolved primary habitat code
        /// is valid for the active layer geometry type.
        /// </summary>
        public bool IsPrimaryValid { get; init; }

        /// <summary>
        /// Gets a value indicating whether all resolved secondary habitat codes
        /// are valid for the active layer geometry type.
        /// </summary>
        public bool AreSecondariesValid { get; init; }

        /// <summary>
        /// Gets a display string for the match status column.
        /// </summary>
        public string Status
        {
            get
            {
                if (!IsMatched)
                    return "No match";
                if (!IsPrimaryValid && !AreSecondariesValid)
                    return "Invalid primary/secondaries";
                if (!IsPrimaryValid)
                    return "Invalid primary";
                if (!AreSecondariesValid)
                    return "Invalid secondaries";
                return "Matched";
            }
        }
    }
}
