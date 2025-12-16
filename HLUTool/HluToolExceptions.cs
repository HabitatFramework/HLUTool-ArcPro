using System;
using System.Collections.Generic;
using System.Linq;

namespace HLU
{
    /// <summary>
    /// Base exception for HLU Tool failures that should be shown to the user.
    /// </summary>
    public class HluToolException : Exception
    {
        public HluToolException(string message) : base(message) { }
        public HluToolException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Thrown when a GIS selection cannot be read or is invalid.
    /// </summary>
    public sealed class GisSelectionException : HluToolException
    {
        public GisSelectionException(string message) : base(message) { }
        public GisSelectionException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Thrown when expected fields do not exist on the active layer.
    /// </summary>
    public sealed class MissingLayerFieldsException : HluToolException
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
    public sealed class DatabaseQueryException : HluToolException
    {
        public DatabaseQueryException(string message) : base(message) { }
        public DatabaseQueryException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Thrown when an edit operation fails.
    /// </summary>
    public sealed class EditOperationException : HluToolException
    {
        public EditOperationException(string message) : base(message) { }
        public EditOperationException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Thrown when there is a configuration error.
    /// </summary>
    public sealed class ConfigurationException : HluToolException
    {
        public ConfigurationException(string message) : base(message) { }
        public ConfigurationException(string message, Exception inner) : base(message, inner) { }
    }
}