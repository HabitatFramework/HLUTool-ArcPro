// HLUTool is used to view and maintain habitat and land use GIS data.
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

using HLU.Data;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// ViewModel for the OSMM xref preview window. Displays a summary of the
    /// unique OSMM attribute combinations found in the selected features, together
    /// with the match result from <c>lut_osmm_habitat_xref</c>.
    /// </summary>
    internal sealed class ViewModelWindowOSMMXrefPreview : ViewModelBase
    {
        #region Fields

        private string _displayName = "OSMM Attribute Preview";
        private ICommand _proceedCommand;
        private ICommand _cancelCommand;
        private ICommand _exportCsvCommand;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initialises the preview ViewModel with the rows to display.
        /// </summary>
        /// <param name="rows">
        /// The preview rows built from the selected features and the xref cache.
        /// </param>
        public ViewModelWindowOSMMXrefPreview(IEnumerable<OsmmXrefPreviewRow> rows)
        {
            OsmmXrefPreviewRows = new ObservableCollection<OsmmXrefPreviewRow>(rows);
            HasUnmatched = OsmmXrefPreviewRows.Any(r => !r.IsMatched);
        }

        #endregion Constructor

        #region ViewModelBase Members

        /// <inheritdoc />
        public override string DisplayName
        {
            get => _displayName;
            set => _displayName = value;
        }

        /// <inheritdoc />
        public override string WindowTitle => DisplayName;

        #endregion ViewModelBase Members

        #region RequestClose

        /// <summary>
        /// Delegate for the <see cref="RequestClose"/> event.
        /// </summary>
        public delegate void RequestCloseEventHandler(bool proceed);

        /// <summary>
        /// Raised when the window should close. The parameter is <see langword="true"/>
        /// if the user chose to proceed with the load, <see langword="false"/> if cancelled.
        /// </summary>
        public event RequestCloseEventHandler RequestClose;

        #endregion RequestClose

        #region Properties

        /// <summary>
        /// Gets the rows to display in the preview grid.
        /// </summary>
        public ObservableCollection<OsmmXrefPreviewRow> OsmmXrefPreviewRows
        {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether any rows have no match in
        /// <c>lut_osmm_habitat_xref</c>.
        /// </summary>
        public bool HasUnmatched
        {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether any rows have invalid primary or secondary codes
        /// for the active layer geometry type.
        /// </summary>
        public bool HasInvalidCodes => OsmmXrefPreviewRows.Any(r => r.IsMatched && (!r.IsPrimaryValid || !r.AreSecondariesValid));

        /// <summary>
        /// Gets the warning message shown when unmatched combinations exist.
        /// Returns <see langword="null"/> when all combinations are matched so the
        /// warning panel collapses automatically via a <c>DataTrigger</c> binding.
        /// </summary>
        public string UnmatchedWarning => HasUnmatched
            ? "Warning: one or more attribute combinations were not found in " +
              "lut_osmm_habitat_xref. Features with no match will be loaded " +
              "without habitat values assigned."
            : null;

        /// <summary>
        /// Gets the warning message shown when invalid habitat codes exist.
        /// Returns <see langword="null"/> when all codes are valid so the
        /// warning panel collapses automatically via a <c>DataTrigger</c> binding.
        /// </summary>
        public string InvalidCodesWarning => HasInvalidCodes
            ? "Warning: one or more habitat codes are not valid for the active " +
              "layer geometry type (polygon/line/point). " +
              "Features with invalid codes will be loaded without those habitat values."
            : null;

        #endregion Properties

        #region Proceed Command

        /// <summary>
        /// Gets the command that closes the window and proceeds with the load.
        /// </summary>
        public ICommand ProceedCommand
        {
            get
            {
                _proceedCommand ??= new RelayCommand(_ => RequestClose?.Invoke(true));
                return _proceedCommand;
            }
        }

        #endregion Proceed Command

        #region Cancel Command

        /// <summary>
        /// Gets the command that closes the window and cancels the load.
        /// </summary>
        public ICommand CancelCommand
        {
            get
            {
                _cancelCommand ??= new RelayCommand(_ => RequestClose?.Invoke(false));
                return _cancelCommand;
            }
        }

        #endregion Cancel Command

        #region Export CSV Command

        /// <summary>
        /// Gets the command that exports the preview grid to a CSV file chosen
        /// by the user via a <see cref="SaveFileDialog"/>.
        /// </summary>
        public ICommand ExportCsvCommand
        {
            get
            {
                _exportCsvCommand ??= new RelayCommand(
                    _ => ExportCsv(),
                    _ => OsmmXrefPreviewRows != null && OsmmXrefPreviewRows.Count > 0);
                return _exportCsvCommand;
            }
        }

        /// <summary>
        /// Writes the preview rows to a user-selected CSV file.
        /// </summary>
        private void ExportCsv()
        {
            SaveFileDialog dlg = new()
            {
                Title = "Export OSMM Xref Preview",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = ".csv",
                FileName = "OSMMXrefPreview"
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                // Open the file for writing. Overwrite if it already exists.
                using StreamWriter sw = new(dlg.FileName, append: false);

                // Write the header row.
                sw.WriteLine(
                    "Make,Desc Group,Desc Term,Theme,Feat Code," +
                    "Count,XRef ID,Habitat Primary,Habitat Secondaries,Status");

                // Write each row, escaping any embedded double-quotes in the field values.
                foreach (OsmmXrefPreviewRow r in OsmmXrefPreviewRows)
                {
                    sw.WriteLine(string.Join(",",
                        CsvEscape(r.Make),
                        CsvEscape(r.DescGroup),
                        CsvEscape(r.DescTerm),
                        CsvEscape(r.Theme),
                        CsvEscape(r.FeatCode),
                        r.Count.ToString(),
                        r.XRefIdDisplay,
                        CsvEscape(r.HabitatPrimary),
                        CsvEscape(r.HabitatSecondaries),
                        CsvEscape(r.Status)));
                }

                MessageBox.Show(
                    $"Preview exported successfully to:\n\n{dlg.FileName}",
                    "OSMM Xref Preview – Export",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to export CSV.\n\n{ex.Message}",
                    "OSMM Xref Preview – Export Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Wraps a field value in double-quotes and escapes any embedded
        /// double-quotes so the CSV is RFC-4180-compliant.
        /// </summary>
        private static string CsvEscape(string value)
        {
            // If the value is null or empty, return an empty quoted string.
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            // Escape any embedded double-quotes by replacing them with two double-quotes.
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        #endregion Export CSV Command
    }
}