// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2011 Hampshire Biodiversity Information Centre
// Copyright © 2019 London & South East Record Centres (LaSER)
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

using System;
using System.Windows;
using HLU.UI.View;
using HLU.UI.ViewModel;
using ArcGIS.Desktop.Framework;

namespace HLU
{
    internal static class ShowMessageWindow
    {
        #region Fields

        private static MessageWindow _messageWindow;
        private static ViewModelWindowMessage _messageWindowViewModel;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes static members of the ShowMessageWindow class.
        /// </summary>
        /// <param name="messageText">The text of the message to display.</param>
        /// <param name="messageHeader">The header of the message to display.</param>
        internal static void ShowMessage(string messageText, string messageHeader)
        {
            // Create message window
            _messageWindow = new()
            {
                // Set ArcGIS Pro as the parent
                Owner = FrameworkApplication.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Topmost = true
            };

            // Create ViewModel to which main window binds
            _messageWindowViewModel = new()
            {
                MessageText = messageText,
                MessageHeader = messageHeader
            };

            // When ViewModel asks to be closed, close window
            _messageWindowViewModel.RequestClose -= CloseMessageWindow; // Safety: avoid double subscription.
            _messageWindowViewModel.RequestClose += new EventHandler(CloseMessageWindow);

            // Allow all controls in window to bind to ViewModel by setting DataContext
            _messageWindow.DataContext = _messageWindowViewModel;

            // Show window
            _messageWindow.ShowDialog();
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// Closes help window and removes close window handler
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        /// <remarks></remarks>
        internal static void CloseMessageWindow(object sender, EventArgs e)
        {
            _messageWindowViewModel.RequestClose -= CloseMessageWindow;
            //_messageWindow.Close();
        }

        #endregion Methods
    }
}