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
        Dictionary<int, string> folderAliasDict;
        Dictionary<int, string> fileNameAliasDict;
        XmlDocument doc = null;
        XmlNode bodyNode = null;
        TranslationProvider tp = null;
        List<int> transHashes;
        int tuId = 0;
        #endregion

        // ctor
        internal TMXExtractor(string path, TranslationProvider tp = null)
        {
            this.extractFileName = Path.Combine(TRGameInfo.Game.WorkFolder, path);
            this.tp = tp;
        }


        internal override void Open()
        {

            // purge old translations
            Clear();
            tuId = 0;

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

        internal override string GetTranslation(string text, FileEntry entry, string[] context)
        {
            if (text.Trim() == "")
                return text;

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

                //if (context != null)
                //{
                //    bool discardValue = false;
                //    StringBuilder sb = new StringBuilder();
                //    for (int i = 0; i < context.Length; i++)
                //    {
                //        if (i % 2 == 0)
                //        {
                //            discardValue = context[i] == "bigfile";
                //            if (!discardValue)
                //                sb.Append("\r\n" + context[i]);
                //        }
                //        else
                //            if (!discardValue)
                //                sb.Append(": " + context[i]);
                //    }
                //    resNode.Comment = sb.ToString().Trim();
                //}

            }
            return trans;
        }

        public void WriteCompressedResX(string fileName)
        {
            //ZipFile file = new ZipFile(fileName);
            //List<string> files = new List<string>();
            //foreach(ZipEntry entry in file)
            //{
            //    if (entry.IsFile)
            //    {
            //        files.Add(entry.Name);
            //        Log.LogDebugMsg("zip:" + entry.Name);
            //    }
            //}
        }

        public void WriteResXFile(string fileName)
        {
            //FileStream fs = new FileStream(fileName, FileMode.CreateNew);
            //try
            //{
            //    WriteResXFile(fs, fileName);
            //}
            //finally
            //{
            //    fs.Close();
            //}
        }

        public void WriteResXFile(Stream stream, string fileName = "")
        {
            //System.ComponentModel.Design.ITypeResolutionService typeRes = null;
            //ResXResourceReader rdr = new ResXResourceReader(stream);
            //rdr.UseResXDataNodes = true;
            //foreach (DictionaryEntry rdrDictEntry in rdr)
            //{
            //    ResXDataNode node = (ResXDataNode)(rdrDictEntry.Value);
            //    string key = rdrDictEntry.Key.ToString();
            //    string value = node.GetValue(typeRes).ToString();
            //    string comment = node.Comment;
            //    ResXDictEntry entry = new ResXDictEntry(key, value, comment, fileName);

            //    ResXDictEntryList entryList;
            //    if (!dict.TryGetValue(entry.SourceHash, out entryList))
            //    {
            //        // new entry
            //        entryList = new ResXDictEntryList();
            //        dict.Add(entry.SourceHash, entryList);
            //    }
            //    entryList.Add(entry);
            //    if (!entryList.Translated)
            //        if (entry.SourceHash != entry.TranslationHash)
            //            entryList.Translated = true;

            //    if (entryList.IsUnique && entryList.Count > 1)
            //        for (int i = 1; i < entryList.Count; i++)
            //        {
            //            if (entryList[0].TranslationHash != entryList[i].TranslationHash)
            //                entryList.IsUnique = false;
            //        }
            //}
        }

        public void Report(string fileName) { }

    }
}