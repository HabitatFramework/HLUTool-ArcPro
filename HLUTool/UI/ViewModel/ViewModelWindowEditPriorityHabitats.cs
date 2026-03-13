// HLUTool is used to view and maintain habitat and land use GIS data.
// Copyright © 2019 Greenspace Information for Greater London CIC
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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using HLU.Data;
using HLU.Data.Model;
using HLU.UI.View;

namespace HLU.UI.ViewModel
{
    /// <summary>
    /// Contains the ViewModel for the Edit Priority Habitats window.
    /// </summary>
    class ViewModelWindowEditPriorityHabitats : ViewModelBase, IDataErrorInfo
    {
        public static HluDataSet HluDatasetStatic = null;

        #region Fields

        private ICommand _okCommand;
        private ICommand _cancelCommand;

        private string _displayName = "Priority Habitats";

        private ViewModelWindowMain _viewModelMain;

        private ObservableCollection<BapEnvironment> _incidBapRowsAuto;

        #endregion Fields

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the ViewModelWindowEditPriorityHabitats class.
        /// </summary>
        /// <param name="viewModelMain">The main ViewModel.</param>
        /// <param name="incidBapHabitatsAuto">The collection of automatically determined BAP habitats.</param>
        public ViewModelWindowEditPriorityHabitats(ViewModelWindowMain viewModelMain, ObservableCollection<BapEnvironment> incidBapHabitatsAuto)
        {
            _viewModelMain = viewModelMain;

            IEnumerable<BapEnvironment> prevBapRowsAuto = null;
            prevBapRowsAuto = from p in incidBapHabitatsAuto
                         select new BapEnvironment(false, false, p.Bap_id, p.Incid, p.Bap_habitat, p.Quality_determination, p.Quality_interpretation, p.Interpretation_comments);

            _incidBapRowsAuto = new ObservableCollection<BapEnvironment>(prevBapRowsAuto);
            OnPropertyChanged(nameof(IncidBapHabitatsAuto));
        }

        #endregion Constructor

        #region ViewModelBase Members

        public override string DisplayName
        {
            get { return _displayName; }
            set { _displayName = value; }
        }

        public override string WindowTitle
        {
            get { return DisplayName; }
        }

        #endregion ViewModelBase Members

        #region RequestClose

        // Declare the delegate since using non-generic pattern
        public delegate void RequestCloseEventHandler(ObservableCollection<BapEnvironment> incidBapRowsAuto);

        // Declare the event
        public event RequestCloseEventHandler RequestClose;

        #endregion RequestClose

        #region Ok Command

        /// <summary>
        /// Gets the command for the Ok button.
        /// </summary>
        /// <value>The command to execute when the Ok button is clicked.</value>
        public ICommand OkCommand
        {
            get
            {
                if (_okCommand == null)
                {
                    Action<object> okAction = new(this.OkCommandClick);
                    _okCommand = new RelayCommand(okAction, param => this.CanOk);
                }

                return _okCommand;
            }
        }

        /// <summary>
        /// Handles events when the Ok button is clicked.
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private void OkCommandClick(object param)
        {
            RequestClose?.Invoke(_incidBapRowsAuto);
        }

        /// <summary>
        /// Gets a value indicating whether the Ok button can be clicked, based on the validity of the automatically determined BAP habitats.
        /// </summary>
        /// <value><c>true</c> if the Ok button can be clicked; otherwise, <c>false</c>.</value>
        public bool CanOk
        {
            get
            {
                //if (_incidBapRowsAuto != null && _incidBapRowsAuto.Count > 0)
                if (_incidBapRowsAuto != null)
                {
                    int countInvalid = _incidBapRowsAuto.Count(be => !be.IsValid());
                    if (countInvalid > 0)
                        return false;
                    else
                        return true;
                }
                return false;
            }
        }

        #endregion Ok Command

        #region Cancel Command

        /// <summary>
        /// Gets the command for the Cancel button.
        /// </summary>
        /// <value>The command to execute when the Cancel button is clicked.</value>
        public ICommand CancelCommand
        {
            get
            {
                if (_cancelCommand == null)
                {
                    Action<object> cancelAction = new(this.CancelCommandClick);
                    _cancelCommand = new RelayCommand(cancelAction);
                }

                return _cancelCommand;
            }
        }

        /// <summary>
        /// Handles events when the Cancel button is clicked.
        /// </summary>
        /// <param name="param">The parameter passed to the command.</param>
        private void CancelCommandClick(object param)
        {
            RequestClose?.Invoke(null);
        }

        #endregion Cancel Command

        #region BAP Habitat

        public HluDataSet.lut_habitat_typeRow[] BapHabitatCodes
        {
            get
            {
                return _viewModelMain.BapHabitatCodes;
            }
        }

        public HluDataSet.lut_quality_determinationRow[] BapDeterminationQualityCodesAuto
        {
            get
            {
                return _viewModelMain.BapDeterminationQualityCodesAuto;
            }
        }

        public HluDataSet.lut_quality_interpretationRow[] BapInterpretationQualityCodes
        {
            get
            {
                return _viewModelMain.InterpretationQualityCodes;
            }
        }

        public bool BapHabitatsAutoEnabled
        {
            get
            {
                return IncidBapHabitatsAuto != null && IncidBapHabitatsAuto.Count > 0;
            }
        }

        public ObservableCollection<BapEnvironment> IncidBapHabitatsAuto
        {
            get { return _incidBapRowsAuto; }
            set
            {
                _incidBapRowsAuto = value;
            }
        }

        #endregion BAP Habitat

        #region IDataErrorInfo Members

        public string Error
        {
            get
            {
                StringBuilder error = new();

                if (error.Length > 0)
                    return error.ToString();
                else
                    return null;
            }
        }

        public string this[string columnName]
        {
            get
            {
                string error = null;

                CommandManager.InvalidateRequerySuggested();

                return error;
            }
        }

        #endregion IDataErrorInfo Members
    }
}