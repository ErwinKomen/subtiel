using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using opsub;
using System.Xml;
using System.Text.RegularExpressions;

namespace opsubeng {
  class engConv {
    // ================================ LOCAL VARIABLES ===========================================
    private ErrHandle errHandle = null;         // Error handler of calling
    private opsub.util.xmlTools oTools = null;  // 
    private String sDirDutch = "";              // Dutch input directory
    private String sDirEnglish = "";            // English input directory
    private String sDirOutput = "";             // Output directory
    private XmlDocument pdxNL = null;
    private XmlDocument pdxEN = null;
    private XmlNamespaceManager nsNL = null;
    private XmlNamespaceManager nsEN = null;
    // ==================== CLASS INITIALISATION ==================================================
    public engConv(ErrHandle oErr) {
      this.errHandle = oErr;
      this.oTools = new opsub.util.xmlTools(oErr);
      pdxEN = new XmlDocument();
      pdxNL = new XmlDocument();
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
     * History:
     * 1/feb/2016 ERK Created
       ------------------------------------------------------------------------------------- */
    public bool ConvertOneEngToFolia(XmlNode ndxLinkGrp, bool bForce, bool bDebug) {
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
        if (arNL.Length == 0) return true;

        // (3) Find the English .folia.xml.gz file (converted FoLiA format)
        sFileEn = getDutchFolia(sSubtitleIdEN);
        String[] arEN = Directory.GetFiles(sDirEnglish, sFileEn + ".gz", SearchOption.AllDirectories);
        // If we don't have this input, then return empty-handed
        if (arEN.Length == 0) return true;

        sName = sFileEn.Replace(".folia.xml", "");
        String sNameNL = sFileNL.Replace(".folia.xml", "");

        // (4) Show what we are doing
        errHandle.Status("Process parallel between " + Path.GetFileNameWithoutExtension(sFileEn) + 
          " and " + Path.GetFileNameWithoutExtension(sFileNL));

        // (5) Read the Dutch and the English folia.xml files (unpack + read)
        if (!getXmlDoc(arNL[0], ref pdxNL, ref nsNL)) return false;
        if (!getXmlDoc(arEN[0], ref pdxEN, ref nsEN)) return false;

        // (6) Determine name of output folia.xml file
        String sYear = pdxNL.SelectSingleNode("./descendant::f:meta[@id='year']", nsNL).InnerText;
        String sDir = sDirOutput + sYear;
        if (!Directory.Exists(sDir)) Directory.CreateDirectory(sDir);
        sFileTr = sDir + "/S-OP_" + Convert.ToInt32(sSubtitleIdNL).ToString("D8") + ".folia.xml";

        // (7) Check existence - skip if needed
        if (!bForce && File.Exists(sFileTr)) return true;

        // (8) Adapt the English input XmlDocument by doing away with all the superfluous <w> elements
        XmlNodeList ndxWlist = pdxEN.SelectNodes("./descendant::f:w", nsEN);
        int iRemoved = 0;
        for (int i=ndxWlist.Count-1;i>=0;i--) {
          // Remove this node
          XmlNode ndxDel = ndxWlist[i];
          XmlNode ndxParent = ndxDel.ParentNode;
          // Delete the one we selected
          ndxDel.RemoveAll();
          ndxParent.RemoveChild(ndxDel);
          iRemoved++;
        }
        // Remove the "xmlns" attributes where they are not needed
        XmlNodeList ndxSlist = pdxEN.SelectNodes("./descendant::f:s", nsEN);
        for (int i=0; i<ndxSlist.Count;i++ ) {
          ndxSlist[i].Attributes.Remove(ndxSlist[i].Attributes["xmlns"]);
        }
        // Make sure oTools is set correctly
        oTools.SetXmlDocument(pdxEN, "http://ilk.uvt.nl/folia");
        // Add an 'annotations' element
        XmlNode ndxAnnot = pdxEN.SelectSingleNode("./descendant::f:annotations", nsEN);
        if (ndxAnnot!= null) {
          // Add my own
          oTools.AddXmlChild(ndxAnnot, "alignment-annotation", 
            "set", "trans", "attribute",
            "annotator", "opsubeng", "attribute",
            "annotatortype", "auto", "attribute",
            "", "", "text");
        }
 
        // (9) Walk through the Parallels
        XmlNode ndxParallel = ndxLinkGrp.SelectSingleNode("./child::link");
        XmlNode ndxCmpAlignS = null;
        while (ndxParallel != null) {
          // Get the information from this link
          String[] arTargets = ndxParallel.Attributes["xtargets"].Value.Split(';');
          if (arTargets.Length == 2) {
            // make arrays of the English source and Dutch destination
            String[] arSrcEN = arTargets[0].Trim().Split(' ');
            String[] arDstNL = arTargets[1].Trim().Split(' ');
            // Do we have an English source?
            if (arSrcEN.Length==0 || arSrcEN[0] == "") {
              // Is there a previous <complexalignments> node?
              if (ndxCmpAlignS ==null) {
                // Create the first complexalignments node right after the first <p>
                ndxCmpAlignS = oTools.AddXmlChild(pdxEN.SelectSingleNode("./descendant::f:p", nsEN), "complexalignment");
              }
            } else {
              // Find the English source node
              String sIdEN = sName + ".p.1.s." + arSrcEN[arSrcEN.Length - 1];
              XmlNode ndxSrcEN = pdxEN.SelectSingleNode("./descendant::f:s[@xml:id = '" + sIdEN + "']", nsEN);
              // Start adding the complex alignment here
              ndxCmpAlignS = oTools.AddXmlSibling(ndxSrcEN, "complexalignments");
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
                String sTextEN = pdxEN.SelectSingleNode("./descendant::f:s[@xml:id='" + sIdEN + "']/child::f:t", nsEN).InnerText;
                // Add the <aref> element
                oTools.AddXmlChild(ndxAlignEN, "aref",
                  "id", sName + ".p.1.s." + arSrcEN[i], "attribute",
                  "t", sTextEN, "attribute",
                  "type", "s", "attribute");
              }
            }
            // Add the Dutch translation references
            XmlNode ndxAlignNL = oTools.AddXmlChild(ndxCmpAlign, "alignment",
              "set", "trans", "attribute",
              "class", "nl", "attribute",
              "xlink:href", sFileNL, "attribute",
              "xlink:type", "simple", "attribute");
            // Do we have a Dutch translation?
            if (arDstNL.Length> 0 && arDstNL[0] != "") {
              // Walk the array of destination references
              for (int i = 0; i < arDstNL.Length; i++) {
                // Find the text for this element
                String sIdNL = sNameNL + ".p.1.s." + arDstNL[i];
                String sTextNL = pdxNL.SelectSingleNode("./descendant::f:s[@xml:id='" + sIdNL + "']/child::f:t", nsNL).InnerText;
                // Add the <aref> element
                oTools.AddXmlChild(ndxAlignNL, "aref",
                  "id", sName + ".p.1.s." + arDstNL[i], "attribute",
                  "t", sTextNL, "attribute",
                  "type", "s", "attribute");
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
    /// getMovieId -- retrieve the movie id number from the given string
    /// </summary>
    /// <param name="sPath"></param>
    /// <returns></returns>
    private String getMovieId(String sPath) {
      String sBack = "";

      try {
        // Start by taking the last part of the path
        sBack = Path.GetFileName(sPath);
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

  }
}
