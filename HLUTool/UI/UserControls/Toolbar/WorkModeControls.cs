// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2024 Andy Foy Consulting
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

using ArcGIS.Desktop.Framework;
using ArcGIS.Desktop.Framework.Contracts;
using HLU.UI.ViewModel;
using System;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace HLU.UI.UserControls.Toolbar
{
    #region WorkModeButton

    /// <summary>
    /// Button that displays the current work mode (non-interactive indicator).
    /// This button shows which mode is currently active but does not trigger
    /// any action when clicked. Users switch modes using the dropdown menu.
    /// </summary>
    internal sealed class WorkModeButton : Button
    {
        private static WorkModeButton _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// The constructor sets the static instance reference so that mode buttons
        /// can update its display when clicked.
        /// </summary>
        public WorkModeButton()
        {
            // Set instance immediately when constructor is called
            lock (_lock)
            {
                if (_instance == null)
                    _instance = this;
            }
        }

        /// <summary>
        /// Gets the singleton instance of the WorkModeButton to allow mode buttons
        /// to update its display when the mode changes.
        /// </summary>
        /// <returns>The singleton instance of WorkModeButton.</returns>
        public static WorkModeButton GetInstance()
        {
            return _instance;
        }

        /// <summary>
        /// Forces the framework to create the button instance if it doesn't exist.
        /// Call this during plugin initialization to ensure the button is available.
        /// </summary>
        public static void EnsureInitialized()
        {
            if (_instance != null)
                return;

            // Force the framework to instantiate the button
            var plugin = FrameworkApplication.GetPlugInWrapper("HLUTool_btnWorkMode");
            if (plugin != null)
            {
                // Accessing the plugin forces instantiation
                _ = plugin.Caption;
            }
        }

        protected override void OnUpdate()
        {
            // Ensure singleton is set (safety net)
            lock (_lock)
            {
                if (_instance == null)
                    _instance = this;
            }
        }

        /// <summary>
        /// Override OnClick to prevent any action when the button is clicked,
        /// as this is just a visual indicator of the current mode.
        /// </summary>
        protected override void OnClick()
        {
            // No action - this is just a visual indicator
        }

        /// <summary>
        /// Updates the display of the mode button with the given caption and images.
        /// Called by mode selection buttons when the user switches modes.
        /// </summary>
        /// <param name="caption">The caption to display on the button (e.g., "Update Mode").</param>
        /// <param name="smallImagePath">The pack URI path to the small (16x16) image.</param>
        /// <param name="largeImagePath">The pack URI path to the large (32x32) image.</param>
        public void UpdateDisplay(string caption, string smallImagePath, string largeImagePath)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateDisplay called: {caption}");

            Caption = caption;
            System.Diagnostics.Debug.WriteLine($"Caption set to: {Caption}");

            if (!string.IsNullOrEmpty(smallImagePath))
                SmallImage = new BitmapImage(new Uri(smallImagePath));

            if (!string.IsNullOrEmpty(largeImagePath))
                LargeImage = new BitmapImage(new Uri(largeImagePath));

            System.Diagnostics.Debug.WriteLine($"About to call NotifyPropertyChanged...");
            NotifyPropertyChanged(nameof(Caption));
            NotifyPropertyChanged(nameof(SmallImage));
            NotifyPropertyChanged(nameof(LargeImage));
            NotifyPropertyChanged(nameof(Tooltip));
            System.Diagnostics.Debug.WriteLine($"NotifyPropertyChanged called");
        }

        /// <summary>
        /// Static helper that ensures UI thread and forces refresh.
        /// </summary>
        public static void UpdateWorkModeDisplay(string caption, string smallImagePath, string largeImagePath)
        {
            // Ensure we're on the UI thread
            FrameworkApplication.Current.Dispatcher.Invoke(() =>
            {
                // Ensure the button is initialized first
                EnsureInitialized();

                // Get the instance
                var button = GetInstance();
                if (button != null)
                {
                    // CRITICAL: Update Tooltip to force UI refresh
                    button.Tooltip = $"{caption} - Current work mode";

                    // Update the button properties
                    button.Caption = caption;

                    if (!string.IsNullOrEmpty(smallImagePath))
                        button.SmallImage = new BitmapImage(new Uri(smallImagePath));

                    if (!string.IsNullOrEmpty(largeImagePath))
                        button.LargeImage = new BitmapImage(new Uri(largeImagePath));

                    // Notify of changes
                    button.NotifyPropertyChanged(nameof(Caption));
                    button.NotifyPropertyChanged(nameof(SmallImage));
                    button.NotifyPropertyChanged(nameof(LargeImage));
                    button.NotifyPropertyChanged(nameof(Tooltip)); // Triggers UI refresh

                    System.Diagnostics.Debug.WriteLine($"WorkModeButton updated: {caption}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("WARNING: WorkModeButton instance is null");
                }

                // Force ribbon to re-evaluate all button states
                CommandManager.InvalidateRequerySuggested();

                var button2 = GetInstance();
            });
        }
    }

    #endregion WorkModeButton

    #region WorkModeDynamicMenu

    /// <summary>
    /// Dynamic menu that provides mode selection buttons. Populated at runtime
    /// with references to the four mode buttons defined in Config.daml.
    /// </summary>
    internal sealed class WorkModeDynamicMenu : DynamicMenu
    {
        /// <summary>
        /// Override OnPopup to add references to the mode buttons so they are included in
        /// the menu and can be enabled/disabled in OnUpdate of each button.
        /// This method is called by the framework when the user opens the dropdown.
        /// </summary>
        protected override void OnPopup()
        {
            AddReference("HLUTool_UpdateModeButton");
            AddReference("HLUTool_OSMMUpdateModeButton");
            AddReference("HLUTool_BulkUpdateModeButton");
            AddReference("HLUTool_OSMMBulkUpdateModeButton");

            // Force all buttons to refresh their state
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    #endregion WorkModeDynamicMenu

    #region WorkModeButtonBase

    /// <summary>
    /// Base class for mode selection buttons.
    /// Provides common functionality for all mode buttons including:
    /// - Access to the main ViewModel
    /// - Mode switching logic
    /// - Enable/disable state management
    /// - Active mode indication via caption suffix
    /// </summary>
    internal abstract class WorkModeButtonBase : Button
    {
        // Cached reference to the main ViewModel
        protected readonly ViewModelWindowMain _viewModel;

        protected abstract WorkMode TargetMode { get; }
        protected abstract string ModeName { get; }
        protected abstract string SmallImagePath { get; }
        protected abstract string LargeImagePath { get; }

        /// <summary>
        /// Determines if this mode can be activated based on application state.
        /// Returns false if the mode is already active or if conditions aren't met.
        /// </summary>
        protected abstract bool CanActivate();

        /// <summary>
        /// Determines if this mode is currently active based on the main view model's WorkMode.
        /// Used to disable the button when already active and to show "(Active)" in caption.
        /// </summary>
        protected abstract bool IsCurrentMode();

        /// <summary>
        /// Constructor gets the main view model from the dockpane and caches it
        /// for efficient access by derived classes.
        /// </summary>
        public WorkModeButtonBase()
        {
            // Get the dockpane DAML id and retrieve the ViewModel instance
            DockPane pane = FrameworkApplication.DockPaneManager.Find(ViewModelWindowMain.DockPaneID);
            _viewModel = pane as ViewModelWindowMain;
        }

        /// <summary>
        /// Override OnClick to set the work mode in the main view model and update
        /// the mode button display. Called by the framework when user clicks the button.
        /// </summary>
        protected override void OnClick()
        {
            if (_viewModel == null)
                return;

            // Switch to the selected mode (this may trigger async operations)
            _viewModel.SetWorkMode(TargetMode);
        }

        /// <summary>
        /// Called by framework to determine if button should be enabled.
        /// Also adds "(Active)" suffix to caption for the currently active mode.
        /// </summary>
        protected override void OnUpdate()
        {
            // Enable button if we can activate this mode (disabled if already active)
            Enabled = CanActivate();

            // Add "(Active)" to caption for current mode
            if (IsCurrentMode())
                Caption = $"{ModeName} (Active)";
            else
                Caption = ModeName;
        }
    }

    #endregion WorkModeButtonBase

    #region Individual Mode Buttons

    /// <summary>
    /// Button to switch to normal Update mode.
    /// This is the default mode for editing individual incids.
    /// </summary>
    internal sealed class UpdateModeButton : WorkModeButtonBase
    {
        protected override WorkMode TargetMode => WorkMode.Edit;
        protected override string ModeName => "Update Mode";
        protected override string SmallImagePath =>
            "pack://application:,,,/HLUTool;component/Images/EditMode16.png";
        protected override string LargeImagePath =>
            "pack://application:,,,/HLUTool;component/Images/EditMode32.png";

        /// <summary>
        /// Update mode is always available unless already active (it's the cancel/default mode).
        /// </summary>
        protected override bool CanActivate()
        {
            if (_viewModel == null)
                return false;

            // Update mode is always available unless already active
            return !IsCurrentMode();
        }

        /// <summary>
        /// Current mode is Update mode if the main view model's WorkMode has ONLY
        /// the Edit flag set (no Bulk, OSMM, etc.)
        /// </summary>
        /// <returns>True if the current mode is Update mode; otherwise, false.</returns>
        protected override bool IsCurrentMode()
        {
            if (_viewModel == null)
                return false;

            // Active if ONLY Edit flag is set (no Bulk, OSMM, etc.)
            return _viewModel.WorkMode == WorkMode.Edit;
        }
    }

    /// <summary>
    /// Button to switch to OSMM Update mode.
    /// Allows review and acceptance/rejection of OSMM updates for individual incids.
    /// </summary>
    internal sealed class OSMMUpdateModeButton : WorkModeButtonBase
    {
        protected override WorkMode TargetMode => WorkMode.Edit | WorkMode.OSMMReview;
        protected override string ModeName => "OSMM Update Mode";
        protected override string SmallImagePath =>
            "pack://application:,,,/HLUTool;component/Images/OSMMUpdate16.png";
        protected override string LargeImagePath =>
            "pack://application:,,,/HLUTool;component/Images/OSMMUpdate32.png";

        /// <summary>
        /// Can activate OSMM Update mode if the main view model indicates it's possible
        /// based on application state (e.g., OSMM updates are available) and the mode
        /// is not already active.
        /// </summary>
        /// <returns>True if the mode can be activated; otherwise, false.</returns>
        protected override bool CanActivate()
        {
            if (_viewModel == null)
                return false;

            // Can't activate if already active
            if (IsCurrentMode())
                return false;

            // Check if mode can be activated based on application state
            return _viewModel.CanOSMMUpdateMode;
        }

        /// <summary>
        /// Current mode is OSMM Update mode if the main view model's WorkMode has
        /// the OSMMReview flag set.
        /// </summary>
        /// <returns>True if the current mode is OSMM Update mode; otherwise, false.</returns>
        protected override bool IsCurrentMode()
        {
            if (_viewModel == null)
                return false;

            return _viewModel.WorkMode.HasFlag(WorkMode.OSMMReview);
        }
    }

    /// <summary>
    /// Button to switch to Bulk Update mode.
    /// Allows applying updates to multiple incids at once.
    /// </summary>
    internal sealed class BulkUpdateModeButton : WorkModeButtonBase
    {
        protected override WorkMode TargetMode => WorkMode.Edit | WorkMode.Bulk;
        protected override string ModeName => "Bulk Update Mode";
        protected override string SmallImagePath =>
            "pack://application:,,,/HLUTool;component/Images/BulkUpdate16.png";
        protected override string LargeImagePath =>
            "pack://application:,,,/HLUTool;component/Images/BulkUpdate32.png";

        /// <summary>
        /// Can activate Bulk Update mode if the main view model indicates it's possible
        /// based on application state (e.g., multiple records are filtered/selected) and
        /// the mode is not already active.
        /// </summary>
        /// <returns>True if the mode can be activated; otherwise, false.</returns>
        protected override bool CanActivate()
        {
            if (_viewModel == null)
                return false;

            // Can't activate if already active
            if (IsCurrentMode())
                return false;

            return _viewModel.CanBulkUpdate;
        }

        /// <summary>
        /// Current mode is Bulk Update mode if the main view model's WorkMode has
        /// the Bulk flag set but NOT the OSMMBulk flag (to differentiate from
        /// Bulk OSMM Update mode).
        /// </summary>
        /// <returns>True if the current mode is Bulk Update mode; otherwise, false.</returns>
        protected override bool IsCurrentMode()
        {
            if (_viewModel == null)
                return false;

            return _viewModel.WorkMode.HasFlag(WorkMode.Bulk) &&
                   !_viewModel.WorkMode.HasFlag(WorkMode.OSMMBulk);
        }
    }

    /// <summary>
    /// Button to switch to Bulk OSMM Update mode.
    /// Allows applying accepted OSMM updates to multiple incids in bulk.
    /// </summary>
    internal sealed class OSMMBulkUpdateModeButton : WorkModeButtonBase
    {
        protected override WorkMode TargetMode => WorkMode.Edit | WorkMode.Bulk | WorkMode.OSMMBulk;
        protected override string ModeName => "Bulk OSMM Update Mode";
        protected override string SmallImagePath =>
            "pack://application:,,,/HLUTool;component/Images/OSMMBulkUpdate16.png";
        protected override string LargeImagePath =>
            "pack://application:,,,/HLUTool;component/Images/OSMMBulkUpdate32.png";

        /// <summary>
        /// Can activate Bulk OSMM Update mode if the main view model indicates it's possible
        /// based on application state (e.g., OSMM updates exist for filtered records) and
        /// the mode is not already active.
        /// </summary>
        /// <returns>True if the mode can be activated; otherwise, false.</returns>
        protected override bool CanActivate()
        {
            if (_viewModel == null)
                return false;

            // Can't activate if already active
            if (IsCurrentMode())
                return false;

            return _viewModel.CanOSMMBulkUpdateMode;
        }

        /// <summary>
        /// Current mode is Bulk OSMM Update mode if the main view model's WorkMode
        /// has the OSMMBulk flag set.
        /// </summary>
        /// <returns>True if the current mode is Bulk OSMM Update mode; otherwise, false.</returns>
        protected override bool IsCurrentMode()
        {
            if (_viewModel == null)
                return false;

            return _viewModel.WorkMode.HasFlag(WorkMode.OSMMBulk);
        }
    }

    #endregion Individual Mode Buttons
}