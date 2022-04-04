using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.ComponentModel;
using System.Threading;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.Schema;
using CdpLauncher2.Model;
using CdpLauncher2.ViewModel;
using HugeLib;
using Condition = CdpLauncher2.Model.Condition;

namespace CdpLauncher2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string startDirectory, exeName, iniName;
        XmlDocument xmlSettings = null;
        XmlNamespaceManager xnmSettings = null;
        XmlDocument xmlProducts = null;
        XmlNamespaceManager xnmProducts = null;
        int scheduleTime = 0;
        System.Timers.Timer schedule = null;

        public ObservableCollection<LogEntry> LogEntries { get; set; }
        MainWindowViewModel mainWindowObject;

        TreeModel treeModel = null;

        private ArrayList products = new ArrayList();


        public MainWindow()
        {
            InitializeComponent();
            icLog.DataContext = LogEntries = new ObservableCollection<LogEntry>();
            this.DataContext = mainWindowObject = new MainWindowViewModel();
            tvcTree.SetContext(treeModel = new TreeModel());

            //this.Title = $"CDP Launcher 2.0 ({System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString()})";

            startDirectory = System.AppDomain.CurrentDomain.BaseDirectory;
            exeName = System.Diagnostics.Process.GetCurrentProcess().MainModule.ModuleName.Replace(".exe", "").Replace(".vshost", "");

            if (!Directory.Exists(System.IO.Path.Combine(startDirectory, "Pending")))
                Directory.CreateDirectory(System.IO.Path.Combine(startDirectory, "Pending"));
            iniName = String.Format(@"{0}Pending\pending.ini", startDirectory);
            LoadXmls();
            try
            {
                scheduleTime = Convert.ToInt32(XmlClass.GetAttribute(xmlSettings, "Schedule", "Timeout", "0", xnmSettings));
            }
            catch
            {
                scheduleTime = 0;
            }
            if (scheduleTime > 0)
            {
                schedule = new System.Timers.Timer(scheduleTime * 1000);
                schedule.Elapsed += Schedule_Elapsed;
                schedule.Start();
            }
            else
                LoadData();
        }

        private void Schedule_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //tvcTree.Dispatcher.Invoke(new Action(delegate ()
            //{
            //    tvcTree.DataContext = null;
            //}), System.Windows.Threading.DispatcherPriority.Background);
            schedule.Stop();            
            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.WorkerSupportsCancellation = true;
            backgroundWorker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
            backgroundWorker.Disposed += BackgroundWorker_Disposed;
            backgroundWorker.DoWork += delegate (object o, DoWorkEventArgs args)
            {
                tvcTree.Dispatcher.Invoke(new Action(delegate ()
                {
                    LoadData();
                    foreach (MyWpfControls.Tree.TreeNode tn in tvcTree.Nodes)
                        tn.IsSelected = true;
                    bRun_Click(this, null);
                }), System.Windows.Threading.DispatcherPriority.Background);
            };
            backgroundWorker.RunWorkerAsync();
        }
        private void AddToLog1(string message, params object[] pars)
        {
            this.Dispatcher.Invoke(new Action(delegate ()
                {
                    LogEntries.Add(new LogEntry() { DateTime = DateTime.Now.ToLongTimeString(), Message = String.Format(message, pars) });
                    LogClass.Level = 100; //пока отключил логи
                    LogClass.WriteToLog(message, pars);
                }));
        }

        private void AddToDebug(string message, params object[] pars)
        {
            DateTime dt = DateTime.Now;
            string strLog = String.Format("{0:HH.mm.ss}:{1:000}\t", dt, dt.Millisecond);
            strLog += String.Format(message, pars);
            Debug.WriteLine(strLog);
        }
        private void LoadXmls()
        {
            AddToLog1($"CDPLauncher {System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString()}");
            xmlSettings = new XmlDocument();
            SetLanguage("english");
            try
            {
                xmlSettings.Load(String.Format("{0}{1}.xml", startDirectory, exeName));
                xnmSettings = new XmlNamespaceManager(xmlSettings.NameTable);
                SetLanguage(XmlClass.GetTag(xmlSettings, "Language", "russian", xnmSettings));
                AddToLog1((string)this.FindResource("Config"));
                AddToLog1($"{(string)this.FindResource("ConfigFile")} {startDirectory}{exeName}.xml {(string)this.FindResource("Loaded")}", startDirectory, exeName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{(string)this.FindResource("ConfigError")} {ex.Message}");
                AddToLog1($"{(string)this.FindResource("ConfigError")} {ex.Message}");
                return;
            }
            xmlProducts = new XmlDocument();
            try
            {
                string prods = XmlClass.GetTag(xmlSettings, "ProductsFile", xnmSettings).Replace("..\\", startDirectory);
                if (prods.Length == 0)
                    prods = String.Format("{0}Products.xml", startDirectory);
                AddToDebug($"{(string)this.FindResource("Products")}");
                xmlProducts.Load(prods);
                int cnt = XmlClass.GetXmlNodeCount(xmlProducts, "", xnmProducts);
                AddToLog1($"{(string)this.FindResource("ProductFile")} {prods} {(string)this.FindResource("Loaded")}. {cnt} {(string)this.FindResource("ProductsLow")}");
                //AddToDebug("Products loaded");

                products.Clear();
                for (int i = 0; i < cnt; i++)
                {
                    ProductClass pc = new ProductClass();
                    XmlDocument pr = XmlClass.GetXmlNode(xmlProducts, "", i, null);
                    pc.ProductName = XmlClass.GetAttribute(pr, "", "Name", null);
                    pc.conditions = new List<List<Condition>>();
                    int allConditionsCnt = XmlClass.GetXmlNodeCount(pr, "Conditions", null);
                    for (int k = 0; k < allConditionsCnt; k++)
                    {
                        pc.conditions.Add(new List<Condition>());
                        XmlDocument oneConditions = XmlClass.GetXmlNode(pr, "Conditions", k, null);
                        int conditionCnt = XmlClass.GetXmlNodeCount(oneConditions, "Condition", null);
                        for (int j = 0; j < conditionCnt; j++)
                        {
                            XmlDocument cn = XmlClass.GetXmlNode(oneConditions, "Condition", j, null);
                            pc.conditions[k].Add(new Condition() { Field = XmlClass.GetAttribute(cn, "", "Field", null), Type = XmlClass.GetAttribute(cn, "", "Type", null), Value = XmlClass.GetAttribute(cn, "", "Value", null) });
                        }
                    }
                    XmlDocument addon = XmlClass.GetXmlNode(pr, "AdditionalProcessing", 0, null);
                    if (addon != null)
                        pc.AdditionalXml = addon.OuterXml;
                    pc.Ignore = XmlClass.GetAttribute(pr, "", "Ignore", "false", xnmProducts).ToLower() == "true";
                    products.Add(pc);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{(string)this.FindResource("ProductError")}: {ex.Message}");
                AddToLog1($"{(string)this.FindResource("ProductError")}: {ex.Message}");
            }
        }

        private void LoadData()
        {
            treeModel.Root.Children.Clear();
            mainWindowObject.FileCount = 0;
            mainWindowObject.RecordCount = 0;
            AddToLog1((string)this.FindResource("InputScan"));
            int cnt = XmlClass.GetXmlNodeCount(xmlSettings, "Inputs/Input", xnmSettings);
            for (int i = 0; i < cnt; i++)
            {
                XmlDocument xd = XmlClass.GetXmlNode(xmlSettings, "Inputs/Input", i, xnmSettings);
                string inName = XmlClass.GetAttribute(xd, "", "Name", null);
                string folder = XmlClass.GetDataXml(xd, "Directory", null).Replace("..\\", startDirectory);
                string mask = XmlClass.GetDataXml(xd, "Mask", null);
                string fields = XmlClass.GetDataXml(xd, "InputFormat", null);
                if (inName.Length == 0)
                {
                    AddToLog1($"{(string)this.FindResource("InputNameError")} {i}");
                    continue;
                }

                if (!Directory.Exists(folder))
                {
                    AddToLog1($"{(string)this.FindResource("InputFolderError")} {folder}");
                    continue;
                }

                AddToLog1($"{(string)this.FindResource("InputFolder")} {folder}, {(string)this.FindResource("InputMask")} {mask}");
                DirectoryInfo di = new DirectoryInfo(folder);
                try
                {
                    foreach (FileInfo fi in di.GetFiles(mask))
                    {
                        TreeData top = new TreeData()
                        {
                            Title = fi.Name,
                            InFormat = inName,
                            ProductName = "",
                            Count = 0,
                            NodeType = TreeDataType.File
                        };
                        CountStruct recCount = LoadFile(fi.FullName, fields, inName, top);
                        top.Count = recCount.Count;
                        top.CountWait = recCount.CountWait;
                        top.FileName = fi.FullName;
                        top.OriginalFileName = fi.FullName;
                        top.InputName = inName;
                        mainWindowObject.RecordCount += top.Count;
                        mainWindowObject.FileCount++;

                        treeModel.Root.Children.Add(top);
                    }
                }
                catch (Exception e)
                {
                    AddToLog1($"{e.Message}");
                }
            }

            tvcTree.SetContext(treeModel);
            foreach (MyWpfControls.Tree.TreeNode tn in tvcTree.Nodes)
            {
                tn.IsSelected = true;
            }
        }

        private CountStruct LoadFile(string fileName, string inputFormat, string inName, TreeData parent)
        {
            CountStruct res = new CountStruct() { Count = 0, CountWait = 0 };
            bool newFile = false;
            FileInfo fi = new FileInfo(fileName);
            string sectionName = String.Format("{0}.{1}", inName, fi.Name);
            StringBuilder sb = new StringBuilder(255);
            string prefix = "";
            DateTime fromIni;
            try
            {
                IniFile.GetPrivateProfileString(sectionName, "WriteTime", "", sb, 255, iniName);
                fromIni = Convert.ToDateTime(sb.ToString());
            }
            catch
            {
                fromIni = DateTime.MinValue;
            }
            if (fromIni.Date == fi.LastWriteTime.Date && fromIni.Hour == fi.LastWriteTime.Hour && fromIni.Minute == fi.LastWriteTime.Minute && fromIni.Second == fi.LastWriteTime.Second)
            {
                newFile = false;
                IniFile.GetPrivateProfileString(sectionName, "Prefix", "", sb, 255, iniName);
                prefix = sb.ToString();
            }
            else
            {
                newFile = true;
                //удаляем всю секцию этого файла
                IniFile.WritePrivateProfileString(sectionName, null, "", iniName);
                //добавляем ее заново, генеря новый префикс
                IniFile.WritePrivateProfileString(sectionName, "WriteTime", fi.LastWriteTime.ToString(), iniName);
                prefix = Guid.NewGuid().ToString();
                IniFile.WritePrivateProfileString(sectionName, "Prefix", prefix, iniName);
            }
            Dictionary<string, int> ht = new Dictionary<string, int>();
            ht.Add("", 0);
            int productCnt = XmlClass.GetXmlNodeCount(xmlProducts, "", xnmProducts);
            XmlDocument xd = XmlClass.GetXmlNode(xmlSettings, "InputFormats/InputFormat", "Name", inputFormat, xnmSettings);
            if (xd == null)
            {
                AddToLog1((string)this.FindResource("ErrorSection"), fileName, inputFormat);
                return res;
            }
            string tp = XmlClass.GetAttribute(xd, "", "Type", xnmSettings);
            switch (tp)
            {
                #region TextFile
                case "Text":
                    int enc = XmlClass.GetDataXmlInt(xd, "", "Encoding", 1251, xnmSettings);
                    using (StreamReader sr = new StreamReader(fileName, Encoding.GetEncoding(enc)))
                    {
                        int nom = 0;
                        sr.BaseStream.Seek(0, SeekOrigin.Begin);
                        while(sr.Peek() >= 0)
                        {
                            AddToDebug($"AddRecord {nom++}");
                            RecordClass rc = new RecordClass();
                            rc.OriginalString = sr.ReadLine();
                            rc.FileName = fileName;
                            rc.ParseFields(xd);
                            bool check = false, ignore = false;

                            #region изменил подход к работе, если products.xml большой (в газпроме больше 500 продуктов), то долго бегает по xml
                            // теперь гружу в память массив продуктов
                            //for (int i = 0; i < productCnt; i++)
                            //{
                            //    XmlDocument pr = XmlClass.GetXmlNode(xmlProducts, "", i, null);
                            //    string prname = XmlClass.GetAttribute(pr, "", "Name", null);
                            //    int allConditionsCnt = XmlClass.GetXmlNodeCount(pr, "Conditions", null);
                            //    for (int k = 0; k < allConditionsCnt; k++)
                            //    {
                            //        XmlDocument oneConditions = XmlClass.GetXmlNode(pr, "Conditions", k, null);
                            //        int conditionCnt = XmlClass.GetXmlNodeCount(oneConditions, "Condition", null);
                            //        for (int j = 0; j < conditionCnt; j++)
                            //        {
                            //            XmlDocument cn = XmlClass.GetXmlNode(oneConditions, "Condition", j, null);
                            //            try
                            //            {
                            //                check = rc.CheckCondition(XmlClass.GetAttribute(cn, "", "Field", null), XmlClass.GetAttribute(cn, "", "Type", null), XmlClass.GetAttribute(cn, "", "Value", null));
                            //                if (!check)
                            //                    break;
                            //            }
                            //            catch (Exception ex)
                            //            {
                            //                AddToLog("ошибка условия: {0}", ex.Message);
                            //            }
                            //        }
                            //        if (check)
                            //            break;
                            //    }
                            //    if (check)
                            //    {
                            //        if (newFile)
                            //        {
                            //            string workname = String.Format(@"{0}Pending\{1}.{2}", startDirectory, prefix, prname);
                            //            StreamWriter sw = new StreamWriter(workname, true, Encoding.GetEncoding(enc));
                            //            sw.WriteLine(rc.OriginalString);
                            //            sw.Close();
                            //        }
                            //        if (ht.ContainsKey(prname))
                            //            ht[prname] = (int)ht[prname] + 1;
                            //        else
                            //            ht.Add(prname, 1);
                            //        break;
                            //    }
                            //}
                            #endregion
                            foreach (ProductClass pc in products)
                            {
                                foreach (List<Condition> cc in pc.conditions)
                                {
                                    foreach (Condition c in cc)
                                    {
                                        check = rc.CheckCondition(c.Field, c.Type, c.Value);
                                        if (!check)
                                            break;
                                    }
                                    if (check)
                                        break;
                                }
                                if (check)
                                {
                                    if (ignore = pc.Ignore)  //здесь мы присваиваем значение и сразу проверяем. Не прозрачно, но нормально
                                        break;
                                    XmlDocument prior = null;
                                    if (!String.IsNullOrEmpty(pc.AdditionalXml))
                                    {
                                        prior = new XmlDocument();
                                        try
                                        {
                                            prior.LoadXml(pc.AdditionalXml);
                                        }
                                        catch { }
                                    }
                                    if (newFile)
                                    {
                                        string workname = String.Format(@"{0}Pending\{1}.{2}", startDirectory, prefix, pc.ProductName);
                                        StreamWriter sw = new StreamWriter(workname, true, Encoding.GetEncoding(enc));
                                        sw.WriteLine(PreProcessing(rc.OriginalString, prior));
                                        sw.Close();
                                        if (rc.AdvanceString.Length > 0)
                                        {
                                            string advname = String.Format(@"{0}Pending\{1}.{2}.adv", startDirectory, prefix, pc.ProductName);
                                            StreamWriter swa = new StreamWriter(advname, true, Encoding.GetEncoding(enc));
                                            swa.WriteLine(rc.AdvanceString);
                                            swa.Close();
                                        }
                                    }
                                    if (ht.ContainsKey(pc.ProductName))
                                        ht[pc.ProductName] = (int)ht[pc.ProductName] + 1;
                                    else
                                        ht.Add(pc.ProductName, 1);
                                    break;
                                }
                            }
                            if (!check)
                            {
                                if (newFile)
                                {
                                    string workname = String.Format(@"{0}Pending\{1}.{2}", startDirectory, prefix, "notfound");
                                    StreamWriter sw = new StreamWriter(workname, true);
                                    sw.WriteLine(rc.OriginalString);
                                    sw.Close();
                                }
                                ht[""] = (int)ht[""] + 1;
                            }
                            if (!ignore)
                                res.Count++;
                        }
                        sr.Close();
                    }
                    break;
                #endregion
                #region Xml file
                case "Xml":
                    XmlDocument xmlIn = new XmlDocument();
                    xmlIn.Load(fileName);
                    XmlNamespaceManager xnmIn = new XmlNamespaceManager(xmlIn.NameTable);
                    xnmIn.AddNamespace(XmlClass.GetAttribute(xd, "Namespace", "Prefix", xnmSettings), XmlClass.GetTag(xd, "Namespace", xnmSettings));
                    int cnt = XmlClass.GetXmlNodeCount(xmlIn, XmlClass.GetTag(xd, "RecordNode", xnmSettings), xnmIn);
                    break;
                #endregion
                default:
                    break;
            }
            foreach(KeyValuePair<string, int>kvp in ht)
            {
                if (kvp.Key.Length == 0 && kvp.Value == 0)
                    continue;
                TreeData td = new TreeData();
                td.NodeType = (kvp.Key.Length == 0) ? TreeDataType.NotFound : TreeDataType.Product;
                td.ProductName = (kvp.Key.Length == 0) ? "Not defined" : kvp.Key;
                td.OriginalFileName = fileName;
                td.FileName = String.Format(@"{0}Pending\{1}.{2}", startDirectory, prefix, (kvp.Key.Length == 0) ? "notfound" : kvp.Key);
                td.InputName = inName;
                td.Title = "";
                td.Count = kvp.Value;
                if (File.Exists(td.FileName))
                {
                    //проверяем кол-во по этому файлу-продукту в pending
                    using (StreamReader sr = new StreamReader(td.FileName))
                    {
                        sr.BaseStream.Seek(0, SeekOrigin.Begin);
                        while (sr.Peek() >= 0)
                        {
                            sr.ReadLine();
                            td.CountWait++;
                            res.CountWait++;
                        }
                        sr.Close();
                    }
                }
                parent.Children.Add(td);
            }
            return res;
        }

        private void bRun_Click(object sender, RoutedEventArgs e)
        {
            //https://elegantcode.com/2009/07/03/wpf-multithreading-using-the-backgroundworker-and-reporting-the-progress-to-the-ui/
            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.WorkerSupportsCancellation = true;
            backgroundWorker.RunWorkerCompleted += BackgroundTimer_RunWorkerCompleted;
            backgroundWorker.Disposed += BackgroundWorker_Disposed;
            backgroundWorker.DoWork += delegate (object o, DoWorkEventArgs args)
            {
                AddToLog1((string)this.FindResource("Start"));
                //сперва проходим по файлам, если выбран
                foreach (MyWpfControls.Tree.TreeNode tn in tvcTree.Nodes)
                {
                    if (!tn.IsSelected)
                        continue;
                    if ((tn.Tag as TreeData).NodeType != TreeDataType.File)
                        continue;
                    foreach (TreeData child in (tn.Tag as TreeData).Children)
                    {
                        if (child.NodeType != TreeDataType.Product)
                            continue;
                        AddToLog1($"{(string)this.FindResource("File")} {(tn.Tag as TreeData).Title} {((string)this.FindResource("Product")).ToLower()} {child.ProductName}");
                        try
                        {
                            int cnt = RunCdp(child);
                            if (cnt > 0)
                            {
                                (tn.Tag as TreeData).CountWait -= cnt;// child.CountWait;
                                child.CountWait = 0;
                            }
                            AddToLog1($"{(string)this.FindResource("Completed")}");
                        }
                        catch (Exception ex)
                        {
                            AddToLog1($"{(string)this.FindResource("Error")}: {ex.Message}");
                        }
                    }
                }
                //
                foreach (MyWpfControls.Tree.TreeNode tn in tvcTree.Nodes)
                {
                    if (tn.IsSelected)
                        continue;
                    if ((tn.Tag as TreeData).NodeType != TreeDataType.File)
                        continue;
                    foreach (MyWpfControls.Tree.TreeNode child in tn.Nodes)
                    {
                        if (!child.IsSelected || (child.Tag as TreeData).NodeType != TreeDataType.Product)
                            continue;
                        AddToLog1($"{(string)this.FindResource("File")} {(tn.Tag as TreeData).Title} {((string)this.FindResource("Product")).ToLower()} {(child.Tag as TreeData).ProductName}");
                        try
                        {
                            int cnt = RunCdp(child.Tag as TreeData);
                            if (cnt > 0)
                            {
                                (tn.Tag as TreeData).CountWait -= cnt;// (child.Tag as TreeData).CountWait;
                                (child.Tag as TreeData).CountWait = 0;
                            }
                            AddToLog1($"{(string)this.FindResource("Completed")}");
                        }
                        catch (Exception ex)
                        {
                            AddToLog1($"{(string)this.FindResource("Error")}: {ex.Message}");
                        }
                    }
                }
                //проходим по всем файла дерева, чтобы предпринять действия с файлом (удалить или перебросить в архив)
                foreach (MyWpfControls.Tree.TreeNode tn in tvcTree.Nodes)
                {
                    TreeData td = tn.Tag as TreeData;
                    if (td.NodeType != TreeDataType.File)
                        continue;
                    if (td.CountWait == 0)
                    {
                        XmlDocument xd = XmlClass.GetXmlNode(xmlSettings, "Inputs/Input", "Name", td.InputName, xnmSettings);
                        string action = XmlClass.GetAttribute(xd, "", "PostAction", xnmSettings).ToLower();
                        if (action == "delete")
                            File.Delete(td.FileName);
                        if (action == "move")
                        {
                            string archive = XmlClass.GetTag(xd, "ArchiveDirectory", "", xnmSettings).Replace(@"..\", startDirectory);
                            if (Directory.Exists(archive))
                            {
                                if (File.Exists(System.IO.Path.Combine(archive, td.Title)))
                                    File.Delete(System.IO.Path.Combine(archive, td.Title));
                                try
                                {
                                    File.Move(td.FileName, System.IO.Path.Combine(archive, td.Title));
                                }
                                catch { }
                            }
                        }
                    }
                }
            };
            backgroundWorker.RunWorkerAsync();
        }

        private void BackgroundTimer_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            mainWindowObject.ProgressBarValue = 0;
            if (schedule != null)
                schedule.Start();
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            mainWindowObject.ProgressBarValue = 0;
            //tvcTree.SetContext(treeModel);
            //tvcTree.Refresh();
            //throw new NotImplementedException();
        }

        private void BackgroundWorker_Disposed(object sender, EventArgs e)
        {
            
            tvcTree.Refresh();
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            bool autoScroll = true;
            // User scroll event : set or unset autoscroll mode
            if (e.ExtentHeightChange == 0)
            {   // Content unchanged : user scroll event
                if ((e.Source as ScrollViewer).VerticalOffset == (e.Source as ScrollViewer).ScrollableHeight)
                {   // Scroll bar is in bottom
                    // Set autoscroll mode
                    autoScroll = true;
                }
                else
                {   // Scroll bar isn't in bottom
                    // Unset autoscroll mode
                    autoScroll = false;
                }
            }

            // Content scroll event : autoscroll eventually
            if (autoScroll && e.ExtentHeightChange != 0)
            {   // Content changed and autoscroll mode set
                // Autoscroll
                (e.Source as ScrollViewer).ScrollToVerticalOffset((e.Source as ScrollViewer).ExtentHeight);
            }
        }

        private void bRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadXmls();
            LoadData();
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="product"></param>
        /// <returns>число обработанных записей</returns>
        private int RunCdp(TreeData product)
        {
            if (product.CountWait == 0)
                return 0; 
            //получение pending файла с данными
            /*StringBuilder sb = new StringBuilder();
            string sectionName = String.Format("{0}.{1}", file.InFormat, file.FileName);
            IniFile.GetPrivateProfileString(sectionName, "Prefix", "", sb, 255, iniName);
            string prefix = sb.ToString();*/
            string cdpDir = XmlClass.GetTag(xmlSettings, "CDP/Directory", xnmSettings);
            if (!cdpDir.EndsWith(@"\"))
                cdpDir += @"\";
            string errFile = XmlClass.GetTag(xmlSettings, "CDP/ErrorFile", xnmSettings).Replace(@"..\", cdpDir);
            XmlDocument xd = XmlClass.GetXmlNode(xmlProducts, "Product", "Name", product.ProductName, xnmProducts);
            if (xd == null)
                throw new Exception($"{(string)this.FindResource("ProductNameError")} {product.ProductName}");
            bool ignore = XmlClass.GetAttribute(xd, "", "Ignore", "", null).ToLower() == "true";
            if (ignore)
                return product.CountWait;
            int appNum = 1;
            try
            {
                appNum = Convert.ToInt32(XmlClass.GetAttribute(xd, "", "AppNum", "1", null));
            }
            catch
            {
                appNum = 1;
            }
            string fileIn = XmlClass.GetTag(xd, "InFile", null).Replace(@"..\", cdpDir);
            if (String.IsNullOrEmpty(fileIn))
                fileIn = XmlClass.GetTag(xmlSettings, "CDP/InFile", xnmSettings).Replace(@"..\", cdpDir);
            if (String.IsNullOrEmpty(fileIn))
                throw new Exception($"{(string)this.FindResource("ProductInFileError")} {product.ProductName}");
            //string fileOut = XmlClass.GetTag(xd, "OutFile", null).Replace(@"..\", cdpDir);
            //if (String.IsNullOrEmpty(fileOut))
            //    fileOut = XmlClass.GetTag(xmlSettings, "CDP/OutFile", xnmSettings).Replace(@"..\", cdpDir);
            //if (String.IsNullOrEmpty(fileOut))
            //    throw new Exception(String.Format("Не найден параметр OutFile продукта {0}", product.ProductName));
            string ini = XmlClass.GetTag(xd, "CDPIni", null).Replace(@"..\", cdpDir);
            bool noCdp = XmlClass.GetAttribute(xd, "", "Cdp", "", null).ToLower() == "none";
            if (String.IsNullOrEmpty(ini))
                ini = XmlClass.GetTag(xmlSettings, "CDP/CDPIni", xnmSettings).Replace(@"..\", cdpDir);
            if (!noCdp && String.IsNullOrEmpty(ini))
                throw new Exception($"{(string)this.FindResource("CDPIni")} {product.ProductName}");
            bool isEmb = (IniFile.GetPrivateProfileInt("Proekt", "UseFile", 0, ini) == 1);
            bool isPin = (IniFile.GetPrivateProfileInt("Proekt", "UsePin", 0, ini) == 1);
            bool isProc = (IniFile.GetPrivateProfileInt("Proekt", "UseProc", 0, ini) == 1);
            bool isProcAfter = (IniFile.GetPrivateProfileInt("Proekt", "WorkProc", 0, ini) != 0);
            //если в секции продукта указаны атрибуты NoPin или NoProc = true, то пусть даже cdp их выгружает, но они не будут переноситься
            string temp = XmlClass.GetAttribute(xd, "", "NoPin", "", null).ToLower();
            if (temp == "true")
                isPin = false;
            temp = XmlClass.GetAttribute(xd, "", "NoProc", "", null).ToLower();
            if (temp == "true")
                isProc = false;

            StringBuilder sb = new StringBuilder(255);
            IniFile.GetPrivateProfileString("Proekt", "OutFileName", "", sb, 255, ini);
            string outCdp = sb.ToString();
            IniFile.GetPrivateProfileString("Proekt", "OutFilePinName", "", sb, 255, ini);
            string pinCdp = sb.ToString();
            IniFile.GetPrivateProfileString("Proekt", "OutFileProcName", "", sb, 255, ini);
            string procCdp = sb.ToString();
            if (noCdp)
            {
                isEmb = true; isPin = false; isProc = false;
                outCdp = fileIn;
            }

            if (isEmb && String.IsNullOrEmpty(outCdp))
                throw new Exception($"{ini}. {(string)this.FindResource("CdpEmbossFileError")}");
            if (isPin && String.IsNullOrEmpty(pinCdp))
                throw new Exception($"{ini}. {(string)this.FindResource("CdpPinFileError")}");
            if (isProc && String.IsNullOrEmpty(procCdp))
                throw new Exception($"{ini}. {(string)this.FindResource("CdpProcFileError")}");
            #region формирование итогового имени
            XmlDocument xdIn = XmlClass.GetXmlNode(xmlSettings, "Inputs/Input", "Name", product.InputName, xnmSettings);
            //string outDir = XmlClass.GetTag(xdIn, "OutDirectory", null).Replace(@"..\", startDirectory);
            bool reformEmbName = HasFieldItem(XmlClass.GetTag(xdIn, "OutFileNameFormat", null));
            bool reformPinName = HasFieldItem(XmlClass.GetTag(xdIn, "PinFileNameFormat", null));
            bool reformProcName = HasFieldItem(XmlClass.GetTag(xdIn, "ProcFileNameFormat", null));

            string inputFormatName = XmlClass.GetTag(xdIn, "InputFormat", null);
            XmlDocument xInputFormat = XmlClass.GetXmlNode(xmlSettings, "InputFormats/InputFormat",
                "Name", inputFormatName, null);
            


            Dictionary<string, object> dEmboss = new Dictionary<string, object>();
            dEmboss.Add("dir", XmlClass.GetTag(xdIn, "OutDirectory", null).Replace(@"..\", startDirectory));
            dEmboss.Add("outFileName", XmlClass.GetTag(xdIn, "OutFileNameFormat", null));
            dEmboss.Add("productName", product.ProductName);
            dEmboss.Add("cdpOutFile", outCdp);
            dEmboss.Add("originalFile", product.OriginalFileName);
            dEmboss.Add("inputFormat", xInputFormat);
            dEmboss.Add("advanceData","");
            dEmboss.Add("resultString", "");
            string outFileName = (isEmb && !reformEmbName) ? CombineOutFilename(dEmboss) : "";
            string pinOutDirectory = XmlClass.GetTag(xdIn, "OutPinDirectory", null).Replace(@"..\", startDirectory);
            if (String.IsNullOrEmpty(pinOutDirectory))
                pinOutDirectory = XmlClass.GetTag(xdIn, "OutDirectory", null).Replace(@"..\", startDirectory);
            Dictionary<string, object> dPin = new Dictionary<string, object>();
            dPin.Add("dir", pinOutDirectory);
            dPin.Add("outFileName", XmlClass.GetTag(xdIn, "PinFileNameFormat", null));
            dPin.Add("productName", product.ProductName);
            dPin.Add("cdpOutFile", outCdp);
            dPin.Add("originalFile", product.OriginalFileName);
            dPin.Add("inputFormat", xInputFormat);
            dPin.Add("advanceData", "");
            dPin.Add("resultString", "");
            string pinFileName = (isPin && !reformPinName) ? CombineOutFilename(dPin) : "";
            string procOutDirectory = XmlClass.GetTag(xdIn, "OutProcDirectory", null).Replace(@"..\", startDirectory);                                                             
            if (String.IsNullOrEmpty(procOutDirectory))
                procOutDirectory = XmlClass.GetTag(xdIn, "OutDirectory", null).Replace(@"..\", startDirectory);
            Dictionary<string, object> dProc = new Dictionary<string, object>();
            dProc.Add("dir", pinOutDirectory);
            dProc.Add("outFileName", XmlClass.GetTag(xdIn, "ProcFileNameFormat", null));
            dProc.Add("productName", product.ProductName);
            dProc.Add("cdpOutFile", outCdp);
            dProc.Add("originalFile", product.OriginalFileName);
            dProc.Add("inputFormat", xInputFormat);
            dProc.Add("advanceData", "");
            dProc.Add("resultString", "");
            string procFileName = (isProc && !reformProcName) ? CombineOutFilename(dProc) : "";
            #endregion
            #region запуск CDP
            //определяем какой формат выходного файла
            string formatout = XmlClass.GetAttribute(xd, "OutFileFormat", "Format", null);
            if (String.IsNullOrEmpty(formatout))
                formatout = XmlClass.GetAttribute(xmlSettings, "CDP/OutFileFormat", "Format", xnmSettings);
            if (String.IsNullOrEmpty(formatout))
                formatout = "Text";
            File.Delete(errFile);
            File.Copy(product.FileName, fileIn, true);
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = true;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.FileName = XmlClass.GetTag(xmlSettings, "CDP/Console", xnmSettings).Replace(@"..\", cdpDir);
            startInfo.Arguments = String.Format("{0}{1}{0}", (char)0x22, ini);
            mainWindowObject.ProgressBarMaximum = product.Count;
            mainWindowObject.ProgressBarValue = 0;
            System.Windows.Threading.Dispatcher pdDispatcher = this.Dispatcher;
            int cnt = 0;
            if (noCdp)
            {
                UpdateProgressDelegate update = new UpdateProgressDelegate(UpdateProgressBar);
                pdDispatcher.BeginInvoke(update, product.Count, product.Count);
                product.CountWait = 0;
                cnt = product.Count;
            }
            if (!noCdp)
            {
                //удаляем результирующий файл cdp, чтобы он не считался сразу же
                File.Delete(outCdp);
                //определяем по какому файлу будем считать готовые, на случай если галки сняты на подготовку файла эмбосс
                string cntFile = outCdp;
                int cntProc = 0;
                if (!isEmb)
                    cntFile = pinCdp;
                if (!isEmb && !isPin)
                {
                    cntFile = procCdp;
                    if (isProcAfter)
                        cntProc = 2;
                }
                using (Process pr = Process.Start(startInfo))
                {
                    while (!pr.HasExited)
                    {
                        pr.Refresh();
                        Thread.Sleep(1000);
                        cnt = 0;
                        if (!File.Exists(cntFile))
                            continue;
                        using (FileStream fs = new FileStream(cntFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            using (StreamReader sr = new StreamReader(fs))
                            {
                                sr.BaseStream.Seek(0, SeekOrigin.Begin);
                                if (formatout == "Binary")
                                {
                                    string separator = XmlClass.GetAttribute(xd, "OutFileFormat", "Separator", null);
                                    if (String.IsNullOrEmpty(separator))
                                        separator = XmlClass.GetAttribute(xmlSettings, "CDP/OutFileFormat", "Separator", xnmSettings);
                                    foreach (string sss in HugeLib.StreamReaderExtensions.ReadUntil(sr, Utils.AHex2String(separator)))
                                        cnt++;
                                }
                                if (formatout == "Text")
                                {
                                    while (sr.ReadLine() != null)
                                        cnt++;
                                }
                                sr.Close();
                            }
                            fs.Close();
                            UpdateProgressDelegate update = new UpdateProgressDelegate(UpdateProgressBar);
                            pdDispatcher.BeginInvoke(update, cnt, product.Count);
                        }
                        product.CountWait = product.Count - cnt;
                    }
                    pr.Close();
                    if (cntProc > 0)
                    {
                        cnt = cnt - cntProc;
                        UpdateProgressDelegate update = new UpdateProgressDelegate(UpdateProgressBar);
                        pdDispatcher.BeginInvoke(update, cnt, product.Count);
                        product.CountWait = product.Count - cnt;
                    }
                    if (File.Exists(errFile))
                    {
                        string errstr = "";
                        using (StreamReader sr = new StreamReader(errFile, Encoding.GetEncoding(1251)))
                        {
                            sr.BaseStream.Seek(0, SeekOrigin.Begin);
                            errstr = sr.ReadLine();
                            sr.Close();
                        }
                        throw new Exception(errstr);
                    }
                }
            }
            #endregion
            ArrayList adv = new ArrayList();
            #region создаем итоговый файл 
            #region бинарный файл (например, с заголовком scpm или разделителем записей 0d0a0d)
            if (formatout == "Binary")
            {
                int encPage = XmlClass.GetDataXmlInt(xd, "OutFileFormat", "Encoding", null);
                if (encPage == 0)
                    encPage = XmlClass.GetDataXmlInt(xmlSettings, "CDP/OutFileFormat", "Encoding", 1252, xnmSettings);
                using (BinaryReader br = new BinaryReader(File.Open(outCdp, FileMode.Open), Encoding.GetEncoding(encPage)))
                {
                    using (BinaryWriter bw = new BinaryWriter(File.Open(outFileName, FileMode.OpenOrCreate), System.Text.Encoding.GetEncoding(encPage)))
                    {
                        bw.Seek(0, SeekOrigin.End);
                        //чтение через произвольный разделитель
                        //https://stackoverflow.com/questions/6655246/how-to-read-text-file-by-particular-line-separator-character
                        string separator = XmlClass.GetAttribute(xd, "OutFileFormat", "Separator", null);
                        if (String.IsNullOrEmpty(separator))
                            separator = XmlClass.GetAttribute(xmlSettings, "CDP/OutFileFormat", "Separator", xnmSettings);
                        //foreach (string str in HugeLib.StreamReaderExtensions.ReadUntil(sr, Utils.AHex2String(separator)))
                        foreach (byte[] bytes in HugeLib.StreamReaderExtensions.ReadBytesUntil(br, Utils.AHex2Bin(separator)))
                        {
                            XmlDocument post = XmlClass.GetXmlNode(xd, "AdditionalProcessing", 0, null);
                            bw.Write(PostProcessing(bytes, post));
                            bw.Write(Utils.AHex2Bin(separator));
                        }
                        bw.Close();
                    }
                    br.Close();
                }
            }
            #endregion
            #region текстовый формат выходного файла cdp
            if (formatout == "Text")
            {
                if (isEmb && File.Exists(outCdp))
                {
                    int encPage = XmlClass.GetDataXmlInt(xd, "OutFileFormat", "Encoding", null);
                    if (encPage == 0)
                        encPage = XmlClass.GetDataXmlInt(xmlSettings, "CDP/OutFileFormat", "Encoding", 1251, xnmSettings);
                    using (StreamReader sr = new StreamReader(outCdp, Encoding.GetEncoding(encPage)))
                    {
                        sr.BaseStream.Seek(0, SeekOrigin.Begin);
                        while (sr.Peek() >= 0)
                        {
                            adv.Clear();
                            adv.Add(product.FileName + ".adv");
                            string str = sr.ReadLine();
                            XmlDocument post = XmlClass.GetXmlNode(xd, "AdditionalProcessing", 0, null);
                            string newstring = PostProcessing(str, post, "AddOn", adv);
                            if (String.IsNullOrEmpty(newstring))
                                continue;
                            string of = outFileName;
                            if (reformEmbName)
                            {
                                if (adv.Count > 1)
                                    dEmboss["advanceData"] = (string)adv[1];
                                dEmboss["resultString"] = newstring;
                                of = CombineOutFilename(dEmboss);
                                //of = CombineOutFilename(
                                //    XmlClass.GetTag(xdIn, "OutDirectory", null).Replace(@"..\", startDirectory),
                                //    XmlClass.GetTag(xdIn, "OutFileNameFormat", null), product.ProductName, outCdp,
                                //    product.OriginalFileName, (string)adv[1], xInputFormat);
                            }
                            //else
                            //{
                            //    if (adv.Count == 2)
                            //        of = CombineOutFilename(
                            //            XmlClass.GetTag(xdIn, "OutDirectory", null).Replace(@"..\", startDirectory),
                            //            XmlClass.GetTag(xdIn, "OutFileNameFormat", null), product.ProductName, outCdp,
                            //            product.OriginalFileName, (string)adv[1]);
                            //}
                            using (StreamWriter sw = new StreamWriter(of, true, Encoding.GetEncoding(encPage)))
                            {
                                sw.BaseStream.Seek(0, SeekOrigin.End);
                                sw.WriteLine(newstring);
                                sw.Close();
                            }
                        }
                        sr.Close();
                    }
                }
            }
            #endregion
            #region файлы пинов и процессинга (считаем что они в текстовом формате)
            if (isPin && File.Exists(pinCdp))
            {
                int encPage = XmlClass.GetDataXmlInt(xd, "OutPinFileFormat", "Encoding", null);
                if (encPage == 0)
                    encPage = XmlClass.GetDataXmlInt(xmlSettings, "CDP/OutPinFileFormat", "Encoding", 1251, xnmSettings);
                int encPageOut = XmlClass.GetDataXmlInt(xd, "OutPinFileFormat", "EncodingOut", null);
                if (encPageOut == 0)
                    encPageOut = XmlClass.GetDataXmlInt(xmlSettings, "CDP/OutPinFileFormat", "EncodingOut", 1251, xnmSettings);
                using (StreamReader sr = new StreamReader(pinCdp, Encoding.GetEncoding(encPage)))
                {
                    sr.BaseStream.Seek(0, SeekOrigin.Begin);
                    while (sr.Peek() >= 0)
                    {
                        adv.Clear();
                        adv.Add(product.FileName + ".adv");
                        string str = sr.ReadLine();
                        XmlDocument post = XmlClass.GetXmlNode(xd, "AdditionalProcessing", 0, null);
                        string newstring = PostProcessing(str, post, "AddOnPin", adv);
                        if (String.IsNullOrEmpty(newstring))
                            continue;
                        string of = pinFileName;
                        if (reformPinName)
                        {
                            if (adv.Count > 1)
                                dPin["advanceData"] = (string)adv[1];
                            dPin["resultString"] = newstring;
                            of = CombineOutFilename(dPin);
                            //string inputFormatName = XmlClass.GetTag(xdIn, "InputFormat", null);
                            //XmlDocument xInputFormat = XmlClass.GetXmlNode(xmlSettings, "InputFormats/InputFormat",
                            //    "Name", inputFormatName, null);

                            //of = CombineOutFilename(pinOutDirectory, XmlClass.GetTag(xdIn, "PinFileNameFormat", null),
                            //    product.ProductName, pinCdp, product.OriginalFileName, (string)adv[1], xInputFormat);
                        }
                        // пока что AdditionalProcessing для пинов не предусмотрен 
                        using (StreamWriter sw = new StreamWriter(of, true, Encoding.GetEncoding(encPageOut)))
                        {
                            sw.BaseStream.Seek(0, SeekOrigin.End);
                            sw.WriteLine(newstring);
                            sw.Close();
                        }
                    }
                    sr.Close();
                }
            }
            if (isProc && File.Exists(procCdp))
            {
                int encPage = XmlClass.GetDataXmlInt(xd, "OutProcFileFormat", "Encoding", null);
                if (encPage == 0)
                    encPage = XmlClass.GetDataXmlInt(xmlSettings, "CDP/OutProcFileFormat", "Encoding", 1251, xnmSettings);
                int encPageOut = XmlClass.GetDataXmlInt(xd, "OutProcFileFormat", "EncodingOut", null);
                if (encPageOut == 0)
                    encPageOut = XmlClass.GetDataXmlInt(xmlSettings, "CDP/OutProcFileFormat", "EncodingOut", 1251, xnmSettings);
                using (StreamReader sr = new StreamReader(procCdp, Encoding.GetEncoding(encPage)))
                {
                    sr.BaseStream.Seek(0, SeekOrigin.Begin);
                    while (sr.Peek() >= 0)
                    {
                        adv.Clear();
                        adv.Add(product.FileName + ".adv");
                        string str = sr.ReadLine();
                        XmlDocument post = XmlClass.GetXmlNode(xd, "AdditionalProcessing", 0, null);
                        string newstring = PostProcessing(str, post, "AddOnProc", adv);
                        if (String.IsNullOrEmpty(newstring))
                            continue;
                        string of = procFileName;
                        if (reformProcName)
                        {
                            if (adv.Count > 1)
                                dProc["advanceData"] = (string)adv[1];
                            dProc["resultString"] = newstring;
                            if (adv.Count > 1)
                            {
                                if (dProc.ContainsKey("advanceData"))
                                    dProc["advanceData"] = (string)adv[1];
                                else
                                    dProc.Add("advanceData", (string)adv[1]);
                            }
                            of = CombineOutFilename(dProc);

                            //string inputFormatName = XmlClass.GetTag(xdIn, "InputFormat", null);
                            //XmlDocument xInputFormat = XmlClass.GetXmlNode(xmlSettings, "InputFormats/InputFormat",
                            //    "Name", inputFormatName, null);

                            //of = CombineOutFilename(procOutDirectory, XmlClass.GetTag(xdIn, "ProcFileNameFormat", null),
                            //    product.ProductName, procCdp, product.OriginalFileName, (string)adv[1], xInputFormat);
                        }
                        using (StreamWriter sw = new StreamWriter(of, true,
                            Encoding.GetEncoding(encPageOut)))
                        {
                            sw.BaseStream.Seek(0, SeekOrigin.End);
                            sw.WriteLine(newstring);
                            sw.Close();
                        }
                    }
                    sr.Close();
                }
            }
            #endregion
            #endregion
            #region del cdp & pending file
            //pending file
            File.Delete(product.FileName);
            File.Delete(product.FileName + ".adv");
            //cdp out file
            string flag = XmlClass.GetAttribute(xmlSettings, "CDP", "DeleteResult", "true", xnmSettings);
            if (flag == "true")
            {
                try {
                    File.Delete(outCdp);
                }
                catch { }
                try {
                    File.Delete(pinCdp);
                }
                catch { }
                try {
                    File.Delete(procCdp);
                }
                catch { }
            }
            #endregion
            return cnt * appNum;
        }

        public bool HasFieldItem(string outFileName)
        {
            XmlDocument xdOut = XmlClass.GetXmlNode(xmlSettings, "OutFileNames/OutFileName", "Name", outFileName,
                xnmSettings);
            int cnt = XmlClass.GetXmlNodeCount(xdOut, "Directory/Item", null);
            for (int i = 0; i < cnt; i++)
            {
                XmlDocument x = XmlClass.GetXmlNode(xdOut, "Directory/Item", i, null);
                string s = XmlClass.GetAttribute(x, "", "Type", null);
                if (s.Equals("InputField") || s.Equals("OutputField"))
                    return true;

            }

            cnt = XmlClass.GetXmlNodeCount(xdOut, "Item", null);
            for (int i = 0; i < cnt; i++)
            {
                XmlDocument x = XmlClass.GetXmlNode(xdOut, "Item", i, null);
                string s = XmlClass.GetAttribute(x, "", "Type", null);
                if (s.Equals("InputField") || s.Equals("OutputField"))
                    return true;
            }
            return false;
        }

        public string CombineOutFilename(Dictionary<string, object> dictionary)
        {
            string dir = (string) dictionary["dir"];
            string file = "";
            XmlDocument xdOut = XmlClass.GetXmlNode(xmlSettings, "OutFileNames/OutFileName", "Name", (string)dictionary["outFileName"], xnmSettings);
            if (xdOut != null)
            {
                int cnt = XmlClass.GetXmlNodeCount(xdOut, "Directory/Item", null);
                for (int i = 0; i < cnt; i++)
                {
                    string sub = "";
                    XmlDocument x = XmlClass.GetXmlNode(xdOut, "Directory/Item", i, null);
                    string s = XmlClass.GetAttribute(x, "", "Type", null);
                    if (s.Equals("Const"))
                        sub = XmlClass.GetAttribute(x, "", "Value", null);
                    if (s.Equals("ProductName"))
                        sub = (string)dictionary["productName"];
                    if (s.Equals("InputField"))
                    {
                        RecordClass rc = new RecordClass();
                        rc.OriginalString = (string)dictionary["advanceData"];
                        rc.ParseFields((XmlDocument)dictionary["inputFormat"]);
                        sub = rc.GetField(XmlClass.GetAttribute(x, "", "Value", null));
                    }
                    if (s.Equals("OutputField"))
                    {
                        string outFormatName = XmlClass.GetAttribute(xdOut, "", "Format", xnmSettings);
                        RecordClass rc = new RecordClass();
                        rc.OriginalString = (string) dictionary["resultString"];
                        XmlDocument xdOutFormat = XmlClass.GetXmlNode(xmlSettings, "OutputFormats/OutputFormat", "Name", outFormatName, xnmSettings);
                        rc.ParseFields(xdOutFormat);
                        sub = rc.GetField(XmlClass.GetAttribute(x, "", "Value", null));
                    }
                    s = XmlClass.GetAttribute(x, "", "Function", null);
                    if (s.Equals("Substring"))
                    {
                        string sStart = XmlClass.GetAttribute(x, "", "Start", "0", null);
                        string sLength = XmlClass.GetAttribute(x, "", "Length", "0", null); //0 до конца
                        int iStart = 0, iLength = 0;
                        try
                        {
                            iStart = Convert.ToInt32(sStart);
                            iLength = Convert.ToInt32(sLength);
                        }
                        catch { }
                        try
                        {
                            if (iLength == 0)
                                sub = sub.Substring(iStart);
                            else
                                sub = sub.Substring(iStart, iLength);
                        }
                        catch { }
                    }
                    if (s.Equals("ArrayItem"))
                    {
                        string sDelim = XmlClass.GetAttribute(x, "", "Separator", "", null);
                        string sIndex = XmlClass.GetAttribute(x, "", "Index", "", null);
                        try
                        {
                            sub = sub.Split(sDelim[0])[Convert.ToInt32(sIndex)];
                        }
                        catch
                        {
                        }
                    }
                    dir = System.IO.Path.Combine(dir, sub);
                }
            }
            Directory.CreateDirectory(dir);
            if (xdOut != null)
            {
                FileInfo fiOriginal = new FileInfo((string)dictionary["originalFile"]);
                int cnt = XmlClass.GetXmlNodeCount(xdOut, "Item", null);
                for (int i = 0; i < cnt; i++)
                {
                    XmlDocument x = XmlClass.GetXmlNode(xdOut, "Item", i, null);
                    string s = XmlClass.GetAttribute(x, "", "Type", null);
                    string val = "";
                    if (s.Equals("Const"))
                        val = XmlClass.GetAttribute(x, "", "Value", null);
                    if (s.Equals("DateTime"))
                        val = String.Format(XmlClass.GetAttribute(x, "", "Format", null), DateTime.Now);
                    if (s.Equals("ProductName"))
                        val = (string)dictionary["productName"];
                    if (s.Equals("CDPOutName"))
                    {
                        FileInfo fiout = new FileInfo((string)dictionary["cdpOutFile"]);
                        val = fiout.Name;
                        if (fiout.Extension.Length > 0)
                            val = val.Replace(fiout.Extension, "");
                    }
                    if (s.Equals("OriginalName"))
                    {
                        val = fiOriginal.Name;
                        if (fiOriginal.Extension.Length > 0)
                            val = val.Replace(fiOriginal.Extension, ""); //удаляем расширение. Его можно добавить отдельно
                    }
                    if (s.Equals("OriginalExtension"))
                        val = fiOriginal.Extension;
                    if (s.Equals("AdvanceValue"))
                        val = (string)dictionary["advanceData"];

                    if (s.Equals("InputField"))
                    {
                        RecordClass rc = new RecordClass();
                        rc.OriginalString = (string)dictionary["advanceData"];
                        rc.ParseFields((XmlDocument)dictionary["inputFormat"]);
                        val = rc.GetField(XmlClass.GetAttribute(x, "", "Value", null));
                    }
                    if (s.Equals("OutputField"))
                    {
                        string outFormatName = XmlClass.GetAttribute(xdOut, "", "Format", xnmSettings);
                        RecordClass rc = new RecordClass();
                        rc.OriginalString = (string)dictionary["resultString"];
                        XmlDocument xdOutFormat = XmlClass.GetXmlNode(xmlSettings, "OutputFormats/OutputFormat", "Name", outFormatName, xnmSettings);
                        rc.ParseFields(xdOutFormat);
                        val = rc.GetField(XmlClass.GetAttribute(x, "", "Value", null));
                    }
                    s = XmlClass.GetAttribute(x, "", "Function", null);
                    if (s.Equals("Substring"))
                    {
                        string sStart = XmlClass.GetAttribute(x, "", "Start", "0", null);
                        string sLength = XmlClass.GetAttribute(x, "", "Length", "0", null); //0 до конца
                        int iStart = 0, iLength = 0;
                        try
                        {
                            iStart = Convert.ToInt32(sStart);
                            iLength = Convert.ToInt32(sLength);
                        }
                        catch { }
                        try
                        {
                            if (iLength == 0)
                                val = val.Substring(iStart);
                            else
                                val = val.Substring(iStart, iLength);
                        }
                        catch { }
                    }
                    if (s.Equals("ArrayItem"))
                    {
                        string sDelim = XmlClass.GetAttribute(x, "", "Separator", "", null);
                        string sIndex = XmlClass.GetAttribute(x, "", "Index", "", null);
                        try
                        {
                            val = val.Split(sDelim[0])[Convert.ToInt32(sIndex)];
                        }
                        catch
                        {
                        }
                    }
                    file += val;
                }
            }
            else
            {
                FileInfo fi1 = new FileInfo((string)dictionary["cdpOutFile"]);
                file = fi1.Name;
            }
            return System.IO.Path.Combine(dir, file);
        }

        public string CombineOutFilename_old(string dir, string outFileName, string productName, string cdpOutFile,
            string originalFile, string advanceData)
        {
            return CombineOutFilename_old(dir, outFileName, productName, cdpOutFile, originalFile, advanceData, null);
        }
        public string CombineOutFilename_old(string dir, string outFileName, string productName, string cdpOutFile, string originalFile, string advanceData, XmlDocument inputFormat)
        { 
            XmlDocument xdOut = XmlClass.GetXmlNode(xmlSettings, "OutFileNames/OutFileName", "Name", outFileName, xnmSettings);
            if (xdOut != null)
            {
                int cnt = XmlClass.GetXmlNodeCount(xdOut, "Directory/Item", null);
                for (int i = 0; i < cnt; i++)
                {
                    string sub = "";
                    XmlDocument x = XmlClass.GetXmlNode(xdOut, "Directory/Item", i, null);
                    string s = XmlClass.GetAttribute(x, "", "Type", null);
                    if (s.Equals("Const"))
                        sub = XmlClass.GetAttribute(x, "", "Value", null);
                    if (s.Equals("ProductName"))
                        sub = productName;
                    if (s.Equals("InputField"))
                    {
                        RecordClass rc = new RecordClass();
                        rc.OriginalString = advanceData;
                        rc.ParseFields(inputFormat);
                        sub = rc.GetField(XmlClass.GetAttribute(x, "", "Value", null));
                    }
                    if (s.Equals("OutputField"))
                    {
                        RecordClass rc = new RecordClass();
                        rc.OriginalString = advanceData;
                    }
                    s = XmlClass.GetAttribute(x, "", "Function", null);
                    if (s.Equals("Substring"))
                    {
                        string sStart = XmlClass.GetAttribute(x, "", "Start", "0", null);
                        string sLength = XmlClass.GetAttribute(x, "", "Length", "0", null); //0 до конца
                        int iStart = 0, iLength = 0;
                        try
                        {
                            iStart = Convert.ToInt32(sStart);
                            iLength = Convert.ToInt32(sLength);
                        }
                        catch { }
                        try
                        {
                            if (iLength == 0)
                                sub = sub.Substring(iStart);
                            else
                                sub = sub.Substring(iStart, iLength);
                        }
                        catch { }
                    }
                    if (s.Equals("ArrayItem"))
                    {
                        string sDelim = XmlClass.GetAttribute(x, "", "Separator", "", null);
                        string sIndex = XmlClass.GetAttribute(x, "", "Index", "", null);
                        try
                        {
                            sub = sub.Split(sDelim[0])[Convert.ToInt32(sIndex)];
                        }
                        catch
                        {
                        }
                    }
                    dir = System.IO.Path.Combine(dir, sub);
                }
            }
            Directory.CreateDirectory(dir);
            outFileName = "";
            if (xdOut != null)
            {
                FileInfo fiOriginal = new FileInfo(originalFile);
                int cnt = XmlClass.GetXmlNodeCount(xdOut, "Item", null);
                for (int i = 0; i < cnt; i++)
                {
                    XmlDocument x = XmlClass.GetXmlNode(xdOut, "Item", i, null);
                    string s = XmlClass.GetAttribute(x, "", "Type", null);
                    string val = "";
                    if (s.Equals("Const"))
                        val = XmlClass.GetAttribute(x, "", "Value", null);
                    if (s.Equals("DateTime"))
                        val = String.Format(XmlClass.GetAttribute(x, "", "Format", null), DateTime.Now);
                    if (s.Equals("ProductName"))
                        val = productName;
                    if (s.Equals("CDPOutName"))
                    {
                        FileInfo fiout = new FileInfo(cdpOutFile);
                        val = fiout.Name;
                        if (fiout.Extension.Length > 0)
                            val = val.Replace(fiout.Extension, "");
                    }
                    if (s.Equals("OriginalName"))
                    {
                        val = fiOriginal.Name;
                        if (fiOriginal.Extension.Length > 0)
                            val = val.Replace(fiOriginal.Extension, ""); //удаляем расширение. Его можно добавить отдельно
                    }
                    if (s.Equals("OriginalExtension"))
                        val = fiOriginal.Extension;
                    if (s.Equals("AdvanceValue"))
                        val = advanceData;

                    if (s.Equals("InputField"))
                    {
                        RecordClass rc = new RecordClass();
                        rc.OriginalString = advanceData;
                        rc.ParseFields(inputFormat);
                        val = rc.GetField(XmlClass.GetAttribute(x, "", "Value", null));
                    }
                    s = XmlClass.GetAttribute(x, "", "Function", null);
                    if (s.Equals("Substring"))
                    {
                        string sStart = XmlClass.GetAttribute(x, "", "Start", "0", null);
                        string sLength = XmlClass.GetAttribute(x, "", "Length", "0", null); //0 до конца
                        int iStart = 0, iLength = 0;
                        try
                        {
                            iStart = Convert.ToInt32(sStart);
                            iLength = Convert.ToInt32(sLength);
                        }
                        catch { }
                        try
                        {
                            if (iLength == 0)
                                val = val.Substring(iStart);
                            else
                                val = val.Substring(iStart, iLength);
                        }
                        catch { }
                    }
                    if (s.Equals("ArrayItem"))
                    {
                        string sDelim = XmlClass.GetAttribute(x, "", "Separator", "", null);
                        string sIndex = XmlClass.GetAttribute(x, "", "Index", "", null);
                        try
                        {
                            val = val.Split(sDelim[0])[Convert.ToInt32(sIndex)];
                        }
                        catch
                        {
                        }
                    }
                    outFileName += val;
                }
            }
            else
            {
                FileInfo fi1 = new FileInfo(cdpOutFile);
                outFileName = fi1.Name;
            }
            return System.IO.Path.Combine(dir, outFileName);
        }
        public string PreProcessing(string line, XmlDocument prior)
        {
            if (prior == null)
                return line;
            int cnt = XmlClass.GetXmlNodeCount(prior, "AddPrior", null);
            for (int i = 0; i < cnt; i++)
            {
                XmlDocument add = XmlClass.GetXmlNode(prior, "AddPrior", i, null);
                string tp = XmlClass.GetAttribute(add, "", "Type", null);
                if (tp == "Insert")
                {
                    string search = XmlClass.GetAttribute(add, "", "SearchString", null);
                    string snum = XmlClass.GetAttribute(add, "", "Count", null);
                    int inum = 0, len = 0;
                    string val = XmlClass.GetAttribute(add, "", "Value", null);
                    string sect = XmlClass.GetAttribute(add, "", "Section", null);
                    string align = XmlClass.GetAttribute(add, "", "Align", "Left", null).ToLower();
                    string length = XmlClass.GetAttribute(add, "", "Lenght", "0", null).ToLower();
                    try
                    {
                        inum = Convert.ToInt32(snum);
                    }
                    catch
                    {
                        inum = 0;
                    }
                    try
                    {
                        len = Convert.ToInt32(length);
                    }
                    catch
                    {
                        len = 0;
                    }
                    string[] strs = line.Split(new string[] { search }, StringSplitOptions.None);
                    if (inum < strs.Length)
                    {
                        if (len > 0)
                            val = val.PadRight(len);
                        if (String.IsNullOrEmpty(sect))
                        {
                            if (align == "right")
                                strs[inum] = $"{strs[inum]}{val}";
                            else
                                strs[inum] = $"{val}{strs[inum]}";
                        }
                        else
                        {
                            int index = strs[inum].IndexOf(sect);
                            if (index >= 0)
                                strs[inum] = strs[inum].Insert(index + sect.Length, val);
                        }
                    }
                    line = String.Join(search, strs);
                }
            }
            return line;
        }
        public string PostProcessing(string line, XmlDocument post, string tagNames, ArrayList advValues)
        {
            if (post == null)
                return line;
            int cnt = XmlClass.GetXmlNodeCount(post, tagNames, null);
            for (int i = 0; i < cnt; i++)
            {
                XmlDocument add = XmlClass.GetXmlNode(post, tagNames, i, null);
                string tp = XmlClass.GetAttribute(add, "", "Type", null);
                if (tp == "Insert")
                {
                    string search = XmlClass.GetAttribute(add, "", "SearchString", null);
                    string snum = XmlClass.GetAttribute(add, "", "Count", null);
                    int inum = 0;
                    string val = XmlClass.GetAttribute(add, "", "Value", null);
                    string sect = XmlClass.GetAttribute(add, "", "Section", null);
                    try
                    {
                        inum = Convert.ToInt32(snum);
                    }
                    catch
                    {
                        inum = 0;
                    }
                    string[] strs = line.Split(new string[] {search}, StringSplitOptions.None);
                    if (inum < strs.Length)
                    {
                        if (String.IsNullOrEmpty(sect))
                            strs[inum] = $"{val}{strs[inum]}";
                        else
                        {
                            int index = strs[inum].IndexOf(sect);
                            if (index >= 0)
                                strs[inum] = strs[inum].Insert(index + sect.Length, val);
                        }
                    }
                    line = String.Join(search, strs);
                }
                if (tp == "Delete")
                {
                    string search = XmlClass.GetAttribute(add, "", "SearchString", null);
                    string snum = XmlClass.GetAttribute(add, "", "Count", null);
                    string del = XmlClass.GetAttribute(add, "", "DeleteDelimitor", "no", null);
                    int inum = 0;
                    try
                    {
                        inum = Convert.ToInt32(snum);
                    }
                    catch
                    {
                        inum = 0;
                    }
                    List<string> strs = line.Split(new string[] { search }, StringSplitOptions.None).ToList();
                    if (inum < strs.Count)
                    {
                        strs[inum] = "";
                        if (del == "yes")
                            strs.RemoveAt(inum);
                    }
                    //if (inum < strs.Length)
                    //{
                    //    if (String.IsNullOrEmpty(sect))
                    //        strs[inum] = $"{val}{strs[inum]}";
                    //    else
                    //    {
                    //        int index = strs[inum].IndexOf(sect);
                    //        if (index >= 0)
                    //            strs[inum] = strs[inum].Insert(index + sect.Length, val);
                    //    }
                    //}
                    line = String.Join(search, strs);
                }
                if (tp == "RemoveRecord")
                {
                    string search = XmlClass.GetAttribute(add, "", "Delimitor", null);
                    string snum = XmlClass.GetAttribute(add, "", "Count", null);
                    string pos = XmlClass.GetAttribute(add, "", "StartPos", null);
                    string length = XmlClass.GetAttribute(add, "", "Length", null);
                    string val = XmlClass.GetAttribute(add, "", "Value", null);
                    int inum = 0;
                    int ipos = -1, ilength = -1;
                    try
                    {
                        inum = Convert.ToInt32(snum);
                    }
                    catch
                    {
                        inum = 0;
                    }
                    try
                    {
                        ipos = Convert.ToInt32(pos);
                    }
                    catch (Exception e)
                    {
                        ipos = -1;
                    }

                    try
                    {
                        ilength = Convert.ToInt32(length);
                    }
                    catch
                    {
                        ilength = -1;
                    }

                    string str = "";
                    if (search.Length > 0 && inum >= 0)
                    {
                        string[] strs = line.Split(new string[] { search }, StringSplitOptions.None);
                        if (inum < strs.Length)
                        {
                            try
                            {
                                str = strs[inum];
                            }
                            catch
                            {
                            }
                        }
                    }

                    if (ipos >= 0 && ilength > 0)
                    {
                        try
                        {
                            str = line.Substring(ipos, ilength);
                        }
                        catch
                        {
                        }
                    }
                    if (str == val)
                        return "";
                }
                if (tp == "ReformName")
                {
                    string search = XmlClass.GetAttribute(add, "", "Delimitor", null);
                    string snum = XmlClass.GetAttribute(add, "", "Count", null);
                    string pos = XmlClass.GetAttribute(add, "", "StartPos", null);
                    string length = XmlClass.GetAttribute(add, "", "Length", null);
                    string spaces = XmlClass.GetAttribute(add, "", "RemoveSpaces", null);
                    int inum = 0;
                    int ipos = -1, ilength = -1;
                    try
                    {
                        inum = Convert.ToInt32(snum);
                    }
                    catch
                    {
                        inum = 0;
                    }
                    try
                    {
                        ipos = Convert.ToInt32(pos);
                    }
                    catch (Exception e)
                    {
                        ipos = -1;
                    }

                    try
                    {
                        ilength = Convert.ToInt32(length);
                    }
                    catch
                    {
                        ilength = -1;
                    }

                    string str = "";
                    if (search.Length > 0 && inum >= 0)
                    {
                        string[] strs = line.Split(new string[] {search}, StringSplitOptions.None);
                        if (inum < strs.Length)
                        {
                            try
                            {
                                str = strs[inum];
                            }
                            catch
                            {
                            }
                        }
                    }

                    if (ipos >= 0 && ilength > 0)
                    {
                        try
                        {
                            str = line.Substring(ipos, ilength);
                        }
                        catch
                        {
                        }
                    }
                    if (spaces == "yes")
                        str = str.Replace(" ", "");
                    if (File.Exists((string) advValues[0]))
                    {
                        using (StreamReader sr = new StreamReader((string)advValues[0], Encoding.GetEncoding(1251)))
                        {
                            while (sr.Peek() >= 0)
                            {
                                string s = sr.ReadLine();
                                if (s.Split((char)0x09)[0] == str)
                                {
                                    advValues.Add(s.Split((char)0x09)[1]);
                                    break;
                                }
                            }

                            sr.Close();
                        }
                    }
                }
                if (tp == "RegenName")
                {

                    string search = XmlClass.GetAttribute(add, "", "SearchString", null);
                    string snum = XmlClass.GetAttribute(add, "", "Count", null);
                    int inum = 0;
                    int ind = 1;
                    string spaces = XmlClass.GetAttribute(add, "", "RemoveSpaces", null);
                    try
                    {
                        inum = Convert.ToInt32(snum);
                    }
                    catch
                    {
                        inum = 0;
                    }

                    try
                    {
                        ind = Convert.ToInt32(XmlClass.GetAttribute(add, "", "Index", null));
                    }
                    catch
                    {
                        ind = 1;
                    }
                    string[] strs = line.Split(new string[] { search }, StringSplitOptions.None);
                    if (inum < strs.Length)
                    {
                        try
                        {
                            string str = strs[inum];
                            if (spaces == "yes")
                                str = str.Replace(" ", "");
                            if (File.Exists((string) advValues[0]))
                            {
                                using (StreamReader sr = new StreamReader((string) advValues[0]))
                                {
                                    while (sr.Peek() >= 0)
                                    {
                                        string s = sr.ReadLine();
                                        if (s.Split((char) 0x09)[0] == str)
                                        {
                                            advValues.Add(s.Split((char) 0x09)[ind]);
                                            break;
                                        }
                                    }

                                    sr.Close();
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            return line;
        }
        public byte[] PostProcessing(byte[] line, XmlDocument post)
        {
            if (post == null)
                return line;
            List<byte> list = new List<byte>();
            foreach (byte b in line)
                list.Add(b);
            int cnt = XmlClass.GetXmlNodeCount(post, "AddOn", null);
            for (int i = 0; i < cnt; i++)
            {
                XmlDocument add = XmlClass.GetXmlNode(post, "", i, null);
                string tp = XmlClass.GetAttribute(add, "", "Type", null);
                if (tp == "Insert")
                {
                    string search = XmlClass.GetAttribute(add, "", "SearchString", null);
                    string snum = XmlClass.GetAttribute(add, "", "Count", null);
                    int inum = 0;
                    string val = XmlClass.GetAttribute(add, "", "Value", null);
                    string sect = XmlClass.GetAttribute(add, "", "Section", null);
                    try
                    {
                        inum = Convert.ToInt32(snum);
                    }
                    catch
                    {
                        inum = 0;
                    }
                    int j = 0, index = 0;
                    byte[] bytes = HugeLib.Utils.String2Bin(search);
                    StreamReaderExtensions.CircularBuffer<byte> part = new StreamReaderExtensions.CircularBuffer<byte>(bytes.Length);
                    while (index < line.Length)
                    {
                        part.Enqueue(line[index]);
                        if (StreamReaderExtensions.ArraysEqual<byte>(part.ToArray(), bytes))
                            j++;
                        index++;
                        if (j == inum)
                            break;
                    }
                    if (j == inum)
                    {
                        if (String.IsNullOrEmpty(sect))
                            list.InsertRange(index, Utils.String2Bin(val));
                        else
                        {
                            //ищем еще допсекцию
                            byte[] sectBytes = Utils.String2Bin(sect);
                            StreamReaderExtensions.CircularBuffer<byte> sectBuffer = new StreamReaderExtensions.CircularBuffer<byte>(sectBytes.Length);
                            while (index < line.Length)
                            {
                                part.Enqueue(line[index]);
                                sectBuffer.Enqueue(line[index]);
                                if (StreamReaderExtensions.ArraysEqual<byte>(sectBuffer.ToArray(), sectBytes))
                                {
                                    list.InsertRange(index+1, Utils.String2Bin(val));
                                    break;
                                }
                                // нашли следующий разделитель, выход
                                if (StreamReaderExtensions.ArraysEqual<byte>(part.ToArray(), bytes))
                                    break;
                                index++;
                            }
                        }
                    }
                }
            }
            return list.ToArray();
        }

        private void bDeleteIn_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult mbr = MessageBox.Show("Delete files?", "", MessageBoxButton.YesNo);
            if (mbr == MessageBoxResult.Yes)
            {
                int cnt = XmlClass.GetXmlNodeCount(xmlSettings, "Inputs/Input", xnmSettings);
                for (int i = 0; i < cnt; i++)
                {
                    XmlDocument xd = XmlClass.GetXmlNode(xmlSettings, "Inputs/Input", i, xnmSettings);
                    string folder = XmlClass.GetDataXml(xd, "Directory", null).Replace("..\\", startDirectory);
                    string mask = XmlClass.GetDataXml(xd, "Mask", null);
                    DirectoryInfo d = new DirectoryInfo(folder);
                    if (!d.Exists)
                        continue;
                    foreach (FileInfo fi in d.GetFiles(mask))
                        fi.Delete();
                }
                DirectoryInfo di = new DirectoryInfo(System.IO.Path.Combine(startDirectory, "Pending"));
                foreach (FileInfo fi in di.GetFiles())
                    fi.Delete();
            }
            LoadData();
        }
        public delegate void UpdateProgressDelegate(int value, int maximum);
        public void UpdateProgressBar(int value, int maximum)
        {
            mainWindowObject.ProgressBarMaximum = maximum;
            mainWindowObject.ProgressBarValue = value;
        }
        public void SetLanguage(string lang)
        {
            ResourceDictionary dict = Application.Current.Resources;
            string resname = "Interface.ru.xaml";
            if (lang.ToLower() == "english")
                resname = "Interface.en.xaml";
            if (lang.ToLower() == "russian")
                resname = "Interface.ru.xaml";
            try
            {
                dict.BeginInit();
                int i = 0;
                for (i = 0; i < dict.MergedDictionaries.Count; i++)
                {
                    try
                    {
                        if (((System.Windows.ResourceDictionary) dict.MergedDictionaries[i]).Source.LocalPath.EndsWith(
                            resname))
                            break;
                    }
                    catch { }
                }

                if (i < dict.MergedDictionaries.Count)
                {
                    ResourceDictionary res = dict.MergedDictionaries[i];
                    dict.MergedDictionaries.Remove(dict.MergedDictionaries[i]);
                    dict.MergedDictionaries.Add(res);
                }
            }
            finally
            {
                dict.EndInit();
            }
        }
    }
}
