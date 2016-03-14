using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.IO;
using opsub;
using System.Collections.ObjectModel;

namespace opsubcombi {
  class cmbConv {
    // ================================ LOCAL VARIABLES ===========================================
    private ErrHandle errHandle = null;
    private xmlTools oTools = null;
    private List<Harvest> lstHarvest = null;
    private List<String> lstCmdi = null;
    private XmlDocument pdxCmdi = null;
    private XmlNamespaceManager nsCmdi = null;
    private Dictionary<String, String> dicCmdi = null;  // Lookup dictionary
    private String sDirOut = "";                        // Output base directory
    private String sDirFinal = "";                      // Subdirectory for *final* results
    private String sDirCopy = "";                       // Subdirectory for *copy* results
    private String sDirNotUsed = "";                    // Subdirectory for *notused* results
    private String sDirOther = "";                      // Subdirectory for any *other* results
    private String sCsvFile = "";                       // Full name of file that contains CSV information for each result
    // ================================ CLASS INITIALISATION ======================================
    public cmbConv(ErrHandle oErr) {
      this.errHandle = oErr;
      this.oTools = new xmlTools(oErr);
      // Start a new list to contain the harvesting results
      lstHarvest = new List<Harvest>();
      pdxCmdi = new XmlDocument();
      dicCmdi = new Dictionary<string, string>();
    }
    // ================================ GET and SET ===============================================
    public int count { get { return this.lstHarvest.Count; } }
    public String output {
      set {
        this.sDirOut = value;
        // Make sure all necessary directories are created
        if (!Directory.Exists(sDirOut)) Directory.CreateDirectory(sDirOut);
        this.sDirFinal = Path.GetFullPath(sDirOut + "/final");
        this.sDirCopy = Path.GetFullPath(sDirOut + "/copy");
        this.sDirNotUsed = Path.GetFullPath(sDirOut + "/notused");
        this.sDirOther = Path.GetFullPath(sDirOut + "/other");
        if (!Directory.Exists(sDirFinal)) Directory.CreateDirectory(sDirFinal);
        if (!Directory.Exists(sDirCopy)) Directory.CreateDirectory(sDirCopy);
        if (!Directory.Exists(sDirNotUsed)) Directory.CreateDirectory(sDirNotUsed);
        if (!Directory.Exists(sDirOther)) Directory.CreateDirectory(sDirOther);
        this.sCsvFile = Path.GetFullPath(sDirOut + "/info.csv");
        if (File.Exists(sCsvFile)) File.Delete(sCsvFile);
      }
    }

    // ================================ METHODS ===================================================
    /* -------------------------------------------------------------------------------------
     * Name:        harvestOneFolia
     * Goal:        Determine the status of this FoLiA file, and then:
     *              1) Store the harvest information in the list 
     *              2) Copy the .folia.xml.gz file to the correct target
     *              3) Copy the .cmdi.xml file to the correct target
     * History:
     * 14/mar/2016 ERK Created
       ------------------------------------------------------------------------------------- */
    public bool harvestOneFolia(String sFileFolia) {
      String sTarget = "";  // Target file name

      try {
        // Check existence of output directories
        if (!Directory.Exists(sDirOut + "/final")) Directory.CreateDirectory(sDirOut + "/final");
        // Create a Harvest-log-item
        Harvest oLog = new Harvest(sFileFolia);
        // Look for the corresponsing .cmdi.xml file
        String sFileCmdi = Path.GetFileName(sFileFolia).Replace(".folia.xml.gz", ".cmdi.xml");
        if (getCmdi(ref sFileCmdi)) {
          // Indicate that we HAVE a cmdi file
          oLog.cmdi = true;
          // The .cmdi.xml file exists: load this as an XML file
          pdxCmdi.Load(sFileCmdi);
          nsCmdi = new XmlNamespaceManager(pdxCmdi.NameTable);
          nsCmdi.AddNamespace("c", pdxCmdi.DocumentElement.NamespaceURI);
          // Find a range of field values
          oLog.movieGenres = getFieldValue("Movie", "GenreList", "");
          oLog.movieId = getFieldValue("Movie", "MovieId", "");
          oLog.movieImdbId = getFieldValue("Movie", "ImdbId", "");
          oLog.movieImdbId = getFieldValue("Movie", "Kind", "");
          oLog.movieLanguage = getFieldValue("Movie", "LanguageList", "");
          oLog.movieName = getFieldValue("Movie", "Name", "");
          oLog.movieRating = getFieldValue("Movie", "imdbRating", "");
          oLog.movieVotes = getFieldValue("Movie", "imdbVotes", "");
          oLog.movieYear = getFieldValue("Movie", "Year", "");
          oLog.releaseName = getFieldValue("Release", "Name", "");
          oLog.stDate = getFieldValue("Subtitle", "Date", "");
          oLog.stId = getFieldValue("Subtitle", "SubtitleId", "");
          oLog.stLanguage = getFieldValue("Subtitle", "languageCode", "");
          oLog.stLicType = getFieldValue("License", "LicenseType", "");
          oLog.stLink = getFieldValue("Subtitle", "StatusInfo", "link");
          oLog.stLng = getFieldValue("Subtitle", "languageCode", "");
          oLog.stSentences = getFieldValue("Subtitle", "nSents", "");
          oLog.stStatus = getFieldValue("Subtitle", "StatusInfo", "status");
          oLog.stUserClass = getFieldValue("Author", "UserClass", "");
          oLog.stUserId = getFieldValue("Author", "UserID", "");
          oLog.stUserPseudo = getFieldValue("Author", "Pseudonym", "");
          oLog.stWords = getFieldValue("Subtitle", "nWords", "");

          String sYear = (oLog.movieYear == "") ? "" : "/" + oLog.movieYear;

          // Action depends on the status we have
          switch (oLog.stStatus) {
            case "copy":
              // Check directory
              if (!Directory.Exists(sDirCopy + sYear)) { Directory.CreateDirectory(sDirCopy + sYear); }
              // Keep the copies separate
              sTarget = Path.GetFullPath(sDirCopy + sYear + "/" + oLog.name);
              break;
            case "":
              // Check directory
              if (!Directory.Exists(sDirOther + sYear)) { Directory.CreateDirectory(sDirOther + sYear); }
              // Not enough information on this movie, so keep separate
              sTarget = Path.GetFullPath( sDirOther + sYear + "/" + oLog.name);
              break;
            default:
              // Check directory
              if (!Directory.Exists(sDirFinal + sYear)) { Directory.CreateDirectory(sDirFinal + sYear); }
              // Copy all to the 'final' directory
              sTarget = Path.GetFullPath(sDirFinal + sYear + "/" + oLog.name);
              break;
          }
          // Perform the correct copies
          File.Copy(sFileFolia, sTarget + ".folia.xml.gz", true);
          File.Copy(sFileCmdi, sTarget + ".cmdi.xml", true);
        } else {
          // Indicate that there is NO CMDI of this file
          oLog.cmdi = false;
          String sYear = "/noyear";
          // Check directory
          if (!Directory.Exists(sDirNotUsed + sYear)) { Directory.CreateDirectory(sDirNotUsed + sYear);}
          // Copy to the [not used] directory
          sTarget = Path.GetFullPath(sDirNotUsed + sYear + "/" + oLog.name) + ".folia.xml.gz";
          File.Copy(sFileFolia, sTarget, true);
        }
        // Add the log to the list
        this.lstHarvest.Add(oLog);
        // Log this entry to the CSV file
        addToCsv(oLog);
        // Show what we have been doing
        errHandle.Status("status=[" + oLog.stStatus + "]\t"+sTarget);
        // Return positively
        return true;
      } catch (Exception ex) {
        errHandle.DoError("cmdConv/harvestOneFolia", ex);
        return false;
      }
    }

    /// <summary>
    /// cmdi -- copy the list of CMDI files to a local dictionary
    /// </summary>
    /// <param name="lstThis"></param>
    public bool cmdi(List<String> lstThis) {
      try {
        // Take over the list
        this.lstCmdi = lstThis;
        // Put the list into the dictionary
        dicCmdi.Clear();
        for (int i = 0; i < lstCmdi.Count; i++) {
          // Create a key
          String sKey = Path.GetFileName(lstCmdi[i]);
          // Add this item to the dictionary
          dicCmdi.Add(sKey, lstCmdi[i]);
        }
        // Return positively
        return true;
      } catch (Exception ex) {
        errHandle.DoError("cmdConv/cmdi", ex);
        return false;
      }
    }

    /// <summary>
    /// addToCsv -- add one Harvest object to the CSV
    /// </summary>
    /// <param name="oThis"></param>
    /// <returns></returns>
    private bool addToCsv(Harvest oThis) {
      try {
        // Create a string with the harvest output
        String sLine = oThis.name + "\t" + oThis.cmdi + "\t" + oThis.movieGenres + "\t" + oThis.movieId + "\t" +
          oThis.movieImdbId + "\t" + oThis.movieKind + "\t" + oThis.movieLanguage + "\t" + oThis.movieName + "\t" +
          oThis.movieRating + "\t" + oThis.movieVotes + "\t" + oThis.movieYear + "\t" + oThis.releaseName + "\t" +
          oThis.stDate + "\t" + oThis.stId + "\t" + oThis.stLanguage + "\t" + oThis.stLicType + "\t" +
          oThis.stLink + "\t" + oThis.stLng + "\t" + oThis.stSentences + "\t" + oThis.stStatus + "\t" +
          oThis.stUserClass + "\t" + oThis.stUserId + "\t" + oThis.stUserPseudo + "\t" + oThis.stWords + "\n";
        File.AppendAllText(this.sCsvFile, sLine, Encoding.UTF8);

        // Return positively
        return true;
      } catch (Exception ex) {
        errHandle.DoError("cmdConv/addToCsv", ex);
        return false;
      }
    }


    /// <summary>
    /// getCmdi - Check if the indicated file exists within the list of cmdi files
    /// </summary>
    /// <param name="sFileCmdi"></param>
    /// <returns></returns>
    private bool getCmdi(ref String sFileCmdi) {
      try {
        if ( dicCmdi.ContainsKey(sFileCmdi)) {
          // Replace the file name string with the full path
          if (dicCmdi.TryGetValue(sFileCmdi, out sFileCmdi))
            return true;
        }
        // Getting here means that the file does not exist
        return false;
      } catch (Exception ex) {
        errHandle.DoError("cmdConv/existsCmdi", ex);
        return false;
      }
    }

    /// <summary>
    /// getFieldValue -- look in the current XML file and return the field value requested
    /// </summary>
    /// <param name="sMain"></param>
    /// <param name="sChild"></param>
    /// <param name="sAttribute"></param>
    /// <returns></returns>
    private String getFieldValue(String sMain, String sChild, String sAttribute) {
      String sBack = "";
      try {
        XmlNode ndxThis = pdxCmdi.SelectSingleNode("./descendant::c:" + sMain, nsCmdi);
        if (ndxThis != null && sChild != "") {
          ndxThis = ndxThis.SelectSingleNode("./child::c:" + sChild, nsCmdi);
        }
        if (ndxThis != null) {
          if (sAttribute == "")
            // Check if I have children
            if (ndxThis.ChildNodes.Count==0)
              sBack = ndxThis.Value;
            else {
              // Get all the values of the child nodes
              StringBuilder sbThis = new StringBuilder();
              XmlNode ndxWork = ndxThis.FirstChild;
              while (ndxWork != null) {
                // Add this value
                if (sbThis.Length > 0) sbThis.Append(";");
                sbThis.Append(ndxThis.InnerText);
                // Find next child
                ndxWork = ndxWork.NextSibling;
              }
              // Return the string
              sBack = sbThis.ToString();
            }
          else {
            // Check if this attribute exists
            if (ndxThis.Attributes[sAttribute] != null) sBack = ndxThis.Attributes[sAttribute].Value;              
          }
        }
        // Return what we found
        return sBack;    
      } catch (Exception ex) {
        errHandle.DoError("cmdConv/getFieldValue", ex);
        return "";
      }
    }

    /* -------------------------------------------------------------------------------------
     * Name:        harvestSaveXml
     * Goal:        Save the harvested information to an XML file
     * History:
     * 14/mar/2016 ERK Created
       ------------------------------------------------------------------------------------- */
    public bool harvestSaveXml(String sFile) {
      try {
        // Make sure the output file name is in order
        String sFileOut = Path.GetFullPath(sFile);
        // Create an XmlDocument with some structure
        XmlDocument pdxOut = new XmlDocument();
        pdxOut.LoadXml("<subtielInfo>"+
          " <Creation>" + DateTime.Today.ToLongDateString() + "</Creation>" +
          " <InfoList></InfoList>" +
          "</subtielInfo>");
        XmlNode ndxInfoList = pdxOut.SelectSingleNode("./descendant::InfoList");
        oTools.SetXmlDocument(pdxOut);
        if (ndxInfoList != null) {
          // Add all lines from the list
          for (int i=0;i<this.lstHarvest.Count;i++) {
            // Access this list item
            Harvest oThis = lstHarvest[i];
            // Create one item here
            oTools.AddXmlChild(ndxInfoList, "Info",
              "name", oThis.name, "attribute",
              "cmdi", ((oThis.cmdi) ? "true" : "false"), "attribute",
              "movieGenres", oThis.movieGenres, "attribute",
              "movieId", oThis.movieId, "attribute",
              "movieImdbId", oThis.movieImdbId, "attribute",
              "movieKind", oThis.movieKind, "attribute",
              "movieLanguage", oThis.movieLanguage, "attribute",
              "movieName", oThis.movieName, "attribute",
              "movieRating", oThis.movieRating, "attribute",
              "movieVotes", oThis.movieVotes, "attribute",
              "movieYear", oThis.movieYear, "attribute",
              "releaseName", oThis.releaseName, "attribute",
              "stDate", oThis.stDate, "attribute",
              "stId", oThis.stId, "attribute",
              "stLanguage", oThis.stLanguage, "attribute",
              "stLicenseType", oThis.stLicType, "attribute",
              "stLink", oThis.stLink, "attribute",
              "stLngAvailable", oThis.stLng, "attribute",
              "stSentences", oThis.stSentences, "attribute",
              "stStatus", oThis.stStatus, "attribute",
              "stUserClass", oThis.stUserClass, "attribute",
              "stUserId", oThis.stUserId, "attribute",
              "stUserPseudo", oThis.stUserPseudo, "attribute",
              "stWords", oThis.stWords, "attribute");
              /*
        String sLine = oThis.name + "\t" + oThis.cmdi + "\t" + oThis.movieGenres + "\t" + oThis.movieId + "\t" +
          oThis.movieImdbId + "\t" + oThis.movieKind + "\t" + oThis.movieLanguage + "\t" + oThis.movieName + "\t" +
          oThis.movieRating + "\t" + oThis.movieVotes + "\t" + oThis.movieYear + "\t" + oThis.releaseName + "\t" +
          oThis.stDate + "\t" + oThis.stId + "\t" + oThis.stLanguage + "\t" + oThis.stLicType + "\t" +
          oThis.stLink + "\t" + oThis.stLng + "\t" + oThis.stSentences + "\t" + oThis.stStatus + "\t" +
          oThis.stUserClass + "\t" + oThis.stUserId + "\t" + oThis.stUserPseudo + "\t" + oThis.stWords + "\n";
          */

          }
        }
        // Write the XML
        pdxOut.Save(sFileOut);

        return true;
      } catch (Exception ex) {
        errHandle.DoError("cmdConv/harvestOneFolia", ex);
        return false;
      }
    }

  }

  // =============================== HELPER CLASS =================================================
  class Harvest {
    // Main obligatory fields
    public String name;           // Name of the file  *without* .folia.xml.gz
    public bool cmdi;             // Does this file have a CMDI?
    // Other fields
    public String movieId;        // Movie/MovieId
    public String movieName;      // Movie/Name
    public String movieYear;      // Movie/Year:      Year this movie was published
    public String movieImdbId;    // Movie/ImdbId:    Available imdbId for this movie
    public String movieKind;      // Movie/Kind:      movie, series etc
    public String movieGenres;    // Movie/GenreList: Semicolon-separated list of genres
    public String movieLanguage;  // Movie/LanguageList:  List of languages used in this movie
    public String movieRating;    // Movie/imdbRating
    public String movieVotes;     // Movie/imdbVotes
    public String releaseName;    // Release/Name
    public String stId;           // Subtitle/SubtitleId
    public String stLanguage;     // Subtitle/languageCode
    public String stDate;         // Subtitle/Date
    public String stLicType;      // Subtitle/License/LicenseType
    public String stUserPseudo;   // Subtitle/Author/Pseudonym
    public String stUserId;       // Subtitle/Author/UserID
    public String stUserClass;    // Subtitle/Author/UserClass
    public String stWords;        // Subtitle/Statistics/nWords
    public String stSentences;    // Subtitle/Statistics/nSents
    public String stLng;          // Subtitle/languageAvailable
    public String stStatus;       // StatusInfo/@status
    public String stLink;         // StatusInfo/@link
    // ===================== Class initialisation =================================================
    public Harvest(String sFile) {
      // Convert the file name to a normal name
      this.name = Path.GetFileName(sFile).Replace(".folia.xml.gz", "");
    }
  }
}
