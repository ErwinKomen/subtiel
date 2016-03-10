using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using opsub;

namespace opsubeng {
  class ParReader {
    // ==================================== LOCAL NAMES ===================================
    private bool bInit = false;           // Initialisation flag
    protected ErrHandle errHandle;        // Link to the global error handler
    private String loc_sFileIn = "";      // The file we are reading from
    private StreamReader rdFileIn = null; // Reading the input file as a stream
    private XmlReader rdFileXml = null;   // Reading the input file as XML
    private XmlDocument pdxThis = null;   // My own local PDX
    private XmlReaderSettings setThis = null; // Settings to read the document
    // ==================================== CLASS INITIALIZER =============================
    public ParReader(String sFileIn, ErrHandle oErr) {
      // Set error handler
      this.errHandle = oErr;
      // Set local file name
      this.loc_sFileIn = sFileIn;
      // Create correct reader settings
      setThis = new XmlReaderSettings();
      setThis.DtdProcessing = DtdProcessing.Ignore;
      setThis.IgnoreComments = true;
      // Validate
      if (!File.Exists(sFileIn)) return;
      // Try opening the file
      rdFileIn = new StreamReader(sFileIn);
      rdFileXml = XmlReader.Create(rdFileIn, setThis);
      // Create an xml document
      pdxThis = new XmlDocument();
      // Set init flag
      bInit = true; 
    }

    // ==================================== METHODS =======================================
    public XmlNode getNextLinkGrp() {
      XmlNode ndBack = null;

      try {
        // Validate
        if (!bInit) return null;
        // (1) Walk through the bare folia input file
        while (!rdFileXml.EOF && rdFileXml.Read()) {
          // (2) Check the input element
          if (rdFileXml.IsStartElement("linkGrp")) {
            // (3) Read this element as a string
            String sWholeS = rdFileXml.ReadOuterXml();
            // (4) Place this into a new xml Document
            pdxThis.LoadXml(sWholeS);
            // Return the correct link
            ndBack = pdxThis.SelectSingleNode("./descendant-or-self::linkGrp");
            return ndBack;
          }
        }
        // Getting here means: we are too far
        return null;
      } catch (Exception ex) {
        errHandle.DoError("getNextLinkGrp", ex); // Provide standard error message
        return null;
      }
    }
    public bool getNextLinkGrp(ref XmlReader rdParallel) {
       try {
        // Validate
        if (!bInit) return false;
        // (1) Walk through the bare folia input file
        while (!rdFileXml.EOF && rdFileXml.Read()) {
          // (2) Check the input element
          if (rdFileXml.IsStartElement("linkGrp")) {
            rdParallel = rdFileXml;
            // Return the reader
            return true;
          }
        }
        // Getting here means: we are too far
        return false;
      } catch (Exception ex) {
        errHandle.DoError("getNextLinkGrp", ex); // Provide standard error message
        return false;
      }
    }
  }
}
