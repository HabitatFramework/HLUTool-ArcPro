using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HLU.Enums
{
    #region enums

    /// <summary>
    /// Update operations.
    /// </summary>
    public enum Operations
    {
        PhysicalMerge,
        PhysicalSplit,
        LogicalMerge,
        LogicalSplit,
        AttributeUpdate,
        BulkUpdate,
        OSMMUpdate
    };

    #endregion enums

}