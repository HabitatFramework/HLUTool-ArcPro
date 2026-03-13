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

using System;
using System.Windows;
using System.Windows.Controls;

namespace HLU.UI.UserControls
{
    /// <summary>
    /// Helper class to enable binding the Password property of a PasswordBox. This is not possible
    /// by default because the Password property is not a DependencyProperty.
    /// </summary>
    static class PasswordBoxAssistant
    {
        #region DependencyProperties

        /// <summary>
        /// The BoundPassword attached property enables binding the Password property of a
        /// PasswordBox. When the BindPassword attached property is set to true, the BoundPassword
        /// attached property will be updated with the value of the Password property of the
        /// PasswordBox. When the BoundPassword attached property is updated, the Password property
        /// of the PasswordBox will be updated with the new value.
        /// </summary>
        public static readonly DependencyProperty BoundPassword =
            DependencyProperty.RegisterAttached("BoundPassword", typeof(string), typeof(PasswordBoxAssistant),
            new FrameworkPropertyMetadata(String.Empty, OnBoundPasswordChanged));

        /// <summary>
        /// The BindPassword attached property enables the behavior of the BoundPassword attached property.
        /// </summary>
        public static readonly DependencyProperty BindPassword = DependencyProperty.RegisterAttached(
            "BindPassword", typeof(bool), typeof(PasswordBoxAssistant), new PropertyMetadata(false, OnBindPasswordChanged));

        /// <summary>
        /// The UpdatingPassword attached property is used to avoid recursive updating of the
        /// Password property. When the Password property is being updated programmatically, this
        /// property is set to true to prevent the PasswordChanged event from being handled recursively.
        /// </summary>
        private static readonly DependencyProperty UpdatingPassword =
            DependencyProperty.RegisterAttached("UpdatingPassword", typeof(bool), typeof(PasswordBoxAssistant));

        #endregion DependencyProperties

        #region Public Methods

        /// <summary>
        /// Gets the value of the BindPassword attached property for a given DependencyObject. This
        /// indicates whether the behavior of the BoundPassword attached property is enabled for the
        /// given DependencyObject.
        /// </summary>
        /// <param name="dp">The DependencyObject for which to get the BindPassword value.</param>
        /// <returns>True if the BindPassword behavior is enabled; otherwise, false.</returns>
        public static bool GetBindPassword(DependencyObject dp)
        {
            return (bool)dp.GetValue(BindPassword);
        }

        /// <summary>
        /// Sets the value of the BindPassword attached property for a given DependencyObject. This
        /// enables or disables the behavior of the BoundPassword attached property for the given DependencyObject.
        /// </summary>
        /// <param name="dp">The DependencyObject for which to set the BindPassword value.</param>
        /// <param name="value">True to enable the BindPassword behavior; otherwise, false.</param>
        public static void SetBindPassword(DependencyObject dp, bool value)
        {
            dp.SetValue(BindPassword, value);
        }

        /// <summary>
        /// Gets the value of the BoundPassword attached property for a given DependencyObject. This
        /// indicates the current value of the BoundPassword property for the given DependencyObject.
        /// </summary>
        /// <param name="dp">The DependencyObject for which to get the BoundPassword value.</param>
        /// <returns>The current value of the BoundPassword property.</returns>
        public static string GetBoundPassword(DependencyObject dp)
        {
            return (string)dp.GetValue(BoundPassword);
        }

        /// <summary>
        /// Sets the value of the BoundPassword attached property for a given DependencyObject. This
        /// updates the current value of the BoundPassword property for the given DependencyObject.
        /// </summary>
        /// <param name="dp">The DependencyObject for which to set the BoundPassword value.</param>
        /// <param name="value">The new value to set for the BoundPassword property.</param>
        public static void SetBoundPassword(DependencyObject dp, string value)
        {
            dp.SetValue(BoundPassword, value);
        }

        #endregion Public Methods

        #region Private Methods

        /// <summary>
        /// When the BoundPassword attached property is updated, this method updates the Password property
        /// of the associated PasswordBox.
        /// </summary>
        /// <param name="d">The DependencyObject for which the BoundPassword property has changed.</param>
        /// <param name="e">The event data for the property change.</param>
        private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            PasswordBox box = d as PasswordBox;

            // Only handle this event when the property is attached to a PasswordBox
            // and when the BindPassword attached property has been set to true
            if ((d == null) || !GetBindPassword(d)) return;

            // Avoid recursive updating by ignoring the box's changed event
            box.PasswordChanged -= HandlePasswordChanged;

            string newPassword = (string)e.NewValue;

            if (!GetUpdatingPassword(box)) box.Password = newPassword;

            box.PasswordChanged += HandlePasswordChanged;
        }

        /// <summary>
        /// When BindPassword attached property is set on a PasswordBox start listening to its
        /// PasswordChanged event
        /// </summary>
        /// <param name="dp">The DependencyObject for which the BindPassword property has changed.</param>
        /// <param name="e">The event data for the property change.</param>
        private static void OnBindPasswordChanged(DependencyObject dp, DependencyPropertyChangedEventArgs e)
        {
            if (dp is not PasswordBox box) return;

            bool wasBound = (bool)e.OldValue;
            bool needToBind = (bool)e.NewValue;

            if (wasBound) box.PasswordChanged -= HandlePasswordChanged;

            if (needToBind) box.PasswordChanged += HandlePasswordChanged;
        }

        /// <summary>
        /// Handles the PasswordChanged event of the PasswordBox. This method updates the
        /// BoundPassword attached property
        /// </summary>
        /// <param name="sender">The PasswordBox whose password has changed.</param>
        /// <param name="e">The event data for the password change.</param>
        private static void HandlePasswordChanged(object sender, RoutedEventArgs e)
        {
            PasswordBox box = sender as PasswordBox;

            // Set flag to indicate we're updating password
            SetUpdatingPassword(box, true);

            // Push new password into BoundPassword property
            SetBoundPassword(box, box.Password);
            SetUpdatingPassword(box, false);
        }

        /// <summary>
        /// Gets the value of the UpdatingPassword attached property for a given DependencyObject.
        /// This property indicates whether the password is currently being updated.
        /// </summary>
        /// <param name="dp">The DependencyObject for which to get the UpdatingPassword value.</param>
        /// <returns>True if the password is being updated; otherwise, false.</returns>
        private static bool GetUpdatingPassword(DependencyObject dp)
        {
            return (bool)dp.GetValue(UpdatingPassword);
        }

        /// <summary>
        /// Sets the value of the UpdatingPassword attached property for a given DependencyObject. This
        /// property indicates whether the password is currently being updated.
        /// </summary>
        /// <param name="dp">The DependencyObject for which to set the UpdatingPassword value.</param>
        /// <param name="value">True if the password is being updated; otherwise, false.</param>
        private static void SetUpdatingPassword(DependencyObject dp, bool value)
        {
            dp.SetValue(UpdatingPassword, value);
        }

        #endregion Private Methods
    }
}