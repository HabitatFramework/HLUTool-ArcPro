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

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using HLU.Data.Model;
using HLU.Properties;

namespace HLU.Data
{
    /// <summary>
    /// Class representing a BAP record, with validation and change notification for use in the UI.
    /// </summary>
    public class BapEnvironment : INotifyDataErrorInfo, INotifyPropertyChanged, ICloneable
    {
        #region Fields

        private int _bap_id;
        private string _incid;
        private bool _bulkUpdateMode;
        private bool _secondaryPriorityHabitat;
        private string _bap_habitat;
        private string _quality_determination;
        private string _quality_interpretation;
        private string _interpretation_comments;
        private readonly Dictionary<string, List<string>> _errors = [];
        private static IEnumerable<BapEnvironment> _bapEnvironmentList;
        private static int _potentialPriorityDetermQtyValidation;

        public readonly static string BAPDetQltyUserAdded = Settings.Default.BAPDeterminationQualityUserAdded;
        public readonly static string BAPDetQltyUserAddedDesc = Settings.Default.BAPDeterminationQualityUserAddedDesc;
        public readonly static string BAPDetQltyPrevious = Settings.Default.BAPDeterminationQualityPrevious;
        public readonly static string BAPDetQltyPreviousDesc = Settings.Default.BAPDeterminationQualityPreviousDesc;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="BapEnvironment"/> class with default values.
        /// </summary>
        public BapEnvironment()
        {
            _bulkUpdateMode = false;
            _secondaryPriorityHabitat = true; // new rows default to secondary as that is what UI needs to create
            _bap_id = -1; // arbitrary PK for a new row
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BapEnvironment"/> class with specified bulk update
        /// mode and secondary priority habitat flag.
        /// </summary>
        /// <param name="bulkUpdateMode">Indicates whether the instance is in bulk update mode.</param>
        /// <param name="isSecondary">Indicates whether the instance represents a secondary priority habitat.</param>
        public BapEnvironment(bool bulkUpdateMode, bool isSecondary)
        {
            _bulkUpdateMode = bulkUpdateMode;
            _secondaryPriorityHabitat = isSecondary;
            _bap_id = -1; // arbitrary PK for a new row
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BapEnvironment"/> class based on the specified data row,
        /// bulk update mode, and secondary priority habitat flag.
        /// </summary>
        /// <param name="bulkUpdateMode">Indicates whether the instance is in bulk update mode.</param>
        /// <param name="isSecondary">Indicates whether the instance represents a secondary priority habitat.</param>
        /// <param name="dataRow">The data row containing the BAP information.</param>
        public BapEnvironment(bool bulkUpdateMode, bool isSecondary, HluDataSet.incid_bapRow dataRow)
        {
            _bulkUpdateMode = bulkUpdateMode;
            _secondaryPriorityHabitat = isSecondary;
            HluDataSet.incid_bapDataTable table = (HluDataSet.incid_bapDataTable)dataRow.Table;
            _bap_id = dataRow.bap_id;
            _incid = dataRow.incid;
            _bap_habitat = dataRow.IsNull(table.bap_habitatColumn) ? null : dataRow.bap_habitat;
            _quality_determination = dataRow.IsNull(table.quality_determinationColumn) ? null : dataRow.quality_determination;
            _quality_interpretation = dataRow.IsNull(table.quality_interpretationColumn) ? null : dataRow.quality_interpretation;
            // Update the _interpretation_comments string directly, rather than via the property,
            // so that the Changed flag is not set.
            if (dataRow.IsNull(table.interpretation_commentsColumn))
                _interpretation_comments = null;
            else
                _interpretation_comments = dataRow.interpretation_comments.Length <= 254 ? dataRow.interpretation_comments : dataRow.interpretation_comments.Substring(0, 254);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BapEnvironment"/> class based on the specified data row,
        /// bulk update mode, secondary priority habitat flag, and BapEnvironment list.
        /// </summary>
        /// <param name="bulkUpdateMode">Indicates whether the instance is in bulk update mode.</param>
        /// <param name="isSecondary">Indicates whether the instance represents a secondary priority habitat.</param>
        /// <param name="dataRow">The data row containing the BAP information.</param>
        /// <param name="beList">The list of BapEnvironment instances.</param>
        public BapEnvironment(bool bulkUpdateMode, bool isSecondary, HluDataSet.incid_bapRow dataRow, IEnumerable<BapEnvironment> beList)
        {
            _bulkUpdateMode = bulkUpdateMode;
            _secondaryPriorityHabitat = isSecondary;
            HluDataSet.incid_bapDataTable table = (HluDataSet.incid_bapDataTable)dataRow.Table;
            _bap_id = dataRow.bap_id;
            _incid = dataRow.incid;
            _bap_habitat = dataRow.IsNull(table.bap_habitatColumn) ? null : dataRow.bap_habitat;
            _quality_determination = dataRow.IsNull(table.quality_determinationColumn) ? null : dataRow.quality_determination;
            _quality_interpretation = dataRow.IsNull(table.quality_interpretationColumn) ? null : dataRow.quality_interpretation;
            // Update the _interpretation_comments string directly, rather than via the property,
            // so that the Changed flag is not set.
            if (dataRow.IsNull(table.interpretation_commentsColumn))
                _interpretation_comments = null;
            else
                _interpretation_comments = dataRow.interpretation_comments.Length <= 254 ? dataRow.interpretation_comments : dataRow.interpretation_comments.Substring(0, 254);

            _bapEnvironmentList = beList;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BapEnvironment"/> class based on the specified bulk update mode,
        /// secondary priority habitat flag, and item array.
        /// </summary>
        /// <param name="bulkUpdateMode">Indicates whether the instance is in bulk update mode.</param>
        /// <param name="isSecondary">Indicates whether the instance represents a secondary priority habitat.</param>
        /// <param name="itemArray">The array of items containing the BAP information.</param>
        public BapEnvironment(bool bulkUpdateMode, bool isSecondary, object[] itemArray)
        {
            _bulkUpdateMode = bulkUpdateMode;
            _secondaryPriorityHabitat = isSecondary;

            if (!Int32.TryParse(itemArray[0].ToString(), out _bap_id))
            {
                throw new ArgumentException("Invalid BAP ID: unable to parse as integer.", nameof(itemArray));
            }

            _incid = itemArray[1].ToString();
            _bap_habitat = itemArray[2].ToString();
            _quality_determination = itemArray[3].ToString();
            _quality_interpretation = itemArray[4].ToString();

            // Update the _interpretation_comments string directly, rather than via the property,
            // so that the Changed flag is not set.
            string comments = itemArray[5]?.ToString();
            _interpretation_comments = comments?.Length <= 254 ? comments : comments?.Substring(0, 254);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BapEnvironment"/> class based on the specified bulk update mode,
        /// secondary priority habitat flag, and BAP information.
        /// </summary>
        /// <param name="bulkUpdateMode">Indicates whether the instance is in bulk update mode.</param>
        /// <param name="isSecondary">Indicates whether the instance represents a secondary priority habitat.</param>
        /// <param name="bap_id">The BAP ID.</param>
        /// <param name="incid">The incid.</param>
        /// <param name="bap_habitat"></param>
        /// <param name="quality_determination"></param>
        /// <param name="quality_interpretation"></param>
        /// <param name="interpretation_comments"></param>
        public BapEnvironment(bool bulkUpdateMode, bool isSecondary, int bap_id, string incid, string bap_habitat,
            string quality_determination, string quality_interpretation, string interpretation_comments)
        {
            _bulkUpdateMode = bulkUpdateMode;
            _secondaryPriorityHabitat = isSecondary;
            _bap_id = bap_id;
            _incid = incid;
            _bap_habitat = bap_habitat;
            _quality_determination = quality_determination;
            _quality_interpretation = quality_interpretation;
            // Update the _interpretation_comments string directly, rather than via the property,
            // so that the Changed flag is not set.
            if (interpretation_comments == null)
                _interpretation_comments = null;
            else
                _interpretation_comments = interpretation_comments.Length <= 254 ? interpretation_comments : interpretation_comments.Substring(0, 254);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BapEnvironment"/> class by copying the values from another instance.
        /// </summary>
        /// <param name="inputBE">The instance to copy values from.</param>
        public BapEnvironment(BapEnvironment inputBE)
        {
            _bulkUpdateMode = inputBE.BulkUpdateMode;
            _secondaryPriorityHabitat = inputBE.SecondaryPriorityHabitat;
            _bap_id = -1; // arbitrary PK for a new row
            _incid = null;
            _bap_habitat = inputBE.Bap_habitat;
            _quality_determination = inputBE.Quality_determination;
            _quality_interpretation = inputBE.Quality_interpretation;
            _interpretation_comments = inputBE.Interpretation_comments;
        }

        /// <summary>
        /// Creates a new object that is a copy of the current instance.
        /// </summary>
        /// <returns>A new <see cref="BapEnvironment"/> object that is a copy of the current instance.</returns>
        public object Clone()
        {
            return new BapEnvironment(this);
        }

        #endregion Constructor

        #region DataChanged

        /// <summary>
        /// Delegate for the <see cref="DataChanged"/> event, which is raised when data in the BAP record changes.
        /// </summary>
        /// <param name="Changed">Indicates whether the data has changed.</param>
        public delegate void DataChangedEventHandler(bool Changed);

        /// <summary>
        /// Event that is raised when data in the BAP record changes, allowing subscribers to
        /// react to changes (e.g., by enabling an "Apply" button).
        /// </summary>
        public event DataChangedEventHandler DataChanged;

        #endregion DataChanged

        #region INotifyPropertyChanged

        /// <summary>
        /// Occurs when a property value changes, allowing the UI to update accordingly.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event for the specified property name, indicating that the property's value has changed.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion INotifyPropertyChanged

        #region Validation

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        /// <summary>
        /// Gets a value indicating whether the BAP record has any validation errors. This is determined
        /// by checking if there are any entries in the _errors dictionary.
        /// </summary>
        /// <value><c>true</c> if the BAP record has validation errors; otherwise, <c>false</c>.</value>
        public bool HasErrors => _errors.Count != 0;

        /// <summary>
        /// Gets the validation errors for a specified property. If the property name is null or empty,
        /// returns all errors for row-level validation.
        /// </summary>
        /// <param name="propertyName">The name of the property to retrieve validation errors for.</param>
        /// <returns>An enumerable collection of validation errors for the specified property, or null if
        /// there are no errors.</returns>
        public IEnumerable GetErrors(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                // Return all errors for row-level validation
                var allErrors = _errors.Values.SelectMany(e => e).ToList();
                return allErrors.Count != 0 ? allErrors : null;
            }

            if (_errors.TryGetValue(propertyName, out List<string> value))
                return value;

            return null;
        }

        /// <summary>
        /// Sets the validation errors for a specified property and raises the ErrorsChanged event if
        /// the errors have changed. If the errors list is null or empty, removes any existing errors
        /// for the property.
        /// </summary>
        /// <param name="propertyName">The name of the property to set validation errors for.</param>
        /// <param name="errors">The list of validation errors to set for the property.</param>
        private void SetErrors(string propertyName, List<string> errors)
        {
            bool errorsChanged = false;

            if (errors != null && errors.Count != 0)
            {
                // Add or update errors
                if (!_errors.TryGetValue(propertyName, out List<string> value) || !value.SequenceEqual(errors))
                {
                    value = errors;
                    _errors[propertyName] = value;
                    errorsChanged = true;
                }
            }
            else
            {
                // Remove errors if they existed
                if (_errors.Remove(propertyName))
                {
                    errorsChanged = true;
                }
            }

            // Only raise ErrorsChanged if something actually changed
            if (errorsChanged)
            {
                OnErrorsChanged(propertyName);
            }
        }

        /// <summary>
        /// Raises the <see cref="ErrorsChanged"/> event for the specified property name, indicating that the
        /// validation errors for that property have changed.
        /// </summary>
        /// <param name="propertyName">The name of the property for which the validation errors have changed.</param>
        private void OnErrorsChanged(string propertyName)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));

            // Also notify for empty string to trigger row-level validation update
            if (!string.IsNullOrEmpty(propertyName))
            {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(string.Empty));
            }

            // Notify that HasErrors property itself has changed
            OnPropertyChanged(nameof(HasErrors));
        }

        /// <summary>
        /// Validates the specified property and updates the validation errors for that property. The validation
        /// errors are stored in the internal dictionary and the ErrorsChanged event is raised if the errors have changed.
        /// </summary>
        /// <param name="propertyName">The name of the property to validate.</param>
        private void ValidateProperty(string propertyName)
        {
            List<string> errors = [];

            switch (propertyName)
            {
                case nameof(Incid):
                    if ((Bap_id != -1) && String.IsNullOrEmpty(Incid))
                    {
                        errors.Add("Error: INCID is a mandatory field");
                    }
                    break;

                case nameof(Bap_habitat):
                    if (String.IsNullOrEmpty(Bap_habitat))
                    {
                        errors.Add("Error: Priority habitat is a mandatory field");
                    }
                    else if ((_bapEnvironmentList != null) && (_bapEnvironmentList.Count(b => b.Bap_habitat == Bap_habitat) > 1))
                    {
                        errors.Add($"Error: Duplicate priority habitat '{Bap_habitat}'");
                    }
                    break;

                case nameof(Quality_determination):
                    if (String.IsNullOrEmpty(Quality_determination))
                    {
                        if (!_bulkUpdateMode)
                        {
                            errors.Add("Error: Determination quality is a mandatory field");
                        }
                    }
                    else
                    {
                        // If this is a user-added priority habitat (i.e. in the secondary list).
                        if (_secondaryPriorityHabitat)
                        {
                            // If potential priority habitat determination quality is
                            // to be validated.
                            if (_potentialPriorityDetermQtyValidation == 1)
                            {
                                // Validate that the determination quality can ONLY be
                                // 'Not present but close to definition' or
                                // 'Previously present, but may no longer exist'.
                                if ((Quality_determination != BAPDetQltyUserAdded)
                                && (Quality_determination != BAPDetQltyPrevious))
                                {
                                    errors.Add($"Error: Determination quality for potential priority habitats can only be '{BAPDetQltyUserAddedDesc}' or '{BAPDetQltyPreviousDesc}'");
                                }
                            }
                        }
                        // If this is an automatic priority habitat.
                        else
                        {
                            // Validate that the determination quality can be anything EXCEPT
                            // 'Not present but close to definition' or
                            // 'Previously present, but may no longer exist'.
                            if ((Quality_determination == BAPDetQltyUserAdded))
                            {
                                errors.Add($"Error: Determination quality cannot be '{BAPDetQltyUserAddedDesc}' for priority habitats");
                            }
                            else if ((Quality_determination == BAPDetQltyPrevious))
                            {
                                errors.Add($"Error: Determination quality cannot be '{BAPDetQltyPreviousDesc}' for priority habitats");
                            }
                        }
                    }
                    break;

                case nameof(Quality_interpretation):
                    if (!_bulkUpdateMode && String.IsNullOrEmpty(Quality_interpretation))
                    {
                        errors.Add("Error: Interpretation quality is a mandatory field");
                    }
                    break;
            }

            // Update the errors for the property
            SetErrors(propertyName, errors.Count != 0 ? errors : null);
        }

        #endregion Validation

        #region Properties

        /// <summary>
        /// Sets the list of BapEnvironment instances, which is used for validation (e.g., to check for
        /// duplicate habitats).
        /// </summary>
        /// <value>The list of BapEnvironment instances.</value>
        public static IEnumerable<BapEnvironment> BapEnvironmentList
        {
            set { _bapEnvironmentList = value; }
        }

        /// <summary>
        /// Sets the flag indicating whether potential priority habitat determination quality should be
        /// validated. If set to 1, validation will be performed to ensure that the determination quality
        /// for potential priority habitats can only be 'Not present but close to definition' or
        /// 'Previously present, but may no longer exist'.
        /// </summary>
        /// <value>The flag indicating whether potential priority habitat determination quality should be validated.</value>
        public static int PotentialPriorityDetermQtyValidation
        {
            set { _potentialPriorityDetermQtyValidation = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the instance is in bulk update mode. When in bulk update mode,
        /// validation is temporarily disabled to improve performance.
        /// </summary>
        /// <value><c>true</c> if the instance is in bulk update mode; otherwise, <c>false</c>.</value>
        public bool BulkUpdateMode
        {
            get { return _bulkUpdateMode; }
            set { _bulkUpdateMode = value; }
        }

        /// <summary>
        /// Gets a value indicating whether the BAP record is a new record that has been added but not yet saved to the database.
        /// </summary>
        /// <value><c>true</c> if the BAP record is a new record that has been added but not yet saved to the database; otherwise, <c>false</c>.</value>
        public bool IsAdded
        {
            get { return _bap_id == -1; }
        }

        /// <summary>
        /// Gets a value indicating whether the BAP record is a secondary priority habitat.
        /// </summary>
        /// <value><c>true</c> if the BAP record is a secondary priority habitat; otherwise, <c>false</c>.</value>
        public bool SecondaryPriorityHabitat
        {
            get { return _secondaryPriorityHabitat; }
        }

        /// <summary>
        /// Gets or sets the BAP ID.
        /// </summary>
        /// <value>The BAP ID.</value>
        public int Bap_id
        {
            get { return _bap_id; }
            set { _bap_id = value; }
        }

        /// <summary>
        /// Gets or sets the incid associated with the BAP record. This is a mandatory field when bap_id is not -1.
        /// </summary>
        /// <value>The incid associated with the BAP record.</value>
        public string Incid
        {
            get { return _incid; }
            set
            {
                if (_incid != value)
                {
                    _incid = value;
                    OnPropertyChanged(nameof(Incid));
                    ValidateProperty(nameof(Incid));
                }
            }
        }

        /// <summary>
        /// Gets or sets the priority habitat associated with the BAP record. This is a mandatory field
        /// and must be unique within the list of BAP records.
        /// </summary>
        /// <value>The priority habitat associated with the BAP record.</value>
        public string Bap_habitat
        {
            get { return _bap_habitat; }
            set
            {
                if (_bap_habitat != value)
                {
                    _bap_habitat = value;
                    OnPropertyChanged(nameof(Bap_habitat));
                    ValidateProperty(nameof(Bap_habitat));
                    // Flag that the current record has changed so that the apply button
                    // will appear.
                    DataChanged?.Invoke(true);
                }
            }
        }

        /// <summary>
        /// Gets or sets the quality of the determination for the priority habitat. This is a
        /// mandatory field when not in bulk update mode.
        /// </summary>
        /// <value>The quality of the determination for the priority habitat.</value>
        public string Quality_determination
        {
            get { return _quality_determination; }
            set
            {
                if (_quality_determination != value)
                {
                    _quality_determination = value;
                    OnPropertyChanged(nameof(Quality_determination));
                    ValidateProperty(nameof(Quality_determination));
                    // Flag that the current record has changed so that the apply button
                    // will appear.
                    DataChanged?.Invoke(true);
                }
            }
        }

        /// <summary>
        /// Gets or sets the quality of the interpretation for the priority habitat. This is a
        /// mandatory field when not in bulk update mode.
        /// </summary>
        /// <value>The quality of the interpretation for the priority habitat.</value>
        public string Quality_interpretation
        {
            get { return _quality_interpretation; }
            set
            {
                if (_quality_interpretation != value)
                {
                    _quality_interpretation = value;
                    OnPropertyChanged(nameof(Quality_interpretation));
                    ValidateProperty(nameof(Quality_interpretation));
                    // Flag that the current record has changed so that the apply button
                    // will appear.
                    DataChanged?.Invoke(true);
                }
            }
        }

        /// <summary>
        /// Gets or sets the interpretation comments for the priority habitat. This field is
        /// optional and has a maximum length of 254 characters. If a longer string is set,
        /// it will be truncated to 254 characters.
        /// </summary>
        /// <value>The interpretation comments for the priority habitat.</value>
        public string Interpretation_comments
        {
            get { return _interpretation_comments; }
            set
            {
                string newValue = value == null || value.Length <= 254 ? value : value.Substring(0, 254);
                if (_interpretation_comments != newValue)
                {
                    _interpretation_comments = newValue;
                    OnPropertyChanged(nameof(Interpretation_comments));
                    ValidateProperty(nameof(Interpretation_comments));
                    // Flag that the current record has changed so that the apply button
                    // will appear.
                    DataChanged?.Invoke(true);
                }
            }
        }

        #endregion Properties

        #region ToItemArray

        /// <summary>
        /// Converts the BAP record to an array of objects, which can be used for data binding or other purposes.
        /// The array contains the following elements:
        /// <list type="bullet">
        /// <item><description>BAP ID</description></item>
        /// <item><description>Incid</description></item>
        /// <item><description>BAP Habitat</description></item>
        /// <item><description>Quality Determination</description></item>
        /// <item><description>Quality Interpretation</description></item>
        /// <item><description>Interpretation Comments</description></item>
        /// </list>
        /// </summary>
        /// <returns>An array of objects representing the BAP record.</returns>
        public object[] ToItemArray()
        {
            return [ _bap_id, _incid, _bap_habitat, _quality_determination,
                _quality_interpretation, _interpretation_comments ];
        }

        /// <summary>
        /// Converts the BAP record to an array of objects, which can be used for data binding or other purposes.
        /// </summary>
        /// <param name="bapID">The BAP ID.</param>
        /// <param name="incid">The incident ID.</param>
        /// <returns>An array of objects representing the BAP record.</returns>
        public object[] ToItemArray(int bapID, string incid)
        {
            return [ bapID, incid, _bap_habitat, _quality_determination,
                _quality_interpretation, _interpretation_comments ];
        }

        /// <summary>
        /// Converts the BAP record to an array of objects, which can be used for data binding or other purposes. If the isSecondary
        /// parameter is true, the record will be marked as a secondary priority habitat.
        /// </summary>
        /// <param name="bapID">The BAP ID.</param>
        /// <param name="incid">The incid.</param>
        /// <param name="isSecondary">Indicates whether the record is a secondary priority habitat.</param>
        /// <returns>An array of objects representing the BAP record.</returns>
        public object[] ToItemArray(int bapID, string incid, bool isSecondary)
        {
            if (isSecondary) MakeSecondary();
            return [ bapID, incid, _bap_habitat, _quality_determination,
                _quality_interpretation, _interpretation_comments ];
        }

        /// <summary>
        /// Marks the BAP record as a secondary priority habitat by setting the internal flag.
        /// This can be used to indicate that the record
        /// is a secondary priority habitat.
        /// </summary>
        public void MakeSecondary()
        {
            _secondaryPriorityHabitat = true;
        }

        #endregion ToItemArray

        #region Static Methods

        /// <summary>
        /// Determines whether the specified row is a secondary priority habitat.
        /// </summary>
        /// <param name="r">The priority habitat row.</param>
        /// <returns>
        ///   <c>true</c> if the specified row is a secondary priority habitat; otherwise, <c>false</c>.
        /// </returns>
        public static bool IsSecondary(HluDataSet.incid_bapRow r)
        {
            return (r.quality_determination == BAPDetQltyPrevious || r.quality_determination == BAPDetQltyUserAdded);
        }

        /// <summary>
        /// Makes the row a secondary priority habitat.
        /// </summary>
        /// <param name="r">The priority habitat row.</param>
        /// <returns>The updated priority habitat row marked as secondary.</returns>
        public static HluDataSet.incid_bapRow MakeSecondary(HluDataSet.incid_bapRow r)
        {
            // Set the determination quality to 'Previously present, but may no longer exist'
            r.quality_determination = BAPDetQltyPrevious;
            return r;
        }

        #endregion Static Methods

        #region Validation

        /// <summary>
        /// Gets a value indicating whether the BAP habitat is a duplicate within the list of
        /// BAP environments. This is determined by checking if any other BAP environment in
        /// the list has the same habitat.
        /// </summary>
        /// <value><c>true</c> if the BAP habitat is a duplicate; otherwise, <c>false</c>.</value>
        public bool IsDuplicate
        {
            get
            {
                return _bapEnvironmentList != null && _bapEnvironmentList.Any(be => be.Bap_habitat == this.Bap_habitat);
            }
        }

        /// <summary>
        /// Determines whether the BAP record is valid by validating all properties and checking
        /// for any validation errors. The method returns <c>true</c> if the record is valid; otherwise, <c>false</c>.
        /// </summary>
        /// <returns><c>true</c> if the BAP record is valid; otherwise, <c>false</c>.</returns>
        public bool IsValid()
        {
            // Validate all properties
            ValidateProperty(nameof(Incid));
            ValidateProperty(nameof(Bap_habitat));
            ValidateProperty(nameof(Quality_determination));
            ValidateProperty(nameof(Quality_interpretation));
            ValidateProperty(nameof(Interpretation_comments));

            return !HasErrors;
        }

        /// <summary>
        /// Determines whether the BAP record is valid by performing row-level validation based
        /// on the current values of the properties.
        /// </summary>
        /// <param name="bulkUpdateMode">Indicates whether the validation is being performed in bulk update mode.</param>
        /// <param name="isSecondary">Indicates whether the BAP record is a secondary priority habitat.</param>
        /// <returns><c>true</c> if the BAP record is valid; otherwise, <c>false</c>.</returns>
        public bool IsValid(bool bulkUpdateMode, bool isSecondary)
        {
            return String.IsNullOrEmpty(ValidateRow(bulkUpdateMode, isSecondary, _bap_id,
                _incid, _bap_habitat, _quality_determination, _quality_interpretation));
        }

        /// <summary>
        /// Determines whether the specified row is valid by performing row-level validation based
        /// on the current values of the properties.
        /// </summary>
        /// <param name="bulkUpdateMode">Indicates whether the validation is being performed in bulk update mode.</param>
        /// <param name="isSecondary">Indicates whether the BAP record is a secondary priority habitat.</param>
        /// <param name="r">The BAP row to validate.</param>
        /// <returns><c>true</c> if the BAP row is valid; otherwise, <c>false</c>.</returns>
        public bool IsValid(bool bulkUpdateMode, bool isSecondary, HluDataSet.incid_bapRow r)
        {
            return String.IsNullOrEmpty(ValidateRow(bulkUpdateMode, isSecondary, r.bap_id,
                r.incid, r.bap_habitat, r.quality_determination, r.quality_interpretation));
        }

        /// <summary>
        /// Gets a string containing all validation error messages for the BAP record, concatenated together
        /// with newlines. This is useful for displaying validation errors to the user in a readable format.
        /// </summary>
        /// <value>A string containing all validation error messages for the BAP record.</value>
        public string ErrorMessages
        {
            get
            {
                var allErrors = GetErrors(string.Empty);
                if (allErrors == null) return string.Empty;

                return string.Join(Environment.NewLine, allErrors.Cast<string>());
            }
        }

        /// <summary>
        /// Validates the specified row by performing row-level validation based on the current values of the properties. The method returns
        /// a string containing all validation error messages for the row, or <c>null</c> if the row is valid.
        /// </summary>
        /// <param name="bulkUpdateMode">Indicates whether the validation is being performed in bulk update mode.</param>
        /// <param name="isSecondary">Indicates whether the BAP record is a secondary priority habitat.</param>
        /// <param name="r">The BAP row to validate.</param>
        /// <returns><c>true</c> if the BAP row is valid; otherwise, <c>false</c>.</returns>
        public static bool ValidateRow(bool bulkUpdateMode, bool isSecondary, HluDataSet.incid_bapRow r)
        {
            return ValidateRow(bulkUpdateMode, isSecondary, r.bap_id, r.incid, r.bap_habitat,
                r.quality_determination, r.quality_interpretation) == null;
        }

        /// <summary>
        /// Validates the specified row by performing row-level validation based on the current values of the properties. The method returns
        /// a string containing all validation error messages for the row, or <c>null</c> if the row is valid.
        /// </summary>
        /// <param name="_bulkUpdateMode">Indicates whether the validation is being performed in bulk update mode.</param>
        /// <param name="isSecondary">Indicates whether the BAP record is a secondary priority habitat.</param>
        /// <param name="bap_id">The BAP ID of the row to validate.</param>
        /// <param name="incid">The INCID of the row to validate.</param>
        /// <param name="bap_habitat">The priority habitat of the row to validate.</param>
        /// <param name="quality_determination">The determination quality of the row to validate.</param>
        /// <param name="quality_interpretation">The quality interpretation of the row to validate.</param>
        /// <returns>A string containing all validation error messages for the row, or <c>null</c> if the row is valid.</returns>
        private static string ValidateRow(bool _bulkUpdateMode, bool isSecondary, int bap_id, string incid,
            string bap_habitat, string quality_determination, string quality_interpretation)
        {
            StringBuilder sbError = new();

            if ((bap_id != -1) && String.IsNullOrEmpty(incid))
                sbError.Append(Environment.NewLine).Append("Error: INCID is a mandatory field");

            if (String.IsNullOrEmpty(bap_habitat))
                sbError.Append(Environment.NewLine).Append("Error: Priority habitat is a mandatory field");

            if (String.IsNullOrEmpty(quality_determination))
            {
                if (!_bulkUpdateMode)
                    sbError.Append(Environment.NewLine).Append("Error: Determination quality is a mandatory field");
            }
            else
            {
                // If this is a user-added priority habitat (i.e. in the secondary list).
                if (isSecondary)
                {
                    // If potential priority habitat determination quality is
                    // to be validated.
                    if (_potentialPriorityDetermQtyValidation == 1)
                    {
                        // Validate that the determination quality can ONLY be
                        // 'Not present but close to definition' or
                        // 'Previously present, but may no longer exist'.
                        if ((quality_determination != BAPDetQltyUserAdded)
                        && (quality_determination != BAPDetQltyPrevious))
                        {
                            sbError.Append(Environment.NewLine)
                                .Append(String.Format("Error: Determination quality for potential priority habitats can only be '{0}' or '{1}'",
                                BAPDetQltyUserAddedDesc, BAPDetQltyPreviousDesc));
                        }
                    }
                }
                // If this is not a secondary priority habitat (i.e. in the primary
                // list).
                else
                {
                    // Validate that the determination quality can be anything EXCEPT
                    // 'Not present but close to definition' or
                    // 'Previously present, but may no longer exist'.
                    if (quality_determination == BAPDetQltyUserAdded)
                    {
                        sbError.Append(Environment.NewLine)
                            .Append(String.Format("Error: Determination quality cannot be '{0}' for 'primary' priority habitats", BAPDetQltyUserAddedDesc));
                    }
                    else if (quality_determination == BAPDetQltyPrevious)
                    {
                        sbError.Append(Environment.NewLine)
                            .Append(String.Format("Error: Determination quality cannot be '{0}' for 'primary' priority habitats", BAPDetQltyPreviousDesc));
                    }
                }
            }

            if (!_bulkUpdateMode && String.IsNullOrEmpty(quality_interpretation))
                sbError.Append(Environment.NewLine).Append("Error: Interpretation quality is a mandatory field");

            return sbError.Length > 0 ? sbError.Remove(0, Environment.NewLine.Length).ToString() : null;
        }

        #endregion Validation
    }
}