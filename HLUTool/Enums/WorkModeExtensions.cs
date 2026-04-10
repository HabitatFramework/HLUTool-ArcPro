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
    /// <summary>
    /// A static class containing extension methods for the WorkMode enum to facilitate checking for specific flags.
    /// </summary>
    public static class WorkModeExtensions
    {
        /// <summary>
        /// Returns true if any of the specified flags are present.
        /// </summary>
        public static bool HasAny(this WorkMode value, WorkMode flags)
        {
            return (value & flags) != WorkMode.None;
        }

        /// <summary>
        /// Returns true if all of the specified flags are present.
        /// </summary>
        public static bool HasAll(this WorkMode value, WorkMode flags)
        {
            return (value & flags) == flags;
        }
    }
}