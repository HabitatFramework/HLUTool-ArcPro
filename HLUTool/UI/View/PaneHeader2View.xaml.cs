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

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HLU.UI.ViewModel;

namespace HLU.UI.View
{
    /// <summary>
    /// Interaction logic for PaneHeader2View.xaml
    /// </summary>
    public partial class PaneHeader2View : System.Windows.Controls.UserControl
    {
        public PaneHeader2View()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Ensure any removed result summary items are actually unselected.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListViewResultSummary_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Get the list of removed items.
            List<ResultSummary> removed = e.RemovedItems.OfType<ResultSummary>().ToList();

            // Ensure any removed items are actually unselected.
            if (removed.Count > 1)
            {
                // Unselect the removed items.
                e.RemovedItems.OfType<ResultSummary>().ToList().ForEach(p => p.IsSelected = false);

                // Get the list of currently selected items.
                var listView = sender as System.Windows.Controls.ListView;
                var selectedItems = listView.SelectedItems.OfType<ResultSummary>().ToList();

                if (selectedItems.Count == 1)
                    listView.Items.OfType<ResultSummary>().ToList().Where(s => selectedItems.All(s2 => s2.Type != s.Type)).ToList().ForEach(p => p.IsSelected = false);
            }
        }

        /// <summary>
        /// Ensure any removed result details are actually unselected.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListViewResultDetail_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Get the list of removed items.
            List<ResultDetail> removed = e.RemovedItems.OfType<ResultDetail>().ToList();

            // Ensure any removed items are actually unselected.
            if (removed.Count > 1)
            {
                // Unselect the removed items.
                e.RemovedItems.OfType<ResultDetail>().ToList().ForEach(p => p.IsSelected = false);

                // Get the list of currently selected items.
                var listView = sender as System.Windows.Controls.ListView;
                var selectedItems = listView.SelectedItems.OfType<ResultDetail>().ToList();

                if (selectedItems.Count == 1)
                    listView.Items.OfType<ResultDetail>().ToList().Where(s => selectedItems.All(s2 => s2.NewKey != s.NewKey)).ToList().ForEach(p => p.IsSelected = false);
            }
        }

        /// <summary>
        /// Display the details when a result item is double-clicked.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ListViewResultDetail_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (this.ButtonZoomToDetail.Command.CanExecute(null))
                this.ButtonZoomToDetail.Command.Execute(null);
        }

        /// <summary>
        /// Return the first visual child object of the required type
        /// for the specified object.
        /// </summary>
        /// <typeparam name="childItem"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        private static childItem FindVisualChild<childItem>(DependencyObject obj)
               where childItem : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is childItem item)
                    return item;
                else
                {
                    childItem childOfChild = FindVisualChild<childItem>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }

            return null;
        }
    }
}