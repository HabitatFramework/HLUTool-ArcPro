using System;
using System.Collections.Generic;
using System.Data;

namespace HLU.GISApplication
{
    /// <summary>
    /// Provides helper logic for mapping requested history columns to GIS fields.
    /// Supports the "additional history field" mechanism via a delimiter.
    /// </summary>
    internal static class HistoryFieldBindingHelper
    {
        /// <summary>
        /// Represents a mapping from an output history column name to a source field index.
        /// </summary>
        internal sealed class HistoryFieldBinding
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="HistoryFieldBinding"/> class.
            /// </summary>
            /// <param name="outputColumnName">The output history column name.</param>
            /// <param name="sourceFieldIndex">The source GIS field index.</param>
            /// <param name="outputType">The output column type.</param>
            public HistoryFieldBinding(string outputColumnName, int sourceFieldIndex, Type outputType)
            {
                OutputColumnName = outputColumnName;
                SourceFieldIndex = sourceFieldIndex;
                OutputType = outputType;
            }

            /// <summary>
            /// Gets the output column name to use in the returned history table.
            /// </summary>
            public string OutputColumnName
            {
                get;
            }

            /// <summary>
            /// Gets the source GIS field index to read values from.
            /// </summary>
            public int SourceFieldIndex
            {
                get;
            }

            /// <summary>
            /// Gets the .NET type to use for the returned history DataColumn.
            /// </summary>
            public Type OutputType
            {
                get;
            }
        }

        /// <summary>
        /// Builds history field bindings from the requested history columns.
        /// Supports additional fields using <paramref name="additionalFieldsDelimiter"/>.
        /// </summary>
        /// <param name="historyColumns">The requested history columns.</param>
        /// <param name="additionalFieldsDelimiter">The delimiter used to encode additional fields.</param>
        /// <param name="mapField">
        /// A function that maps a field name to a source field index using the schema map.
        /// </param>
        /// <param name="fuzzyFieldOrdinal">
        /// A function that performs fuzzy matching (e.g. truncated shapefile names) to locate a source field index.
        /// </param>
        /// <returns>A list of bindings describing which fields to read and what output column names to use.</returns>
        internal static List<HistoryFieldBinding> BuildHistoryFieldBindings(
            DataColumn[] historyColumns,
            string additionalFieldsDelimiter,
            Func<string, int> mapField,
            Func<string, int> fuzzyFieldOrdinal)
        {
            // Check parameters.
            ArgumentNullException.ThrowIfNull(historyColumns);

            ArgumentNullException.ThrowIfNull(additionalFieldsDelimiter);

            ArgumentNullException.ThrowIfNull(mapField);

            ArgumentNullException.ThrowIfNull(fuzzyFieldOrdinal);

            List<HistoryFieldBinding> bindings = [];

            foreach (DataColumn c in historyColumns)
            {
                if (c == null)
                    continue;

                string requestedName = c.ColumnName;
                if (string.IsNullOrWhiteSpace(requestedName))
                    continue;

                // Additional-field encoding:
                // "<prefix><DELIM><sourceFieldName>".
                // Output column name becomes "<prefix><sourceFieldName>".
                // Source field index is resolved against "<sourceFieldName>".
                string outputName;
                string sourceFieldName;

                int delimIx = requestedName.IndexOf(additionalFieldsDelimiter, StringComparison.Ordinal);
                if (delimIx >= 0)
                {
                    string prefix = requestedName.Substring(0, delimIx);
                    sourceFieldName = requestedName.Substring(delimIx + additionalFieldsDelimiter.Length);
                    outputName = prefix + sourceFieldName;
                }
                else
                {
                    outputName = requestedName;
                    sourceFieldName = requestedName;
                }

                int ix = mapField(sourceFieldName);
                if (ix == -1)
                    ix = fuzzyFieldOrdinal(sourceFieldName);

                // If a column cannot be mapped, it is ignored, mirroring the existing HistorySchema behaviour.
                if (ix == -1)
                    continue;

                bindings.Add(new HistoryFieldBinding(outputName, ix, c.DataType));
            }

            return bindings;
        }
    }
}