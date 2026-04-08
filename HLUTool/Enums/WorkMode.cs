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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HLU.Enums
{
    #region enums

    /// <summary>
    /// Represents the current operational state(s) of the HLU tool.
    ///
    /// This enum uses the [Flags] attribute, meaning each value corresponds
    /// to a single bit in a binary number. Because of that, multiple values
    /// can be combined using bitwise OR (e.g. Edit | Bulk).
    ///
    /// For example:
    ///   Edit       = 0001 (1)
    ///   Bulk       = 0010 (2)
    ///   OsmmReview = 0100 (4)
    ///   OsmmBulk   = 1000 (8)
    ///
    /// If the tool is simultaneously in Edit mode and Bulk Update mode,
    /// the combined state is:
    ///   0001 | 0010 = 0011  (decimal value 3)
    ///
    /// Checking whether a specific mode is active is done with:
    ///   WorkMode.HasAll(WorkMode.Bulk)
    /// </summary>
    [Flags]
    public enum WorkMode
    {
        /// <summary>
        /// No mode set.
        /// </summary>
        None = 0,

        /// <summary>
        /// Editing is allowed and the tool is in edit mode.
        /// This is the default mode when the tool is active and a valid map/layer is selected.
        /// </summary>
        CanEdit = 1 << 0, // Previously CanEdit.

        /// <summary>
        /// Normal edit mode (alias for CanEdit for clarity in mode switching).
        /// Use this when referring to the "Update Mode" in the UI.
        /// </summary>
        Edit = CanEdit, // Alias for CanEdit

        /// <summary>
        /// Bulk Update mode is active, which allows the user to update a subset of features for an incid.
        /// This mode may be active simultaneously with Edit mode and OsmmBulk modes.
        /// </summary>
        Bulk = 1 << 1, // Previously _bulkUpdateMode.

        /// <summary>
        /// OSMM review mode is active, which allows the user to review and update OSMM updates.
        /// This mode may be active simultaneously with Edit mode.
        /// </summary>
        OSMMReview = 1 << 2, // Previously _osmmReviewMode.

        /// <summary>
        /// OSMM Bulk Update mode is active, which allows the user to review and update a subset of features based on OSMM updates.
        /// This mode may be active simultaneously with Edit and Bulk modes.
        /// </summary>
        OSMMBulk = 1 << 3,  // Previously _osmmBulkUpdateMode.

        /// <summary>
        /// Both a reason and process are required for updates. This flag is used to determine whether reason and process values have been selected.
        /// </summary>
        HasReasonAndProcess = 1 << 4,

        /// <summary>
        /// Editing is allowed and the tool is in edit mode, and both a reason and process have been selected.
        /// </summary>
        EditReady = CanEdit | HasReasonAndProcess
    }

    #endregion enums
}