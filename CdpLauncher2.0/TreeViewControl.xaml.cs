using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CdpLauncher2
{
    /// <summary>
    /// Interaction logic for TreeViewControl.xaml
    /// </summary>
    public partial class TreeViewControl : UserControl
    {
        public System.Collections.IList SelectedItems
        {
            get
            {
                return tlFiles.SelectedItems;
            }
        }
        public System.Collections.IList Nodes
        {
            get
            {
                return tlFiles.Nodes;
            }
        }
        public TreeViewControl()
        {
            InitializeComponent();
        }
        public void SetContext(TreeModel tm)
        {
            tlFiles.Model = null;
            tlFiles.Model = tm;
        }
        public void Refresh()
        {
            tlFiles.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Render, new Action(delegate () { }));
        }
    }
}
