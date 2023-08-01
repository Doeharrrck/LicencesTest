namespace LicenseHandling
{
    using System.Collections.Generic;
    using System.Windows;

    public partial class UserMessage
    {
        /// <summary>
        /// This enum implements the values for the Buttons in a MessageBox and their return values.
        /// </summary>
        internal enum Buttons
        {
            OK = 0,
            OKCancel = 1,
            AbortRetryIgnore = 2,
            YesNoCancel = 3,
            YesNo = 4,
            RetryCancel = 5
        } // enum Buttons

        internal enum ButtonsClicked
        {
            OK = 1,
            Cancel = 2,
            Abort = 3,
            Retry = 4,
            Ignore = 5,
            Yes = 6,
            No = 7
        } // enum ButtonsClicked

        /// <summary>
        /// This enum implements the values for the icons in a MessageBox.
        /// </summary>
        internal enum Icons
        {
            Error = 0x10,
            Warning = 0x30,
            Information = 0x40
        } // enum Icons

        /// <summary>
        /// This class manages the synchronization between threads and the results
        /// of different error messages.
        /// </summary>
        class MessageManagement
        {
            private readonly List<Triple<int, DialogResult, Dictionary<string, object>>> _Results;
            private static readonly object Sync = new object();

            public MessageManagement()
            {
                _Results = new List<Triple<int, DialogResult, Dictionary<string, object>>>();
            } // MessageManagement()

            public void Enter(Dictionary<string, object> Result)
            {
                lock (Sync)
                {
                    // check if this error message is already being processed
                    if (GetFromList(Result))
                    {
                        // yes - nothing to do
                        return;
                    } // if
                      // no - add a synchronization object and store the parameter list for later use
                    Result.Add("Sync", new object());
                    _Results.Add(new Triple<int, DialogResult, Dictionary<string, object>>(1, DialogResult.None, Result));
                } // lock
            } // Enter()

            public void Leave(Dictionary<string, object> Result, ref DialogResult res)
            {
                lock (Sync)
                {
                    RemoveFromList(Result, ref res);
                } // lock
            } // Enter()

            private bool GetFromList(Dictionary<string, object> Result)
            {
                // check if this error is already in the list
                foreach (Triple<int, DialogResult, Dictionary<string, object>> t in _Results)
                {
                    Dictionary<string, object> dict = t.Third;
                    if ((int)dict["AxProtectorErrorCode"] == (int)Result["AxProtectorErrorCode"])
                    {
                        switch ((int)dict["AxProtectorErrorCode"])
                        {
                            case 100000:
                                // version too old - display only once
                                SetSync(Result, dict);
                                t.First++;
                                return true;
                            case 100001:
                                // expiration time warning - display only once
                                SetSync(Result, dict);
                                t.First++;
                                return true;
                            case 100002:
                                // unit counter warning - display only once
                                SetSync(Result, dict);
                                t.First++;
                                return true;
                            case 100004:
                                if (CompareParameter(dict, Result, "ErrorCode"))
                                    switch ((int)((Dictionary<string, object>)Result["Parameters"])["ErrorCode"])
                                    {
                                        case 100010:
                                            SetSync(Result, dict);
                                            t.First++;
                                            return true;
                                        default:
                                            // display only once if the parameters are the same
                                            if (CompareParameter(dict, Result, "CopyProtectionSystem") &&
                                              CompareParameter(dict, Result, "License") &&
                                              CompareParameter(dict, Result, "ErrorCode"))
                                            {
                                                SetSync(Result, dict);
                                                t.First++;
                                                return true;
                                            } // if
                                            break;
                                    } // switch
                                break;
                            case 100006:
                                // certified time elapsed - display only once
                                SetSync(Result, dict);
                                t.First++;
                                return true;
                            case 100007:
                                // certified time warning - display only once
                                SetSync(Result, dict);
                                t.First++;
                                return true;
                            case 100008:
                                // usage period warning - display only once
                                SetSync(Result, dict);
                                t.First++;
                                return true;
                            case 100009:
                                // system time difference - display only once if the parameters are the same
                                if (CompareParameter(dict, Result, "CopyProtectionSystem") &&
                                  CompareParameter(dict, Result, "License"))
                                {
                                    SetSync(Result, dict);
                                    t.First++;
                                    return true;
                                } // if
                                break;
                            case 100011:
                                // runtime check error - display only once if the parameters are the same
                                if (CompareParameter(dict, Result, "CopyProtectionSystem") &&
                                  CompareParameter(dict, Result, "License") &&
                                  CompareParameter(dict, Result, "ErrorCode"))
                                {
                                    SetSync(Result, dict);
                                    t.First++;
                                    return true;
                                } // if
                                break;
                            default:
                                // no license - display only once if the parameters are the same
                                if (CompareParameter(dict, Result, "Licenses"))
                                {
                                    SetSync(Result, dict);
                                    t.First++;
                                    return true;
                                } // if
                                break;
                        } // switch
                    } // if
                } // foreach
                return false;
            } // GetFromList()

            private bool CompareParameter(Dictionary<string, object> d1, Dictionary<string, object> d2,
              string Name)
            {
                if (d1.ContainsKey("Parameters"))
                {
                    return CompareParameter((Dictionary<string, object>)d1["Parameters"],
                      (Dictionary<string, object>)d2["Parameters"], Name);
                } // if
                  // compare the objects
                if (d1.ContainsKey(Name) && d2.ContainsKey(Name))
                {
                    // objects are string, int, ...
                    if (d1[Name].GetType().IsValueType || (d1[Name] is string))
                    {
                        return d1[Name].Equals(d2[Name]);
                    } // if
                      // objects are Dictionary<> (License)
                    if (d1[Name] is Dictionary<string, object>)
                    {
                        bool Result = true;
                        // compare all objects stored in the dictionary
                        foreach (string Key in ((Dictionary<string, object>)d1[Name]).Keys)
                        {
                            if (d2.ContainsKey(Key))
                            {
                                Result &= CompareParameter((Dictionary<string, object>)d1[Name], (Dictionary<string, object>)d2[Name], Key);
                            } // if
                        } // foreach
                        return Result;
                    } // if
                      // objects are List<Dictionary<>> (Licenses)
                    if ((d1[Name] is List<Dictionary<string, object>>) &&
                      (((List<Dictionary<string, object>>)d1[Name]).Count == ((List<Dictionary<string, object>>)d2[Name]).Count))
                    {
                        // compare all licenses in the list
                        bool Result = true;
                        for (int i = 0; i < ((List<Dictionary<string, object>>)d1[Name]).Count; i++)
                        {
                            foreach (string Key in ((List<Dictionary<string, object>>)d1[Name])[i].Keys)
                            {
                                if (((List<Dictionary<string, object>>)d2[Name])[i].ContainsKey(Key))
                                {
                                    Result &= CompareParameter(((List<Dictionary<string, object>>)d1[Name])[i],
                                      ((List<Dictionary<string, object>>)d2[Name])[i], Key);
                                } // if
                            } // foreach
                        } // for
                        return Result;
                    } // if
                }// if
                return false;
            } // CompareParameter

            private void SetSync(Dictionary<string, object> Target, Dictionary<string, object> Source)
            {
                // copy the sync object
                Target.Add("Sync", Source["Sync"]);
            } // SetSync()

            private void RemoveFromList(Dictionary<string, object> Result, ref DialogResult res)
            {
                // get or set the result stored in the list (similar to GetFromList())
                for (int i = 0; i < _Results.Count; i++)
                {
                    Dictionary<string, object> dict = _Results[i].Third;
                    if ((int)dict["AxProtectorErrorCode"] == (int)Result["AxProtectorErrorCode"])
                    {
                        switch ((int)dict["AxProtectorErrorCode"])
                        {
                            case 100000:
                                GetOrSetResult(i, ref res);
                                return;
                            case 100001:
                                GetOrSetResult(i, ref res);
                                return;
                            case 100002:
                                GetOrSetResult(i, ref res);
                                return;
                            case 100004:
                                if (CompareParameter(dict, Result, "ErrorCode"))
                                    switch ((int)((Dictionary<string, object>)Result["Parameters"])["ErrorCode"])
                                    {
                                        case 100010:
                                            GetOrSetResult(i, ref res);
                                            return;
                                        default:
                                            if (CompareParameter(dict, Result, "CopyProtectionSystem") &&
                                              CompareParameter(dict, Result, "License") &&
                                              CompareParameter(dict, Result, "ErrorCode"))
                                            {
                                                GetOrSetResult(i, ref res);
                                                return;
                                            } // if
                                            break;
                                    } // switch
                                break;
                            case 100006:
                                GetOrSetResult(i, ref res);
                                return;
                            case 100007:
                                GetOrSetResult(i, ref res);
                                return;
                            case 100008:
                                GetOrSetResult(i, ref res);
                                return;
                            case 100009:
                                if (CompareParameter(dict, Result, "CopyProtectionSystem") &&
                                  CompareParameter(dict, Result, "License"))
                                {
                                    GetOrSetResult(i, ref res);
                                    return;
                                } // if
                                break;
                            case 100011:
                                if (CompareParameter(dict, Result, "CopyProtectionSystem") &&
                                  CompareParameter(dict, Result, "License") &&
                                  CompareParameter(dict, Result, "ErrorCode"))
                                {
                                    GetOrSetResult(i, ref res);
                                    return;
                                } // if
                                break;
                            default:
                                if (CompareParameter(dict, Result, "Licenses"))
                                {
                                    GetOrSetResult(i, ref res);
                                    return;
                                } // if
                                break;
                        } // switch
                    } // if
                } // foreach
            } // RemoveFromList()

            private void GetOrSetResult(int i, ref DialogResult res)
            {
                if (DialogResult.None == _Results[i].Second)
                {
                    // not input - result -> list
                    _Results[i].Second = res;
                }
                else
                {
                    // input - list -> result
                    res = _Results[i].Second;
                } // if
                  // decrease the instance counter
                DecreaseCounter(i);
            } // GetOrSetResult()

            private void DecreaseCounter(int i)
            {
                _Results[i].First--;
                if (0 == _Results[i].First)
                {
                    // no more instances active - remove the entry from the list
                    _Results.RemoveAt(i);
                } // if
            } // DecreaseCounter
        } // class MessageManagement
    } // class UserMessage
}
