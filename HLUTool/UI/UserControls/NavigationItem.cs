using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace HLU.UI.UserControls
{
    public class NavigationItem
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public UserControl Content { get; set; }
    }
}
