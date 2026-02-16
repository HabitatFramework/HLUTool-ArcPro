// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2014 Sussex Biodiversity Record Centre
// Copyright © 2019 London & South East Record Centres (LaSER)
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

using System;
using System.Collections.Generic;
using System.Linq;

namespace HLU.Helpers
{
    /// <summary>
    /// Static helper class for validating GIS feature selections.
    /// </summary>
    public static class SelectionValidator
    {
        #region Merge Validation

        /// <summary>
        /// Validates whether a selection is valid for a merge operation.
        /// </summary>
        /// <param name="incidCount">Number of unique Incids selected.</param>
        /// <param name="toidCount">Number of unique TOIDs selected.</param>
        /// <param name="fragCount">Number of fragments selected.</param>
        /// <param name="errorMessage">Output parameter containing the validation error message, if any.</param>
        /// <returns>True if the selection is valid for merging; otherwise, false.</returns>
        public static bool ValidateForMerge(
            int incidCount,
            int toidCount,
            int fragCount,
            out string errorMessage)
        {
            errorMessage = null;

            // Must have at least 2 fragments to merge
            if (fragCount < 2)
            {
                errorMessage = "At least two features must be selected to perform a merge operation.";
                return false;
            }

            // For physical merge: must all be from same incid
            // For logical merge: can be different incids

            return true;
        }

        /// <summary>
        /// Validates whether a selection is valid for a physical merge operation.
        /// </summary>
        /// <param name="incidCount">Number of unique Incids selected.</param>
        /// <param name="toidCount">Number of unique TOIDs selected.</param>
        /// <param name="fragCount">Number of fragments selected.</param>
        /// <param name="errorMessage">Output parameter containing the validation error message, if any.</param>
        /// <returns>True if the selection is valid for physical merging; otherwise, false.</returns>
        public static bool ValidateForPhysicalMerge(
            int incidCount,
            int toidCount,
            int fragCount,
            out string errorMessage)
        {
            errorMessage = null;

            // Must have at least 2 fragments
            if (fragCount < 2)
            {
                errorMessage = "At least two features must be selected to perform a physical merge.";
                return false;
            }

            // All fragments must belong to the same Incid
            if (incidCount != 1)
            {
                errorMessage = $"All selected features must belong to the same Incid for a physical merge. Currently {incidCount} Incids are selected.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates whether a selection is valid for a logical merge operation.
        /// </summary>
        /// <param name="incidCount">Number of unique Incids selected.</param>
        /// <param name="toidCount">Number of unique TOIDs selected.</param>
        /// <param name="fragCount">Number of fragments selected.</param>
        /// <param name="errorMessage">Output parameter containing the validation error message, if any.</param>
        /// <returns>True if the selection is valid for logical merging; otherwise, false.</returns>
        public static bool ValidateForLogicalMerge(
            int incidCount,
            int toidCount,
            int fragCount,
            out string errorMessage)
        {
            errorMessage = null;

            // Must have at least 2 Incids
            if (incidCount < 2)
            {
                errorMessage = "At least two different Incids must be selected to perform a logical merge.";
                return false;
            }

            // Must have at least 2 fragments
            if (fragCount < 2)
            {
                errorMessage = "At least two features must be selected to perform a logical merge.";
                return false;
            }

            return true;
        }

        #endregion Merge Validation

        #region Split Validation

        /// <summary>
        /// Validates whether a selection is valid for a split operation.
        /// </summary>
        /// <param name="incidCount">Number of unique Incids selected.</param>
        /// <param name="toidCount">Number of unique TOIDs selected.</param>
        /// <param name="fragCount">Number of fragments selected.</param>
        /// <param name="totalFragCountForIncid">Total number of fragments for the Incid in the database.</param>
        /// <param name="errorMessage">Output parameter containing the validation error message, if any.</param>
        /// <returns>True if the selection is valid for splitting; otherwise, false.</returns>
        public static bool ValidateForSplit(
            int incidCount,
            int toidCount,
            int fragCount,
            int totalFragCountForIncid,
            out string errorMessage)
        {
            errorMessage = null;

            // Can only split one Incid at a time
            if (incidCount != 1)
            {
                errorMessage = $"Only one Incid can be selected for splitting. Currently {incidCount} Incids are selected.";
                return false;
            }

            // Must have at least one fragment selected
            if (fragCount < 1)
            {
                errorMessage = "At least one feature must be selected to perform a split.";
                return false;
            }

            // Cannot select ALL fragments (nothing left to split from)
            if (fragCount >= totalFragCountForIncid)
            {
                errorMessage = "Cannot split all fragments from an Incid. At least one fragment must remain unselected.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates whether a selection is valid for a physical split operation.
        /// </summary>
        /// <param name="incidCount">Number of unique Incids selected.</param>
        /// <param name="toidCount">Number of unique TOIDs selected.</param>
        /// <param name="fragCount">Number of fragments selected.</param>
        /// <param name="totalFragCountForIncid">Total number of fragments for the Incid in the database.</param>
        /// <param name="errorMessage">Output parameter containing the validation error message, if any.</param>
        /// <returns>True if the selection is valid for physical splitting; otherwise, false.</returns>
        public static bool ValidateForPhysicalSplit(
            int incidCount,
            int toidCount,
            int fragCount,
            int totalFragCountForIncid,
            out string errorMessage)
        {
            // Physical split has same requirements as general split
            return ValidateForSplit(incidCount, toidCount, fragCount, totalFragCountForIncid, out errorMessage);
        }

        /// <summary>
        /// Validates whether a selection is valid for a logical split operation.
        /// </summary>
        /// <param name="incidCount">Number of unique Incids selected.</param>
        /// <param name="toidCount">Number of unique TOIDs selected.</param>
        /// <param name="fragCount">Number of fragments selected.</param>
        /// <param name="totalFragCountForIncid">Total number of fragments for the Incid in the database.</param>
        /// <param name="errorMessage">Output parameter containing the validation error message, if any.</param>
        /// <returns>True if the selection is valid for logical splitting; otherwise, false.</returns>
        public static bool ValidateForLogicalSplit(
            int incidCount,
            int toidCount,
            int fragCount,
            int totalFragCountForIncid,
            out string errorMessage)
        {
            // Logical split has same requirements as general split
            return ValidateForSplit(incidCount, toidCount, fragCount, totalFragCountForIncid, out errorMessage);
        }

        #endregion Split Validation

        #region Update Validation

        /// <summary>
        /// Validates whether a selection is valid for an update operation.
        /// </summary>
        /// <param name="incidCount">Number of unique Incids selected.</param>
        /// <param name="fragCount">Number of fragments selected.</param>
        /// <param name="errorMessage">Output parameter containing the validation error message, if any.</param>
        /// <returns>True if the selection is valid for updating; otherwise, false.</returns>
        public static bool ValidateForUpdate(
            int incidCount,
            int fragCount,
            out string errorMessage)
        {
            errorMessage = null;

            // Must have exactly one Incid selected
            if (incidCount != 1)
            {
                errorMessage = $"Only one Incid can be selected for updating. Currently {incidCount} Incids are selected.";
                return false;
            }

            // Must have at least one fragment
            if (fragCount < 1)
            {
                errorMessage = "At least one feature must be selected to perform an update.";
                return false;
            }

            return true;
        }

        #endregion Update Validation

        #region General Validation

        /// <summary>
        /// Validates whether any selection exists.
        /// </summary>
        /// <param name="fragCount">Number of fragments selected.</param>
        /// <param name="errorMessage">Output parameter containing the validation error message, if any.</param>
        /// <returns>True if at least one feature is selected; otherwise, false.</returns>
        public static bool HasSelection(int fragCount, out string errorMessage)
        {
            errorMessage = null;

            if (fragCount < 1)
            {
                errorMessage = "No features are currently selected.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates that the GIS selection matches the expected counts.
        /// </summary>
        /// <param name="expectedFragCount">Expected number of fragments.</param>
        /// <param name="actualFragCount">Actual number of fragments selected in GIS.</param>
        /// <param name="errorMessage">Output parameter containing the validation error message, if any.</param>
        /// <returns>True if counts match; otherwise, false.</returns>
        public static bool ValidateGISSync(
            int expectedFragCount,
            int actualFragCount,
            out string errorMessage)
        {
            errorMessage = null;

            if (actualFragCount != expectedFragCount)
            {
                errorMessage = $"GIS selection out of sync. Expected {expectedFragCount} features, but {actualFragCount} are selected in GIS.";
                return false;
            }

            return true;
        }

        #endregion General Validation
    }
}