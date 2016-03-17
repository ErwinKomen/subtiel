using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using opsub;

namespace opsubeng {
  class engConv {
    // ================================ LOCAL VARIABLES ===========================================
    private ErrHandle errHandle = null;         // Error handler of calling
    private opsub.util.xmlTools oTools = null;  // 
    private String sDirDutch = "";              // Dutch input directory
    private String sDirEnglish = "";            // English input directory
    private String sDirOutput = "";             // Output directory
    private List<subTrans> lstEn = null;        // List of english translations per <s> element
    private List<subTrans> lstNl = null;        // List of dutch translations per <s> element
    private XmlDocument pdxNL = null;
    private XmlDocument pdxEN = null;
    private XmlNamespaceManager nsNL = null;
    private XmlNamespaceManager nsEN = null;
    private XmlReaderSettings setThis = null; // Settings to read the document
    private XmlWriterSettings setWr = null;   // Writer settings
    private List<String> lstEnGZ = null;
    private List<String> lstNlGZ = null;
    // ==================== CLASS INITIALISATION ==================================================
    public engConv(ErrHandle oErr) {
      this.errHandle = oErr;
      this.oTools = new opsub.util.xmlTools(oErr);
      pdxEN = new XmlDocument();
      pdxNL = new XmlDocument();
      lstEn = new List<subTrans>();
      lstNl = new List<subTrans>();
      // Create correct reader settings
      setThis = new XmlReaderSettings();
      setThis.DtdProcessing = DtdProcessing.Ignore;
      setThis.IgnoreComments = true;
      // Create good writer settings
      setWr = new XmlWriterSettings();
      setWr.Indent = true;
      setWr.CloseOutput = true;
      setWr.NamespaceHandling = NamespaceHandling.OmitDuplicates;
    }
    // ==================== GET/SET routines ======================================================
    public String dutch { set { sDirDutch = value; } }
    public String english { set { sDirEnglish = value; } }
    public String output {
      set {
        sDirOutput = value;
        if (!sDirOutput.EndsWith("/") && !sDirOutput.EndsWith("\\"))
          sDirOutput = sDirOutput + "/";
      }
    }
    // ============================================================================================

    /* -------------------------------------------------------------------------------------
     * Name:        ConvertOneEngToFolia
     * Goal:        Create a .folia.xml file with metadata for the indicated input file
     *              Note: the .cmdi file will be placed in the same directory as FileIn
     * Parameters:  sFileIn     - File to be processed
     *              bForce      - Create result, even though it exists already
     *              bIsDebug    - Debugging mode on or off
      * Note:        This is the XmlReader/XmlWriter implementation that works very fast
      *                under both Windows and Linux/MONO.
      *
     * History:
     * 1/feb/2016 ERK Created XmlDocument/XPath version
     * 7/mar/2016 ERK Started conversion to XmlReader version
       ------------------------------------------------------------------------------------- */
    public bool ConvertOneEngToFolia(XmlReader rdLinkGrp, bool bForce, bool bDebug) {
      String sFileCmdi = "";      // A .cmdi.xml file belonging to the Dutch .folia.xml file
      String sSubtitleIdNL = "";  // Dutch   open-subtitles subtitle id
      String sSubtitleIdEN = "";  // English open-subtitles subtitle id
      String sFileNL = "";        // Dutch subtitle .folia.xml file
      String sFileEn = "";        // English .folia.xml output file
      String sFileTr = "";        // A .folia.xml file that contains the translation in <complexalignments>
      String sFileTrGz = "";      // Translation file, but zipped
      String sName = "";          // Name we are currently dealing with

      try {
        // Validate
        if (rdLinkGrp == null) { errHandle.DoError("ConvertOneEngToFolia", "No linkGrp"); return false; }
        if (!Directory.Exists(sDirOutput)) { errHandle.DoError("ConvertOneEngToFolia", "No output directory"); return false; }

        // (1) Get the open subtitle id's
        sSubtitleIdNL = getMovieId(rdLinkGrp.GetAttribute("toDoc"));
        sSubtitleIdEN = getMovieId(rdLinkGrp.GetAttribute("fromDoc"));
        errHandle.Status("EngConv [" + sSubtitleIdEN + "/" + sSubtitleIdNL + "]");
        // ====================== DEBUG ==============
        if (sSubtitleIdNL == "23233") {
          int i = 0;
        }
        // ===========================================

        // (2) Find the Dutch .folia.xml.gz file (converted FoLiA format)
        sFileNL = getDutchFolia(sSubtitleIdNL);
        String sGzNL = getXmlGzFile(sFileNL, "nl");
        // If we don't have this input, then return empty-handed
        if (sGzNL == "") {
          errHandle.Status("EngConv 1: cannot find Dutch " + Path.GetFileNameWithoutExtension(sFileNL));
          return true;
        }

        // (3) Find the English .folia.xml.gz file (converted FoLiA format)
        sFileEn = getDutchFolia(sSubtitleIdEN);
        String sGzEN = getXmlGzFile(sFileEn, "en");
        // If we don't have this input, then return empty-handed
        if (sGzEN == "") {
          errHandle.Status("EngConv 2: cannot find English " + Path.GetFileNameWithoutExtension(sFileEn));
          return true;
        }

        sName = sFileEn.Replace(".folia.xml", "");
        String sNameNL = sFileNL.Replace(".folia.xml", "");

        // (4) Show what we are doing
        errHandle.Status("Process parallel between " + Path.GetFileNameWithoutExtension(sFileEn) + 
          " and " + Path.GetFileNameWithoutExtension(sFileNL));

        // (5a) Re-initialize the lists of translations
        lstNl.Clear(); lstEn.Clear();

        // (5) Read the Dutch and the English folia.xml files (unpack + read)
        String sYear = "";
        if (!getTranslation(sGzNL, lstNl, ref sYear, true)) {errHandle.DoError("ConvertOneEngToFolia", "getTranslation NL problem"); return false;}
        if (!getTranslation(sGzEN, lstEn, ref sYear, false)) { errHandle.DoError("ConvertOneEngToFolia", "getTranslation EN problem"); return false; }

        // (6) Determine name of output folia.xml file
        String sDir = sDirOutput + sYear;
        if (!Directory.Exists(sDir)) Directory.CreateDirectory(sDir);
        sFileTr = sDir + "/S-OP_" + Convert.ToInt32(sSubtitleIdNL).ToString("D8") + ".folia.xml";
        sFileTrGz = sFileTr + ".gz";

        // (7) Check existence - skip if needed
        if (!bForce && (File.Exists(sFileTr) || File.Exists(sFileTrGz))) return true;

        // (7b) set the parallel at the first child node
        while (!rdLinkGrp.EOF && !rdLinkGrp.IsStartElement("link")) { rdLinkGrp.Read(); }
        // (8) Start reading the English and re-writing as translation FoLiA
        //         using (FoliaXmlWriter wrEN = new FoliaXmlWriter(sFileTr, Encoding.UTF8))
        //         using (XmlWriter wrEN = XmlWriter.Create(sFileTr, setWr)) {
        String sFileEng = sGzEN.Replace(".gz", "");
        using (StreamReader rdFileIn = new StreamReader(sFileEng))
        using (XmlReader rdFileXml = XmlReader.Create(rdFileIn, setThis))
        using (FoliaXmlWriter wrEN = new FoliaXmlWriter(sFileTr, Encoding.UTF8)) {
          // Read through the preamble of the English FoLiA and add my own annotation tag
          bool bAnnotDone = false;
          bool bMetaDone = false;
          while (!rdFileXml.EOF && (!bAnnotDone || !bMetaDone) && rdFileXml.Read()) {
            if (rdFileXml.IsStartElement("annotations")) {
              // Copy the <annotations> starting node content
              oTools.WriteShallowNode(rdFileXml, wrEN);
              // Read until (but not including) the end element
              while (!rdFileXml.EOF && !bAnnotDone && rdFileXml.Read()) {
                if (rdFileXml.NodeType == XmlNodeType.EndElement && rdFileXml.Name == "annotations") {
                  // Insert my own annotation comment here
                  XmlDocument pdxTmp = new XmlDocument();
                  oTools.SetXmlDocument(pdxTmp, "http://ilk.uvt.nl/folia");
                  oTools.AddXmlChild(null, "alignment-annotation",
                    "set", "trans", "attribute",
                    "annotator", "opsubeng", "attribute",
                    "annotatortype", "auto", "attribute",
                    "", "", "text");
                  wrEN.WriteNode(XmlReader.Create(new StringReader(pdxTmp.OuterXml)), true);
                  // Copy the <annotations> end node content
                  oTools.WriteShallowNode(rdFileXml, wrEN);
                  // Tell the world to continue
                  bAnnotDone = true;
                } else {
                  // Copy the <annotations> starting node content
                  oTools.WriteShallowNode(rdFileXml, wrEN);
                }
              }
            } else {
              // Test for metadata end
              if (rdFileXml.NodeType == XmlNodeType.EndElement && rdFileXml.Name == "metadata") bMetaDone = true;
              // Copy the content to the xml-writer output
              oTools.WriteShallowNode(rdFileXml, wrEN);
            }
          }
          // Double check: has annotation been processed
          if (!bAnnotDone || !bMetaDone) {
            errHandle.Status("ConvertOneEngToFolia problem: annotation has not been processed correctly: "+bAnnotDone+" "+bMetaDone);
            return false;
          }
          // Read and write until having finished reading the first <p> element
          bool bPstarted = false; 
          while (!rdFileXml.EOF && !bPstarted && rdFileXml.Read()) {
            if (rdFileXml.IsStartElement("p")) bPstarted = true;
            // Copy the content to the xml-writer output
            oTools.WriteShallowNode(rdFileXml, wrEN);
          }
          //
          bool bHaveLink = rdLinkGrp.IsStartElement("link");
          bool bPfinish = false;
          // Loop through all <link> children of <linkGrp>
          while (!rdLinkGrp.EOF && bHaveLink && !rdFileXml.EOF) {
            // Read the parameters for this English-Dutch match
            String sXtargets = rdLinkGrp.GetAttribute("xtargets");
            // Be prepared: skip until the next start element (if there is any!!)
            while (!rdLinkGrp.EOF && rdLinkGrp.Read() && rdLinkGrp.NodeType != XmlNodeType.Element);
            // Set a flag to signal that the next one will be LINK
            bHaveLink = (!rdLinkGrp.EOF && rdLinkGrp.NodeType == XmlNodeType.Element && rdLinkGrp.Name == "link");
            // errHandle.Status("Processing: [" + sXtargets + "]");
            String[] arTargets = sXtargets.Split(';');
            if (arTargets.Length ==2) {
              // make arrays of the English source and Dutch destination
              String[] arSrcEN = arTargets[0].Trim().Split(' ');
              String[] arDstNL = arTargets[1].Trim().Split(' ');
              // Do we have an English source?
              if (arSrcEN.Length == 0 || arSrcEN[0] == "") {
                // There is no English source sentence specification
                // This means we can put the alignment anywhere
                // Get the appropriate translation code
                String sEnNl = "";
                if (!getTranslationCode(arSrcEN, arDstNL, sName, sNameNL, sFileNL, ref sEnNl)) {
                  errHandle.DoError("engConv/ConvertOneEngToFolia 0x0010", "No translation code");
                  return false;
                }
                try {
                  wrEN.WriteNode(XmlReader.Create(new StringReader(sEnNl), setThis), true);
                } catch (Exception ex) {
                  errHandle.DoError("engConv/ConvertOneEngToFolia 0x0011", ex);
                  return false;
                }
              } else {
                // Find the English source node
                String sIdEN = sName + ".p.1.s." + arSrcEN[arSrcEN.Length - 1];
                bool bCorrectS = false; 
                // Read the English source until we reach this <s> node
                while (!rdFileXml.EOF && !bCorrectS && rdFileXml.Read()) {
                  // (2) Check the input element
                  if (rdFileXml.IsStartElement("s")) {
                    // Copy the <s> starting node content
                    oTools.WriteShallowNode(rdFileXml, wrEN);
                    // Check the ID of this element
                    String sId = rdFileXml.GetAttribute("xml:id");
                    // Check if this is the correct node and if we are able to PLACE it at this point
                    if (sId == sIdEN ) {
                      bCorrectS = true;   // Found the correct node
                      // Can we place it here?
                      if (bPfinish) {
                        // Unable to place the node here
                        errHandle.Status("Unable to output English id=" + sIdEN);
                      } else {
                        // Read <s> content until having processed the end-element
                        bool bEndOfS = false;
                        while (!rdFileXml.EOF && !bEndOfS && rdFileXml.Read()) {
                          // Make sure we skip the <w> elements
                          if (rdFileXml.IsStartElement("w")) {
                            // The <w> elements need to be skipped, so read through until the matching end-element
                            // rdFileXml.ReadToNextSibling("w");
                            rdFileXml.ReadOuterXml();
                          } else if (rdFileXml.NodeType == XmlNodeType.EndElement && rdFileXml.Name == "s") {
                            // Copy the output node
                            oTools.WriteShallowNode(rdFileXml, wrEN);
                            // Get the appropriate translation code
                            String sEnNl = "";
                            if (!getTranslationCode(arSrcEN, arDstNL, sName, sNameNL, sFileNL, ref sEnNl)) {
                              errHandle.DoError("ConvertOneEngToFolia", "getTranslation EN problem"); return false;
                            }
                            try {
                              wrEN.WriteNode(XmlReader.Create(new StringReader(sEnNl), setThis), true);
                            } catch (Exception ex) {
                              errHandle.DoError("engConv/ConvertOneEngToFolia 0x0012", ex);
                              return false;
                            }
                            // Signal this is the end of the s
                            bEndOfS = true;
                          } else {
                            // Copy input to output
                            oTools.WriteShallowNode(rdFileXml, wrEN);
                          }
                        }
                      }

                      int iStop = 1;
                    }
                  } else if (rdFileXml.IsStartElement("w")) {
                    // The <w> elements need to be skipped, so read through until the matching end-element
                    rdFileXml.ReadOuterXml();
                  } else if (rdFileXml.NodeType == XmlNodeType.EndElement && rdFileXml.Name == "p") {
                    // This is an ending </p>
                    // (1) Is a <link> still following?
                    if (bHaveLink) {
                      // Process links until we are through...
                      int iE = 3;
                    }
                    // (1) write it out
                    oTools.WriteShallowNode(rdFileXml, wrEN);
                    // (2) Warn the while-loop that this is a finishing </p> element
                    bPfinish = true;
                  } else {
                    // If there is another opening <p> we can reset the <p> finish
                    if (rdFileXml.IsStartElement("p")) bPfinish = false;
                    // Copy the content to the xml-writer output
                    oTools.WriteShallowNode(rdFileXml, wrEN);
                  }
                }
                // Check if </p> has been found
                if (bPfinish) {

                }
                // Check whether we found the correct S or not
                if (!bCorrectS) {
                  // Did not find the correct S -- skip through
                  int iMissed = 1;
                  errHandle.Status("Missing node "+ sIdEN);
                }
              }
            }
            // The next <link> is retrieved at the start of this loop
          }
        }
        // Zip the resulting .folia.xml file
        if (opsub.util.General.CompressFile(sFileTr, sFileTrGz)) {
          // If zipping was successful, then delete the file
          File.Delete(sFileTr);
        }

        return true;
      } catch (Exception ex) {
        errHandle.DoError("engConv/ConvertOneEngToFolia", ex);
        return false;
      }
    }

    /// <summary>
    /// getTranslationCode -- Produce translation code for the specified input
    /// </summary>
    /// <param name="sXmlOut"></param>
    /// <returns></returns>
    private bool getTranslationCode(String[] arSrcEN, String[] arDstNL, String sName, String sNameNL, String sFileNL, ref String sXmlOut) {
      XmlDocument pdxTmp = new XmlDocument();
      try {
        // Add the translation here
        oTools.SetXmlDocument(pdxTmp, "http://ilk.uvt.nl/folia");
        // oTools.SetXmlDocument(pdxTmp);
        // First create a <complexalignments>  node
        XmlNode ndxCmpAlignS = oTools.AddXmlChild(null, "complexalignments");
        // Add a <complexalignment> child under the <complexalignments>
        XmlNode ndxCmpAlign = oTools.AddXmlChild(ndxCmpAlignS, "complexalignment");
        // Add the English source references
        XmlNode ndxAlignEN = oTools.AddXmlChild(ndxCmpAlign, "alignment",
          "set", "trans", "attribute",
          "class", "eng", "attribute",
          "", "", "text");
        // Do we have an English source?
        if (arSrcEN.Length > 0 && arSrcEN[0] != "") {
          // Walk the array of source references
          for (int i = 0; i < arSrcEN.Length; i++) {
            // Find the text for this element
            String sIdEN = sName + ".p.1.s." + arSrcEN[i];
            // Get the text belonging to this English node
            String sTextEN = getSubTransText(sIdEN, this.lstEn);
            // Add the <aref> element
            oTools.AddXmlChild(ndxAlignEN, "aref",
              // "id", sName + ".p.1.s." + arSrcEN[i], "attribute",
              "id", sIdEN, "attribute",
              "t", sTextEN, "attribute",
              "type", "s", "attribute");
          }
        }
        // Add the Dutch translation references
        XmlNode ndxAlignNL = oTools.AddXmlChild(ndxCmpAlign, "alignment",
          "set", "trans", "attribute",
          "class", "nld", "attribute",
          "xlink:href", sFileNL, "attribute",
          "xlink:type", "simple", "attribute",
          "", "", "text");
        // Do we have a Dutch translation?
        if (arDstNL.Length > 0 && arDstNL[0] != "") {
          // Walk the array of destination references
          for (int i = 0; i < arDstNL.Length; i++) {
            // Find the text for this element
            String sIdNL = sNameNL + ".p.1.s." + arDstNL[i];
            // Get the text belonging to this Dutch node
            String sTextNL = getSubTransText(sIdNL, this.lstNl);
            // Add the <aref> element
            oTools.AddXmlChild(ndxAlignNL, "aref",
              "id", sIdNL, "attribute",
              // "id", sNameNL + ".p.1.s." + arDstNL[i], "attribute",
              "t", sTextNL, "attribute",
              "type", "s", "attribute");
          }
        }

        sXmlOut = pdxTmp.OuterXml;
        return true;
      } catch (Exception ex) {
        errHandle.DoError("engConv/getTranslationCode", ex);
        return false;
      }
    }

    /* -------------------------------------------------------------------------------------
      * Name:        ConvertOneEngToFolia_ORG
      * Goal:        Create a .folia.xml file with metadata for the indicated input file
      *              Note: the .cmdi file will be placed in the same directory as FileIn
      * Note:        This is the XmlDocument/XPath implementation that works very fast
      *                under Windows. It runs extremely slow under MONO, however.
      *
      * Parameters:  sFileIn     - File to be processed
      *              bForce      - Create result, even though it exists already
      *              bIsDebug    - Debugging mode on or off
      * History:
      * 1/feb/2016 ERK Created
        ------------------------------------------------------------------------------------- */
    public bool ConvertOneEngToFolia_ORG(XmlNode ndxLinkGrp, bool bForce, bool bDebug) {
      String sFileCmdi = "";      // A .cmdi.xml file belonging to the Dutch .folia.xml file
      String sSubtitleIdNL = "";  // Dutch   open-subtitles subtitle id
      String sSubtitleIdEN = "";  // English open-subtitles subtitle id
      String sFileNL = "";        // Dutch subtitle .folia.xml file
      String sFileEn = "";        // English .folia.xml output file
      String sFileTr = "";        // A .folia.xml file that contains the translation in <complexalignments>
      String sName = "";          // Name we are currently dealing with

      try {
        // Validate
        if (ndxLinkGrp == null) return false;
        if (!Directory.Exists(sDirOutput)) { errHandle.DoError("ConvertOneEngToFolia", "No output directory"); return false; }
        // if (!File.Exists(sFileCmdi)) return false;

        // (1) Get the open subtitle id's
        sSubtitleIdNL = getMovieId(ndxLinkGrp.Attributes["toDoc"].Value);
        sSubtitleIdEN = getMovieId(ndxLinkGrp.Attributes["fromDoc"].Value);

        // (2) Find the Dutch .folia.xml.gz file (converted FoLiA format)
        sFileNL = getDutchFolia(sSubtitleIdNL);
        String[] arNL = Directory.GetFiles(sDirDutch, sFileNL + ".gz", SearchOption.AllDirectories);
        // If we don't have this input, then return empty-handed
        if (arNL.Length == 0) {
          errHandle.Status("EngConv 1: cannot find Dutch " + Path.GetFileNameWithoutExtension(sFileNL));
          return true;
        }

        // (3) Find the English .folia.xml.gz file (converted FoLiA format)
        sFileEn = getDutchFolia(sSubtitleIdEN);
        String[] arEN = Directory.GetFiles(sDirEnglish, sFileEn + ".gz", SearchOption.AllDirectories);
        // If we don't have this input, then return empty-handed
        if (arEN.Length == 0) {
          errHandle.Status("EngConv 2: cannot find English " + Path.GetFileNameWithoutExtension(sFileEn));
          return true;
        }

        sName = sFileEn.Replace(".folia.xml", "");
        String sNameNL = sFileNL.Replace(".folia.xml", "");

        // (4) Show what we are doing
        errHandle.Status("Process parallel between " + Path.GetFileNameWithoutExtension(sFileEn) +
          " and " + Path.GetFileNameWithoutExtension(sFileNL));

        // (5) Read the Dutch and the English folia.xml files (unpack + read)
        if (!getXmlDoc(arNL[0], ref pdxNL, ref nsNL)) return false;
        if (!getXmlDoc(arEN[0], ref pdxEN, ref nsEN)) return false;

        // errHandle.Status("engConv #3");

        // (6) Determine name of output folia.xml file
        String sYear = pdxNL.SelectSingleNode("./descendant::f:meta[@id='year']", nsNL).InnerText;
        String sDir = sDirOutput + sYear;
        if (!Directory.Exists(sDir)) Directory.CreateDirectory(sDir);
        sFileTr = sDir + "/S-OP_" + Convert.ToInt32(sSubtitleIdNL).ToString("D8") + ".folia.xml";

        // errHandle.Status("engConv #4");

        // (7) Check existence - skip if needed
        if (!bForce && File.Exists(sFileTr)) return true;

        // (8) Adapt the English input XmlDocument by doing away with all the superfluous <w> elements
        XmlNodeList ndxWlist = pdxEN.SelectNodes("./descendant::f:w", nsEN);
        int iRemoved = 0;
        for (int i = ndxWlist.Count - 1; i >= 0; i--) {
          // Remove this node
          XmlNode ndxDel = ndxWlist[i];
          XmlNode ndxParent = ndxDel.ParentNode;
          // Delete the one we selected
          ndxDel.RemoveAll();
          ndxParent.RemoveChild(ndxDel);
          iRemoved++;
        }
        // errHandle.Status("engConv #5");

        // Remove the "xmlns" attributes where they are not needed
        XmlNodeList ndxSlist = pdxEN.SelectNodes("./descendant::f:s", nsEN);
        for (int i = 0; i < ndxSlist.Count; i++) {
          XmlAttribute ndxToBeRemoved = ndxSlist[i].Attributes["xmlns"];
          if (ndxToBeRemoved != null) {
            ndxSlist[i].Attributes.Remove(ndxToBeRemoved);
          }
        }
        // errHandle.Status("engConv #6");
        // Make sure oTools is set correctly
        oTools.SetXmlDocument(pdxEN, "http://ilk.uvt.nl/folia");
        // Add an 'annotations' element
        XmlNode ndxAnnot = pdxEN.SelectSingleNode("./descendant::f:annotations", nsEN);
        if (ndxAnnot != null) {
          // Add my own
          oTools.AddXmlChild(ndxAnnot, "alignment-annotation",
            "set", "trans", "attribute",
            "annotator", "opsubeng", "attribute",
            "annotatortype", "auto", "attribute",
            "", "", "text");
        }
        // errHandle.Status("engConv #7");

        // (9) Walk through the Parallels
        XmlNode ndxParallel = ndxLinkGrp.SelectSingleNode("./child::link");
        XmlNode ndxCmpAlignS = null;
        XmlNode ndxSentEn = null;     // Last accessed English sentence
        XmlNode ndxSentNl = null;     // last accessed Dutch sentence
        while (ndxParallel != null) {
          // Get the information from this link
          String sXtargets = ndxParallel.Attributes["xtargets"].Value;

          // errHandle.Status("engConv #8: " + sXtargets);

          String[] arTargets = sXtargets.Split(';');
          if (arTargets.Length == 2) {
            // make arrays of the English source and Dutch destination
            String[] arSrcEN = arTargets[0].Trim().Split(' ');
            String[] arDstNL = arTargets[1].Trim().Split(' ');
            // Do we have an English source?
            if (arSrcEN.Length == 0 || arSrcEN[0] == "") {
              // Is there a previous <complexalignments> node?
              if (ndxCmpAlignS == null) {
                // Create the first complexalignments node right after the first <p>
                ndxCmpAlignS = oTools.AddXmlChild(pdxEN.SelectSingleNode("./descendant::f:p", nsEN), "complexalignment");
              }
            } else {
              // Find the English source node
              String sIdEN = sName + ".p.1.s." + arSrcEN[arSrcEN.Length - 1];
              // Get the node with this text - look forward
              if (!getNextS(ref pdxEN, ref ndxSentEn, sIdEN, nsEN, true)) return false;
              // XmlNode ndxSrcEN = pdxEN.SelectSingleNode("./following::f:s[@xml:id = '" + sIdEN + "']", nsEN);
              // Start adding the complex alignment here
              ndxCmpAlignS = oTools.AddXmlSibling(ndxSentEn, "complexalignments");
            }
            // Add a <complexalignment> child under the <complexalignments>
            XmlNode ndxCmpAlign = oTools.AddXmlChild(ndxCmpAlignS, "complexalignment");
            // Add the English source references
            XmlNode ndxAlignEN = oTools.AddXmlChild(ndxCmpAlign, "alignment",
              "set", "trans", "attribute",
              "class", "en", "attribute");
            // Do we have an English source?
            if (arSrcEN.Length > 0 && arSrcEN[0] != "") {
              // Walk the array of source references
              for (int i = 0; i < arSrcEN.Length; i++) {
                // Find the text for this element
                String sIdEN = sName + ".p.1.s." + arSrcEN[i];
                // Get the node with this text -- look backward for the first node, and forward for the next one
                if (!getNextS(ref pdxEN, ref ndxSentEn, sIdEN, nsEN, (i > 0))) return false;
                if (ndxSentEn != null) {
                  String sTextEN = ndxSentEn.SelectSingleNode("./child::f:t", nsEN).InnerText;
                  // Add the <aref> element
                  oTools.AddXmlChild(ndxAlignEN, "aref",
                    "id", sName + ".p.1.s." + arSrcEN[i], "attribute",
                    "t", sTextEN, "attribute",
                    "type", "s", "attribute");
                }
              }
            }
            // Add the Dutch translation references
            XmlNode ndxAlignNL = oTools.AddXmlChild(ndxCmpAlign, "alignment",
              "set", "trans", "attribute",
              "class", "nld", "attribute",
              "xlink:href", sFileNL, "attribute",
              "xlink:type", "simple", "attribute");
            // Do we have a Dutch translation?
            if (arDstNL.Length > 0 && arDstNL[0] != "") {
              // Walk the array of destination references
              for (int i = 0; i < arDstNL.Length; i++) {
                // Find the text for this element
                String sIdNL = sNameNL + ".p.1.s." + arDstNL[i];
                // Get the node with this text - keep looking forward
                if (!getNextS(ref pdxNL, ref ndxSentNl, sIdNL, nsNL, true)) return false;
                if (ndxSentNl != null) {
                  String sTextNL = ndxSentNl.SelectSingleNode("./child::f:t", nsNL).InnerText;
                  // Add the <aref> element
                  oTools.AddXmlChild(ndxAlignNL, "aref",
                    "id", sName + ".p.1.s." + arDstNL[i], "attribute",
                    "t", sTextNL, "attribute",
                    "type", "s", "attribute");

                }
              }
            }
          }

          // Go to the next link
          ndxParallel = ndxParallel.SelectSingleNode("./following-sibling::link");
        }



        /*
        // (7) Create an appropriate XML writer
        XmlWriterSettings settings = new XmlWriterSettings();
        settings.Indent = true;
        settings.CloseOutput = true;
        XmlWriter wrEN = XmlWriter.Create(sFileTr, settings);
        // (?) Save the new output file
        // pdxEN.Save(sFileTr);
        pdxEN.Save(wrEN);
        wrEN.Close();
        */
        pdxEN.Save(sFileTr);

        return true;
      } catch (Exception ex) {
        errHandle.DoError("engConv/ConvertOneEngToFolia_ORG", ex);
        return false;
      }
    }

    /// <summary>
    /// getNextS - Find the next 's' element satisfying conditions
    /// </summary>
    /// <param name="pdxThis"></param>
    /// <param name="ndxThis"></param>
    /// <param name="sId"></param>
    /// <param name="nsThis"></param>
    /// <param name="bForward"></param>
    /// <returns></returns>
    private bool getNextS(ref XmlDocument pdxThis, ref XmlNode ndxThis, String sId, XmlNamespaceManager nsThis, bool bForward) {
      XmlNode ndxPrev = null;

      try {
        if (ndxThis == null) {
          // First one is just descendant
          ndxThis = pdxThis.SelectSingleNode("./descendant::f:s[@xml:id='" + sId + "']", nsThis);
        } else {
          // Following one...
          if (ndxThis.Attributes["xml:id"].Value != sId) {
            ndxPrev = ndxThis;
            if (bForward) {
              ndxThis = ndxThis.SelectSingleNode("./following::f:s[@xml:id='" + sId + "']", nsThis);
            } else {
              ndxThis = ndxThis.SelectSingleNode("./preceding::f:s[@xml:id='" + sId + "']", nsThis);
            }
            // Double check if 'following' worked...
            if (ndxThis == null)
              ndxThis = pdxThis.SelectSingleNode("./descendant::f:s[@xml:id='" + sId + "']", nsThis);
          }
        }

        return true;
      } catch (Exception ex) {
        errHandle.DoError("engConv/ConvertOneEngToFolia", ex);
        return false;
      }
    }

    /// <summary>
    /// getXmlDoc -- Turn a .xml.gz file into an XmlDocument with its accompanying namespacemanager
    /// </summary>
    /// <param name="sFileGZ"></param>
    /// <param name="pdxThis"></param>
    /// <param name="nsThis"></param>
    /// <returns></returns>
    private bool getXmlDoc(String sFileGZ, ref XmlDocument pdxThis, ref XmlNamespaceManager nsThis) {
      try {
        String sFileTmp = sFileGZ.Replace(".xml.gz", ".xml");
        if (!opsub.util.General.DecompressFile(sFileGZ, sFileTmp)) return false;
        if (pdxThis == null) pdxThis = new XmlDocument();
        pdxThis.Load(sFileTmp);
        nsThis = new XmlNamespaceManager(pdxThis.NameTable);
        nsThis.AddNamespace("f", pdxThis.DocumentElement.NamespaceURI);
        // Remove the temporary file
        File.Delete(sFileTmp);
        return true;
      } catch (Exception ex) {
        errHandle.DoError("engConv/getXmlDoc", ex);
        return false;
      }
    }

    /// <summary>
    /// getTranslation -- Retrieve the translation from the XML document
    /// </summary>
    /// <param name="sFileGZ"></param>
    /// <param name="lstThis"></param>
    /// <param name="bCleanUp"></param>
    /// <returns></returns>
    private bool getTranslation(String sFileGZ, List<subTrans> lstThis, ref String sYear, bool bCleanUp) {
      try {
        String sFileTmp = sFileGZ.Replace(".xml.gz", ".xml");
        if (!opsub.util.General.DecompressFile(sFileGZ, sFileTmp)) return false;
        // Start reading using an XmlReader
        using (StreamReader rdFileIn = new StreamReader(sFileTmp))
        using (XmlReader rdFileXml = XmlReader.Create(rdFileIn, setThis)) {
          // (1) Walk through the folia input file
          while (!rdFileXml.EOF && rdFileXml.Read()) {
            // (2) Check the input element
            if (rdFileXml.IsStartElement("s")) {
              // Get the id attribute
              String sId = rdFileXml.GetAttribute("xml:id");
              // Read through until finding another start element -- that is the child
              bool bFound = false;    // Flag to see whether we have the <t> node
              while (!rdFileXml.EOF && !bFound) {
                // Read
                rdFileXml.Read();
                // Check if this is a 'w' or a 't'
                if (rdFileXml.IsStartElement("w")) {
                  // Read sibling <t>
                  if (!rdFileXml.EOF && rdFileXml.ReadToNextSibling("t")) {
                    bFound = true;
                  }
                } else if (rdFileXml.IsStartElement("t")) {
                  // Got the <t> child of <s> immediately
                  bFound = true;
                }
                // Do we have the <t> node?
                if (bFound) {
                  // Get the inner text of the <t> element
                  while (!rdFileXml.EOF && rdFileXml.NodeType != XmlNodeType.Text) {
                    rdFileXml.Read();
                  }
                  String sT = rdFileXml.Value;
                  // Add the combination [id,text]
                  lstThis.Add(new subTrans(sId, sT));
                }
              }
            } else if (rdFileXml.IsStartElement("meta")) {
              // Check if this gets us the year
              if (rdFileXml.GetAttribute("id") == "year") {
                // Read the inner text
                while (!rdFileXml.EOF && rdFileXml.NodeType != XmlNodeType.Text) {
                  rdFileXml.Read();
                }
                sYear = rdFileXml.Value;
              }
            }
          }
        }

          if (bCleanUp) {
          // Remove the temporary file
          File.Delete(sFileTmp);
        }
        return true;
      } catch (Exception ex) {
        errHandle.DoError("engConv/getXmlDoc", ex);
        return false;
      }
    }

    /// <summary>
    /// getMovieId -- retrieve the movie id number from the given string
    /// </summary>
    /// <param name="sPath"></param>
    /// <returns></returns>
    private String getMovieId(String sPath) {
      String sBack = "";

      try {
        // Start by taking the last part of the path
        sBack = Path.GetFileName(sPath);

        /* // =============== DEBUG ===
        if (sBack.Contains("23233") || sPath.Contains("/1939/")) {
          int i = 2;
        }
        errHandle.Status("getMovieId: " + sPath);
        // ========================= */

        // Find the underscore
        int iUnd = sBack.IndexOf("_");
        if (iUnd > 0) {
          sBack = sBack.Substring(0, iUnd);
        }
        // Return what we found
        return sBack;
      } catch (Exception ex) {
        errHandle.DoError("engConv/ConvertOneEngToFolia", ex);
        return "";
      }
    }

    /// <summary>
    /// getDutchFolia -- Find the .folia.xml.gz belonging to the movie-id
    /// 
    /// </summary>
    /// <param name="sMovieId"></param>
    /// <returns></returns>
    private String getDutchFolia(String sMovieId) {
      String sBack = "";

      try {
        // Think of file name
        int iMovieId = Convert.ToInt32(sMovieId);
        sBack = "S-O_" + iMovieId.ToString("D8") + ".folia.xml";

        // Return what we found
        return sBack;
      } catch (Exception ex) {
        errHandle.DoError("engConv/getDutchFolia", ex);
        return "";
      }
    }

    /// <summary>
    /// getSubTransText -- get the text stored with the given @sId
    /// </summary>
    /// <param name="sId"></param>
    public String getSubTransText(String sId, List<subTrans> lstThis) {
      try {
        if (lstThis == null) return "";
        for (int i=0;i<lstThis.Count;i++) {
          if (lstThis[i].id == sId) {
            return lstThis[i].text;
          }
        }
        return "";
      } catch (Exception ex) {
        errHandle.DoError("engConv/getSubTransText", ex);
        return "";
      }
    }

    private String getXmlGzFile(String sFile, String sLng) {
      List<String> lstThis = null;
      try {
        // Are we initialized?
        if (this.lstEnGZ == null) {
          // Read the files
          errHandle.Status("Reading list of English .xml.gz files...");
          this.lstEnGZ = Directory.GetFiles(sDirEnglish, "*.xml.gz", SearchOption.AllDirectories).ToList<String>();
          errHandle.Status("Reading list of Dutch .xml.gz files...");
          this.lstNlGZ = Directory.GetFiles(sDirDutch, "*.xml.gz", SearchOption.AllDirectories).ToList<String>();
          errHandle.Status("Continuing...");
        }
        // Set the correct list to look in
        if (sLng == "en")
          lstThis = this.lstEnGZ;
        else
          lstThis = this.lstNlGZ;
        // Look for the file name
        for (int i=0;i<lstThis.Count;i++) {
          if (lstThis[i].Contains(sFile)) return lstThis[i];
        }
        // Return failure
        return "";
      } catch (Exception ex) {
        errHandle.DoError("engConv/getXmlGzFile", ex);
        return "";
      }
    }

  }

  class subTrans {
    public String id;
    public String text;
    public subTrans(String sId, String sTxt) {
      this.id = sId; this.text = sTxt;
    }
 
  }
  

}
