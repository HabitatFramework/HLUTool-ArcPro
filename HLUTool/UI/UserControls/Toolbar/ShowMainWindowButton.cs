using HLU.UI.ViewModel;
using System;
using System.Collections.Generic;
using ArcGIS.Desktop.Framework.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HLU.UI.UserControls.Toolbar
{
    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class ShowMainWindowButton : Button
    {
        protected override async void OnClick()
        {
            // Show the dock pane.
            await ViewModelWindowMain.ShowDockPane();
        }
    }

}
