using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;

public partial class UserMessage
{
  private static readonly string ErrorMessageText = "ErrorMessageText";
  private static readonly string NoLicenseText = "NoLicenseText";
  private static readonly string ExpirationTimeWarningText = "ExpirationTimeWarningText";
  private static readonly string UsagePeriodWarningText = "UsagePeriodWarningText";
  private static readonly string UnitCounterWarningText = "UnitCounterWarningText";
  private static readonly string CertifiedTimeWarningText = "CertifiedTimeWarningText";
  private static readonly string CertifiedTimeElapsedText = "CertifiedTimeElapsedText";
  private static readonly string VersionTooOldText = "VersionTooOldText";

  private static readonly string WindowTitle = "WIBU-SYSTEMS protected application";

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
  private int RetryTimeout;
  private int NumberOfRetries;
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
  private UInt32 Flags; //Flags are used to set which AccesData is delivered: 1 ProductItemRef,2 BoxMask and SerialNumber,4 UserDefinedId,8 UserDefinedText,16 Server
  private UInt16 ProductItemRef;
  private UInt16 BoxMask;
  private UInt32 SerialNumber;
  private UInt32 UserDefinedId;
  private string UserDefinedText;
  private string Server;

  private StreamWriter LoggingStreamWriter;
  private IniFile iniFile;
  private int Retries;
  private DateTime LastRetry;

  private static readonly MessageManagement mm = new MessageManagement();

  /// <summary>
  /// The default constructor calls the Init() method which reads the INI-file.
  /// </summary>
  public UserMessage()
  {
    CodeMeterErrorMessages = new List<Pair<int, List<Pair<string, string>>>>();
    WibuKeyErrorMessages = new List<Pair<int, List<Pair<string, string>>>>();
    LastRetry = DateTime.UtcNow;

    // [Service]      
    Logging = false;
    LogPath = "";
    RetryTimeout = 5; // seconds
    NumberOfRetries = 3;
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
          int ErrorCode;
          if (Category.First.Length > 2)
          {
            try
            {
              switch (Category.First.Substring(0, 2))
              {
                case "CM":
                  ErrorCode = System.Convert.ToInt32(Category.First.Substring(2));
                  CodeMeterErrorMessages.Add(new Pair<int, List<Pair<string, string>>>(ErrorCode, Category.Second));
                  break;
                case "WK":
                  ErrorCode = System.Convert.ToInt32(Category.First.Substring(2));
                  WibuKeyErrorMessages.Add(new Pair<int, List<Pair<string, string>>>(ErrorCode, Category.Second));
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
        value = iniFile.Find("Service", "RetryTimeout");
        if (null != value)
        {
          RetryTimeout = System.Convert.ToInt32(value) * 1000; // seconds -> milliseconds
        } // if
        value = iniFile.Find("Service", "NumberOfRetries");
        if (null != value)
        {
          NumberOfRetries = System.Convert.ToInt32(value);
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
          if (!UInt32.TryParse(value, out Flags))
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
            //ProductItemRef = System.Convert.ToUInt16(value);
            if (!UInt16.TryParse(value, out ProductItemRef))
            {
              //throw new FormatException(string.Format("UserMessage.ini Section [AccessData] {0} value {1} cant be converted to UInt16", ProductItemRefName, value));
            } //if
          } // if

          value = iniFile.Find("AccessData", BoxMaskName);
          if (null != value)
          {
            //BoxMask = System.Convert.ToUInt16(value);
            if (!UInt16.TryParse(value, out BoxMask))
            {
              //throw new OverflowException(string.Format("UserMessage.ini Section [AccessData] {0} value {1} causes UInt16 overflow", BoxMaskName, value));
            } //if
          } // if

          value = iniFile.Find("AccessData", SerialNumberName);
          if (null != value)
          {
            //SerialNumber = System.Convert.ToUInt32(value);
            if (!UInt32.TryParse(value, out SerialNumber))
            {
              //throw new OverflowException(string.Format("UserMessage.ini Section [AccessData] {0} value {1} causes UInt32 overflow", SerialNumberName, value));
            } //if
          } // if
          value = iniFile.Find("AccessData", UserDefinedIdName);
          if (null != value)
          {
            //UserDefinedId = System.Convert.ToUInt32(value);
            if (!UInt32.TryParse(value, out UserDefinedId))
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

        Retries = NumberOfRetries;

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
      string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData );
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
    if (assemblyToCheck==null || assemblyToCheck.GlobalAssemblyCache)
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
    if (assemblyDirectory!=string.Empty)
    {     
      resultNames.Add(assemblyDirectory);      
    }
    assemblyDirectory = GetAssemblyDirectory(Assembly.GetEntryAssembly());
    if (assemblyDirectory!=string.Empty)
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
  /// <param name="Text">the text to be logged</param>
  private void Log(string Text)
  {
    if (Logging)
    {
      // write the text into the file and flush it
      Text = DateTime.Now.ToString() + " " + Text;
      LoggingStreamWriter.WriteLine(Text);
      LoggingStreamWriter.Flush();
    } // if
  } // Log()

  [ThreadStatic]
  private static int ErrorCodeToBeFound;

  /// <summary>
  /// This method tries to find an error code specific error text. If none is found,
  /// it returns false.
  /// </summary>
  /// <param name="CopyProtectionSystem">the used copy protection system</param>
  /// <param name="ErrorCode">the error code</param>
  /// <param name="Caption">the specified caption for this error</param>
  /// <param name="Header">the specified header for this error</param>
  /// <param name="Text">the specified text for this error</param>
  /// <returns>true if the error text was found, otherwise false</returns>
  private bool GetErrorText(string CopyProtectionSystem,
                            int ErrorCode,
                            out string Caption,
                            out string Header,
                            out string Text)
  {
    // initialize all out arguments
    Caption = "";
    Header = "";
    Text = "";
    ErrorCodeToBeFound = ErrorCode;
    Pair<int, List<Pair<string, string>>> result;
    switch (CopyProtectionSystem)
    {
      case "CodeMeter":
      case "CodeMeterAct":

        // search list of all codemeter errors for this error code
        result = CodeMeterErrorMessages.Find(FindErrorMessage);
        if (null != result)
        {
          // error code found: get the specified values
          GetTexts(result.Second, out Caption, out Header, out Text);
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
          GetTexts(result.Second, out Caption, out Header, out Text);
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
  /// <param name="Caption"></param>
  /// <param name="Header"></param>
  /// <param name="Text"></param>
  private void GetTexts(List<Pair<string, string>> elements,
                        out string Caption,
                        out string Header,
                        out string Text)
  {
    Caption = WindowTitle;
    Header = "";
    Text = "";
    foreach (Pair<string, string> element in elements)
    {
      switch (element.First)
      {
        case "Caption":
          Caption = element.Second;
          break;
        case "Headline":
          Header = element.Second;
          break;
        case "MainText":
          Text = element.Second;
          break;
      } // switch
    } // foreach
  } // GetTexts()


  private DialogResult ShowDialog(string Caption,
                                  string Header,
                                  string Text,
                                  Buttons Buttons,
                                  string ProgressBarLabel,
                                  int ProgressBarMax,
                                  int ProgressBarValue)
  {
    Text = Text.Replace("\\n", Environment.NewLine).Replace("\\t", "\t");
    Header = Header.Replace("\\n", Environment.NewLine).Replace("\\t", "\t");

        MessageBox.Show($"Header: {Header}/r/nCaption: {Caption}/r/nText: {Text}/r/n", "Custom Stuff", MessageBoxButton.YesNoCancel);


    Log(Text);
    switch (Buttons)
    {
      case Buttons.RetryCancel:
        if (DateTime.UtcNow.Subtract(LastRetry).TotalSeconds > 3.0)
        {
          LastRetry = DateTime.UtcNow;
          Retries = NumberOfRetries;
        } // if
        if (Retries > 0)
        {
          Retries--;
          System.Threading.Thread.Sleep(RetryTimeout);
          LastRetry = DateTime.UtcNow;
          return DialogResult.Retry;
        }
        else
        {
          return DialogResult.Cancel;
        } // if
      case Buttons.AbortRetryIgnore:
        if (DateTime.UtcNow.Subtract(LastRetry).TotalSeconds > 3.0)
        {
          LastRetry = DateTime.UtcNow;
          Retries = NumberOfRetries;
        } // if
        if (Retries > 0)
        {
          Retries--;
          System.Threading.Thread.Sleep(RetryTimeout);
          LastRetry = DateTime.UtcNow;
          return DialogResult.Retry;
        }
        else
        {
          return DialogResult.Abort;
        } // if
      case Buttons.OKCancel:
      case Buttons.OK:
        return DialogResult.OK;
      default:
        Debug.Assert(false);
        return DialogResult.None;
    } // switch
  } // ShowDialog()


  public void StartupMessage(string CopyProtectionSystem)
  {
    string Category = "StartMessage";
    switch (CopyProtectionSystem)
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
      ShowDialog(Caption, Header, Text, Buttons.OK, null, 0, 0);
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
  public DialogResult ShowMessage(int AxProtectorErrorCode, Dictionary<string, object> Parameters)
  {
    // synchronization with other threads
    Dictionary<string, object> MyResult = new Dictionary<string, object>
    {
      {"AxProtectorErrorCode", AxProtectorErrorCode},
      {"Parameters", Parameters}
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
        return res;
      } // lock
    } // if
    switch (AxProtectorErrorCode)
    {
      case 100000:
        VersionTooOld((string)Parameters["CopyProtectionSystem"],
          Parameters["Owner"],
          (Version)Parameters["InstalledVersion"],
          (Version)Parameters["RequiredVersion"]);
        break;
      case 100001:
        ExpirationTimeWarning(Parameters["Owner"],
          (string)Parameters["CopyProtectionSystem"],
          (DateTime)Parameters["ExpirationTime"],
          (int)Parameters["WarningLevel"]);
        break;
      case 100002:
        UnitCounterWarning(Parameters["Owner"],
          (string)Parameters["CopyProtectionSystem"],
          (int)Parameters["UnitCounter"],
          (int)Parameters["WarningLevel"]);
        break;
      case 100004:
        switch ((int)Parameters["ErrorCode"])
        {
          case 100010:
            AssemblyNotFound((string)Parameters["ErrorText"]);
            break;

          default:
            res = GeneralError((string)Parameters["CopyProtectionSystem"],
              Parameters["Owner"],
              (Buttons)Parameters["Buttons"],
              (Icons)Parameters["Icon"],
              (Dictionary<string, object>)Parameters["License"],
              (int)Parameters["ErrorCode"],
              (string)Parameters["ErrorText"]);
            break;
        } // switch
        break;
      case 100006:
        CertifiedTimeElapsed(Parameters["Owner"],
          (string)Parameters["CopyProtectionSystem"],
          (Buttons)Parameters["Buttons"],
          (Icons)Parameters["Icon"],
          (Dictionary<string, object>)Parameters["License"]);
        break;
      case 100007:
        CertifiedTimeWarning(Parameters["Owner"],
          (string)Parameters["CopyProtectionSystem"],
          (DateTime)Parameters["CertifiedTime"],
          (DateTime)Parameters["SystemTime"],
          (int)Parameters["WarningLevel"]);
        break;
      case 100008:
        res = UsagePeriodWarning(Parameters["Owner"],
          (string)Parameters["CopyProtectionSystem"],
          (int)Parameters["Hours"]);
        break;
      case 100009:
        res = SystemTimeDifferenceError(Parameters["Owner"],
          (string)Parameters["CopyProtectionSystem"],
          (Buttons)Parameters["Buttons"],
          (Icons)Parameters["Icon"],
          (Dictionary<string, object>)Parameters["License"]);
        break;
      case 100011:
        res = RuntimeCheckError((string)Parameters["CopyProtectionSystem"],
          Parameters["Owner"],
          (Buttons)Parameters["Buttons"],
          (Icons)Parameters["Icon"],
          (Dictionary<string, object>)Parameters["License"],
          (int)Parameters["ErrorCode"],
          (string)Parameters["ErrorText"]);
        break;
      default:
        res = NoLicense(Parameters["Owner"],
          (Buttons)Parameters["Buttons"],
          (List<Dictionary<string, object>>)Parameters["Licenses"]);
        break;
    } // switch

    // set the result for other threads with the same error message
    mm.Leave(MyResult, ref res);
    Monitor.Exit(MyResult["Sync"]);
    return res;
  } // ShowMessage()
  
  private void CertifiedTimeElapsed(object Owner,
                                    string CopyProtectionSystem,
                                    Buttons Buttons,
                                    Icons Icon,
                                    Dictionary<string, object> Lic)
  {
    string Category = GetCategory(CopyProtectionSystem, "CertifiedElapsedMessage");
    string Caption = iniFile.Find(Category, "Caption");
    string Header = iniFile.Find(Category, "Headline");
    string Text = iniFile.Find(Category, "MainText");
    if ((null == Caption) || (null == Header) || (null == Text))
    {
      Caption = WindowTitle;
      Text = CertifiedTimeElapsedText;
      Header = "";
    } // if
    ShowDialog(Caption, Header, Text, Buttons.OK, null, 0, 0);
  } // CertifiedTimeElapsed()
  
  private void CertifiedTimeWarning(object Owner,
                                    string CopyProtectionSystem,
                                    DateTime CertifiedTime,
                                    DateTime SystemTime,
                                    int WarningLevel)
  {
    string Category = GetCategory(CopyProtectionSystem, "CertifiedWarningMessage");
    string Caption = iniFile.Find(Category, "Caption");
    string Header = iniFile.Find(Category, "Headline");
    string Text = iniFile.Find(Category, "MainText");
    if ((null == Caption) || (null == Header) || (null == Text))
    {
      Caption = WindowTitle;
      Text = CertifiedTimeWarningText;
      Header = "";
    } // if
    Text = Text.Replace("#hours#", SystemTime.Subtract(CertifiedTime).TotalHours.ToString());
    ShowDialog(Caption, Header, Text, Buttons.OK, null, 0, 0);
  } // CertifiedTimeWarning()
  
  private void ExpirationTimeWarning(object /*IWin32Window*/ Owner,
                                     string CopyProtectionSystem,
                                     DateTime ExpirationTime,
                                     int WarningLevel)
  {
    string Category = GetCategory(CopyProtectionSystem, "ExpirationTimeWarningMessage");
    string Caption = iniFile.Find(Category, "Caption");
    string Header = iniFile.Find(Category, "Headline");
    string Text = iniFile.Find(Category, "MainText");
    if ((null == Caption) || (null == Header) || (null == Text))
    {
      Caption = WindowTitle;
      Text = ExpirationTimeWarningText;
      Header = "";
    } // if
    int CurrentValue = (int)ExpirationTime.Subtract(DateTime.UtcNow).TotalDays;
    Text = Text.Replace("#remaindays#", CurrentValue.ToString());

    ShowDialog(Caption, Header, Text, Buttons.OK, ExpirationTimeWarningLabelText, WarningLevel, CurrentValue);
  } // ExpirationTimeWarning()

  private DialogResult GeneralError(string CopyProtectionSystem,
                                      object /*IWin32Window*/ Owner,
                                      Buttons Buttons,
                                      Icons Icon,
                                      Dictionary<string, object> Lic,
                                      int ErrorCode,
                                      string ErrorText)
  {
    if (!GetErrorText(CopyProtectionSystem, ErrorCode, out string Caption, out string Header, out string Text))
    {
      Caption = WindowTitle;
      Text = ErrorMessageText + "\n\n" + GetLicenseString(Lic) + "\n\n" + ErrorText;
      Header = "";
    }
    else
    {
      Text = Text.Replace("#FirmProductCode#", GetLicenseString(Lic));
    } // if

    return ShowDialog(Caption, Header, Text, Buttons, null, 0, 0);
  } // ShowGeneralMessage()

  private DialogResult NoLicense(object /*IWin32Window*/ Owner,
                                 Buttons Buttons,
                                 List<Dictionary<string, object>> Licenses)
  {
    StringBuilder LicensesText = new StringBuilder();
    string ErrorText;
    foreach (Dictionary<string, object> Lic in Licenses)
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
    foreach (Dictionary<string, object> Lic in Licenses)
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

    return ShowDialog(Caption, Header, Text, Buttons, null, 0, 0);
  } // NoLicense()
  
  private DialogResult RuntimeCheckError(string CopyProtectionSystem,
                                         object /*IWin32Window*/ Owner,
                                         Buttons Buttons,
                                         Icons Icon,
                                         Dictionary<string, object> Lic,
                                         int ErrorCode,
                                         string ErrorText)
  {
    if (!GetErrorText(CopyProtectionSystem, ErrorCode, out string Caption, out string Header, out string Text))
    {
      Caption = WindowTitle;
      Text = ErrorMessageText + "\n\n" + GetLicenseString(Lic) + "\n\n" + ErrorText;
      Header = "";
    }
    else
    {
      Text = Text.Replace("#FirmProductCode#", GetLicenseString(Lic));
    } // if
    return ShowDialog(Caption, Header, Text, Buttons, null, 0, 0);
  } // RuntimeCheckError()

  private void UnitCounterWarning(object /*IWin32Window*/ Owner,
                                  string CopyProtectionSystem,
                                  int Units,
                                  int WarningLevel)
  {
    string Category = GetCategory(CopyProtectionSystem, "UnitCounterWarningMessage");
    string Caption = iniFile.Find(Category, "Caption");
    string Header = iniFile.Find(Category, "Headline");
    string Text = iniFile.Find(Category, "MainText");
    if ((null == Caption) || (null == Header) || (null == Text))
    {
      Caption = WindowTitle;
      Text = UnitCounterWarningText;
      Header = "";
    } // if
    Text = Text.Replace("#units#", Units.ToString());
    ShowDialog(Caption, Header, Text, Buttons.OK, UnitCounterWarningLabelText, WarningLevel, Units);
  } // UnitCounterWarning()

  private DialogResult UsagePeriodWarning(object Owner, string CopyProtectionSystem, int Hours)
  {
    string Category = GetCategory(CopyProtectionSystem, "UsagePeriodWarningMessage");
    string Caption = iniFile.Find(Category, "Caption");
    string Header = iniFile.Find(Category, "Headline");
    string Text = iniFile.Find(Category, "MainText");
    if ((null == Caption) || (null == Header) || (null == Text))
    {
      Caption = WindowTitle;
      Text = UsagePeriodWarningText;
      Header = "";
    } // if
    Text = Text.Replace("#hours#", Hours.ToString());
    return ShowDialog(Caption, Header, Text, Buttons.OKCancel, null, 0, 0);
  } // UsagePeriodWarning()

  private void VersionTooOld(string CopyProtectionSystem,
                             object /*IWin32Window*/ Owner,
                             Version InstalledVersion,
                             Version RequiredVersion)
  {
    if (!GetErrorText(CopyProtectionSystem, 100000, out string Caption, out string Header, out string Text))
    {
      Caption = WindowTitle;
      Text = VersionTooOldText;
      Header = "";
    } // if
    Text = Text.Replace("#requiredversion#", RequiredVersion.ToString());
    Text = Text.Replace("#currversion#", InstalledVersion.ToString());
    ShowDialog(Caption, Header, Text, Buttons.OK, null, 0, 0);
  } // VersionTooOld()
  
  private void AssemblyNotFound(string AssemblyName)
  {
    string Caption = "Assembly Not Found";
    string Header = "Assembly Not Found";
    string Text = string.Format($"The assembly \"{AssemblyName}\" could not be found.");
    ShowDialog(Caption, Header, Text, Buttons.OK, null, 0, 0);
  } // AssemblyNotFound()

  private DialogResult SystemTimeDifferenceError(object /*IWin32Window*/ Owner,
                                                 string CopyProtectionSystem,
                                                 Buttons Buttons,
                                                 Icons Icon,
                                                 Dictionary<string, object> Lic)
  {
    string Category = GetCategory(CopyProtectionSystem, "SystemTimeDifference");
    string Caption = iniFile.Find(Category, "Caption");
    string Header = iniFile.Find(Category, "Headline");
    string Text = iniFile.Find(Category, "MainText");
    return ShowDialog(Caption, Header, Text, Buttons, null, 0, 0);
  } // SystemTimeDifferenceError()
  
  private string GetCategory(string CopyProtectionSystem, string CategoryText)
  {
    switch (CopyProtectionSystem)
    {
      case "CodeMeter":
      case "CodeMeterAct":
        return "CM_" + CategoryText;
      case "WibuKey":
        return "WK_" + CategoryText;
    } // switch
    return CategoryText;
  }

  private string GetLicenseString(Dictionary<string, object> License)
  {
    // build message text
    string result = $"\n{(string)License["CopyProtectionSystem"]} {(UInt32)License["FirmCode"]}:{(UInt32)License["ProductCode"]}";
    if (0 != (UInt32)License["FeatureCode"])
    {
      result += string.Format(" FeatureCode {0} (0x{0:x})", (UInt32)License["FeatureCode"]);
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
        Flags = (UInt32)axAccValues[FlagsName];
      }
      if (axAccValues.ContainsKey(ProductItemRefName))
      {
        ProductItemRef = (UInt16)axAccValues[ProductItemRefName];
      }
      if (axAccValues.ContainsKey(BoxMaskName))
      {
        BoxMask = (UInt16)axAccValues[BoxMaskName];
      }
      if (axAccValues.ContainsKey(SerialNumberName))
      {
        SerialNumber = (UInt32)axAccValues[SerialNumberName];
      }
      if (axAccValues.ContainsKey(UserDefinedIdName))
      {
        UserDefinedId = (UInt32)axAccValues[UserDefinedIdName];
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