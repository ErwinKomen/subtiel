using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.IO;
using System.Threading.Tasks;

namespace opsubRpc.conv {
  /* -------------------------------------------------------------------------------------
   * Name:  XmlConv
   * Goal:  Class for xml to xml conversion
   * History:
   * 1/feb/2016 ERK Created
     ------------------------------------------------------------------------------------- */
  class XmlConv {
    // Variables valid for all implementations of XmlToXml
    protected ErrHandle errHandle;
    protected util.xmlTools oXmlTools;
    protected util.psdxTools oPsdxTools;
    protected XmlDocument pdxThis = null;

    // ================= Class initializer =======================================================
    public XmlConv(ErrHandle oErr) {
      this.errHandle = oErr;
    }

    // ================= Methods for this class ==================================================

    /* -------------------------------------------------------------------------------------
     * Name:        getFoliaHeader
     * Goal:        Get the <metadata> header of the .folia.xml file @sFile
     * Parameters:  sFile       - File to be processed
     *              ndxHeader   - Returned XmlNode to the <metadata> header
     * History:
     * 1/feb/2016 ERK Created
       ------------------------------------------------------------------------------------- */
    public bool getFoliaHeader(String sFile, ref XmlNode ndxHeader, ref XmlNamespaceManager nsFolia) {
      try {
        // Validate
        if (!File.Exists(sFile)) return false;

        // Create a new XmlDocument
        pdxThis = new XmlDocument();

        // Initialisations
        ndxHeader = null;

        // Start reading file
        using (StreamReader rdFileTmp = new StreamReader(sFile))
        using (XmlReader rdFolia = XmlReader.Create(rdFileTmp)) {
          // (1) Walk through the bare folia input file
          while (!rdFolia.EOF && rdFolia.Read()) {
            // (2) Check the input element
            if (rdFolia.IsStartElement("metadata")) {
              // (3) Read this as string
              String sWholeS = rdFolia.ReadOuterXml();
              // (4) Place this into a new xml Document
              pdxThis.LoadXml(sWholeS);
              // (2) Create a namespace mapping for the opensubtitles *source* xml document
              nsFolia = new XmlNamespaceManager(pdxThis.NameTable);
              nsFolia.AddNamespace("f", pdxThis.DocumentElement.NamespaceURI);
              // (5) Return the header
              ndxHeader = pdxThis.SelectSingleNode("./descendant-or-self::f:metadata", nsFolia);
              break;
            }
          }
        }

        // Return success
        return true;
      } catch (Exception ex) {
        errHandle.DoError("getFoliaHeader", ex); // Provide standard error message
        return false;
      }
    }
  }
}
