using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Windows;

namespace CdpLauncher2.ViewModel
{
    class MainWindowViewModel : INotifyPropertyChanged
    {
        private int fileCount;
        public int FileCount
        {
            get
            {
                return fileCount;
            }
            set
            {
                fileCount = value;
                RaisePropertyChanged("Statistics");
            }
        }
        private int recordCount;
        public int RecordCount
        {
            get
            {
                return recordCount;
            }
            set
            {
                recordCount = value;
                RaisePropertyChanged("Statistics");
            }
        }
        private int progressBarMaximum;
        public int ProgressBarMaximum
        {
            get
            {
                return progressBarMaximum;
            }
            set
            {
                progressBarMaximum = value;
                RaisePropertyChanged("ProgressBarMaximum");
            }
        }
        private int progressBarValue = -1;
        public int ProgressBarValue
        {
            get
            {
                return progressBarValue;
            }
            set
            {
                progressBarValue = value;
                RaisePropertyChanged("ProgressBarValue");
                RaisePropertyChanged("ProgressBarString");
            }
        }
        public string ProgressBarString
        {
            get
            {
                if (progressBarMaximum > 0)
                    return $"{progressBarValue} / {progressBarMaximum} {((string)Application.Current.FindResource("Records")).ToLower()}";
                    //return String.Format("{0} / {1} records", progressBarValue, progressBarMaximum);
                else
                    return "";
            }
        }
        public string Statistics
        {
            get
            {
                return $"{(string)Application.Current.FindResource("Files")}: {fileCount}, {(string)Application.Current.FindResource("Records")}: {recordCount}";
            }
        }
        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        public void RaisePropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
