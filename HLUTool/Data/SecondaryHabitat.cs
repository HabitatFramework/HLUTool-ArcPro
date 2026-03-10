// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2022 Greenspace Information for Greater London CIC
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

using HLU.Data.Model;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace HLU.Data
{
    /// <summary>
    /// Class representing a secondary habitat record. Implements INotifyDataErrorInfo for validation and INotifyPropertyChanged for data binding.
    /// </summary>
    public class SecondaryHabitat : INotifyDataErrorInfo, INotifyPropertyChanged, ICloneable
    {
        #region Fields

        private int _secondary_id;
        private string _incid;
        private string _secondary_habitat;
        private int _secondary_habitat_int;
        private string _secondary_group;

        private bool _bulkUpdateMode;

        private readonly Dictionary<string, List<string>> _errors = new Dictionary<string, List<string>>();
        private static IEnumerable<SecondaryHabitat> _secondaryHabitatList;
        private static IEnumerable<string> _validSecondaryCodes;
        private static Dictionary<string, String> _secondaryGroupCodes;

        private static int _primarySecondaryCodeValidation;

        #endregion Fields

        #region ctor

        /// <summary>
        /// Default constructor for a new SecondaryHabitat record. Sets bulk update mode to false and
        /// initializes secondary_id to -1 to indicate a new record.
        /// </summary>
        public SecondaryHabitat()
        {
            _bulkUpdateMode = false;

            _secondary_id = -1; // arbitrary PK for a new row
        }

        /// <summary>
        /// Constructor to initialize a SecondaryHabitat from a data row. The bulkUpdateMode parameter
        /// indicates whether the record is being initialized as part of a bulk update, which can affect
        /// validation behavior.
        /// </summary>
        /// <param name="bulkUpdateMode">Indicates whether the record is being initialized as part of a bulk update.</param>
        /// <param name="dataRow">The data row from which to initialize the SecondaryHabitat.</param>
        public SecondaryHabitat(bool bulkUpdateMode, HluDataSet.incid_secondaryRow dataRow)
        {
            _bulkUpdateMode = bulkUpdateMode;

            HluDataSet.incid_secondaryDataTable table = (HluDataSet.incid_secondaryDataTable)dataRow.Table;
            _secondary_id = dataRow.secondary_id;
            _incid = dataRow.incid;
            _secondary_habitat = dataRow.IsNull(table.secondaryColumn) ? null : dataRow.secondary;
            int secondary_habitat_int;
            if (Int32.TryParse(_secondary_habitat, out secondary_habitat_int))
                _secondary_habitat_int = secondary_habitat_int;
            else
                _secondary_habitat_int = 0;
            _secondary_group = dataRow.secondary_group;
        }

        /// <summary>
        /// Constructor to initialize a SecondaryHabitat from a data row, with an additional parameter
        /// for the list of existing secondary habitats. This can be used to perform duplicate validation
        /// against the existing list.
        /// </summary>
        /// <param name="bulkUpdateMode">Indicates whether the record is being initialized as part of a bulk update.</param>
        /// <param name="dataRow">The data row from which to initialize the SecondaryHabitat.</param>
        /// <param name="shList">The list of existing secondary habitats for duplicate validation.</param>
        public SecondaryHabitat(bool bulkUpdateMode, HluDataSet.incid_secondaryRow dataRow, IEnumerable<SecondaryHabitat> shList)
        {
            _bulkUpdateMode = bulkUpdateMode;

            HluDataSet.incid_secondaryDataTable table = (HluDataSet.incid_secondaryDataTable)dataRow.Table;
            _secondary_id = dataRow.secondary_id;
            _incid = dataRow.incid;
            _secondary_habitat = dataRow.IsNull(table.secondaryColumn) ? null : dataRow.secondary;
            int secondary_habitat_int;
            if (Int32.TryParse(_secondary_habitat, out secondary_habitat_int))
                _secondary_habitat_int = secondary_habitat_int;
            else
                _secondary_habitat_int = 0;
            _secondary_group = dataRow.secondary_group;
        }

        /// <summary>
        /// Constructor to initialize a SecondaryHabitat from an object array, typically representing a
        /// row of data. The bulkUpdateMode parameter indicates whether the record is being initialized
        /// as part of a bulk update, which can affect validation behavior.
        /// </summary>
        /// <param name="bulkUpdateMode">Indicates whether the record is being initialized as part of a bulk update.</param>
        /// <param name="itemArray">The object array from which to initialize the SecondaryHabitat.</param>
        public SecondaryHabitat(bool bulkUpdateMode, object[] itemArray)
        {
            _bulkUpdateMode = bulkUpdateMode;

            Int32.TryParse(itemArray[0].ToString(), out _secondary_id);
            _incid = itemArray[1].ToString();
            _secondary_habitat = itemArray[2].ToString();
            int secondary_habitat_int;
            if (Int32.TryParse(_secondary_habitat, out secondary_habitat_int))
                _secondary_habitat_int = secondary_habitat_int;
            else
                _secondary_habitat_int = 0;
            _secondary_group = itemArray[3].ToString();
        }

        /// <summary>
        /// Constructor to initialize a SecondaryHabitat from individual parameters. The bulkUpdateMode parameter
        /// indicates whether the record is being initialized as part of a bulk update, which can affect validation behavior.
        /// </summary>
        /// <param name="bulkUpdateMode">Indicates whether the record is being initialized as part of a bulk update.</param>
        /// <param name="secondary_id">The ID of the secondary habitat.</param>
        /// <param name="incid">The incident ID associated with the secondary habitat.</param>
        /// <param name="secondary_habitat">The name or description of the secondary habitat.</param>
        /// <param name="secondary_group">The group to which the secondary habitat belongs.</param>
        public SecondaryHabitat(bool bulkUpdateMode, int secondary_id, string incid, string secondary_habitat, string secondary_group)
        {
            _bulkUpdateMode = bulkUpdateMode;

            _secondary_id = secondary_id;
            _incid = incid;
            _secondary_habitat = secondary_habitat;
            int secondary_habitat_int;
            if (Int32.TryParse(_secondary_habitat, out secondary_habitat_int))
                _secondary_habitat_int = secondary_habitat_int;
            else
                _secondary_habitat_int = 0;
            _secondary_group = secondary_group;
        }

        /// <summary>
        /// Copy constructor to create a new SecondaryHabitat based on an existing one. This can be used
        /// to create a duplicate
        /// </summary>
        /// <param name="inputSH">The existing SecondaryHabitat to copy.</param>
        public SecondaryHabitat(SecondaryHabitat inputSH)
        {
            _bulkUpdateMode = false;

            _secondary_id = -1; // arbitrary PK for a new row
            _incid = null;
            _secondary_habitat = inputSH.secondary_habitat;
            _secondary_habitat_int = inputSH.secondary_habitat_int;
            _secondary_group = inputSH.secondary_group;
        }

        /// <summary>
        /// Clone method to create a new instance of SecondaryHabitat with the same values as the
        /// current instance. This is used to support the ICloneable interface and allows for
        /// creating a copy of the current record, which can be useful in scenarios such as undo/redo
        /// or when editing a record without modifying the original until changes are applied.
        /// </summary>
        /// <returns>A new instance of SecondaryHabitat with the same values as the current instance.</returns>
        public object Clone()
        {
            return new SecondaryHabitat(this);
        }

        #endregion ctor

        #region DataChanged

        /// <summary>
        /// Delegate for the DataChanged event, which is raised when a property value changes that
        /// affects the validity of the record. The event handler receives a boolean parameter
        /// indicating whether the data has changed and may require the apply button to be enabled.
        /// </summary>
        /// <param name="Changed">Indicates whether the data has changed and may require the
        /// apply button to be enabled.</param>
        public delegate void DataChangedEventHandler(bool Changed);

        /// <summary>
        /// Event raised when a property value changes that affects the validity of the record. Subscribers
        /// to this event can take appropriate actions, such as enabling or disabling the apply button.
        /// </summary>
        public event DataChangedEventHandler DataChanged;

        #endregion DataChanged

        #region Error Handling

        /// <summary>
        /// Event raised when a property value changes that may affect the validity of the record. This is part
        /// of the INotifyDataErrorInfo interface and allows for notifying the UI or other components that they
        /// should update their error state.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Method to raise the PropertyChanged event for a given property name. This is called whenever a
        /// property value changes to notify any subscribers (such as the UI) that they should update their
        /// display or validation state.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Event raised when the validation errors for a property have changed. This is part of the
        /// INotifyDataErrorInfo interface and allows for notifying the UI or other components that
        /// they should update their error state for the specified property.
        /// </summary>
        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        /// <summary>
        /// Indicates whether the record has any validation errors. This is part of the INotifyDataErrorInfo
        /// interface.
        /// </summary>
        public bool HasErrors => _errors.Count != 0;

        /// <summary>
        /// Gets the validation errors for a specified property. If the property name is null or empty,
        /// returns all errors for row-level validation. This is part of the INotifyDataErrorInfo
        /// interface and allows for retrieving the current validation errors for a given property,
        /// which can be used by the UI to display error messages or indicate invalid fields.
        /// </summary>
        /// <param name="propertyName">The name of the property for which to retrieve validation errors.</param>
        /// <returns>An enumerable collection of validation errors for the specified property, or null if there are no errors.</returns>
        public IEnumerable GetErrors(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                // Return all errors for row-level validation
                var allErrors = _errors.Values.SelectMany(e => e).ToList();
                return allErrors.Count != 0 ? allErrors : null;
            }

            if (_errors.ContainsKey(propertyName))
                return _errors[propertyName];

            return null;
        }

        /// <summary>
        /// Sets the validation errors for a specified property and raises the ErrorsChanged event
        /// if the errors have changed. If the errors list is null or empty, removes any existing
        /// errors for the property. This method is used internally to manage the validation state
        /// of the record and notify subscribers when validation errors change.
        /// </summary>
        /// <param name="propertyName">The name of the property for which to set validation errors.</param>
        /// <param name="errors">The list of validation errors to set for the specified property.</param>
        private void SetErrors(string propertyName, List<string> errors)
        {
            bool errorsChanged = false;

            if (errors != null && errors.Count != 0)
            {
                // Add or update errors
                if (!_errors.ContainsKey(propertyName) || !_errors[propertyName].SequenceEqual(errors))
                {
                    _errors[propertyName] = errors;
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
        /// Raises the ErrorsChanged event for a specified property and also raises it for an empty
        /// string to trigger row-level validation update.
        /// </summary>
        /// <param name="propertyName">The name of the property for which the errors have changed.</param>
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
        /// Validates a specified property and updates the validation errors accordingly. This method contains
        /// the logic for property-level validation and is called internally whenever a property's value changes.
        /// </summary>
        /// <param name="propertyName">The name of the property to validate.</param>
        private void ValidateProperty(string propertyName)
        {
            List<string> errors = new List<string>();

            switch (propertyName)
            {
                case nameof(incid):
                    if ((secondary_id != -1) && String.IsNullOrEmpty(incid))
                    {
                        errors.Add("Error: INCID is a mandatory field");
                    }
                    break;

                case nameof(secondary_habitat):
                    // Only validate if not in bulk update mode and errors are to be shown
                    if (!_bulkUpdateMode)
                    {
                        if (String.IsNullOrEmpty(secondary_habitat))
                        {
                            errors.Add("Error: Secondary habitat is a mandatory field");
                        }
                        else if (_validSecondaryCodes == null)
                        {
                            errors.Add("Error: Secondary habitat is not valid without primary habitat");
                        }
                        else if ((_secondaryHabitatList != null) && (_secondaryHabitatList.Count(b => b.secondary_habitat == secondary_habitat) > 1))
                        {
                            errors.Add("Error: Duplicate secondary habitat");
                        }
                        else if (_primarySecondaryCodeValidation > 0)
                        {
                            if ((_validSecondaryCodes != null) && (!_validSecondaryCodes.Contains(secondary_habitat)))
                            {
                                errors.Add("Error: Secondary habitat is not valid for primary habitat");
                            }
                        }
                    }
                    break;
            }

            // Set the errors for the property, which will raise the ErrorsChanged event if they have changed.
            SetErrors(propertyName, errors.Count != 0 ? errors : null);
        }

        #endregion Error Handling

        #region Static properties

        /// <summary>
        /// Gets or sets the list of existing secondary habitats. This static property is used for
        /// validation purposes, such as checking for duplicates. When setting this property, it
        /// can be used to provide the current list of secondary habitats against which new entries
        /// can be validated.
        /// </summary>
        /// <value>The list of existing secondary habitats.</value>
        public static IEnumerable<SecondaryHabitat> SecondaryHabitatList
        {
            get
            {
                return _secondaryHabitatList;
            }
            set
            {
                _secondaryHabitatList = value;
            }
        }

        /// <summary>
        /// Gets or sets the dictionary of secondary group codes. This static property can be used to
        /// provide a mapping of secondary group codes for validation or other purposes.
        /// </summary>
        /// <value>The dictionary of secondary group codes.</value>
        public static Dictionary<string, String> SecondaryGroupCodes
        {
            get
            {
                return _secondaryGroupCodes;
            }
            set
            {
                _secondaryGroupCodes = value;
            }
        }

        /// <summary>
        /// Gets or sets the list of valid secondary habitat codes based on the selected primary
        /// habitat. This static property can be used to provide a list of valid secondary habitat
        /// codes for validation purposes.
        /// </summary>
        /// <value>The list of valid secondary habitat codes.</value>
        public static IEnumerable<string> ValidSecondaryCodes
        {
            get
            {
                return _validSecondaryCodes;
            }
            set
            {
                _validSecondaryCodes = value;
            }
        }

        /// <summary>
        /// Gets or sets the flag indicating whether to perform primary-secondary code validation.
        /// This static property can be used to enable or disable validation that checks whether
        /// the secondary habitat is valid for the selected primary habitat.
        /// </summary>
        /// <value>The flag indicating whether to perform primary-secondary code validation.</value>
        public static int PrimarySecondaryCodeValidation
        {
            get
            {
                return _primarySecondaryCodeValidation;
            }
            set
            {
                _primarySecondaryCodeValidation = value;
            }
        }

        #endregion Static properties

        #region Properties

        /// <summary>
        /// Gets or sets the flag indicating whether the record is being updated as part of a bulk update operation.
        /// </summary>
        /// <value><c>true</c> if the record is being updated as part of a bulk update operation; otherwise, <c>false</c>.</value>
        public bool BulkUpdateMode
        {
            get
            {
                return _bulkUpdateMode;
            }
            set
            {
                _bulkUpdateMode = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the current record is a new record that has not yet been added to the database.
        /// </summary>
        /// <value><c>true</c> if the current record is a new record; otherwise, <c>false</c>.</value>
        public bool IsAdded
        {
            get
            {
                return _secondary_id == -1;
            }
        }

        /// <summary>
        /// Gets or sets the ID of the secondary habitat record. This is typically a primary key in the database
        /// and is used to uniquely identify each record. When initializing a new record, this value is set to
        /// -1 to indicate that it has not yet been saved to the database.
        /// </summary>
        /// <value>The ID of the secondary habitat record.</value>
        public int secondary_id
        {
            get
            {
                return _secondary_id;
            }
            set
            {
                _secondary_id = value;
            }
        }

        /// <summary>
        /// Gets or sets the incid associated with the secondary habitat record. This is a mandatory field for
        /// existing records and is used to link the secondary habitat to a specific incident. When the value
        /// of this property changes, it triggers validation and raises the DataChanged event to indicate that
        /// the record's validity may have changed.
        /// </summary>
        /// <value>The incid associated with the secondary habitat record.</value>
        public string incid
        {
            get
            {
                return _incid;
            }
            set
            {
                if (_incid != value)
                {
                    _incid = value;
                    OnPropertyChanged(nameof(incid));
                    ValidateProperty(nameof(incid));
                }
            }
        }

        /// <summary>
        /// Gets or sets the secondary habitat code or description. This is a mandatory field and is used to specify
        /// the type of secondary habitat associated with the record. When the value of this property changes, it
        /// triggers validation and raises the DataChanged event to indicate that the record's validity may have changed.
        /// </summary>
        /// <value>The secondary habitat code or description.</value>
        public string secondary_habitat
        {
            get
            {
                return _secondary_habitat;
            }
            set
            {
                if (_secondary_habitat != value)
                {
                    _secondary_habitat = value;
                    OnPropertyChanged(nameof(secondary_habitat));
                    ValidateProperty(nameof(secondary_habitat));

                    // Flag that the current record has changed so that the apply button
                    // will appear.
                    this.DataChanged?.Invoke(true);
                }
            }
        }

        /// <summary>
        /// Gets or sets the integer representation of the secondary habitat code. This is used for sorting
        /// purposes and is derived from the secondary_habitat property. When the secondary_habitat property
        /// changes, this property is updated accordingly.
        /// </summary>
        /// <value>The integer representation of the secondary habitat code.</value>
        public int secondary_habitat_int
        {
            get
            {
                return _secondary_habitat_int;
            }
            set
            {
                _secondary_habitat_int = value;
            }
        }

        /// <summary>
        /// Gets or sets the secondary group associated with the secondary habitat. This is an
        /// optional field that can be used to group secondary habitats into categories. When the
        /// value of this property changes, it does not trigger validation or raise the DataChanged event.
        /// </summary>
        /// <value>The secondary group associated with the secondary habitat.</value>
        public string secondary_group
        {
            get
            {
                return _secondary_group;
            }
            set
            {
                _secondary_group = value;
            }
        }

        #endregion Properties

        #region ToItemArray

        /// <summary>
        /// Converts the current SecondaryHabitat record into an object array, typically for use in
        /// data binding scenarios such as displaying in a DataGridView. The order of the elements
        /// in the array corresponds to the expected order of columns in the UI or data source.
        /// </summary>
        /// <returns>An array of objects representing the current SecondaryHabitat record.</returns>
        public object[] ToItemArray()
        {
            return [_secondary_id, _incid, _secondary_habitat, _secondary_group];
        }

        /// <summary>
        /// Converts the current SecondaryHabitat record into an object array with specified
        /// secondary ID and incid. This can be used when creating a new record where the secondary
        /// ID and incid are not yet set in the object but need to be included in the array for data
        /// binding or other purposes.
        /// </summary>
        /// <param name="secondaryID">The secondary ID to include in the array.</param>
        /// <param name="incid">The INCID to include in the array.</param>
        /// <returns>
        /// An array of objects representing the current SecondaryHabitat record with the specified
        /// secondary ID and incid.
        /// </returns>
        public object[] ToItemArray(int secondaryID, string incid)
        {
            return [secondaryID, incid, _secondary_habitat, _secondary_group];
        }

        #endregion ToItemArray

        #region Validation

        /// <summary>
        /// Gets a value indicating whether the current secondary habitat is a duplicate of another
        /// record in the list of existing secondary habitats. This is determined by checking if
        /// there are any records in the _secondaryHabitatList that have the same secondary_habitat
        /// value as the current record. This property is used for validation purposes to prevent
        /// duplicate entries.
        /// </summary>
        /// <value><c>true</c> if the current secondary habitat is a duplicate; otherwise, <c>false</c>.</value>
        public bool IsDuplicate
        {
            get
            {
                return _secondaryHabitatList != null && _secondaryHabitatList.Any(sh => sh.secondary_habitat == this.secondary_habitat);
            }
        }

        /// <summary>
        /// Determines whether the current record is valid based on the validation rules defined in
        /// the class.
        /// </summary>
        /// <returns><c>true</c> if the current record is valid; otherwise, <c>false</c>.</returns>
        public bool IsValid()
        {
            ValidateProperty(nameof(incid));
            ValidateProperty(nameof(secondary_habitat));

            return !HasErrors;
        }

        /// <summary>
        /// Determines whether the current record is valid based on the validation rules defined in
        /// the class.
        /// </summary>
        /// <param name="bulkUpdateMode">
        /// A value indicating whether the validation is being performed in bulk update mode.
        /// </param>
        /// <returns><c>true</c> if the current record is valid; otherwise, <c>false</c>.</returns>
        public bool IsValid(bool bulkUpdateMode)
        {
            return String.IsNullOrEmpty(ValidateRow(bulkUpdateMode, _secondary_id,
                _incid, _secondary_habitat, _secondary_group));
        }

        /// <summary>
        /// Determines whether the specified data row is valid based on the validation rules defined in
        /// the class.
        /// </summary>
        /// <param name="bulkUpdateMode">
        /// A value indicating whether the validation is being performed in bulk update mode.
        /// </param>
        /// <param name="r">The data row to validate.</param>
        /// <returns><c>true</c> if the specified data row is valid; otherwise, <c>false</c>.</returns>
        public bool IsValid(bool bulkUpdateMode, HluDataSet.incid_secondaryRow r)
        {
            return String.IsNullOrEmpty(ValidateRow(bulkUpdateMode, r.secondary_id,
                r.incid, r.secondary, r.secondary_group));
        }

        /// <summary>
        /// Gets a string containing all validation error messages for the current record,
        /// concatenated with new line separators. This can be used to display a summary of all
        /// validation errors for the record in the UI or for logging purposes.
        /// </summary>
        /// <value>A string containing all validation error messages for the current record.</value>
        public string ErrorMessages
        {
            get
            {
                var allErrors = GetErrors(string.Empty);
                if (allErrors == null)
                    return string.Empty;

                return string.Join(Environment.NewLine, allErrors.Cast<string>());
            }
        }

        /// <summary>
        /// Determines whether the specified data row is valid based on the validation rules defined
        /// in the class.
        /// </summary>
        /// <param name="bulkUpdateMode">
        /// A value indicating whether the validation is being performed in bulk update mode.
        /// </param>
        /// <param name="r">The data row to validate.</param>
        /// <returns><c>true</c> if the specified data row is valid; otherwise, <c>false</c>.</returns>
        public static bool ValidateRow(bool bulkUpdateMode, HluDataSet.incid_secondaryRow r)
        {
            return ValidateRow(bulkUpdateMode, r.secondary_id, r.incid, r.secondary, r.secondary_group) == null;
        }

        private static string ValidateRow(bool _bulkUpdateMode, int secondary_id, string incid,
            string secondary_habitat, string secondary_group)
        {
            StringBuilder sbError = new();

            // Only validate if not in bulk update mode and errors are to be shown
            if (!_bulkUpdateMode)
            {
                if ((secondary_id != -1) && String.IsNullOrEmpty(incid))
                    sbError.Append(Environment.NewLine).Append("Error: INCID is a mandatory field");

                if (String.IsNullOrEmpty(secondary_habitat))
                    sbError.Append(Environment.NewLine).Append("Error: Secondary habitat is a mandatory field");

                if (_validSecondaryCodes == null)
                    sbError.Append(Environment.NewLine).Append("Error: Secondary habitat is not valid without primary habitat");

                if ((_secondaryHabitatList != null) && (_secondaryHabitatList.Count(b => b.secondary_habitat == secondary_habitat) > 1))
                    sbError.Append(Environment.NewLine).Append("Error: Duplicate secondary habitat");

                if (_primarySecondaryCodeValidation > 0)
                {
                    if ((_validSecondaryCodes != null) && (!_validSecondaryCodes.Contains(secondary_habitat)))
                        sbError.Append(Environment.NewLine).Append("Error: Secondary habitat is not valid for primary habitat");
                }
            }

            return sbError.Length > 0 ? sbError.Remove(0, Environment.NewLine.Length).ToString() : null;
        }

        #endregion Validation
    }
}