using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.ComponentModel;
using System.Windows.Media;

namespace HLU.UI.UserControls
{
    /// <summary>
    /// A class that represents a code/description pair with a boolean value.
    /// </summary>
    public class CodeDescriptionBool : INotifyPropertyChanged
    {
        private string _code;
        private string _description;
        private string _nvc_codes;
        private bool _preferred;

        #region Fields

        public string code
        {
            get => _code;
            set
            {
                _code = value;
                OnPropertyChanged(nameof(code));
            }
        }

        public string description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged(nameof(description));
            }
        }

        public string nvc_codes
        {
            get => _nvc_codes;
            set
            {
                _nvc_codes = value;
                OnPropertyChanged(nameof(nvc_codes));
            }
        }

        public bool preferred
        {
            get => _preferred;
            set
            {
                _preferred = value;
                OnPropertyChanged(nameof(preferred));
            }
        }

        #endregion Fields

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion Events

        #region Constructor

        public CodeDescriptionBool() { }

        public CodeDescriptionBool(string code, string description)
        {
            this.code = code;
            this.description = description;
        }

        public CodeDescriptionBool(string code, string description, bool preferred)
        {
            this.code = code;
            this.description = description;
            this.preferred = preferred;
        }

        #endregion Constructor

        #region Event invokers

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion Event invokers

        #region Methods

        public override string ToString()
        {
            return description;
        }

        #endregion Methods
    }
}
