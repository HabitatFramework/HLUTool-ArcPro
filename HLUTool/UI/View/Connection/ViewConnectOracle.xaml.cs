// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
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

using ArcGIS.Desktop.Framework.Controls;
using HLU.UI.ViewModel;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;

namespace HLU.UI.View.Connection
{
    /// <summary>
    /// Interaction logic for ViewConnectOracle.xaml
    /// </summary>
    public partial class ViewConnectOracle : ProWindow
    {
        IntPtr _windowHandle;

        public ViewConnectOracle()
        {
            InitializeComponent();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            if (PresentationSource.FromVisual(this) is HwndSource hwndSrc) _windowHandle = hwndSrc.Handle;

            if (this.ComboBoxDataSource.Items.Count == 1) this.ComboBoxDataSource.SelectedIndex = 0;
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            BindingOperations.GetBindingExpression(ComboBoxDataSource, ComboBox.TextProperty)?.UpdateSource();
            BindingOperations.GetBindingExpression(TextBoxUserID, TextBox.TextProperty)?.UpdateSource();
            BindingOperations.GetBindingExpression(ComboBoxDefaultSchema, ComboBox.TextProperty)?.UpdateSource();

            // Force WPF to re-evaluate IDataErrorInfo validation on the validated fields
            // so that error adorners appear immediately when the window first opens with blank values.
            if (DataContext is ViewModelConnectOracle vm)
            {
                vm.NotifyValidationOnLoad();
            }
        }

        private void ComboBox_DropDownOpened(object sender, EventArgs e)
        {
            Binding bnd = BindingOperations.GetBinding((ComboBox)sender, ComboBox.SelectedItemProperty);
            if (bnd != null)
                ((ViewModelConnectOracle)this.DataContext).ViewEvents(_windowHandle, bnd.Path.Path);
            else
                ((ViewModelConnectOracle)this.DataContext).ViewEvents(_windowHandle, null);
        }
    }
}