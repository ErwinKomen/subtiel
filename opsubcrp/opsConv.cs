using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.Threading.Tasks;
using opsubcrp.conv;
using System.Text.RegularExpressions;
using opsub;

namespace opsubcrp {
  /* -------------------------------------------------------------------------------------
   * Name:  opsConv
   * Goal:  Routines that perform the actual conversion
   * History:
   * 27/jan/2016 ERK Created
     ------------------------------------------------------------------------------------- */
  class opsConv {
    // ========================= Declarations local to me =================================
    private ErrHandle errHandle = new ErrHandle();
    private Dictionary<String, String> dicMovie = new Dictionary<string, string>();
    private string FOLIA_VERSION = "0.12.2";
    private bool bInit = false;
    // ======================== Getters and setters =======================================

    // ======================== Public methods ============================================

    /* -------------------------------------------------------------------------------------
     * Name:        ConvertOneOpsToFolia
     * Goal:        Convert one opensubtitle gz file to a folia.xml file
     * Parameters:  sFileIn     - File to be processed
     *              sDirIn      - Directory under which the input files are located
     *              sDirOut     - Directory where the output files should come
     *              bForce      - Create result, even though it exists already
     *              bMulti      - Only do multi-part files
     *              bIsDebug    - Debugging mode on or off
     * History:
     * 27/jan/2016 ERK Created
       ------------------------------------------------------------------------------------- */
    public bool ConvertOneOpsToFolia(String sFileIn, String sDirIn, String sDirOut, bool bForce, 
      bool bMulti, bool bIsDebug) {
      // XmlReader rdOps = null;
      XmlWriter wrFolia = null;
      List<Sent> lSentBuf = new List<Sent>(); // Buffer containing sentences and words

      try {
        // Validate
        if (!File.Exists(sFileIn)) return false;
        if (!Directory.Exists(sDirOut)) return false;

        // If the input file cannot be read, then skip it
        if (!util.General.CanReadFile(sFileIn)) return true;

        // Make sure dirout does not contain a trailing /
        if (sDirOut.EndsWith("/") || sDirOut.EndsWith("\\"))
          sDirOut = sDirOut.Substring(0, sDirOut.Length - 1);

        // Retrieve the idmovie and the movie's name from the file name
        String sIdMovie = ""; String sMovieName = ""; String sMovieYear = "";
        if (!getMovieNameId(sFileIn, ref sMovieName, ref sIdMovie, ref sMovieYear)) return false;

        // Get the destination directory
        String sDst = sDirOut + "/" + sFileIn.Substring(sDirIn.Length + 1);
        String sDstDir = Path.GetDirectoryName(sDst);
        // Check existence of destination directory
        if (!Directory.Exists(sDstDir)) Directory.CreateDirectory(sDstDir);

        // Create a destination file name inside [sDirOut]
        String sFileName = Path.GetFileNameWithoutExtension(sFileIn);
        string sFileBase = Path.GetFullPath( Path.GetDirectoryName(sFileIn) + "/" + Path.GetFileName(sFileIn));
        String sBare = sFileName;
        int iUs = sBare.IndexOf('_');
        int iVersion = 1;   // Default version number
        int iMax = 1;       // Maximum version number
        int iSubtitle = 0;  // Default subtitles.org number
        if (iUs >=0) { 
          sBare = sBare.Substring(0, iUs);
          // Get to the second part
          String sSecond = sFileName.Substring(iUs + 1);
          // Find two numbers there
          Match mcOne = Regex.Match(sSecond, "\\d+");
          if (!mcOne.Success) {
            errHandle.DoError("ConvertOneOpsToFolia","cannot get number from ["+sSecond+"]");
            return false;
          }
          // Get first number
          iVersion = Convert.ToInt32(mcOne.Value);
          // Go to next number
          mcOne = mcOne.NextMatch();
          if (!mcOne.Success) {
            errHandle.DoError("ConvertOneOpsToFolia", "cannot get number from [" + sSecond + "]");
            return false;
          }
          iMax = Convert.ToInt32(mcOne.Value);
          /*
          iVersion = Convert.ToInt32(sFileName.Substring(iUs + 1, 1));
          iMax = Convert.ToInt32(sFileName.Substring(iUs + 4, 1));
          */
        }

        // Check if we need to continue
        if (bMulti && iMax == 1) {
          debug("Skipping (non-multi): " + sFileIn);
          return true;
        }

        UInt64 iNumber;
        if (UInt64.TryParse(sBare, out iNumber)) {
          iSubtitle = Convert.ToInt32( iNumber);
          sBare = "S-O_" + iNumber.ToString("D8");
        } else {
          sBare = "S-O_" + sBare;
        }
        // sBare = "S-O_" + string.Format("00000000", ((int) sBare));
        String sDstBare = Path.GetFullPath(sDstDir + "/" + sBare);
       // String sFileTmp = Path.GetFullPath(sDstBare + ".xml");
        String sFileOut = Path.GetFullPath(sDstBare + ".folia.xml");
        String sFileOutZ = Path.GetFullPath(sDstBare + ".folia.xml.gz");

        // Check if output file already exists
        if (!bForce && File.Exists(sFileOutZ)) {
          debug("Skipping existing: " + sFileIn);
          return true;
        }

        // Other initialisations before the conversion starts
        OpsToFolia objOpsFolia = new OpsToFolia(errHandle);
        // objOpsFolia.setCurrentSrcFile(sFileTmp);
        objOpsFolia.setCurrentSrcFile("");
        int iSentNum = 0;
        // Set up an XML writer according to our liking
        XmlWriterSettings wrSet = new XmlWriterSettings();
        wrSet.Indent = true;
        wrSet.NewLineHandling = NewLineHandling.Replace;
        wrSet.NamespaceHandling = NamespaceHandling.OmitDuplicates;
        wrSet.ConformanceLevel = ConformanceLevel.Document;

        /*
        // DOuble check to see if the conversion was already done
        if (!bForce && File.Exists(sFileOut) && !File.Exists(sFileTmp)) return true;
        // If this is a different version, and we already have a copy, then skip it
        if (iVersion > 1 && File.Exists(sFileOut) && !File.Exists(sFileTmp)) return true;
         * */

        // Create a temporary folia output file with the bare essentials
        String sFileTmpF = sFileOut + ".tmp";
        // Bare folia text
        String sBareFolia = objOpsFolia.getIntro(sBare, sIdMovie, "", sMovieYear, iSubtitle, iVersion, iMax) +
          objOpsFolia.getExtro();
        // Write this text to a file
        XmlDocument pdxBareF = new XmlDocument();
        try {
          pdxBareF.LoadXml(sBareFolia);
        } catch (Exception ex) {
          errHandle.DoError("ConvertOneOpsToFolia/BareFolia problem", ex); // Provide standard error message
          return false;
        }
        // Adapt the moviename
        XmlNamespaceManager nsBare = new XmlNamespaceManager(pdxBareF.NameTable);
        nsBare.AddNamespace("b", pdxBareF.DocumentElement.NamespaceURI);
        XmlNode ndxMvName = pdxBareF.SelectSingleNode("./descendant::b:meta[@id='name']", nsBare);
        if (ndxMvName != null) ndxMvName.InnerText = sMovieName;
        pdxBareF.Save(sFileTmpF);
        // Don't need objOpsFolia anymore
        objOpsFolia = null;

        // Convert the temporary files to the output file
        // (1) Open the input file
        debug("Starting: " + sFileIn);

        // (2) Make sure we have a 'global' start and finish time
        String sTimeStart = ""; String sTimeEnd = "";
        String sLineSrt = "";   // Line number within the SRT file
        bool bStart = false;    // This word has the time-start marker
        bool bEnd = false;      // This word has the time-end marker

        // Determine the file name template we will be looking for
        String sFileInTemplate = sFileName.Replace("1of" + iMax, "*of" + iMax) + ".gz";
        // Get all the files fulfilling this template
        String[] arFileIn = Directory.GetFiles(Path.GetDirectoryName(sFileIn), sFileInTemplate, SearchOption.TopDirectoryOnly);
        // Make sure the files are sorted correctly
        List<String> lFileIn = arFileIn.ToList<String>();
        lFileIn.Sort();
        int iFile = 0;    // Number of the file from the [lFileIn] list

        // Skip those that do not have the correct number of parts
        if (iMax != lFileIn.Count) {
          debug("--Part/Count mismatch");
          return true;
        }

        // (3) Open the bare FoLiA file for input
        using (StreamReader rdFileTmpF = new StreamReader(sFileTmpF))
        using (XmlReader rdFolia = XmlReader.Create(rdFileTmpF))

        // (5) Create an output file 
        // using (StreamWriter wrFileTmpOut = new StreamWriter(sFileOut))
        //  using (wrFolia = XmlWriter.Create(wrFileTmpOut, wrSet)) {
        using (wrFolia = new FoliaXmlWriter(sFileOut, Encoding.UTF8)) {
  

          // (6) Walk through the bare folia input file
          while (!rdFolia.EOF && rdFolia.Read()) {
            // (7) Check what kind of input this is
            // Note: we need to pause after reading the <p> start element
            if (rdFolia.IsStartElement("p")) {
              // (8) Do make sure that we WRITE the start element away
              WriteShallowNode(rdFolia, wrFolia);

              // Check the index
              if (iFile<0 || iFile >= lFileIn.Count) {
                // Cannot do this
                errHandle.DoError("ConvertOneOpsToFolia", "iFile="+iFile+" but lFileIn="+lFileIn.Count);
              }
              // Retrieve the next input file from the list
              String sFileTmp = lFileIn[iFile].Replace(".xml.gz", "xml");
              // Unzip the file
              if (!util.General.DecompressFile(lFileIn[iFile], sFileTmp)) return false;

              // Increment the file number for the next run and reset the sentence number
              iFile += 1; iSentNum = 0;

              // (4) Open the open-subtitle xml file for input
              using (StreamReader rdFileTmp = new StreamReader(sFileTmp))
              using (XmlReader rdOps = XmlReader.Create(rdFileTmp)) {
                // (9) Walk through the *actual* open-subtitle input file
                while (!rdOps.EOF && rdOps.Read()) {
                  // (10) Check what this is
                  if (rdOps.IsStartElement("s")) {
                    // Read the <s> element as one string
                    String sWholeS = rdOps.ReadOuterXml();
                    iSentNum++;
                    // Process the <s> element:
                    // (1) put it into an XmlDocument
                    XmlDocument pdxSrc = new XmlDocument();
                    pdxSrc.LoadXml(sWholeS);
                    // (2) Create a namespace mapping for the opensubtitles *source* xml document
                    XmlNamespaceManager nsOps = new XmlNamespaceManager(pdxSrc.NameTable);
                    nsOps.AddNamespace("df", pdxSrc.DocumentElement.NamespaceURI);
                    // (3) Create a sentence identifier
                    String sSentId = sBare + ".p."+iFile+".s." + iSentNum;
                    // (4) The first <s> should at least have some <w> elements
                    if (iSentNum == 1) {
                      // Check out first sentence
                      if (pdxSrc.SelectSingleNode("./descendant::df:w", nsOps) == null) {
                        // Skip this file -- it contains RAW info, no <w> elements
                        return true;
                      }
                    }
                    // (5) Create a new sentence in the buffer
                    Sent oSent = new Sent(sSentId);
                    lSentBuf.Add(oSent);
                    // (6) Process all *descendants* of this <s> one-by-one
                    XmlNodeList ndChild = pdxSrc.SelectNodes("./descendant-or-self::df:s/descendant::df:*", nsOps);
                    int iWord = 0;
                    for (int i = 0; i < ndChild.Count; i++) {
                      // Get this child
                      XmlNode ndThis = ndChild.Item(i);
                      // Action depends on the type of child this is
                      switch (ndThis.Name) {
                        case "time":
                          // Get the time id and value
                          String sTimeId = ndThis.Attributes["id"].Value;
                          String sTimeVal = ndThis.Attributes["value"].Value;
                          // Is this the *start* or *end* time?
                          if (sTimeId.EndsWith("S")) {
                            // Keep track of the latest *start* time and reset the end time
                            sTimeStart = sTimeVal; sTimeEnd = ""; bStart = true;
                            // Get the SRT line number
                            sLineSrt = sTimeId.Substring(1, sTimeId.Length - 2);
                          } else {
                            // Add the end-time
                            sTimeEnd = sTimeVal;
                            // Reset the starting time
                            sTimeStart = "";
                            bEnd = true;
                            if (bEnd) {
                              // Reset flag
                              bEnd = false;
                              // Add mark for starting
                              lSentBuf[lSentBuf.Count - 1].markEnd();
                            }
                            // Process the sentence buffer
                            // (a) Process all previous sentences
                            int iCount = lSentBuf.Count;
                            if (!FlushOpsToFolia(ref lSentBuf, ref wrFolia, iCount - 1, sTimeEnd)) {
                              return false;
                            }
                            // (b) Process the words in the current sentence
                            List<WordBuf> lCurrent = lSentBuf[lSentBuf.Count - 1].words;
                            for (int j = 0; j < lCurrent.Count; j++) {
                              // Check if this word needs to have an end-time
                              if (lCurrent[j].e == "") lCurrent[j].e = sTimeEnd;
                            }
                          }
                          break;
                        case "i": // Some have an <i> tag to indicate what goes on one line: ignore
                          break;
                        case "w":
                          // Process this word
                          iWord++;
                          // Get the text of this word
                          String sWord = ndThis.InnerText;
                          // Create an identifier for this word
                          // String sWordId = sSentId + ".w." + iWord;
                          String sWordCl = (util.General.DoLike(sWord, ".|,|...|..|-|?|!|\"|'|:|;|(|)")) ? "Punct" : "Vern";
                          // Add this word to the buffer of this current sentence
                          lSentBuf[lSentBuf.Count - 1].addWord(sWord, sWordCl);
                          // Add the start-time to this word
                          lSentBuf[lSentBuf.Count - 1].addStart(sTimeStart);
                          // Add the SRT number to this word
                          lSentBuf[lSentBuf.Count - 1].addLine(sLineSrt);
                          if (bStart) {
                            // Reset flag
                            bStart = false;
                            // Add mark for starting
                            lSentBuf[lSentBuf.Count - 1].markStart();
                          }

                          break;
                        default:
                          // No idea what to do here -- just skip??
                          break;
                      }
                    }
                  }
                }   // End while
                // Process any remaining sentences
                if (!FlushOpsToFolia(ref lSentBuf, ref wrFolia, lSentBuf.Count, sTimeEnd)) {
                  return false;
                }
                rdOps.Close();    // rdOps.Dispose();
                rdFileTmp.Close();
                // Remove the temporary file
                File.Delete(sFileTmp);
              }

              // }
            } else {
              // Process reading the input
              WriteShallowNode(rdFolia, wrFolia);
            }
          }
          // debug("Point 3: locked = " + util.General.IsFileLocked(sFileTmp));
          wrFolia.Flush();
          wrFolia.Close();  // wrFolia.Dispose();
          rdFolia.Close();  // rdFolia.Dispose();
          rdFileTmpF.Close();
          // debug("Point 4: locked = " + util.General.IsFileLocked(sFileTmp));
        }
        
        // Remove the temporary Folia file
        File.Delete(sFileTmpF);

        // Convert to compressed-file
        if (!util.General.CompressFile(sFileOut, sFileOutZ)) return false;
        // Remove the original output
        File.Delete(sFileOut);

        // Return positively
        return true;
      } catch (Exception ex) {
        errHandle.DoError("ConvertOneOpsToFolia", ex); // Provide standard error message
        return false;
        throw;
      }
    }

    /// <summary>
    /// FlushOpsToFolia --
    ///     Write sentences 0 until iCount-1 from the @lSentBuf to wrFolia
    ///     Add the ending time whereever this is needed
    /// 
    /// </summary>
    /// <param name="lSentBuf"></param>
    /// <param name="wrFolia"></param>
    /// <param name="iCount"></param>
    /// <param name="sTimeEnd"></param>
    /// <returns></returns>
    private bool FlushOpsToFolia(ref List<Sent> lSentBuf, ref XmlWriter wrFolia, int iCount, String sTimeEnd) {
      try {
        // Validate
        if (iCount == 0) return true;
        // Walk all the sentences preceding the current one
        for (int j = 0; j < iCount; j++) {
          String sClause = ""; String sBegin = ""; String sEnd = "";

          // Retrieve this sentence's buffer
          Sent oSentThis = lSentBuf[j];
          // Validate: length of sentence buffer must be larger than 0
          if (oSentThis.words.Count == 0) continue;

          // Start producing output for this sentence
          XmlDocument pdxTmp = new XmlDocument();
          util.xmlTools oTool = new util.xmlTools(this.errHandle);
          oTool.SetXmlDocument(pdxTmp);
          XmlNode ndS = oTool.AddXmlChild(null, "s", 
            "xml:id", oSentThis.id, "attribute",
            "begintime", "", "attribute",
            "endtime", "", "attribute",
            "xmlns", "http://ilk.uvt.nl/folia", "attribute");

          // Process all the words in this sentence
          List<WordBuf> lSentWrds = oSentThis.words;
          for (int k = 0; k < lSentWrds.Count; k++) {
            if (lSentWrds[k].e == "") lSentWrds[k].e = sTimeEnd;
            XmlNode ndW = oTool.AddXmlChild(ndS, "w",
              "xml:id", oSentThis.id + ".w." + (k + 1), "attribute",
              "class", lSentWrds[k].c, "attribute",
              "t", lSentWrds[k].w, "child");
            // Build the clause
            if (sClause != "") sClause += " ";
            sClause += lSentWrds[k].w;
            if (lSentWrds[k].bStart) {
              // Adapt the begin
              sBegin = lSentWrds[k].s.Replace(',', '.');
              // Add the modification as feature to this node
              oTool.AddXmlChild(ndW, "feat", "subset", "begintime", "attribute", "class", sBegin, "attribute");
              oTool.AddXmlChild(ndW, "feat", "subset", "n", "attribute", "class", lSentWrds[k].n, "attribute");
            }
            if (lSentWrds[k].bEnd) {
              // Adapt the begin
              sEnd = lSentWrds[k].e.Replace(',', '.');
              // Add the modification as feature to this node
              oTool.AddXmlChild(ndW, "feat", "subset", "endtime", "attribute", "class", sEnd, "attribute");
            }
          }
          XmlNode ndT_nl = oTool.AddXmlChild(ndS, "t", "class", "nld", "attribute");
          ndT_nl.InnerText = sClause;
          /* 
          // Helaas wordt dit niet in dank afgenomen 
          XmlNode ndT_or = oTool.AddXmlChild(ndS, "t", "class", "original", "attribute");
          */
          // Add the @begintime and @endtime values for this sentence
          sBegin = lSentWrds[0].s.Replace(',', '.');
          sEnd = lSentWrds[lSentWrds.Count - 1].e.Replace(',', '.');
          if (sEnd == "") sEnd = sBegin;
          ndS.Attributes["begintime"].Value = sBegin;
          ndS.Attributes["endtime"].Value = sEnd;
          // The content of this sentence can now be output
          wrFolia.WriteNode(XmlReader.Create(new StringReader(pdxTmp.OuterXml)), true);
        }
        // Remove everything we flushed from the sentence buffer
        for (int j=iCount-1;j>=0;j--) {
          lSentBuf.RemoveAt(j);
        }

        // Return positively
        return true;
      } catch (Exception ex) {
        errHandle.DoError("FlushOpsToFolia", ex); // Provide standard error message
        return false;
      }
    }

    /// <summary>
    /// getMovieNameId --
    ///     Calculate the movie's name and the idmovie from the filename
    ///     
    /// </summary>
    /// <param name="sFileIn"></param>
    /// <param name="sMovieName"></param>
    /// <param name="sIdMovie"></param>
    /// <returns></returns>
    private bool getMovieNameId(String sFileIn, ref String sMovieName, ref String sIdMovie, ref String sMovieYear) {
      string[] arFileIn = null;

      try {
        // Convert the full name into an array
        sFileIn = sFileIn.Replace("\\", "/");
        arFileIn = sFileIn.Split(new string[] { "/" }, StringSplitOptions.None);
        // Validate
        if (arFileIn.Length < 3) return false;
        // The idmovie is the penultimate part
        sIdMovie = arFileIn[arFileIn.Length-2];
        // The year is even before this
        sMovieYear = arFileIn[arFileIn.Length - 3];
        // Find the movie's name through this id
        if (!bInit) return false;
        // Locate the movie's name
        if (dicMovie.ContainsKey(sIdMovie)) {
          dicMovie.TryGetValue(sIdMovie, out sMovieName);
        } else {
          sMovieName = "(name not found)";
        }

        // Return positively
        return true;
      } catch (Exception ex) {
        errHandle.DoError("getMovieNameId", ex); // Provide standard error message
        return false;
      }
    }

    /// <summary>
    /// loadMovieDictionary ---
    ///     Load the movie dictionary
    /// </summary>
    /// <param name="sFileIn"></param>
    /// <returns></returns>
    public bool loadMovieDictionary(String sFileIn) {
      try {
        // Validate
        if (!File.Exists(sFileIn)) return false;
        // Load the file into an array
        String[] arLine = File.ReadAllLines(sFileIn);
        this.dicMovie.Clear();
        for (int i = 0; i < arLine.Length; i++) {
          String sLine = arLine[i];
          if (sLine != "") {
            // Get this line
            String[] arPart = sLine.Split('\t');
            if (arPart.Length > 1) {
              // Get the id and the name
              String sIdMovie = arPart[0];
              String sMovieName = arPart[1];
              // Possibly take off leading and ending quotation marks
              if (sMovieName.StartsWith("\"") && sMovieName.EndsWith("\"")) {
                sMovieName = sMovieName.Substring(1, sMovieName.Length - 2);
              }
              // Does this item already exist?
              if (dicMovie.ContainsKey(sIdMovie)) {
                // Check if the movie's name is equal
                String sMovieStored = "";
                if (!dicMovie.TryGetValue(sIdMovie, out sMovieStored)) return false;
                if (sMovieStored != sMovieName) {
                  // No idea what to do now...
                  int j = 25;
                }
              } else {
                // Store it in the dictionary
                dicMovie.Add(sIdMovie, sMovieName);
              }
            }
          }
        }

        // Return positively
        bInit = true;
        return true;
      } catch (Exception ex) {
        errHandle.DoError("getMovieNameId", ex); // Provide standard error message
        return false;
      }
    }


    /// <summary>
    /// Write a debugging message on the console
    /// </summary>
    /// <param name="sMsg"></param>
    static void debug(String sMsg) { Console.WriteLine(sMsg); }

    /* -------------------------------------------------------------------------------------
      * Name:  WriteShallowNode
      * Goal:  Copy piece-by-piece
      * History:
      * 2/oct/2015 ERK Created
        ------------------------------------------------------------------------------------- */
    public void WriteShallowNode(XmlReader reader, XmlWriter writer) {
      if (reader == null) {
        throw new ArgumentNullException("reader");
      }
      if (writer == null) {
        throw new ArgumentNullException("writer");
      }
      try {
        switch (reader.NodeType) {
          case XmlNodeType.Element:
            writer.WriteStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);
            bool bIsFOLIA = (reader.LocalName == "FoLiA");
            // writer.WriteStartElement(reader.LocalName);
            // Process attributes one by one
            if (reader.HasAttributes) {
              if (reader.MoveToFirstAttribute()) {
                do {
                  if (reader.Name != "xmlns") {
                    // Check for FoLiA version
                    if (bIsFOLIA && reader.Name == "version") {
                      // Adapt version number
                      writer.WriteAttributeString(reader.Name, FOLIA_VERSION);
                    } else {
                      String[] arName = reader.Name.Split(':');
                      if (arName.Length > 1) {
                        writer.WriteAttributeString(arName[0], arName[1], null, reader.Value);

                      } else {
                        writer.WriteAttributeString(reader.Name, reader.Value);
                      }
                    }
                  }
                } while (reader.MoveToNextAttribute());
              }
            }

            // writer.WriteAttributes(reader, true);
            // writer.WriteAttributes(reader, false);
            if (reader.IsEmptyElement) {
              writer.WriteEndElement();
            }
            break;
          case XmlNodeType.Text:
            writer.WriteString(reader.Value);
            break;
          case XmlNodeType.Whitespace:
          case XmlNodeType.SignificantWhitespace:
            writer.WriteWhitespace(reader.Value);
            break;
          case XmlNodeType.CDATA:
            writer.WriteCData(reader.Value);
            break;
          case XmlNodeType.EntityReference:
            writer.WriteEntityRef(reader.Name);
            break;
          case XmlNodeType.XmlDeclaration:
          case XmlNodeType.ProcessingInstruction:
            writer.WriteProcessingInstruction(reader.Name, reader.Value);
            break;
          case XmlNodeType.DocumentType:
            writer.WriteDocType(reader.Name, reader.GetAttribute("PUBLIC"), reader.GetAttribute("SYSTEM"), reader.Value);
            break;
          case XmlNodeType.Comment:
            writer.WriteComment(reader.Value);
            break;
          case XmlNodeType.EndElement:
            writer.WriteFullEndElement();
            break;
        }
      } catch (Exception ex) {
        errHandle.DoError("WriteShallowNode", ex); // Provide standard error message
      }
    }

  }
  /// <summary>
  /// WordBuf: all the information for one particular word
  /// </summary>
  class WordBuf {
    public String w;  // Actual word
    public String s;  // Start time
    public String e;  // End time
    public String n;  // Line number of SRT file
    public String c;  // Class for this word
    public bool bStart; // Add start time here
    public bool bEnd;   // Add end time here
    public WordBuf(String sWord, String sClass) {
      this.w = sWord; this.c = sClass;
      s = ""; e = "";
    }
  }
  /// <summary>
  /// Sent: one sentence
  /// </summary>
  class Sent {
    public String id;
    public List<WordBuf> words;
    /// <summary>
    /// Class initializer
    /// </summary>
    /// <param name="iId"></param>
    public Sent(String iId) {
      // Store the sentence identifier
      this.id = iId;
      // Start a new buffer for words
      words = new List<WordBuf>();
    }
    public void addWord(String sWord, String sClass) {
      this.words.Add(new WordBuf(sWord, sClass));
    }
    public void addStart(String sStartTime) {
      // Validate
      if (this.words.Count == 0) return;    // Just skip if there is no space
      // Get the last word in this buffer
      WordBuf wLast = this.words[this.words.Count - 1];
      wLast.s = sStartTime;
    }
    public void addLine(String sLineSrt) {
      // Validate
      if (this.words.Count == 0) return;    // Just skip if there is no space
      // Get the last word in this buffer
      WordBuf wLast = this.words[this.words.Count - 1];
      wLast.n = sLineSrt;
    }
    public void markStart() {
      // Validate
      if (this.words.Count == 0) return;    // Just skip if there is no space
      // Get the last word in this buffer
      WordBuf wLast = this.words[this.words.Count - 1];
      wLast.bStart = true;
    }
    public void markEnd() {
      // Validate
      if (this.words.Count == 0) return;    // Just skip if there is no space
      // Get the last word in this buffer
      WordBuf wLast = this.words[this.words.Count - 1];
      wLast.bEnd = true;
    }
  }

}
