namespace LicenseHandling
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Windows;

    public partial class UserMessage
    {
        private static readonly string ErrorMessageText = "Error Message";
        private static readonly string NoLicenseText = "No License Found";
        private static readonly string ExpirationTimeWarningText = "License will expire in a few days.";
        private static readonly string UsagePeriodWarningText = "A deactivated Usage Period will now be activated.";
        private static readonly string UnitCounterWarningText = "Credits of use almost used up.";
        private static readonly string CertifiedTimeWarningText = "The Certified Time will elapse in the near future.";
        private static readonly string CertifiedTimeElapsedText = "The Certified Time is elapsed.";
        private static readonly string VersionTooOldText = "This software requires a driver with a higher version.";

        private static readonly string WindowTitle = "WIBU-SYSTEMS Protected Application";

        private readonly List<Pair<int, List<Pair<string, string>>>> CodeMeterErrorMessages;
        private readonly List<Pair<int, List<Pair<string, string>>>> WibuKeyErrorMessages;

        internal const string PresetAccessDataName = "PresetAccessData";
        internal const string EnableSetAccessDataName = "EnableSetAccessData";
        internal const string EnableQueryAccessDataName = "EnableQueryAccessData";
        internal const string FlagsName = "Flags";
        internal const string ProductItemRefName = "ProductItemRef";
        internal const string BoxMaskName = "BoxMask";
        internal const string SerialNumberName = "SerialNumber";
        internal const string UserDefinedIdName = "UserDefinedId";
        internal const string UserDefinedTextName = "UserDefinedText";
        internal const string ServerName = "Server";

        internal Dictionary<string, object> axAccValues;

        // [Service]    
        private bool Logging;

        private string LogPath;
        private bool AppendPidToLog;

        // [Main]    
        private bool BuyHint;

        private string UnitCounterWarningLabelText;
        private string ExpirationTimeWarningLabelText;

        //[AccessData]
        //This members are used to hold the AccessData Information during Application Runtime
        private bool PresetAccessData; //true enables reading Data from .ini file during startup
        private bool EnableSetAccessData; //true enables delivery of the stored values via the Dictionary object to the application
        private bool EnableQueryAccessData; //true enables storing of the applications AccessData
        private uint Flags; //Flags are used to set which AccessData is delivered: 1 ProductItemRef,2 BoxMask and SerialNumber,4 UserDefinedId,8 UserDefinedText,16 Server
        private ushort ProductItemRef;
        private ushort BoxMask;
        private uint SerialNumber;
        private uint UserDefinedId;
        private string UserDefinedText;
        private string Server;

        private StreamWriter LoggingStreamWriter;
        private IniFile iniFile;

        private static readonly MessageManagement mm = new MessageManagement();

        /// <summary>
        /// The default constructor calls the Init() method which reads the INI-file.
        /// </summary>
        public UserMessage()
        {
            CodeMeterErrorMessages = new List<Pair<int, List<Pair<string, string>>>>();
            WibuKeyErrorMessages = new List<Pair<int, List<Pair<string, string>>>>();

            // [Service]      
            Logging = false;
            LogPath = "";
            AppendPidToLog = false;

            // [Main]      
            ExpirationTimeWarningLabelText = "Days:";
            UnitCounterWarningLabelText = "Units:";
            axAccValues = new Dictionary<string, object>();
            BuyHint = false;
            LoggingStreamWriter = null;

            PresetAccessData = false;
            EnableSetAccessData = false;
            EnableQueryAccessData = false;
            Flags = 0;
            ProductItemRef = 0;
            BoxMask = 0;
            SerialNumber = 0;
            UserDefinedId = 0;
            UserDefinedText = "";
            Server = "127.0.0.1";

            Init();
        } // UserMessage()


        /// <summary>
        /// This method reads the INI-file and stores all read values.
        /// The name of the INI file is culture dependent. Example for the search order:
        /// 1. UserMessageDE-de.ini
        /// 2. UserMessageDE.ini
        /// 3. UserMessage.ini
        /// </summary>
        private void Init()
        {
            var fileNames = GetCultureSpecificFileNames();
            var basePathNames = GetBasePathNames();
            var Filename = GetFileName(basePathNames, fileNames);
            if (Filename == string.Empty)
            {
                // set empty ini-file
                iniFile = new IniFile("");
                return;
            }

            // open the file and read the values into RAM
            iniFile = new IniFile(Filename);
            if (iniFile.Read())
            {
                List<Pair<string, List<Pair<string, string>>>> AllCategories = iniFile.GetAll();
                try
                {
                    // search all specified error texts for the different copy protection systems
                    foreach (Pair<string, List<Pair<string, string>>> Category in AllCategories)
                    {
                        int errorCode;
                        if (Category.First.Length > 2)
                        {
                            try
                            {
                                switch (Category.First.Substring(0, 2))
                                {
                                    case "CM":
                                        if (!int.TryParse(Category.First.Substring(2), out errorCode))
                                        {
                                            errorCode = GetErrorCodeByName(Category.First);
                                        }

                                        CodeMeterErrorMessages.Add(new Pair<int, List<Pair<string, string>>>(errorCode, Category.Second));
                                        break;
                                    case "WK":
                                        if (!int.TryParse(Category.First.Substring(2), out errorCode))
                                        {
                                            errorCode = GetErrorCodeByName(Category.First);
                                        }

                                        WibuKeyErrorMessages.Add(new Pair<int, List<Pair<string, string>>>(errorCode, Category.Second));
                                        break;
                                } // switch
                            }
                            catch
                            {
                                continue;
                            } // try/catch
                        } // if
                    } // foreach
                    CodeMeterErrorMessages.Sort(SortErrorCodes);

                    // search all other (string-)values
                    string value = iniFile.Find("Service", "Logging");
                    if (null != value)
                    {
                        Logging = value.ToLower() == "on";
                    } // if
                    value = iniFile.Find("Service", "LogPath");
                    if (null != value)
                    {
                        LogPath = value.Replace("\"", "");
                    } // if
                    value = iniFile.Find("Service", "AppendPidToLog");
                    if (null != value)
                    {
                        AppendPidToLog = value.ToLower() == "yes";
                    } // if

                    value = iniFile.Find("Main", "BuyHint");
                    if (null != value)
                    {
                        BuyHint = value.ToLower() == "on";
                    } // if
                    value = iniFile.Find("Main", "UnitCounterText");
                    if (null != value)
                    {
                        UnitCounterWarningLabelText = value;
                    } // if
                    value = iniFile.Find("Main", "ExpirationTimeText");
                    if (null != value)
                    {
                        ExpirationTimeWarningLabelText = value;
                    } // if

                    // [AccessData]
                    value = iniFile.Find("AccessData", PresetAccessDataName);
                    if (null != value)
                    {
                        PresetAccessData = value.ToLower() == "true";
                    } // if
                    value = iniFile.Find("AccessData", EnableSetAccessDataName);
                    if (null != value)
                    {
                        EnableSetAccessData = value.ToLower() == "true";
                    } // if
                    value = iniFile.Find("AccessData", EnableQueryAccessDataName);
                    if (null != value)
                    {
                        EnableQueryAccessData = value.ToLower() == "true";
                    } // if
                    value = iniFile.Find("AccessData", FlagsName);
                    if (null != value)
                    {
                        //Flags = System.Convert.ToUInt32(value);
                        if (!uint.TryParse(value, out Flags))
                        {
                            //throw new OverflowException(string.Format("UserMessage.ini Section [AccessData] {0} value {1} causes UInt32 overflow",FlagsName, value));
                        } //if
                    } // if
                    if (PresetAccessData)
                    {
                        // Preset AccessData values on startup if PresetAccessData is set to true
                        value = iniFile.Find("AccessData", ProductItemRefName);
                        if (null != value)
                        {
                            //ProductItemRef = System.Convert.TouUInt16(value);
                            if (!ushort.TryParse(value, out ProductItemRef))
                            {
                                //throw new FormatException(string.Format("UserMessage.ini Section [AccessData] {0} value {1} cant be converted to UInt16", ProductItemRefName, value));
                            } //if
                        } // if

                        value = iniFile.Find("AccessData", BoxMaskName);
                        if (null != value)
                        {
                            //BoxMask = System.Convert.ToUInt16(value);
                            if (!ushort.TryParse(value, out BoxMask))
                            {
                                //throw new OverflowException(string.Format("UserMessage.ini Section [AccessData] {0} value {1} causes UInt16 overflow", BoxMaskName, value));
                            } //if
                        } // if

                        value = iniFile.Find("AccessData", SerialNumberName);
                        if (null != value)
                        {
                            //SerialNumber = System.Convert.ToUInt32(value);
                            if (!uint.TryParse(value, out SerialNumber))
                            {
                                //throw new OverflowException(string.Format("UserMessage.ini Section [AccessData] {0} value {1} causes UInt32 overflow", SerialNumberName, value));
                            } //if
                        } // if
                        value = iniFile.Find("AccessData", UserDefinedIdName);
                        if (null != value)
                        {
                            //UserDefinedId = System.Convert.ToUInt32(value);
                            if (!uint.TryParse(value, out UserDefinedId))
                            {
                                //throw new OverflowException(string.Format("UserMessage.ini Section [AccessData] {0} value {1} causes UInt32 overflow", UserDefinedIdName, value));
                            } //if
                        } // if

                        value = iniFile.Find("AccessData", UserDefinedTextName);
                        if (null != value)
                        {
                            UserDefinedText = value;
                        } // if

                        value = iniFile.Find("AccessData", ServerName);
                        if (null != value)
                        {
                            Server = value;
                        } // if        
                    } //if

                    // start logging
                    if (Logging)
                    {
                        var loggingFileName = GetLoggingFileName(LogPath, AppendPidToLog);

                        // open the file (append or create depending on whether the file already exists)
                        FileInfo fi = new FileInfo(loggingFileName);
                        if (fi.Exists)
                        {
                            LoggingStreamWriter = fi.AppendText();
                        }
                        else
                        {
                            if (!fi.Directory.Exists)
                            {
                                fi.Directory.Create();
                            } // if
                            LoggingStreamWriter = fi.CreateText();
                        } // if
                    } // if
                }
                catch
                {
                    // catch all errors
                } // try/catch
                if (null == LoggingStreamWriter)
                {
                    Logging = false;
                } // if
            } // if

            // init done
            //StartupMessage();
        } // Init()

        /// <summary>
        /// Mapping from default UserMessage.ini
        /// </summary>
        private int GetErrorCodeByName(string name)
        {
            switch (name.Substring(0, 2))
            {
                case "CM":
                    switch (name.Substring(3))
                    {
                        case "StartMessage":
                            return 100003;

                        case "ExpirationTimeWarningMessage":
                            return 100001;


                        case "UnitCounterWarningMessage":
                            return 100002;


                        case "CertifiedWarningMessage":
                            return 100007;


                        case "UsagePeriodWarningMessage":
                            return 100008;


                        case "CertifiedElapsedMessage":
                            return 100006;


                        case "WibuCmNetWrongVersion":
                            return 100010;


                        case "DllNotFound":
                            return 100012;

                        case "CanceledUsagePeriod":
                            return 100013;
                    }
                    break;
                case "WK":
                    switch (name.Substring(3))
                    {
                        case "StartMessage":
                            return 100003;

                        case "ExpirationTimeWarningMessage":
                            return 100001;

                        case "UnitCounterWarningMessage":
                            return 100002;

                        case "DllNotFound":
                            return 100012;
                    }
                    break;
            } // switch

            return -1;
        }

        private string GetLoggingFileName(string logPath, bool bAppendPidToLog)
        {
            // name of the logfile == <AssemblyName>.log or <AssemblyName>[<PID>].log
            string AssemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            if (bAppendPidToLog)
            {
                try
                {
                    AssemblyName += "[" + Process.GetCurrentProcess().Id.ToString() + "]";
                }
                catch
                {
                    // use PID 0 if PID cannot be determined (e.g. due to insufficient rights)
                    AssemblyName += "[0]";
                } // try/catch
            } // if
            if ("" == logPath)
            {
                //Environment.SpecialFolder.ApplicationData:
                //Windows: C:\Users\username\AppData\Roaming
                //Linux/Mac: /home/username/.config
                string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                AssemblyName = Path.Combine(Path.Combine(AppData ?? "", AssemblyName),
                  AssemblyName + ".log");
            }
            else
            {
                AssemblyName = Path.Combine(logPath, AssemblyName + ".log");
            } // if
            return AssemblyName;
        }

        private string GetAssemblyDirectory(Assembly assemblyToCheck)
        {
            if (assemblyToCheck == null || assemblyToCheck.GlobalAssemblyCache)
            {
                return string.Empty;
            }
            var uri = new Uri(assemblyToCheck.CodeBase);
            return Path.GetDirectoryName(uri.LocalPath + uri.Fragment);
        }

        private List<string> GetCultureSpecificFileNames()
        {
            string fileName = "UserMessage";
            string fileSuffix = "ini";

            CultureInfo culture = CultureInfo.CurrentUICulture;

            List<string> resultNames = new List<string>();
            // look for culture specific INI-file        
            while (!culture.Equals(culture.Parent))
            {
                resultNames.Add(fileName + culture.Name + "." + fileSuffix);

                culture = culture.Parent;
            } // while      
            resultNames.Add(fileName + "." + fileSuffix);

            return resultNames;
        }

        private List<string> GetBasePathNames()
        {
            List<string> resultNames = new List<string>();
            string assemblyDirectory = GetAssemblyDirectory(Assembly.GetExecutingAssembly());
            if (assemblyDirectory != string.Empty)
            {
                resultNames.Add(assemblyDirectory);
            }
            assemblyDirectory = GetAssemblyDirectory(Assembly.GetEntryAssembly());
            if (assemblyDirectory != string.Empty)
            {
                resultNames.Add(assemblyDirectory);
            }

            return resultNames;
        }

        private string GetFileName(List<string> basePathNames, List<string> cultureSpecificFileNames)
        {
            foreach (var basePath in basePathNames)
            {
                foreach (var fileName in cultureSpecificFileNames)
                {
                    if (File.Exists(Path.Combine(basePath, fileName)))
                    {
                        return Path.Combine(basePath, fileName);
                    }
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// This is a helper function to sort the list of error messages by error code
        /// </summary>
        /// <param name="error1"></param>
        /// <param name="error2"></param>
        /// <returns></returns>
        private int SortErrorCodes(Pair<int, List<Pair<string, string>>> error1,
                                   Pair<int, List<Pair<string, string>>> error2)
        {
            return error1.First - error2.First;
        } // SortErrorCodes()


        /// <summary>
        /// This method writes the specified text into the logfile if logging es enabled.
        /// </summary>
        /// <param name="text">the text to be logged</param>
        private void Log(string text)
        {
            if (Logging)
            {
                // write the text into the file and flush it
                text = DateTime.Now.ToString() + " " + text;
                LoggingStreamWriter.WriteLine(text);
                LoggingStreamWriter.Flush();
            } // if
        } // Log()

        [ThreadStatic]
        private static int ErrorCodeToBeFound;

        /// <summary>
        /// This method tries to find an error code specific error text. If none is found,
        /// it returns false.
        /// </summary>
        /// <param name="copyProtectionSystem">the used copy protection system</param>
        /// <param name="errorCode">the error code</param>
        /// <param name="caption">the specified caption for this error</param>
        /// <param name="header">the specified header for this error</param>
        /// <param name="text">the specified text for this error</param>
        /// <returns>true if the error text was found, otherwise false</returns>
        private bool GetErrorText(string copyProtectionSystem,
                                  int errorCode,
                                  out string caption,
                                  out string header,
                                  out string text)
        {
            // initialize all out arguments
            caption = "";
            header = "";
            text = "";
            ErrorCodeToBeFound = errorCode;
            Pair<int, List<Pair<string, string>>> result;
            switch (copyProtectionSystem)
            {
                case "CodeMeter":
                case "CodeMeterAct":

                    // search list of all codemeter errors for this error code
                    result = CodeMeterErrorMessages.Find(FindErrorMessage);
                    if (null != result)
                    {
                        // error code found: get the specified values
                        GetTexts(result.Second, out caption, out header, out text);
                        return true;
                    } // if

                    // error code not found
                    return false;
                case "WibuKey":

                    // search list of all wibukey errors for this error code
                    result = WibuKeyErrorMessages.Find(FindErrorMessage);
                    if (null != result)
                    {
                        // error code found: get the specified values
                        GetTexts(result.Second, out caption, out header, out text);
                        return true;
                    } // if
                    return false;
            } // switch
            return false;
        } // GetErrorText()

        /// <summary>
        /// This is a helper function to find the Pair with a specific error code.
        /// </summary>
        /// <param name="element"></param>
        /// <returns></returns>
        private static bool FindErrorMessage(Pair<int, List<Pair<string, string>>> element)
        {
            if (element.First == ErrorCodeToBeFound)
            {
                return true;
            }
            else
            {
                return false;
            } // if
        } // FindErrorMessage

        /// <summary>
        /// This method
        /// </summary>
        /// <param name="elements"></param>
        /// <param name="caption"></param>
        /// <param name="header"></param>
        /// <param name="text"></param>
        private void GetTexts(List<Pair<string, string>> elements,
                              out string caption,
                              out string header,
                              out string text)
        {
            caption = WindowTitle;
            header = "";
            text = "";
            foreach (Pair<string, string> element in elements)
            {
                switch (element.First)
                {
                    case "Caption":
                        caption = element.Second;
                        break;
                    case "Headline":
                        header = element.Second;
                        break;
                    case "MainText":
                        text = element.Second;
                        break;
                } // switch
            } // foreach
        } // GetTexts()


        private DialogResult ShowDialog(string caption,
                                        string header,
                                        string message,
                                        MessageBoxButtons buttons,
                                        string progressBarLabel,
                                        int progressBarMax,
                                        int progressBarValue)
        {
            message = message.Replace("\\n", Environment.NewLine).Replace("\\t", "\t");
            header = header.Replace("\\n", Environment.NewLine).Replace("\\t", "\t");

            DialogResult result = DialogResult.None;

            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    result = MessageDialogWindow.ShowDialog(message, caption, buttons, progressBarLabel, progressBarMax, progressBarValue);
                });
            }
            else
            {
                result = MessageDialogWindow.ShowDialog(message, caption, buttons, progressBarLabel, progressBarMax, progressBarValue);
            }

            return result;
        } // ShowDialog()


        public void StartupMessage(string copyProtectionSystem)
        {
            string Category = "StartMessage";
            switch (copyProtectionSystem)
            {
                case "CodeMeter":
                case "CodeMeterAct":
                    Category = "CM_" + Category;
                    break;
                case "WibuKey":
                    Category = "WK_" + Category;
                    break;
            } // switch
            string Caption = iniFile.Find(Category, "Caption");
            string Header = iniFile.Find(Category, "Headline");
            string Text = iniFile.Find(Category, "MainText");
            if ((null != Caption) && (null != Header) && (null != Text))
            {
                bool b = BuyHint;
                BuyHint = false;
                ShowDialog(Caption, Header, Text, MessageBoxButtons.OK, null, 0, 0);
                BuyHint = b;
            } // if
        } // StartupMessage()

        /******************************************************************************************************************************
         * AxProtectorErrorCode   Return Values             Key                                     Type
         * 
         * 0 (NoLicense)          MessageBoxButtons.Retry   Owner (Window handle, currently null)   object
         *                        MessageBoxButtons.Cancel  Buttons                                 MessageBoxButtons
         *                                                  Licenses                                List<Dictionary<string,object>> (*)
         * 
         * 100000                 MessageBoxButtons.OK      CopyProtectionSystem                    String
         * (VersionTooOld)                                  Owner (Window handle, currently null)   object
         *                                                  InstalledVersion                        Version
         *                                                  RequiredVersion                         Version
         * 
         * 100001                 MessageBoxButtons.OK      CopyProtectionSystem                    String
         * (ExpirationTimeWarning)                          Owner (Window handle, currently null)   object
         *                                                  ExpirationTime                          DateTime
         *                                                  WarningLevel                            Int32
         * 100002                 MessageBoxButtons.OK      CopyProtectionSystem                    String
         * (UnitCounterWarning)                             Owner (Window handle, currently null)   object
         *                                                  UnitCounter                             Int32
         *                                                  WarningLevel                            Int32
         * 
         * 100004                 -> Parameters["Buttons"]  CopyProtectionSystem                    String
         * (ShowGeneralMessage)                             Owner (Window handle, currently null)   object
         *                                                  Buttons                                 MessageBoxButtons
         *                                                  Icon                                    MessageBoxIcon
         *                                                  License                                 Dictionary<string,object> (*)
         *                                                  ErrorCode                               Int32
         *                                                  ErrorText                               String
         * 
         * 100006                 MessageBoxButtons.OK      CopyProtectionSystem                    String
         * (CertifiedTimeElapsed)                           Owner (Window handle, currently null)   object
         *                                                  Buttons                                 MessageBoxButtons
         *                                                  Icon                                    MessageBoxIcon
         *                                                  License                                 Dictionary<string,object> (*)
         * 
         * 100007                 MessageBoxButtons.OK      CopyProtectionSystem                    String
         * (CertifiedTimeWarning)                           Owner (Window handle, currently null)   object
         *                                                  CertifiedTime                           DateTime
         *                                                  SystemTime                              DateTime
         *                                                  WarningLevel                            Int32
         * 
         * 100008                 MessageBoxButtons.OK      CopyProtectionSystem                    String
         * (UsagePeriodWarning)   MessageBoxButtons.Cancel  Owner (Window handle, currently null)   object
         *                                                  Hours                                   Int32
         * 
         * 100009                 -> Parameters["Buttons"]  CopyProtectionSystem                    String
         * (SystemTimeDifferenceError)                      Owner (Window handle, currently null)   object
         *                                                  Buttons                                 MessageBoxButtons
         *                                                  Icon                                    MessageBoxIcon
         *                                                  License                                 Dictionary<string,object> (*)
         * 
         * 100011                 -> Parameters["Buttons"]  CopyProtectionSystem                    String
         * (RuntimeCheckError)                              Owner (Window handle, currently null)   object
         *                                                  Buttons                                 MessageBoxButtons
         *                                                  Icon                                    MessageBoxIcon
         *                                                  License                                 Dictionary<string,object> (*)
         *                                                  ErrorCode                               Int32
         *                                                  ErrorText                               String
         * 
         * (*) License                                      CopyProtectionSystem                    String
         *                                                  FirmCode                                UInt32
         *                                                  ProductCode                             UInt32
         *                                                  FeatureCode                             UInt32
         *                                                  ErrorCode (only in NoLicense()(**))     Int32
         *                                                  ErrorText (only in NoLicense()(**))     String
         *                                                  
         * (**) 100010 (CM_WibuCmNetWrongVersion) "WibuCmNET.dll has version x.xx instead of y.yy."
         * (**) 100012 (CM_DllNotFound)           "Cannot access CodeMeter Api (WibuCmNET.dll)."
         * (**) 100012 (WK_DllNotFound)           "xxx not found." (WkWin32.dll/WkWin64.dll)
         * (**) 100013 (CM_CanceledUsagePeriod)   "Canceled by user because of UsagePeriod."
         * (**) 100014 (CpsrtNotFound)            "cpsrt libarary not found." (cpsrt32.dll/cpsrt64.dll)
         ******************************************************************************************************************************/
        public int ShowMessage(int axProtectorErrorCode, Dictionary<string, object> parameters)
        {
            Trace.WriteLine($"Message: {axProtectorErrorCode} {string.Join("; ", parameters.Select(kvp => kvp.Key + " - " + kvp.Value))}");

            // synchronization with other threads
            Dictionary<string, object> MyResult = new Dictionary<string, object>
            {
              {"AxProtectorErrorCode", axProtectorErrorCode},
              {"Parameters", parameters}
            };
            mm.Enter(MyResult);
            DialogResult res = DialogResult.None;

            // check if another thread already shows this error message
            if (!Monitor.TryEnter(MyResult["Sync"]))
            {
                // yes - wait for result and return the same
                lock (MyResult["Sync"])
                {
                    mm.Leave(MyResult, ref res);
                    return Convert.ToInt32(res);
                } // lock
            } // if
            switch (axProtectorErrorCode)
            {
                case 100000:
                    VersionTooOld((string)parameters["CopyProtectionSystem"],
                      parameters["Owner"],
                      (Version)parameters["InstalledVersion"],
                      (Version)parameters["RequiredVersion"]);
                    break;
                case 100001:
                    ExpirationTimeWarning(parameters["Owner"],
                      (string)parameters["CopyProtectionSystem"],
                      (DateTime)parameters["ExpirationTime"],
                      (int)parameters["WarningLevel"]);
                    break;
                case 100002:
                    UnitCounterWarning(parameters["Owner"],
                      (string)parameters["CopyProtectionSystem"],
                      (int)parameters["UnitCounter"],
                      (int)parameters["WarningLevel"]);
                    break;
                case 100004:
                    switch ((int)parameters["ErrorCode"])
                    {
                        case 100010:
                            AssemblyNotFound((string)parameters["ErrorText"]);
                            break;

                        default:
                            res = GeneralError((string)parameters["CopyProtectionSystem"],
                              parameters["Owner"],
                              (MessageBoxButtons)parameters["Buttons"],
                              (Icons)parameters["Icon"],
                              (Dictionary<string, object>)parameters["License"],
                              (int)parameters["ErrorCode"],
                              (string)parameters["ErrorText"]);
                            break;
                    } // switch
                    break;
                case 100006:
                    CertifiedTimeElapsed(parameters["Owner"],
                      (string)parameters["CopyProtectionSystem"],
                      (Buttons)parameters["Buttons"],
                      (Icons)parameters["Icon"],
                      (Dictionary<string, object>)parameters["License"]);
                    break;
                case 100007:
                    CertifiedTimeWarning(parameters["Owner"],
                      (string)parameters["CopyProtectionSystem"],
                      (DateTime)parameters["CertifiedTime"],
                      (DateTime)parameters["SystemTime"],
                      (int)parameters["WarningLevel"]);
                    break;
                case 100008:
                    res = UsagePeriodWarning(parameters["Owner"],
                      (string)parameters["CopyProtectionSystem"],
                      (int)parameters["Hours"]);
                    break;
                case 100009:
                    res = SystemTimeDifferenceError(parameters["Owner"],
                      (string)parameters["CopyProtectionSystem"],
                      (MessageBoxButtons)parameters["Buttons"],
                      (Icons)parameters["Icon"],
                      (Dictionary<string, object>)parameters["License"]);
                    break;
                case 100011:
                    res = RuntimeCheckError((string)parameters["CopyProtectionSystem"],
                      parameters["Owner"],
                      (MessageBoxButtons)parameters["Buttons"],
                      (Icons)parameters["Icon"],
                      (Dictionary<string, object>)parameters["License"],
                      (int)parameters["ErrorCode"],
                      (string)parameters["ErrorText"]);
                    break;
                default:
                    res = NoLicense(parameters["Owner"],
                      (MessageBoxButtons)parameters["Buttons"],
                      (List<Dictionary<string, object>>)parameters["Licenses"]);
                    break;
            } // switch

            // set the result for other threads with the same error message
            mm.Leave(MyResult, ref res);
            Monitor.Exit(MyResult["Sync"]);
            return Convert.ToInt32(res);
        } // ShowMessage()

        private void CertifiedTimeElapsed(object owner,
                                          string copyProtectionSystem,
                                          Buttons buttons,
                                          Icons icon,
                                          Dictionary<string, object> lic)
        {
            string Category = GetCategory(copyProtectionSystem, "CertifiedElapsedMessage");
            string Caption = iniFile.Find(Category, "Caption");
            string Header = iniFile.Find(Category, "Headline");
            string Text = iniFile.Find(Category, "MainText");
            if ((null == Caption) || (null == Header) || (null == Text))
            {
                Caption = WindowTitle;
                Text = CertifiedTimeElapsedText;
                Header = "";
            } // if
            ShowDialog(Caption, Header, Text, MessageBoxButtons.OK, null, 0, 0);
        } // CertifiedTimeElapsed()

        private void CertifiedTimeWarning(object owner,
                                          string copyProtectionSystem,
                                          DateTime certifiedTime,
                                          DateTime systemTime,
                                          int warningLevel)
        {
            string Category = GetCategory(copyProtectionSystem, "CertifiedWarningMessage");
            string Caption = iniFile.Find(Category, "Caption");
            string Header = iniFile.Find(Category, "Headline");
            string Text = iniFile.Find(Category, "MainText");
            if ((null == Caption) || (null == Header) || (null == Text))
            {
                Caption = WindowTitle;
                Text = CertifiedTimeWarningText;
                Header = "";
            } // if
            Text = Text.Replace("#hours#", systemTime.Subtract(certifiedTime).TotalHours.ToString());
            ShowDialog(Caption, Header, Text, MessageBoxButtons.OK, null, 0, 0);
        } // CertifiedTimeWarning()

        private void ExpirationTimeWarning(object /*IWin32Window*/ owner,
                                           string copyProtectionSystem,
                                           DateTime expirationTime,
                                           int warningLevel)
        {
            string Category = GetCategory(copyProtectionSystem, "ExpirationTimeWarningMessage");
            string Caption = iniFile.Find(Category, "Caption");
            string Header = iniFile.Find(Category, "Headline");
            string Text = iniFile.Find(Category, "MainText");
            if ((null == Caption) || (null == Header) || (null == Text))
            {
                Caption = WindowTitle;
                Text = ExpirationTimeWarningText;
                Header = "";
            } // if
            int CurrentValue = (int)expirationTime.Subtract(DateTime.UtcNow).TotalDays;
            Text = Text.Replace("#remaindays#", CurrentValue.ToString());

            ShowDialog(Caption, Header, Text, MessageBoxButtons.OK, ExpirationTimeWarningLabelText, warningLevel, CurrentValue);
        } // ExpirationTimeWarning()

        private DialogResult GeneralError(string copyProtectionSystem,
                                            object /*IWin32Window*/ owner,
                                            MessageBoxButtons buttons,
                                            Icons icon,
                                            Dictionary<string, object> lic,
                                            int errorCode,
                                            string errorText)
        {
            if (!GetErrorText(copyProtectionSystem, errorCode, out string Caption, out string Header, out string Text))
            {
                Caption = WindowTitle;
                Text = ErrorMessageText + "\n\n" + GetLicenseString(lic) + "\n\n" + errorText;
                Header = "";
            }
            else
            {
                Text = Text.Replace("#FirmProductCode#", GetLicenseString(lic));
            } // if

            return ShowDialog(Caption, Header, Text, buttons, null, 0, 0);
        } // ShowGeneralMessage()

        private DialogResult NoLicense(object /*IWin32Window*/ owner,
                                       MessageBoxButtons buttons,
                                       List<Dictionary<string, object>> licenses)
        {
            StringBuilder LicensesText = new StringBuilder();
            string ErrorText;
            foreach (Dictionary<string, object> Lic in licenses)
            {
                LicensesText.Append(GetLicenseString(Lic));
                LicensesText.Append(" (");
                string errorText = (string)Lic["ErrorText"];
                switch ((int)Lic["ErrorCode"])
                {
                    case 100010:
                        ErrorText = iniFile.Find("CM_WibuCmNetWrongVersion", "ErrorText");
                        if (null == ErrorText)
                        {
                            LicensesText.Append(errorText);
                        }
                        else
                        {
                            System.Text.RegularExpressions.MatchCollection VersionNumbers =
                              System.Text.RegularExpressions.Regex.Matches(errorText, @"[0-9]+\.[0-9]+");
                            LicensesText.Append(ErrorText.Replace("#ActualVersion#", VersionNumbers[0].Value).Replace("#RequiredVersion#", VersionNumbers[1].Value));
                        } // if
                        break;
                    case 100012:
                        string Prefix = ("WibuKey" == (string)Lic["CopyProtectionSystem"]) ? "WK_" : "CM_";
                        ErrorText = iniFile.Find(Prefix + "DllNotFound", "ErrorText");
                        if (null == ErrorText)
                        {
                            LicensesText.Append(errorText);
                        }
                        else
                        {
                            LicensesText.Append("WibuKey" == (string)Lic["CopyProtectionSystem"]
                              ? ErrorText.Replace("#DllName#", IntPtr.Size == 4 ? "WkWin32.dll" : "WkWin64.dll")
                              : ErrorText);
                        } // if
                        break;
                    case 100013:
                        ErrorText = iniFile.Find("CM_CanceledUsagePeriod", "ErrorText");
                        LicensesText.Append(ErrorText ?? errorText);
                        break;
                    case 100014:
                        ErrorText = iniFile.Find("CpsrtNotFound", "ErrorText");
                        LicensesText.Append(ErrorText ?? errorText);
                        break;
                    default:
                        LicensesText.Append(errorText);
                        break;
                } // switch
                LicensesText.Append(")\n");
            }
            string Category = "NoLicenseMessage";
            bool WibuKey = false;
            bool CodeMeter = false;
            foreach (Dictionary<string, object> Lic in licenses)
            {
                switch ((string)Lic["CopyProtectionSystem"])
                {
                    case "CodeMeter":
                    case "CodeMeterAct":
                        CodeMeter = true;
                        break;
                    case "WibuKey":
                        WibuKey = true;
                        break;
                } // switch
            } // foreach

            if (WibuKey)
            {
                Category = "WK_" + Category;
            } // if
            if (CodeMeter)
            {
                Category = "CM_" + Category;
            } // if
            string Caption = iniFile.Find(Category, "Caption");
            string Header = iniFile.Find(Category, "Headline");
            string Text = iniFile.Find(Category, "MainText");
            if ((null != Caption) && (null != Header) && (null != Text))
            {
                Text = Text.Replace("#FirmProductCode#", LicensesText.ToString());
            }
            else
            {
                Caption = WindowTitle;
                Text = NoLicenseText + "\n\n" + LicensesText.ToString();
                Header = "";
            } // if

            return ShowDialog(Caption, Header, Text, buttons, null, 0, 0);
        } // NoLicense()

        private DialogResult RuntimeCheckError(string copyProtectionSystem,
                                               object /*IWin32Window*/ owner,
                                               MessageBoxButtons buttons,
                                               Icons icon,
                                               Dictionary<string, object> lic,
                                               int errorCode,
                                               string errorText)
        {
            if (!GetErrorText(copyProtectionSystem, errorCode, out string Caption, out string Header, out string Text))
            {
                Caption = WindowTitle;
                Text = ErrorMessageText + "\n\n" + GetLicenseString(lic) + "\n\n" + errorText;
                Header = "";
            }
            else
            {
                Text = Text.Replace("#FirmProductCode#", GetLicenseString(lic));
            } // if
            return ShowDialog(Caption, Header, Text, buttons, null, 0, 0);
        } // RuntimeCheckError()

        private void UnitCounterWarning(object /*IWin32Window*/ owner,
                                        string copyProtectionSystem,
                                        int units,
                                        int warningLevel)
        {
            string Category = GetCategory(copyProtectionSystem, "UnitCounterWarningMessage");
            string Caption = iniFile.Find(Category, "Caption");
            string Header = iniFile.Find(Category, "Headline");
            string Text = iniFile.Find(Category, "MainText");
            if ((null == Caption) || (null == Header) || (null == Text))
            {
                Caption = WindowTitle;
                Text = UnitCounterWarningText;
                Header = "";
            } // if
            Text = Text.Replace("#units#", units.ToString());
            ShowDialog(Caption, Header, Text, MessageBoxButtons.OK, UnitCounterWarningLabelText, warningLevel, units);
        } // UnitCounterWarning()

        private DialogResult UsagePeriodWarning(object owner, string copyProtectionSystem, int hours)
        {
            string Category = GetCategory(copyProtectionSystem, "UsagePeriodWarningMessage");
            string Caption = iniFile.Find(Category, "Caption");
            string Header = iniFile.Find(Category, "Headline");
            string Text = iniFile.Find(Category, "MainText");
            if ((null == Caption) || (null == Header) || (null == Text))
            {
                Caption = WindowTitle;
                Text = UsagePeriodWarningText;
                Header = "";
            } // if
            Text = Text.Replace("#hours#", hours.ToString());
            return ShowDialog(Caption, Header, Text, MessageBoxButtons.OKCancel, null, 0, 0);
        } // UsagePeriodWarning()

        private void VersionTooOld(string copyProtectionSystem,
                                   object /*IWin32Window*/ owner,
                                   Version installedVersion,
                                   Version requiredVersion)
        {
            if (!GetErrorText(copyProtectionSystem, 100000, out string Caption, out string Header, out string Text))
            {
                Caption = WindowTitle;
                Text = VersionTooOldText;
                Header = "";
            } // if
            Text = Text.Replace("#requiredversion#", requiredVersion.ToString());
            Text = Text.Replace("#currversion#", installedVersion.ToString());
            ShowDialog(Caption, Header, Text, MessageBoxButtons.OK, null, 0, 0);
        } // VersionTooOld()

        private void AssemblyNotFound(string assemblyName)
        {
            string Caption = "Assembly Not Found";
            string Header = "Assembly Not Found";
            string Text = string.Format($"The assembly \"{assemblyName}\" could not be found.");
            ShowDialog(Caption, Header, Text, MessageBoxButtons.OK, null, 0, 0);
        } // AssemblyNotFound()

        private DialogResult SystemTimeDifferenceError(object /*IWin32Window*/ owner,
                                                       string copyProtectionSystem,
                                                       MessageBoxButtons buttons,
                                                       Icons icon,
                                                       Dictionary<string, object> lic)
        {
            string Category = GetCategory(copyProtectionSystem, "SystemTimeDifference");
            string Caption = iniFile.Find(Category, "Caption");
            string Header = iniFile.Find(Category, "Headline");
            string Text = iniFile.Find(Category, "MainText");
            return ShowDialog(Caption, Header, Text, buttons, null, 0, 0);
        } // SystemTimeDifferenceError()

        private string GetCategory(string copyProtectionSystem, string categoryText)
        {
            switch (copyProtectionSystem)
            {
                case "CodeMeter":
                case "CodeMeterAct":
                    return "CM_" + categoryText;
                case "WibuKey":
                    return "WK_" + categoryText;
            } // switch
            return categoryText;
        }

        private string GetLicenseString(Dictionary<string, object> license)
        {
            // build message text
            string result = $"\n{(string)license["CopyProtectionSystem"]} {(uint)license["FirmCode"]}:{(uint)license["ProductCode"]}";
            if (0 != (uint)license["FeatureCode"])
            {
                result += string.Format(" FeatureCode {0} (0x{0:x})", (uint)license["FeatureCode"]);
            } // if
            return result;
        } // GetLicenseString()

        /*****************************************************************************
        SetAccessData
        ==============================================================================

        This function sets additional access data to CmAccess(), CmAcces2()
        or WkbAccess2() .

         *****************************************************************************/

        public void SetAccessData(out Dictionary<string, object> axAccValues)
        {
            axAccValues = new Dictionary<string, object>();

            if (EnableSetAccessData)
            {
                axAccValues[FlagsName] = Flags;
                axAccValues[ProductItemRefName] = ProductItemRef;
                axAccValues[BoxMaskName] = BoxMask;
                axAccValues[SerialNumberName] = SerialNumber;
                axAccValues[UserDefinedIdName] = UserDefinedId;
                axAccValues[UserDefinedTextName] = UserDefinedText;
                axAccValues[ServerName] = Server;
            }
        } // SetAccessData()


        /*****************************************************************************
        QueryAccessData
        ==============================================================================

        This function sets additional access data to CmAccess(), CmAcces2()
        or WkbAccess2() .

         *****************************************************************************/

        public void QueryAccessData(Dictionary<string, object> axAccValues)
        {
            if (EnableQueryAccessData)
            {
                if (axAccValues.ContainsKey(FlagsName))
                {
                    Flags = (uint)axAccValues[FlagsName];
                }
                if (axAccValues.ContainsKey(ProductItemRefName))
                {
                    ProductItemRef = (ushort)axAccValues[ProductItemRefName];
                }
                if (axAccValues.ContainsKey(BoxMaskName))
                {
                    BoxMask = (ushort)axAccValues[BoxMaskName];
                }
                if (axAccValues.ContainsKey(SerialNumberName))
                {
                    SerialNumber = (uint)axAccValues[SerialNumberName];
                }
                if (axAccValues.ContainsKey(UserDefinedIdName))
                {
                    UserDefinedId = (uint)axAccValues[UserDefinedIdName];
                }
                if (axAccValues.ContainsKey(UserDefinedTextName))
                {
                    UserDefinedText = (string)axAccValues[UserDefinedTextName];
                }
                if (axAccValues.ContainsKey(ServerName))
                {
                    Server = (string)axAccValues[ServerName];
                }
            } //if
        } // QueryAccessData()
    } // class UserMessage
}