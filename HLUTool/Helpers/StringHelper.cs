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

using System.Text.RegularExpressions;

namespace HLU.Helpers
{
    public static partial class StringHelper
    {
        /// <summary>
        /// Defines a compiled regular expression that matches capitalized words in a string.
        /// </summary>
        /// <remarks>
        /// - The pattern `[A-Z][^A-Z]*` matches:
        ///   - An uppercase letter (`[A-Z]`) at the beginning of a word.
        ///   - Followed by zero or more non-uppercase letters (`[^A-Z]*`).
        /// - This effectively extracts words that start with a capital letter and continue until
        ///   the next capital letter is encountered.
        /// - The `[GeneratedRegex]` attribute compiles the regex at compile time for performance benefits.
        /// </remarks>
        /// <returns>A `Regex` instance that can be used to match capitalized words in a string.</returns>
        [GeneratedRegex("[A-Z][^A-Z]*")]
        private static partial Regex CapitalisedRegex();

        public static Regex GetCapitalisedRegex() => CapitalisedRegex();

        /// <summary>
        /// Defines a compiled regular expression that matches one or more whitespace characters.
        /// </summary>
        /// <remarks>
        /// - The pattern `\s+` matches:
        ///   - One or more whitespace characters (`\s+`), including spaces, tabs, and newlines.
        /// - This regex is useful for detecting or replacing multiple whitespace occurrences in a string.
        /// - The `[GeneratedRegex]` attribute ensures that the regex is compiled at compile-time,
        ///   improving performance.
        /// </remarks>
        /// <returns>A <see cref="Regex"/> instance that can be used to match whitespace sequences.</returns>
        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();

        public static Regex GetWhitespaceRegex() => WhitespaceRegex();
    }
}