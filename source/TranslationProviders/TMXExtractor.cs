﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.IO;
using System.Resources;
using System.Xml;

namespace TRTR
{
    class TMXExtractor : TranslationProvider
    {
        #region private declarations
        //private Dictionary<int, ResXDictEntryList> dict = new Dictionary<int, ResXDictEntryList>();
        private string extractFileName;
        private Dictionary<int, string> folderAliasDict;
        private Dictionary<int, string> fileNameAliasDict;
        private XmlDocument doc = null;
        private XmlNode bodyNode = null;
        private TranslationProvider tp = null;
        private List<int> transHashes;
        RUTMXExtractor tpRUS = null;
        private int tuId = 0;
        private static string[] bigfile_locals = new string[] { "ARABIC", "ENGLISH", "FRENCH", "GERMAN", "ITALIAN", "POLISH", "RUSSIAN", "SPANISH" };
        #endregion

        internal string SrcLang { get; set; }

        // ctor
        internal TMXExtractor(string path, TranslationProvider tp = null)
        {
            this.extractFileName = Path.Combine(TRGameInfo.Game.WorkFolder, path);
            this.tp = tp;
            this.SrcLang = "en";
        }

        internal override void Open()
        {

            // purge old translations
            Clear();
            tuId = 0;
            tpRUS = new RUTMXExtractor(Path.ChangeExtension(extractFileName, ".ru-ext.tmx"), tp);
            tpRUS.Open();
            // load filename aliases file
            LoadPathAliasesFile(Path.Combine(TRGameInfo.Game.WorkFolder, "path_aliases.txt"), out folderAliasDict, out fileNameAliasDict);

            this.doc = new XmlDocument();

            doc.AppendChild(doc.CreateProcessingInstruction("xml", "version=\"1.0\" encoding=\"utf-8\""));
            doc.AppendChild(doc.CreateComment(string.Format("Generated by Tomb Raider Translator v{0} at {1}", Settings.version.ToString(), DateTime.Now)));
            XmlElement elemTmx = doc.CreateElement("tmx");
            elemTmx.SetAttribute("version", "1.4");
            XmlNode nodeTmx = doc.AppendChild(elemTmx);
            XmlElement elemHeader = doc.CreateElement("header");

            elemHeader.SetAttribute("creationtool", "Tomb Raider Translator TMX Exporter");
            elemHeader.SetAttribute("creationtoolversion", Settings.version.ToString());
            elemHeader.SetAttribute("datatype", "PlainText");
            elemHeader.SetAttribute("segtype", "sentence");
            elemHeader.SetAttribute("adminlang", "en");
            elemHeader.SetAttribute("srclang", "en");
            elemHeader.SetAttribute("o-tmf", "TRTR2TranslationMemory");
            XmlNode nodeHeader = nodeTmx.AppendChild(elemHeader);
            bodyNode = nodeTmx.AppendChild(doc.CreateElement("body"));

            this.transHashes = new List<int>();
        }

        internal override void Close()
        {
            // flush files
            // compress files, if needed
            // dump statistics

            //foreach (string file in files)
            //    ReadResXFile(file);
            //Report(Path.Combine(TRGameInfo.Game.WorkFolder, "translation report.txt"));
            //Log.LogDebugMsg(string.Format("{0} translation entries added", dict.Count));

            //string zippedFileName = Path.Combine(TRGameInfo.Game.WorkFolder, "hu.zip");
            //if (File.Exists(zippedFileName))
            //    ReadCompressedResX(zippedFileName);

            if (!Directory.Exists(Path.GetDirectoryName(extractFileName)))
                Directory.CreateDirectory(Path.GetDirectoryName(extractFileName));
            doc.Save(this.extractFileName);
            if(tpRUS != null)
                tpRUS.Close();
        }

        protected override bool getUseContext() { return true; }

        private void LoadPathAliasesFile(string fileName, out Dictionary<int, string> folderAlias, out Dictionary<int, string> fileNameAlias)
        {
            folderAlias = new Dictionary<int, string>();
            fileNameAlias = new Dictionary<int, string>();
            if (File.Exists(fileName))
            {
                TextReader rdr = new StreamReader(fileName);

                string line;
                while ((line = rdr.ReadLine()) != null)
                {
                    string[] elements = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    if (elements.Length == 3)
                    {
                        if (elements[0] == "S")
                            folderAlias.Add(elements[1].GetHashCode(), elements[2]);
                        else
                            fileNameAlias.Add(elements[1].GetHashCode(), elements[2]);
                    }
                    else
                        if (elements.Length != 0)
                        {
                            throw new Exception(string.Format("Invalid path alias entry: \"{0}\"", line));
                        }
                }
            }
        }

        internal override void Clear()
        {
            if (File.Exists(extractFileName))
                File.Delete(extractFileName);
        }

        private static int compareByPathLength(string file1, string file2)
        {
            string path1 = Path.GetDirectoryName(file1);
            string path2 = Path.GetDirectoryName(file2);

            int compareRes = path1.CompareTo(path2);

            if (compareRes == 0)
                compareRes = string.Compare(file1, path1.Length, file2, path2.Length, int.MaxValue);

            return compareRes;
        }

        internal override string GetTranslation(string text, IFileEntry entry, Dictionary<string, string> context)
        {
            if (text.Trim() == "")
                return text;

            string textSrcLang = this.SrcLang;
            if (context.TryGetValue("SrcLang", out textSrcLang))
                if (textSrcLang == "ru")
                {
                    return tpRUS.GetTranslation(text, entry, context);
                }

            string trans = text;

            int textHashCode = text.GetHashCode();
            if (!transHashes.Contains(textHashCode))
            {
                transHashes.Add(textHashCode);

                if (tp != null)
                    trans = tp.GetTranslation(text, entry, context);

                //if (trans != string.Empty && trans != text)
                {

                    XmlElement elemTu = doc.CreateElement("tu");

                    elemTu.SetAttribute("tuid", (tuId++).ToString("X6"));

                    XmlNode nodeTu = bodyNode.AppendChild(elemTu);
                    if (context != null)
                    {
                        StringBuilder sb = new StringBuilder();
                        XmlElement elemNote = doc.CreateElement("note");
                        XmlAttribute attr = null;
                        foreach (string key in context.Keys)
                        {
                            string value = context[key];
                            if (key == "IBigFile")
                            {
                                for (int j = 0; j < bigfile_locals.Length; j++)
                                    if (value.ToUpper().EndsWith(bigfile_locals[j]))
                                    {
                                        value = value.Substring(0, bigfile_locals[j].Length) + "_LOCALIZED";
                                    }
                            }
                            attr = doc.CreateAttribute(key);
                            attr.Value = value;
                            elemNote.Attributes.Append(attr);
                        }
                        nodeTu.AppendChild(elemNote);
                    }

                    //source
                    XmlElement elemTuvSource = doc.CreateElement("tuv");
                    elemTuvSource.SetAttribute("xml:lang", "en");
                    XmlNode nodeTuvSource = nodeTu.AppendChild(elemTuvSource);


                    XmlElement elemSegSource = doc.CreateElement("seg");
                    elemSegSource.InnerText = text;
                    nodeTuvSource.AppendChild(elemSegSource);
                    //nodeTuvSource.AppendChild(doc.CreateElement("crowdin-metadata"));

                    // trans
                    XmlElement elemTuvTrans = doc.CreateElement("tuv");
                    elemTuvTrans.SetAttribute("xml:lang", "hu");
                    XmlNode nodeTuvTrans = nodeTu.AppendChild(elemTuvTrans);


                    XmlElement elemSegTrans = doc.CreateElement("seg");

                    elemSegTrans.InnerText = trans;
                    nodeTuvTrans.AppendChild(elemSegTrans);
                    //nodeTuvTrans.AppendChild(doc.CreateElement("crowdin-metadata"));

                }
            }
            return trans;
        }

        public void Report(string fileName) { }

    }
}
