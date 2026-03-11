using ArcGIS.Desktop.Internal.Framework.Controls;
using System;

namespace HLU.UI.Model
{
    /// <summary>
    /// Represents a single message item in the message queue.
    /// </summary>
    public class MessageItem
    {
        /// <summary>
        /// Gets or sets the unique identifier for this message.
        /// </summary>
        public string Id
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the message text.
        /// </summary>
        public string Message
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the message level/type.
        /// </summary>
        public MessageType Level
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the timestamp when the message was created.
        /// </summary>
        public DateTime Timestamp
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the category of the message (e.g., "Navigation", "Validation", "Database").
        /// </summary>
        public string Category
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this message can be dismissed by the user.
        /// </summary>
        public bool IsDismissible
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the number of seconds after which the message should automatically dismiss itself.
        /// </summary>
        public int AutoDismissSeconds
        {
            get; set;
        }

        /// <summary>
        /// Gets the priority of the message based on its level.
        /// Higher values indicate higher priority.
        /// </summary>
        public int Priority => Level switch
        {
            MessageType.Error => 3,
            MessageType.Warning => 2,
            MessageType.Information => 1,
            _ => 0
        };

        /// <summary>
        /// Initializes a new instance of the MessageItem class.
        /// </summary>
        public MessageItem()
        {
            Id = Guid.NewGuid().ToString();
            Timestamp = DateTime.Now;
            IsDismissible = true;
        }

        /// <summary>
        /// Initializes a new instance of the MessageItem class with the specified parameters.
        /// </summary>
        /// <param name="message">The message text.</param>
        /// <param name="level">The message level.</param>
        /// <param name="category">The message category.</param>
        /// <param name="isDismissible">Whether the message can be dismissed.</param>
        /// <param name="autoDismissSeconds">The number of seconds after which the message should automatically dismiss itself.</param>
        public MessageItem(string message, MessageType level, string category = null, bool isDismissible = true, int autoDismissSeconds = 0)
        {
            Id = Guid.NewGuid().ToString();
            Message = message;
            Level = level;
            Category = category;
            IsDismissible = isDismissible;
            AutoDismissSeconds = autoDismissSeconds;
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>
    /// Common message categories used throughout the application.
    /// </summary>
    public static class MessageCategory
    {
        public const string Navigation = "Navigation";
        public const string Validation = "Validation";
        public const string Database = "Database";
        public const string Save = "Save";
        public const string Filter = "Filter";
        public const string BulkUpdate = "BulkUpdate";
        public const string OSMMUpdate = "OSMMUpdate";
        public const string GIS = "GIS";
        public const string Export = "Export";
        public const string Import = "Import";
        public const string Split = "Split";
        public const string Merge = "Merge";
        public const string General = "General";
    }
}