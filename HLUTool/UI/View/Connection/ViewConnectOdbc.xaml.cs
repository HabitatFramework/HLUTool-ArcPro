﻿// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
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

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using HLU.UI.ViewModel;

namespace HLU.UI.View.Connection
{
    /// <summary>
    /// Interaction logic for ViewConnectOdbc.xaml
    /// </summary>
    public partial class ViewConnectOdbc : Window
    {
        private IntPtr _windowHandle;

        public ViewConnectOdbc()
        {
            InitializeComponent();
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            if (PresentationSource.FromVisual(this) is HwndSource hwndSrc) _windowHandle = hwndSrc.Handle;

            ((ViewModelConnectOdbc)this.DataContext).ViewEvents(_windowHandle, null);

            if ((this.ComboBoxDsn.Items.Count == 1) && (String.IsNullOrEmpty(this.ComboBoxDsn.Text) ||
                this.ComboBoxDsn.Items.Contains(this.ComboBoxDsn.Text))) this.ComboBoxDsn.SelectedIndex = 0;
        }

        private void ComboBox_DropDownOpened(object sender, EventArgs e)
        {
            Binding bnd = BindingOperations.GetBinding((ComboBox)sender, ComboBox.SelectedItemProperty);
            if (bnd != null)
                ((ViewModel.ViewModelConnectOdbc)this.DataContext).ViewEvents(_windowHandle, bnd.Path.Path);
            else
                ((ViewModel.ViewModelConnectOdbc)this.DataContext).ViewEvents(_windowHandle, null);
        }
    }
}
