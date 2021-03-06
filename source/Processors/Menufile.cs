﻿using System;
using System.Collections.Generic;
//using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.Resources; //resource writer
using ExtensionMethods;
using System.Globalization;
using System.Diagnostics;

namespace TRTR
{
    class MenuTable
    {
        internal UInt32 StartOffs { get; set; }
        internal UInt32 EndOffs { get; set; }
        internal bool PlaceHolder { get; set; }

        internal UInt32 Length { get { return EndOffs - StartOffs; } }
    }
    class MenuFile
    {
        #region private declarations
        //private static TextConv textConv = new TextConv(new char[] { }, new char[] { }, Encoding.UTF8); // null;
        #endregion

        internal static bool Process(IFileEntry entry, Stream outStream, TranslationProvider tran)
        {
            bool ret = false;
            FileStream fs = TRGameInfo.FilePool.Open(entry);
            try
            {
                fs.Position = entry.Offset;
                ret = Process(entry, fs, entry.Raw.Length, outStream, tran);
            }
            finally
            {
                TRGameInfo.FilePool.Close(entry);
            }
            return ret;
        }

        internal static bool Process(IFileEntry entry, Stream inStream, long contentLength, Stream outStream, TranslationProvider tp)
        {
            if (outStream == null)
                outStream = Stream.Null;
            //            entry.BigFile.Parent.DumpToFile(@"c:\tmp\menu", entry.ReadContent());
            Int64 startInPos = inStream.Position;
            //Int64 startOutPos = -1;
            MemoryStream textBlockStream = new MemoryStream();


            // 4 byte = lang code (eng = 0x00)
            // 4 byte = entry count (+1?)
            // 4 byte = 0
            // entries
            UInt32 langCode = inStream.ReadUInt32();
            UInt32 entryCount1 = inStream.ReadUInt32();
            UInt32 entryCount2 = inStream.ReadUInt32() - 1;
            UInt32 entryCount = entryCount1 + entryCount2;
            UInt32 textBlockStartOfs = (entryCount + 3) * sizeof(UInt32);

            if (outStream != Stream.Null)
            {
                //startOutPos = outStream.Position;
                outStream.WriteUInt32(langCode);
                outStream.WriteUInt32(entryCount1);
                outStream.WriteUInt32(entryCount2 + 1);
            }

            MenuTable[] table = new MenuTable[entryCount];
            MenuTable lastValidEntry = null;

            UInt32 startOffs = 0;
            for (int i = 0; i < entryCount; i++)
            {
                if (i == 0x1413)
                    entryCount += 0;
                startOffs = inStream.ReadUInt32();
                bool placeHolder = startOffs <= entryCount * 4;
                table[i] = new MenuTable
                {
                    StartOffs = startOffs,
                    EndOffs = 0,
                    PlaceHolder = placeHolder,
                };

                if (lastValidEntry != null && !placeHolder)
                {
                    lastValidEntry.EndOffs = startOffs - 1;
                }
                if (!placeHolder)
                    lastValidEntry = table[i];
            }
            table[table.Length - 1].EndOffs = (UInt32)(contentLength);
            lastValidEntry.EndOffs = (UInt32)(contentLength);

            int debugPlaceHolderCount = 0;
            int debugValidEntryCount = 0;

            byte[] textBuf = new byte[30];
            for (int i = 0; i < table.Length; i++)
            {
                MenuTable tableEntry = table[i];
                if (tableEntry.PlaceHolder)
                    debugPlaceHolderCount++;

                // if we aren't in read-only mode: write table
                if (outStream != Stream.Null)
                {
                    if (tableEntry.PlaceHolder)
                        outStream.WriteUInt32(tableEntry.StartOffs);
                    else
                        outStream.WriteUInt32((UInt32)(textBlockStartOfs + textBlockStream.Length));
                }

                string translation = string.Empty;

                // StartIdx isn't zero if it has content
                if ((tableEntry.StartOffs > 0) && !tableEntry.PlaceHolder)
                {
                    if (tableEntry.Length > 0)
                    {
                        // increase buffer size
                        if (tableEntry.Length > textBuf.Length)
                            textBuf = new byte[tableEntry.Length];

                        inStream.Position = startInPos + tableEntry.StartOffs;
                        inStream.Read(textBuf, 0, (int)(tableEntry.Length));

                        string[] keyPlaceHolders;
                        string text = CharToKeyPlaceholders(TRGameInfo.Conv.Enc.GetString(textBuf, 0, (int)(tableEntry.Length)).Trim(new char[] { '\0' }), out keyPlaceHolders);

                        Dictionary<string, string> context = null;
                        if (tp.UseContext)
                            context = new Dictionary<string, string> {
                                {"index", i.ToString()},
                                //{"prefix", prefix},
                                {"filename", entry.Extra.FileNameForced},
                                {"hash", entry.HashText},
                                {"bigfile", entry.BigFile.Name},
                                      };
                        translation = TRGameInfo.Conv.ToGameFormat(tp.GetTranslation(text.Replace("\n", "\r\n"), entry, context));
                        // if we aren't in read-only mode: write translation
                        if (outStream != Stream.Null && translation != string.Empty)
                        {
                            byte[] buf;
                            if (keyPlaceHolders.Length > 0)
                                translation = KeyPlaceholdersToChar(translation, keyPlaceHolders);
                            buf = TRGameInfo.Conv.Enc.GetBytes(translation.Replace("\r\n", "\n") + (char)(0));
                            textBlockStream.Write(buf, 0, buf.Length);
                        }
                    }
                    debugValidEntryCount++;
                }
            }
            // copy text content stream to outstream
            if (outStream != Stream.Null)
            {
                textBlockStream.Position = 0;
                outStream.WriteFromStream(textBlockStream, textBlockStream.Length);
            }

            return true;
        }

        private static string CharToKeyPlaceholders(string text, out string[] chars)
        {
            chars = null;
            //return text;

            //if (text.Contains("You've unlocked a new secondary weapo"))
            //    Debug.Flush();
            List<string> charsFind = new List<string>();
            List<int> pos = new List<int>();

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                UnicodeCategory cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(text, i);
                List<UnicodeCategory> ign = new List<UnicodeCategory> { 
                    UnicodeCategory.LowercaseLetter, 
                    UnicodeCategory.UppercaseLetter, 
                    //UnicodeCategory.OtherPunctuation, 
                    UnicodeCategory.DecimalDigitNumber,
                    UnicodeCategory.SpaceSeparator

                    /*
                     * filter: Control, DashPunctuation
                     * OtherLetter, NonSpacingMark
                     * 
                     * */
                };

                if (TRGameInfo.Game.GameDefaults.MenuPlaceholderChars.Contains(c))
                {
                    charsFind.Add(c.ToString());
                    pos.Add(i);

                }
                else
                {
                    if (text.Trim().Length == 1)
                        Debug.Flush();
                    //if ((int)c > 0x1800 && (int)c < 1900)
                    if ((int)c > 0xFF)
                    {
                        //Debug.WriteLine(string.Format("unicodeinfo: \"{0}\" ({1})", c, cat));
                        Debug.WriteLine(" == unicodeinfo == ");
                        //string placeHolder = "";
                        //if (TRGameInfo.Game.GameDefaults.MenuPlaceholderChars.TryGetValue(c, placeHolder)

                        Debug.WriteLine(string.Format("Char: \"{0}\" ({1:X8}) {2})", c, (int)c, cat));
                        Debug.WriteLine(string.Format("Text: \"{0}\")", text));
                        Debug.WriteLine(" == /unicodeinfo == ");
                        Debug.Flush();
                        charsFind.Add(c.ToString());
                        pos.Add(i);
                    }

                }
                /*
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(text, i) == UnicodeCategory.PrivateUse)
                {
                    charsFind.Add(c.ToString());
                    pos.Add(i);
                }
                 * */
            }

            chars = charsFind.ToArray();

            if (pos.Count > 1)
                Noop.DoIt();
            if (pos.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                int lastPos = 0;
                for (int i = 0; i < pos.Count; i++)
                {
                    sb.Append(text.Substring(lastPos, pos[i] - lastPos));
                    sb.Append(string.Format("{{{0}}}", i));
                    lastPos = pos[i] + 1;
                }
                if (lastPos != text.Length - 1)
                    sb.Append(text.Substring(lastPos, text.Length - lastPos));

                if (pos.Count > 1)
                    Noop.DoIt();

                string rb = KeyPlaceholdersToChar(text, chars);
                if (rb != text)
                    Noop.DoIt();
                return sb.ToString();
            }

            return text;
        }

        private static string KeyPlaceholdersToChar(string text, string[] chars)
        {
            if (chars == null)
                return text;
            if (chars.Length == 0)
                return text;

            return string.Format(text, chars);

            //for (int i = 0; i < chars.Length; i++)
            //    ret.Replace("{" + i.ToString() + "}", chars[i].ToString()); 
            //return ret;
        }
    }
}
