using System;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Globalization;
using System.ComponentModel;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace CdpLauncher2
{
    public class RegImageConverter : IValueConverter
    {
        public object Convert(object o, Type type, object parameter, CultureInfo culture)
        {
            //if (o is RegValue)
            //{
            //    if (((RegValue)o).Kind == Microsoft.Win32.RegistryValueKind.String)
            //        return "/Images/dataString.png";
            //    else
            //        return "/Images/data.png";
            //}
            //else
            if (o is TreeData)
            {
                if (((TreeData)o).NodeType == TreeDataType.File)
                    return "/Images/folder.png";
            }
            return null;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public struct CountStruct
    {
        public int Count;
        public int CountWait;
    }
    public enum TreeDataType
    {
        File,
        Product,
        NotFound
    }

    public class TreeData : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged Members
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
        #endregion
        private readonly ObservableCollection<TreeData> children = new ObservableCollection<TreeData>();
        public ObservableCollection<TreeData> Children
        {
            get {
                return children;
            }
        }
        public string InFormat { get; set; }
        public string Title { get; set; }
        public string ProductName { get; set; }
        public string OriginalFileName { get; set; }
        public string FileName { get; set; }
        public string InputName { get; set; }
        public int Count { get; set; }
        private int countWait;
        public int CountWait
        {
            get
            {
                return countWait;
            }
            set
            {
                countWait = value;
                OnPropertyChanged("CountMade");
                OnPropertyChanged("ItemColor");
            }
        }
        public string CountMade
        {
            get
            {
                //if (appNum == 1)
                    return (Count - CountWait).ToString();
                //else
                  //  return String.Format("{0}", Count - appNum * CountWait);
            }
        }
        public System.Windows.Media.Brush ItemColor
        {
            get
            {
                if (CountWait == 0)
                    return System.Windows.Media.Brushes.Black;
                else
                    return System.Windows.Media.Brushes.Red;
            }
        }
      
        //private int countMake;
        //public int CountMake
        //{
        //    get
        //    {
        //        return countMake;
        //    }
        //    set
        //    {
        //        countMake = value;
        //        OnPropertyChanged("CountMake");
        //    }
        //}
        private int appNum;
        public int AppNum
        {
            set
            {
                appNum = value;
            }
        }
        public TreeDataType NodeType { get; set; }
        public int Id { get; set; }
        static int _i;
        public TreeData()
        {
            Id = ++_i;
        }
        public override string ToString()
        {
            return Title;
        }
    }
    public class TreeModel : MyWpfControls.Tree.ITreeModel
    {
        
        public TreeData Root { get; private set; }

        public TreeModel()
        {
            Root = new TreeData();
        }

        public System.Collections.IEnumerable GetChildren(object parent)
        {
            if (parent == null)
                parent = Root;
            return (parent as TreeData).Children;
        }

        public bool HasChildren(object parent)
        {
            return (parent as TreeData).Children.Count > 0;
        }
    }
}