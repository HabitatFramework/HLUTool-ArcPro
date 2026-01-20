using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework.Contracts;

namespace HLU
{
    internal class HLUToolModule : Module
    {
        /// <summary>
        /// Gets or sets whether Split menu should be enabled.
        /// </summary>
        internal static bool CanSplit
        {
            get;
            set;
        } = true;

        /// <summary>
        /// Gets or sets whether the Merge menu should be enabled.
        /// </summary>
        internal static bool CanMerge
        {
            get;
            set;
        } = true;
    }
}