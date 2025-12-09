using HLU.UI.ViewModel;
using System;
using System.Collections.Generic;
using ArcGIS.Desktop.Framework.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ArcGIS.Desktop.Framework.Dialogs;

namespace HLU.UI.UserControls.Toolbar
{
    /// <summary>
    /// Button implementation to show the DockPane.
    /// </summary>
    internal class MainWindowButton : Button
    {
        protected override async void OnClick()
        {
            // Show the dock pane.
            try
            {
                await ViewModelWindowMain.ShowDockPane();
            }
            catch (Exception ex)
            {
                // Surface hidden exceptions while debugging.
                MessageBox.Show($"Error starting HLU Tool:{Environment.NewLine}{ex.Message}.", "HLU Tool error.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }
    }

}
