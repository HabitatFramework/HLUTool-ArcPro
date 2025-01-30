// The DataTools are a suite of ArcGIS Pro addins used to extract, sync
// and manage biodiversity information from ArcGIS Pro and SQL Server
// based on pre-defined or user specified criteria.
//
// Copyright © 2024 Andy Foy Consulting.
//
// This file is part of DataTools suite of programs..
//
// DataTools are free software: you can redistribute it and/or modify
// them under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// DataTools are distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with with program.  If not, see <http://www.gnu.org/licenses/>.

using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HLU.UI.View
{
    /// <summary>
    /// Interaction logic for WindowMain.xaml
    /// </summary>
    public partial class WindowMain : UserControl
    {
        private ComboBox[] _comboBoxes;

        public WindowMain()
        {
            InitializeComponent();
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            IInputElement focusedElement = Keyboard.FocusedElement;
            Control focusedControl = focusedElement as Control;

            // Ignore keyup if focus is on a data grid cell
            if ((focusedControl != null) && (focusedControl.Parent is DataGridCell))
                return;

            // Ignore keyup if any comboboxes are currently open
            foreach (ComboBox cbx in _comboBoxes)
                if (cbx.IsDropDownOpen) return;

            // Action any "movement" keys
            switch (e.Key)
            {
                case Key.Tab:
                    // When the tab key is used in the OSMM Record
                    // Number text box (which is the last field on
                    // the form) then tab away from (and back to)
                    // the text box to trigger the property changed
                    // event.
                    if (this.TextBoxOSMMRecordNumber.IsFocused)
                    {
                        this.TextBoxIncid.Focus();
                        this.TextBoxOSMMRecordNumber.Focus();
                    }
                    break;
                case Key.Return:
                    // When the return key is used in either of the
                    // Record Number text boxes then tab away from
                    // (and back to) the text box to trigger the
                    // relevant property changed event.
                    if (this.TextBoxRecordNumber.IsFocused)
                    {
                        this.TextBoxIncid.Focus();
                        this.TextBoxRecordNumber.Focus();
                    }
                    //if (this.TextBoxOSMMRecordNumber.IsFocused)
                    //{
                    //    this.TextBoxIncid.Focus();
                    //    this.TextBoxOSMMRecordNumber.Focus();
                    //}
                    break;
                case Key.Home:
                    if ((focusedControl != null) && (focusedControl is TextBox)) return;
                    if (this.ButtonFirstRecord.Command.CanExecute(null))
                        this.ButtonFirstRecord.Command.Execute(null);
                    break;
                case Key.Prior:
                    if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                        goto case Key.Prior;
                    else if (this.ButtonPreviousRecord.Command.CanExecute(null))
                        this.ButtonPreviousRecord.Command.Execute(null);
                    break;
                case Key.Next:
                    if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                        goto case Key.End;
                    else if (this.ButtonNextRecord.Command.CanExecute(null))
                        this.ButtonNextRecord.Command.Execute(null);
                    break;
                case Key.End:
                    if ((focusedControl != null) && (focusedControl is TextBox)) return;
                    if (this.ButtonLastRecord.Command.CanExecute(null))
                        this.ButtonLastRecord.Command.Execute(null);
                    break;
            }

            // Note if the control key is pressed
            if (Keyboard.Modifiers != ModifierKeys.Control)
            {
                this.ButtonOSMMAccept.Tag = "";
                this.ButtonOSMMReject.Tag = "";
            }

        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                this.ButtonOSMMAccept.Tag = "Ctrl";
                this.ButtonOSMMReject.Tag = "Ctrl";
            }
        }

        private void EditableComboBox_KeyUp(object sender, KeyEventArgs e)
        {
            ValidateComboboxText(sender as ComboBox);
        }

        private void ValidateComboboxText(ComboBox cb)
        {
            if ((cb == null) || (cb.Items.Count == 0)) return;

            PropertyInfo pi = cb.Items[0].GetType().GetProperty(cb.DisplayMemberPath);

            for (int i = 0; i < cb.Items.Count; i++)
            {
                if (pi.GetValue(cb.Items[i], null).ToString().Equals(cb.Text))
                    return;
            }

            if (cb.SelectedIndex != -1)
            {
                cb.Text = pi.GetValue(cb.SelectedItem, null).ToString();
            }
            else
            {
                TextBox tbx = (TextBox)cb.Template.FindName("PART_EditableTextBox", cb);
                int caretIx = tbx.CaretIndex;

                // Check combobox text is not null before finding list item
                if (!string.IsNullOrEmpty(cb.Text))
                {
                    string validText = cb.Text.Substring(0, caretIx < 1 ? 0 : caretIx);
                    //string validText = cb.Text.Substring(0, caretIx < 1 ? 0 : caretIx - 1);
                    for (int i = 0; i < cb.Items.Count; i++)
                    {
                        if (pi.GetValue(cb.Items[i], null).ToString().StartsWith(validText))
                        {
                            cb.SelectedIndex = i;
                            tbx.CaretIndex = caretIx;
                            return;
                        }
                    }
                    cb.Text = null;
                }
            }
        }
    }
}