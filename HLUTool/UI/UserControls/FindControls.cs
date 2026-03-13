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
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace HLU.UI.UserControls
{
    /// <summary>
    /// Provides methods to find controls in the visual and logical tree of a WPF application.
    /// </summary>
    static class FindControls
    {
        /// <summary>
        /// Finds all logical children of a given type in the logical tree of a WPF application.
        /// </summary>
        /// <typeparam name="T">The type of the logical children to find.</typeparam>
        /// <param name="depObj">The parent dependency object.</param>
        /// <returns>An enumerable of logical children of the specified type.</returns>
        public static IEnumerable<T> FindLogicalChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                foreach (object c in LogicalTreeHelper.GetChildren(depObj))
                {
                    DependencyObject child = c as DependencyObject;
                    if ((child != null) && (child is T typedChild))
                    {
                        yield return typedChild;
                    }

                    foreach (T childOfChild in FindLogicalChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        /// <summary>
        /// Finds all visual children of a given type in the visual tree of a WPF application.
        /// </summary>
        /// <typeparam name="T">The type of the visual children to find.</typeparam>
        /// <param name="depObj">The parent dependency object.</param>
        /// <returns>An enumerable of visual children of the specified type.</returns>
        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T typedChild)
                    {
                        yield return typedChild;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        /// <summary>
        /// Finds all visual children of a given type in the visual tree of a WPF application and adds them to a provided list.
        /// </summary>
        /// <param name="reference">The parent dependency object.</param>
        /// <param name="childType">The type of the visual children to find.</param>
        /// <param name="childrenList">The list to which the found children will be added.</param>
        /// <returns>The first found child of the specified type, or null if none are found.</returns>
        public static DependencyObject GetChildren(this DependencyObject reference, Type childType,
            ref List<DependencyObject> childrenList)
        {
            DependencyObject foundChild = null;
            childrenList ??= [];
            if (reference != null)
            {
                int childrenCount = VisualTreeHelper.GetChildrenCount(reference);
                for (int i = 0; i < childrenCount; i++)
                {
                    var child = VisualTreeHelper.GetChild(reference, i);
                    if (child.GetType() != childType)
                    {
                        foundChild = GetChildren(child, childType, ref childrenList);
                    }
                    else
                    {
                        childrenList.Add(child);
                    }
                }
            }
            return foundChild;
        }

        /// <summary>
        /// Finds a child of a given type and name in the visual tree of a WPF application.
        /// </summary>
        /// <param name="reference">The parent dependency object.</param>
        /// <param name="childName">The name of the child to find.</param>
        /// <param name="childType">The type of the child to find.</param>
        /// <returns>The first found child of the specified type and name, or null if none are found.</returns>
        public static DependencyObject FindChild(this DependencyObject reference, string childName, Type childType)
        {
            DependencyObject foundChild = null;
            if (reference != null)
            {
                int childrenCount = VisualTreeHelper.GetChildrenCount(reference);
                for (int i = 0; i < childrenCount; i++)
                {
                    var child = VisualTreeHelper.GetChild(reference, i);
                    if (child.GetType() != childType)
                    {
                        foundChild = FindChild(child, childName, childType);
                    }
                    else if (!String.IsNullOrEmpty(childName))
                    {
                        if (child is FrameworkElement frameworkElement && frameworkElement.Name == childName)
                        {
                            foundChild = child;
                            break;
                        }
                    }
                    else
                    {
                        foundChild = child;
                        break;
                    }
                }
            }
            return foundChild;
        }
    }
}