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
        private string _incid_bak;
        private readonly Dictionary<string, List<string>> _errors = new Dictionary<string, List<string>>();
        private static IEnumerable<BapEnvironment> _bapEnvironmentList;
        private static int _potentialPriorityDetermQtyValidation;

        public readonly static string BAPDetQltyUserAdded = Settings.Default.BAPDeterminationQualityUserAdded;
        public readonly static string BAPDetQltyUserAddedDesc = Settings.Default.BAPDeterminationQualityUserAddedDesc;
        public readonly static string BAPDetQltyPrevious = Settings.Default.BAPDeterminationQualityPrevious;
        public readonly static string BAPDetQltyPreviousDesc = Settings.Default.BAPDeterminationQualityPreviousDesc;

        #endregion

        #region ctor

        public BapEnvironment()
        {
            _bulkUpdateMode = false;
            _secondaryPriorityHabitat = true; // new rows default to secondary as that is what UI needs to create
            _bap_id = -1; // arbitrary PK for a new row
        }

        public BapEnvironment(bool bulkUpdateMode, bool isSecondary)
        {
            _bulkUpdateMode = bulkUpdateMode;
            _secondaryPriorityHabitat = isSecondary;
            _bap_id = -1; // arbitrary PK for a new row
        }

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

        public BapEnvironment(bool bulkUpdateMode, bool isSecondary, object[] itemArray)
        {
            _bulkUpdateMode = bulkUpdateMode;
            _secondaryPriorityHabitat = isSecondary;
            Int32.TryParse(itemArray[0].ToString(), out _bap_id);
            _incid = itemArray[1].ToString();
            _bap_habitat = itemArray[2].ToString();
            _quality_determination = itemArray[3].ToString();
            _quality_interpretation = itemArray[4].ToString();
            // Update the _interpretation_comments string directly, rather than via the property,
            // so that the Changed flag is not set.
            if (itemArray[5].ToString() == null)
                _interpretation_comments = null;
            else
                _interpretation_comments = itemArray[5].ToString().Length <= 254 ? itemArray[5].ToString() : itemArray[5].ToString().Substring(0, 254);
        }

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

        public BapEnvironment(BapEnvironment inBH)
        {
            _bulkUpdateMode = inBH.BulkUpdateMode;
            _secondaryPriorityHabitat = inBH.SecondaryPriorityHabitat;
            _bap_id = -1; // arbitrary PK for a new row
            _incid = null;
            _bap_habitat = inBH.bap_habitat;
            _quality_determination = inBH.quality_determination;
            _quality_interpretation = inBH.quality_interpretation;
            _interpretation_comments = inBH.interpretation_comments;
        }

        public object Clone()
        {
            return new BapEnvironment(this);
        }

        #endregion

        #region DataChanged

        // Create a handler so that updates to the BAP records can be picked
        // up back in the main window.
        //
        // declare the delegate since using the generic pattern
        public delegate void DataChangedEventHandler(bool Changed);

        // declare the event
        public event DataChangedEventHandler DataChanged;

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region INotifyDataErrorInfo

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        public bool HasErrors => _errors.Any();

        public IEnumerable GetErrors(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                // Return all errors for row-level validation
                var allErrors = _errors.Values.SelectMany(e => e).ToList();
                return allErrors.Any() ? allErrors : null;
            }

            if (_errors.ContainsKey(propertyName))
                return _errors[propertyName];

            return null;
        }

        private void SetErrors(string propertyName, List<string> errors)
        {
            bool errorsChanged = false;

            if (errors != null && errors.Any())
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

        private void OnErrorsChanged(string propertyName)
        {
            //TODO: Debug
            System.Diagnostics.Debug.WriteLine($"ErrorsChanged fired for property={propertyName}, bap_id={_bap_id}, HasErrors={HasErrors}");

            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));

            // Also notify for empty string to trigger row-level validation update
            if (!string.IsNullOrEmpty(propertyName))
            {
                ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(string.Empty));
            }

            // THIS IS THE KEY FIX: Notify that HasErrors property itself has changed
            OnPropertyChanged(nameof(HasErrors));
        }

        private void ValidateProperty(string propertyName)
        {
            List<string> errors = new List<string>();

            switch (propertyName)
            {
                case nameof(incid):
                    if ((bap_id != -1) && String.IsNullOrEmpty(incid))
                    {
                        errors.Add("Error: INCID is a mandatory field");
                    }
                    break;

                case nameof(bap_habitat):
                    if (String.IsNullOrEmpty(bap_habitat))
                    {
                        errors.Add("Error: Priority habitat is a mandatory field");
                    }
                    else if ((_bapEnvironmentList != null) && (_bapEnvironmentList.Count(b => b.bap_habitat == bap_habitat) > 1))
                    {
                        errors.Add("Error: Duplicate priority habitat");
                    }
                    break;

                case nameof(quality_determination):
                    if (String.IsNullOrEmpty(quality_determination))
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
                                if ((quality_determination != BAPDetQltyUserAdded)
                                && (quality_determination != BAPDetQltyPrevious))
                                {
                                    errors.Add(String.Format("Error: Determination quality for potential priority habitats can only be '{0}' or '{1}'",
                                        BAPDetQltyUserAddedDesc, BAPDetQltyPreviousDesc));
                                }
                            }
                        }
                        // If this is an automatic priority habitat.
                        else
                        {
                            // Validate that the determination quality can be anything EXCEPT
                            // 'Not present but close to definition' or
                            // 'Previously present, but may no longer exist'.
                            if ((quality_determination == BAPDetQltyUserAdded))
                            {
                                errors.Add(String.Format("Error: Determination quality cannot be '{0}' for priority habitats",
                                    BAPDetQltyUserAddedDesc));
                            }
                            else if ((quality_determination == BAPDetQltyPrevious))
                            {
                                errors.Add(String.Format("Error: Determination quality cannot be '{0}' for priority habitats",
                                    BAPDetQltyPreviousDesc));
                            }
                        }
                    }
                    break;

                case nameof(quality_interpretation):
                    if (!_bulkUpdateMode && String.IsNullOrEmpty(quality_interpretation))
                    {
                        errors.Add("Error: Interpretation quality is a mandatory field");
                    }
                    break;
            }

            SetErrors(propertyName, errors.Any() ? errors : null);
        }

        #endregion

        #region Properties

        public static IEnumerable<BapEnvironment> BapEnvironmentList
        {
            set { _bapEnvironmentList = value; }
        }

        public static int PotentialPriorityDetermQtyValidation
        {
            set { _potentialPriorityDetermQtyValidation = value; }
        }

        public bool BulkUpdateMode
        {
            get { return _bulkUpdateMode; }
            set { _bulkUpdateMode = value; }
        }

        public bool IsAdded
        {
            get { return _bap_id == -1; }
        }

        public bool SecondaryPriorityHabitat
        {
            get { return _secondaryPriorityHabitat; }
        }

        #region incid_bapRow

        public int bap_id
        {
            get { return _bap_id; }
            set { _bap_id = value; }
        }

        public string incid
        {
            get { return _incid; }
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

        public string bap_habitat
        {
            get { return _bap_habitat; }
            set
            {
                if (_bap_habitat != value)
                {
                    _bap_habitat = value;
                    OnPropertyChanged(nameof(bap_habitat));
                    ValidateProperty(nameof(bap_habitat));
                    // Flag that the current record has changed so that the apply button
                    // will appear.
                    DataChanged?.Invoke(true);
                }
            }
        }

        public string quality_determination
        {
            get { return _quality_determination; }
            set
            {
                if (_quality_determination != value)
                {
                    _quality_determination = value;
                    OnPropertyChanged(nameof(quality_determination));
                    ValidateProperty(nameof(quality_determination));
                    // Flag that the current record has changed so that the apply button
                    // will appear.
                    DataChanged?.Invoke(true);
                }
            }
        }

        public string quality_interpretation
        {
            get { return _quality_interpretation; }
            set
            {
                if (_quality_interpretation != value)
                {
                    _quality_interpretation = value;
                    OnPropertyChanged(nameof(quality_interpretation));
                    ValidateProperty(nameof(quality_interpretation));
                    // Flag that the current record has changed so that the apply button
                    // will appear.
                    DataChanged?.Invoke(true);
                }
            }
        }

        public string interpretation_comments
        {
            get { return _interpretation_comments; }
            set
            {
                string newValue = value == null || value.Length <= 254 ? value : value.Substring(0, 254);
                if (_interpretation_comments != newValue)
                {
                    _interpretation_comments = newValue;
                    OnPropertyChanged(nameof(interpretation_comments));
                    ValidateProperty(nameof(interpretation_comments));
                    // Flag that the current record has changed so that the apply button
                    // will appear.
                    DataChanged?.Invoke(true);
                }
            }
        }

        #endregion

        #endregion

        #region Public methods

        public object[] ToItemArray()
        {
            return [ _bap_id, _incid, _bap_habitat, _quality_determination,
                _quality_interpretation, _interpretation_comments ];
        }

        public object[] ToItemArray(int bapID, string incid)
        {
            return [ bapID, incid, _bap_habitat, _quality_determination,
                _quality_interpretation, _interpretation_comments ];
        }

        public object[] ToItemArray(int bapID, string incid, bool isSecondary)
        {
            if (isSecondary) MakeSecondary();
            return [ bapID, incid, _bap_habitat, _quality_determination,
                _quality_interpretation, _interpretation_comments ];
        }

        public void MakeSecondary()
        {
            _secondaryPriorityHabitat = true;
        }

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
        /// <returns></returns>
        public static HluDataSet.incid_bapRow MakeSecondary(HluDataSet.incid_bapRow r)
        {
            // Set the determination quality to 'Previously present, but may no longer exist'
            r.quality_determination = BAPDetQltyPrevious;
            return r;
        }

        #endregion

        #region Validation

        public bool IsDuplicate
        {
            get
            {
                return _bapEnvironmentList != null && _bapEnvironmentList.Any(be => be.bap_habitat == this.bap_habitat);
            }
        }

        public bool IsValid()
        {
            // Validate all properties
            ValidateProperty(nameof(incid));
            ValidateProperty(nameof(bap_habitat));
            ValidateProperty(nameof(quality_determination));
            ValidateProperty(nameof(quality_interpretation));
            ValidateProperty(nameof(interpretation_comments));

            return !HasErrors;
        }

        public bool IsValid(bool bulkUpdateMode, bool isSecondary)
        {
            return String.IsNullOrEmpty(ValidateRow(bulkUpdateMode, isSecondary, _bap_id,
                _incid, _bap_habitat, _quality_determination, _quality_interpretation));
        }

        public bool IsValid(bool bulkUpdateMode, bool isSecondary, HluDataSet.incid_bapRow r)
        {
            return String.IsNullOrEmpty(ValidateRow(bulkUpdateMode, isSecondary, r.bap_id,
                r.incid, r.bap_habitat, r.quality_determination, r.quality_interpretation));
        }

        public string ErrorMessages
        {
            get
            {
                var allErrors = GetErrors(string.Empty);
                if (allErrors == null) return string.Empty;

                return string.Join(Environment.NewLine, allErrors.Cast<string>());
            }
        }

        public static bool ValidateRow(bool bulkUpdateMode, bool isSecondary, HluDataSet.incid_bapRow r)
        {
            return ValidateRow(bulkUpdateMode, isSecondary, r.bap_id, r.incid, r.bap_habitat,
                r.quality_determination, r.quality_interpretation) == null;
        }

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

        #endregion
    }
}