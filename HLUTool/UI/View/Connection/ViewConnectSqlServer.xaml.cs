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
    /// Interaction logic for ViewConnectSqlServer.xaml
    /// </summary>
    public partial class ViewConnectSqlServer : ProWindow
    {
        private IntPtr _windowHandle;

        public ViewConnectSqlServer()
        {
            InitializeComponent();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            if (PresentationSource.FromVisual(this) is HwndSource hwndSrc)
                _windowHandle = hwndSrc.Handle;

            if ((this.ComboBoxServer.Items.Count == 1) && (String.IsNullOrEmpty(this.ComboBoxServer.Text) ||
                this.ComboBoxServer.Items.Contains(this.ComboBoxServer.Text)))
                this.ComboBoxServer.SelectedIndex = 0;
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            if (DataContext is not ViewModelConnectSqlServer vm)
                return;

            // Force WPF to validate all required fields immediately on load so that
            // error adorners appear for blank values without the user having to interact first.
            vm.NotifyValidationOnLoad(
            [
                BindingOperations.GetBindingExpression(ComboBoxServer, ComboBox.TextProperty),
                BindingOperations.GetBindingExpression(TextBoxUsername, TextBox.TextProperty),
                BindingOperations.GetBindingExpression(ComboBoxDatabase, ComboBox.TextProperty),
                BindingOperations.GetBindingExpression(ComboBoxSchema, ComboBox.TextProperty),
            ]);
        }

        private void ComboBox_DropDownOpened(object sender, EventArgs e)
        {
            Binding bnd = BindingOperations.GetBinding((ComboBox)sender, ComboBox.TextProperty);
            if (bnd != null)
                ((ViewModelConnectSqlServer)this.DataContext).ViewEvents(_windowHandle, bnd.Path.Path);
            else
                ((ViewModelConnectSqlServer)this.DataContext).ViewEvents(_windowHandle, null);
        }

        private void ComboBoxServer_TextChanged(object sender, TextChangedEventArgs e)
        {
            // WPF suppresses pushing an empty string back to the source via PropertyChanged
            // on an editable ComboBox. Force UpdateSource when the text is cleared so that
            // the Server setter fires and resets the dependent databases/schemata lists.
            if (string.IsNullOrEmpty(ComboBoxServer.Text))
                BindingOperations.GetBindingExpression(ComboBoxServer, ComboBox.TextProperty)?.UpdateSource();
        }
    }
}