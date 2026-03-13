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
using System.Text.RegularExpressions;

namespace HLU
{
    /// Original code from:
    /// http://www.codeproject.com/Articles/11556/Converting-Wildcards-to-Regexes<summary>
    /// Represents a wildcard running on the
    /// <see cref="System.Text.RegularExpressions"/> engine.
    /// </summary>
    public class Wildcard : Regex
    {
        #region Constructor

        /// <summary>
        /// Initializes a wildcard with the given search pattern.
        /// </summary>
        /// <param name="pattern">The wildcard pattern to match.</param>
        public Wildcard(string pattern)
            : base(WildcardToRegex(pattern))
        {
        }

        /// <summary>
        /// Initializes a wildcard with the given search pattern and options.
        /// </summary>
        /// <param name="pattern">The wildcard pattern to match.</param>
        /// <param name="options">A combination of one or more
        /// <see cref="System.Text.RegexOptions"/>.</param>
        public Wildcard(string pattern, RegexOptions options)
            : base(WildcardToRegex(pattern), options)
        {
        }

        /// <summary>
        /// Initializes a wildcard with the given search pattern, schema and options.
        /// </summary>
        /// <param name="pattern">The wildcard pattern to match.</param>
        /// <param name="options">A combination of one or more
        /// <see cref="System.Text.RegexOptions"/>.</param>
        public Wildcard(string pattern, string schema, RegexOptions options)
            : base(WildcardToRegex(pattern, schema), options)
        {
        }

        /// <summary>
        /// Converts a wildcard to a regex.
        /// </summary>
        /// <param name="pattern">The wildcard pattern to convert.</param>
        /// <param name="schema">"The database schema to append before each part.</param>
        /// <returns>A regex equivalent of the given wildcard.</returns>
        public static string WildcardToRegex(string pattern, string schema)
        {
            if (schema is null)
                return "^" + Escape(pattern).
                 Replace("\\*", ".*").
                 Replace("\\?", "*").
                 Replace("\\|", "|") + "$";
            else
                return "^" + schema + "." + Escape(pattern).
                 Replace("\\*", ".*").
                 Replace("\\?", "*").
                 Replace("\\|", "|" + schema + ".") + "$";
        }

        /// <summary>
        /// Converts a wildcard to a regex.
        /// </summary>
        /// <param name="pattern">The wildcard pattern to convert.</param>
        /// <returns>A regex equivalent of the given wildcard.</returns>
        public static string WildcardToRegex(string pattern)
        {
            return "^" + Escape(pattern).
             Replace("\\*", ".*").
             Replace("\\?", "*").
             Replace("\\|", "|") + "$";
            //Replace("\\*", ".*").
        }

        #endregion Constructor
    }

    /// <summary>
    /// This class provides a variety of functions to check and modify
    /// strings.
    /// </summary>
    public class StringFunctions
    {
        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="StringFunctions"/> class.
        /// </summary>
        public StringFunctions()
        {
            // constructor takes no arguments.
        }

        #endregion Constructor

        #region Characters

        /// <summary>
        /// Remove all potentially special characters from a string and return the result.
        /// </summary>
        /// <param name="inputString">The input string to process.</param>
        /// <param name="repChar">The character to replace illegal characters with.</param>
        /// <param name="isFileName">Indicates whether the input string is a file name.</param>
        /// <returns>The processed string with illegal characters removed or replaced.</returns>
        public static string StripIllegals(string inputString, string repChar, bool isFileName = false)
        {
            // If it is a file name, check if there is a '.' at fourth place before last.
            bool addFileDot = false;
            if (isFileName)
            {
                char chTest = inputString[^4];
                if (chTest == '.') addFileDot = true;
            }

            string outputString = inputString;
            List<string> theIllegals = [@"\", "%", "$", ":", "*", "/", "?", "<", ">", "|", "~", "£", "."];
            foreach (string searchString in theIllegals)
            {
                outputString = outputString.Replace(searchString, repChar);
            }
            if (addFileDot)
            {
                if (repChar.Length > 0)
                    outputString = outputString.Remove(outputString.Length - 4, repChar.Length);
                outputString = outputString.Insert(outputString.Length - 3, ".");
            }
            return outputString;
        }

        /// <summary>
        /// Check if the supplied replacement character is a valid character.
        /// </summary>
        /// <param name="repChar">The character to check for validity.</param>
        /// <returns><c>true</c> if the character is valid; otherwise, <c>false</c>.</returns>
        public static bool IsValid(string repChar)
        {
            List<string> theIllegals = [@"\", "%", "$", ":", "*", "/", "?", "<", ">", "|", "~", "£", "."];

            if (theIllegals.IndexOf(repChar) == -1)
                return true;
            return false;
        }

        /// <summary>
        /// Keeps numbers and spaces from an input string and returns the results.
        /// </summary>
        /// <param name="inputString">The input string to process.</param>
        /// <param name="repChar">The character to replace invalid characters with.</param>
        /// <returns>The processed string with only numbers and spaces.</returns>
        public static string KeepNumbersAndSpaces(string inputString, string repChar)
        {
            string strOutputString = "";
            int aCount = 0;
            foreach (char strTest in inputString)
            {
                if (Int32.TryParse(strTest.ToString(), out int a) == true)
                {
                    strOutputString += strTest.ToString();
                    aCount++;
                }
                // Replace characters and spaces are not included at the start of the reference.
                else if ((strTest == ' ' || strTest.ToString() == repChar) && aCount > 0)
                    strOutputString += strTest.ToString();
            }
            return strOutputString;
        }

        /// <summary>
        /// Calculate the current financial year.
        /// </summary>
        /// <param name="curDate">The current date to calculate the financial year for.</param>
        /// <returns>The financial year as a string in the format "YYYY".</returns>
        public static string FinancialYear(DateTime curDate)
        {
            string CurrYr = curDate.ToString("yy");
            string PrevYr = curDate.AddYears(-1).ToString("yy");
            string NextYr = curDate.AddYears(1).ToString("yy");
            string FinYear;

            if (curDate.Month > 3)
                FinYear = CurrYr + NextYr;
            else
                FinYear = PrevYr + CurrYr;

            return FinYear;
        }

        #endregion Characters

        #region References

        /// <summary>
        /// Gets the sub-reference out of a short reference string.
        /// </summary>
        /// <param name="inputString">The input string containing the short reference.</param>
        /// <param name="repChar">The character used as a separator in the short reference.</param>
        /// <returns>The sub-reference extracted from the short reference string.</returns>
        public static string GetSubref(string inputString, string repChar)
        {
            // Input should look like xx.xxxx or xxxx where x is an integer.
            int a = inputString.IndexOf(repChar) + 1; // The index of the first numeric character after the replace character.
            return inputString.Substring(a, inputString.Length - a);
        }

        /// <summary>
        /// Replace standard search strings in a supplied text string.
        /// </summary>
        /// <param name="rawName">The raw input string containing placeholders.</param>
        /// <param name="reference">The reference string to replace the %ref% placeholder.</param>
        /// <param name="siteName">The site name to replace the %sitename% placeholder.</param>
        /// <param name="shortRef">The short reference to replace the %shortref% placeholder.</param>
        /// <param name="subRef">The sub-reference to replace the %subref% placeholder.</param>
        /// <param name="radius">The radius to replace the %radius% placeholder.</param>
        /// <returns>The processed string with placeholders replaced by actual values.</returns>
        public static string ReplaceSearchStrings(string rawName, string reference, string siteName, string shortRef, string subRef, string radius = "")
        {
            string cleanName = rawName;
            cleanName = cleanName.Replace("%ref%", reference);
            cleanName = cleanName.Replace("%shortref%", shortRef);
            cleanName = cleanName.Replace("%subref%", subRef);
            cleanName = cleanName.Replace("%sitename%", siteName);
            cleanName = cleanName.Replace("%radius%", radius);

            // Take account of the occurrence of dangling underscores (if no site name was given).
            if (cleanName.Substring(cleanName.Length - 1, 1) == "_")
                cleanName = cleanName.Substring(0, cleanName.Length - 1);

            return cleanName;
        }

        #endregion References

        #region Separators

        /// <summary>
        /// Replace a comma separated string with semi-colon separators.
        /// </summary>
        /// <param name="aGroupColumnString">The input string containing comma-separated values.</param>
        /// <returns>The formatted string with semi-colon separators.</returns>
        public static string GetGroupColumnsFormatted(string aGroupColumnString)
        {
            List<string> strColumns = [.. aGroupColumnString.Split(',')];
            string strFormatted = "";
            foreach (string strEntry in strColumns)
            {
                strFormatted = strFormatted + strEntry.Trim() + ";";
            }
            if (!String.IsNullOrEmpty(strFormatted))
                return strFormatted.Substring(0, strFormatted.Length - 1); // Remove the final semicolon.
            else
                return "";
        }

        /// <summary>
        /// Replace a dollar separated string with semi-colon separators.
        /// </summary>
        /// <param name="aStatsColumnString">The input string containing dollar-separated values.</param>
        /// <returns>The formatted string with semi-colon separators.</returns>
        public static string GetStatsColumnsFormatted(string aStatsColumnString)
        {
            List<string> strEntries = [.. aStatsColumnString.Split('$')];
            string strFormatted = "";
            foreach (string strEntry in strEntries)
            {
                strFormatted = strFormatted + strEntry.Replace(";", " ") + ";";
            }
            if (!String.IsNullOrEmpty(strFormatted))
                return strFormatted.Substring(0, strFormatted.Length - 1); // Remove the final comma.
            else
                return "";
        }

        #endregion Separators

        #region Columns

        /// <summary>
        /// Align statistics columns.
        /// </summary>
        /// <param name="AllColumns">A string containing all column names, separated by commas.</param>
        /// <param name="StatsColumns">A string containing the statistics column names, separated by semi-colons.</param>
        /// <param name="GroupColumns">A string containing the group column names, separated by semi-colons.</param>
        /// <returns>The aligned statistics columns as a string.</returns>
        public static string AlignStatsColumns(string AllColumns, string StatsColumns, string GroupColumns)
        {
            if (String.IsNullOrEmpty(GroupColumns) || String.IsNullOrEmpty(AllColumns))
                return StatsColumns;

            List<string> liAllColumns = [.. AllColumns.Split(',')];
            foreach (string strFieldName in liAllColumns)
            {
                string strFieldNameTr = strFieldName.Trim();
                if (strFieldNameTr.Substring(0, 1) != "\"")
                {
                    // Is it in the group columns?
                    if (!GroupColumns.Contains(strFieldNameTr, StringComparison.OrdinalIgnoreCase))
                    {
                        // Is it in the stats columns?
                        if (!StatsColumns.Contains(strFieldNameTr, StringComparison.OrdinalIgnoreCase))
                        {
                            // It is in neither - add it.
                            if (!String.IsNullOrEmpty(StatsColumns))
                                StatsColumns += ";";
                            StatsColumns = StatsColumns + strFieldNameTr + " FIRST";
                        }
                    }
                }
            }

            return StatsColumns;
        }

        #endregion Columns

        #region Groups

        /// <summary>
        /// Look at each layer in a list of layers and returns the unique
        /// group names (in front of any hyphen in the layer names).
        /// </summary>
        /// <param name="LayerList">A list of layer names to extract group names from.</param>
        /// <returns>List<string> containing the unique group names.</returns>
        public static List<string> ExtractGroups(List<string> LayerList)
        {
            List<string> liGroups = [];
            foreach (string strLayerName in LayerList)
            {
                int intHyphenIndex = strLayerName.IndexOf('-');
                if (intHyphenIndex != -1) // It has a group name
                {
                    string strGroupName = strLayerName.Substring(0, intHyphenIndex); // Check if we already have this one
                    if (liGroups.IndexOf(strGroupName) == -1)
                    {
                        liGroups.Add(strGroupName); // If not, add it.
                    }
                }
            }
            return liGroups; // Return the list of group names.
        }

        /// <summary>
        /// Return the group name from the front of a hyphen in
        /// the layer name.
        /// </summary>
        /// <param name="LayerName">The name of the layer from which to extract the group name.</param>
        /// <returns>The group name if present; otherwise, an empty string.</returns>
        public static string GetGroupName(string LayerName)
        {
            int intHyphenIndex = LayerName.IndexOf('-');
            if (intHyphenIndex != -1) // It has a group name
            {
                string strGroupName = LayerName.Substring(0, intHyphenIndex);
                return strGroupName; // Return the group name
            }
            else
                return ""; // No group name.
        }

        #endregion Groups
    }
}