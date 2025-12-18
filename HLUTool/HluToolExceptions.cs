using System;
using System.Collections.Generic;
using System.Linq;

namespace HLU
{
    /// <summary>
    /// Base exception for HLU Tool failures that should be shown to the user.
    /// </summary>
    public class HLUToolException : Exception
    {
        public HLUToolException(string message) : base(message) { }
        public HLUToolException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Thrown when a GIS selection cannot be read or is invalid.
    /// </summary>
    public sealed class GisSelectionException : HLUToolException
    {
        public GisSelectionException(string message) : base(message) { }
        public GisSelectionException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Thrown when expected fields do not exist on the active layer.
    /// </summary>
    public sealed class MissingLayerFieldsException : HLUToolException
    {
        public IReadOnlyList<string> MissingFields { get; }

        public MissingLayerFieldsException(IEnumerable<string> missingFields)
            : base("The active layer is missing required field(s): " + string.Join(", ", missingFields))
        {
            MissingFields = missingFields.ToList();
        }
    }

    /// <summary>
    /// Thrown when a database query fails.
    /// </summary>
    public sealed class DatabaseQueryException : HLUToolException
    {
        public DatabaseQueryException(string message) : base(message) { }
        public DatabaseQueryException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Thrown when an edit operation fails.
    /// </summary>
    public sealed class EditOperationException : HLUToolException
    {
        public EditOperationException(string message) : base(message) { }
        public EditOperationException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Thrown when there is a configuration error.
    /// </summary>
    public sealed class ConfigurationException : HLUToolException
    {
        public ConfigurationException(string message) : base(message) { }
        public ConfigurationException(string message, Exception inner) : base(message, inner) { }
    }
}