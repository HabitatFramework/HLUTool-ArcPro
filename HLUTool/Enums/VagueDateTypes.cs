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
    /// Indicates whether a date is a start date, end date, or vague date.
    /// </summary>
    public enum DateType
    {
        Start, End, Vague
    };

    /// <summary>
    /// Indicates the type of vague date, which can be a single date, a date range, a month and
    /// year, a month and year range, a year, a year range, a season, a season range, or unknown.
    /// </summary>
    public enum VagueDateTypes
    {
        [EnumCode("D")]
        StartDate,

        [EnumCode("DD")]
        StartAndEndDates,

        [EnumCode("D-")]
        StartDateRange,

        [EnumCode("-D")]
        EndDateRange,

        [EnumCode("O")]
        StartMonthAndYear,

        [EnumCode("OO")]
        StartAndEndMonthAndYear,

        [EnumCode("O-")]
        StartMonthRange,

        [EnumCode("-O")]
        EndMonthRange,

        [EnumCode("Y")]
        StartYear,

        [EnumCode("YY")]
        StartAndEndYear,

        [EnumCode("Y-")]
        StartYearRange,

        [EnumCode("-Y")]
        EndYearRange,

        [EnumCode("P")]
        StartSeason,

        [EnumCode("PP")]
        StartAndEndSeason,

        [EnumCode("P-")]
        StartSeasonRange,

        [EnumCode("-P")]
        EndSeasonRange,

        [EnumCode("U")]
        Unknown
    }

    #endregion Enums
}