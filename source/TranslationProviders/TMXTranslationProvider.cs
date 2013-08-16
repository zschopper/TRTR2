﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;

namespace TRTR
{
    class TMXProvider : TranslationProvider
    {
        Dictionary<int, string> dict = new Dictionary<int, string>();
        internal override void LoadTranslations()
        {
            Dictionary<int, string> dict = new Dictionary<int, string>();
            string fileName = Path.Combine(TRGameInfo.Game.WorkFolder, "hu.tmx");
            if (File.Exists(fileName))
            {

                XmlDocument doc = new XmlDocument();
                XmlNamespaceManager mgr = new XmlNamespaceManager(doc.NameTable);
                mgr.AddNamespace("xml", "http://www.w3.org/XML/1998/namespace");
                doc.Load(Path.Combine(TRGameInfo.Game.WorkFolder, "hu.tmx"));
                foreach (XmlNode node in doc.SelectNodes("/tmx/body/tu"))
                {
                    string source = node.SelectSingleNode("tuv[@xml:lang='en']/seg", mgr).InnerText;
                    string value = node.SelectSingleNode("tuv[@xml:lang='hu']/seg", mgr).InnerText;

                    string replaced = source.Replace("&#13;", "\\r");//.Replace("\n", "\\n");
                    if (replaced != source)
                        Noop.DoIt();

                    source = replaced;// source.Replace("&#13;", "\\r").Replace("\n", "\\n");
                    value = value.Replace("&#13;", "\\r");//.Replace("\n", "\\n");

                    if (source.StartsWith("After a fortnight"))
                        Noop.DoIt();
                    int key = source.GetHashCode();
                    string value1;
                    if (dict.TryGetValue(key, out value1))
                    {
                        Log.LogDebugMsg("Key exists.");
                        Log.LogDebugMsg(string.Format("  Key: \"{0}\"", source));
                        Log.LogDebugMsg(string.Format("  Value1: \"{0}\"", value1));
                        Log.LogDebugMsg(string.Format("  Value1: \"{0}\"", value));
                    }
                    else
                        dict.Add(key, value);
                }
            }
        }

        internal override void Clear()
        {
            dict.Clear();
        }

        internal override string GetTranslation(string text, FileEntry entry, string[] context)
        {
            string ret = string.Empty;
            string replaced = text.Replace("\r", "");//.Replace("\r", "\\r").Replace("\n", "\\n");
            if (!dict.TryGetValue(replaced.GetHashCode(), out ret))
            {
                if (!dict.TryGetValue(replaced.Trim().GetHashCode(), out ret))
                {
                    Log.LogDebugMsg(string.Format("No translation for \"{0}\"", text));
                    return text;
                }
            }
            return ret;
            
        }
    }
}