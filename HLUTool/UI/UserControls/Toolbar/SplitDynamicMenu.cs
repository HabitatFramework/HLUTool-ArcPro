using ArcGIS.Desktop.Framework.Contracts;
using HLU.UI.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace HLU.UI.UserControls.Toolbar
{
    /// <summary>
    /// Split menu populated at runtime so it can be enabled/disabled in OnUpdate.
    /// </summary>
    internal sealed class SplitDynamicMenu : DynamicMenu
    {
        #region Fields

        private ViewModelWindowMain _viewModel;

        #endregion Fields

        /// <inheritdoc />
        protected override void OnUpdate()
        {
            if (_viewModel == null)
            {
                Enabled = false;
                DisabledTooltip = "HLU main window is not available.";
                return;
            }

            // Enable or disable the button based on CanSplit and main grid visibility.
            bool canPaste = (HLU.HLUToolModule.CanSplit && _viewModel.GridMainVisibility == Visibility.Visible);
            Enabled = canPaste;
        }

        /// <inheritdoc />
        protected override void OnPopup()
        {
            AddReference("HLUTool_btnPhysicalSplit");
            AddReference("HLUTool_btnLogicalSplit");
        }
    }
}