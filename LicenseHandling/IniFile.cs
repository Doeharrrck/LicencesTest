namespace LicenseHandling
{
    using System.Collections.Generic;
    using System.IO;

    class IniFile
    {
        private readonly List<Pair<string, List<Pair<string, string>>>> Categories;
        private readonly FileInfo fiIniFile;
        internal string DirectoryName => (null == fiIniFile) ? "" : fiIniFile.DirectoryName;  // DirectoryName

        public IniFile(string FileName)
        {
            if (!string.IsNullOrEmpty(FileName))
            {
                fiIniFile = new FileInfo(FileName);
            } // if
            Categories = new List<Pair<string, List<Pair<string, string>>>>();
        } // IniFile()

        public bool Read()
        {
            try
            {
                if ((null == fiIniFile) || !fiIniFile.Exists)
                {
                    return false;
                } // if
                StreamReader sr = fiIniFile.OpenText();
                string CurrentCategory = "";
                List<Pair<string, string>> KeyValuePairs = new List<Pair<string, string>>();
                while (!sr.EndOfStream)
                {
                    string Line = sr.ReadLine();
                    // remove comments
                    Line = Line.IndexOf(';') >= 0 ? Line.Substring(0, Line.IndexOf(';')).Trim() : Line.Trim();
                    if (Line.Length < 2)
                    {
                        continue;
                    } // if
                    if ((Line[0] == '[') && (Line[Line.Length - 1] == ']'))
                    {
                        AddCategory(CurrentCategory, KeyValuePairs);
                        CurrentCategory = Line.Substring(1, Line.Length - 2);
                        KeyValuePairs = new List<Pair<string, string>>();
                    } // if
                    if (Line.IndexOf('=') > 0)
                    {
                        string First = Line.Substring(0, Line.IndexOf('=')).Trim();
                        string Second = "";
                        if (Line.IndexOf('=') < (Line.Length - 1))
                        {
                            Second = Line.Substring(Line.IndexOf('=') + 1).Trim();
                        } // if
                        KeyValuePairs.Add(new Pair<string, string>(First, Second));
                    } // if
                } // while
                AddCategory(CurrentCategory, KeyValuePairs);
                sr.Close();
                return true;
            }
            catch
            {
            } // try/finally
            return false;
        } // Read()


        private void AddCategory(string Category, List<Pair<string, string>> KeyValuePairs)
        {
            if ("" != Category)
            {
                Categories.Add(new Pair<string, List<Pair<string, string>>>(Category, KeyValuePairs));
            } // if
        } // AddCategory()


        public List<Pair<string, string>> Find(string Category)
        {
            return Find(Category, false);
        } // Find()

        public List<Pair<string, string>> Find(string Category, bool CaseSensitive)
        {
            if (!CaseSensitive)
            {
                Category = Category.ToLower();
            } // if
            foreach (Pair<string, List<Pair<string, string>>> pair in Categories)
            {
                if (CaseSensitive)
                {
                    if (pair.First == Category)
                    {
                        return pair.Second;
                    } // if
                }
                else
                {
                    if (pair.First.ToLower() == Category)
                    {
                        return pair.Second;
                    } // if
                } // if
            } // foreach
              // not found - return empty list
            return new List<Pair<string, string>>();
        } // Find()

        public string Find(string Category, string Key)
        {
            return Find(Category, Key, false);
        } // Find()

        public string Find(string Category, string Key, bool CaseSensitive)
        {
            if (!CaseSensitive)
            {
                Key = Key.ToLower();
            } // if
            List<Pair<string, string>> CompleteCategory = Find(Category, CaseSensitive);
            foreach (Pair<string, string> KeyValuePair in CompleteCategory)
            {
                if (CaseSensitive)
                {
                    if (KeyValuePair.First == Key)
                    {
                        return KeyValuePair.Second;
                    } // if
                }
                else
                {
                    if (KeyValuePair.First.ToLower() == Key)
                    {
                        return KeyValuePair.Second;
                    } // if
                } // if
            } // foreach
              // not found - return null
            return null;
        } // Find()

        public List<Pair<string, List<Pair<string, string>>>> GetAll()
        {
            return Categories;
        } // GetAll()

    } // class IniFile
}
