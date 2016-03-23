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
    private String sDirEqual = "";                      // Subdirectory for *equal* results
    private String sDirNotUsed = "";                    // Subdirectory for *notused* results
    private String sDirOther = "";                      // Subdirectory for any *other* results
    private String sCsvFile = "";                       // Full name of file that contains CSV information for each result
    private bool bInit = false;                         // Initialisation flag
    private string DEFAULT_TIME = "00:00:00.000";
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
        this.sDirEqual = Path.GetFullPath(sDirOut + "/equal");
        this.sDirNotUsed = Path.GetFullPath(sDirOut + "/notused");
        this.sDirOther = Path.GetFullPath(sDirOut + "/other");
        if (!Directory.Exists(sDirFinal)) Directory.CreateDirectory(sDirFinal);
        if (!Directory.Exists(sDirCopy)) Directory.CreateDirectory(sDirCopy);
        if (!Directory.Exists(sDirEqual)) Directory.CreateDirectory(sDirEqual);
        if (!Directory.Exists(sDirNotUsed)) Directory.CreateDirectory(sDirNotUsed);
        if (!Directory.Exists(sDirOther)) Directory.CreateDirectory(sDirOther);
        this.sCsvFile = Path.GetFullPath(sDirOut + "/info.csv");
        if (File.Exists(sCsvFile)) File.Delete(sCsvFile);
        // Set init flag
        bInit = true;
      }
    }

    // ================================ METHODS ===================================================
    /* -------------------------------------------------------------------------------------
     * Name:        repairOneFolia
     * Goal:        Check and repair one folia file
     * History:
     * 21/mar/2016 ERK Created
       ------------------------------------------------------------------------------------- */
    public bool repairOneFolia(String sFileFoliaGz) {
      try {
        // Get the unzipped file name
        String sFileFolia = sFileFoliaGz.Replace(".gz", "");
        // Unzip the file
        if (!General.DecompressFile(sFileFoliaGz, sFileFolia)) { errHandle.DoError("cmbConv/repairOneFolia", "Could not decompress"); return false; }
        // Check for empty begintime/endtime
        String[] arLine = File.ReadAllLines(sFileFolia);
        /*
        if (sFileFoliaGz.Contains("S-O_00214801")) {
          int iStop = 2;
        } */
        bool bChanged = false; int iChanges = 0;
        for (int i=0;i<arLine.Length;i++) {
          String sLine = arLine[i];
          // Check the syntax of the begintime and the endtime
          if (adaptTime(ref sLine, sLine.IndexOf("begintime="))) bChanged = true;
          if (adaptTime(ref sLine, sLine.IndexOf("endtime="))) bChanged = true;
          /*
          if (sLine.Contains("begintime=\"\"")) {
            sLine = sLine.Replace("begintime=\"\"", "begintime=\"00:00:00.000\"");
            bChanged = true;
          }
          if (sLine.Contains("endtime=\"\"")) {
            sLine = sLine.Replace("endtime=\"\"", "endtime=\"00:00:00.000\"");
            bChanged = true;
          } */
          if (bChanged) { arLine[i] = sLine; iChanges++; bChanged = false; }
        }
        // Only save results if something changed
        if (iChanges>0) {
          File.WriteAllLines(sFileFolia, arLine);
          // Show repair log
          errHandle.Status("Repaired file: [" + sFileFoliaGz + "] ("+iChanges+" repairs)");
          // And compress into .gz
          if (!General.CompressFile(sFileFolia, sFileFoliaGz)) { errHandle.DoError("cmbConv/repairOneFolia", "Could not compress"); return false; }
        } else {
          // Show repair log
          errHandle.Status("Unchanged: [" + sFileFoliaGz + "]");
        }
        // Remove the unzipped file again
        File.Delete(sFileFolia);

        // Return positively
        return true;
      } catch (Exception ex) {
        errHandle.DoError("cmdConv/repairOneFolia", ex);
        return false;
      }
    }

    /// <summary>
    /// adaptTime -- Adapt the time within [sLine] starting at iStart
    /// </summary>
    /// <param name="sLine"></param>
    /// <param name="iStart"></param>
    /// <returns></returns>
    private bool adaptTime(ref String sLine, int iStart) {
      try {
        // Check if any time has been detected
        if (iStart < 0) return false;
        // Get the first and the last quotation mark position
        int iQuotFirst = sLine.IndexOf('"', iStart + 1);
        if (iQuotFirst > 0) {
          int iQuotLast = sLine.IndexOf('"', iQuotFirst + 1);
          if (iQuotLast > 0) {
            // Check the size we have
            if (iQuotLast - iQuotFirst != 13) {
              // The size is incorrect, so change it to ZEROES
              sLine = sLine.Substring(0, iQuotFirst+1) + DEFAULT_TIME + sLine.Substring(iQuotLast);
              return true;
            }
          }
        }
        // No changes, so return false
        return false;
      } catch (Exception ex) {
        errHandle.DoError("cmbConv/adaptTime", ex);
        return false;
      }
    }
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
        // Validate
        if (!bInit) { errHandle.DoError("cmdConv/harvestOneFolia", "The [output] has not been set"); return false; }
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
          oLog.stLng = getFieldValue("Subtitle", "languageAvailable", "");
          oLog.stSentences = getFieldValue("Subtitle", "nSentences", "");
          if (oLog.stSentences == "") oLog.stSentences = getFieldValue("Subtitle", "nSents", "");
          oLog.stStatus = getFieldValue("Subtitle", "StatusInfo", "status");
          oLog.stUserClass = getFieldValue("Author", "UserClass", "");
          oLog.stUserId = getFieldValue("Author", "UserID", "");
          oLog.stUserPseudo = getFieldValue("Author", "Pseudonym", "");
          oLog.stWords = getFieldValue("Subtitle", "nWords", "");

          String sYear = (oLog.movieYear == "") ? "" : "/" + oLog.movieYear;

          // Logging of status
          errHandle.Status("harvestOneFolia [" + sFileCmdi + "] status=[" + oLog.stStatus + "]");

          // Action depends on the status we have
          String sYearDir = "";
          switch (oLog.stStatus) {  
            case "copy": sYearDir = sDirCopy + sYear; break;    // This is a 'copy'
            case "equal": sYearDir = sDirEqual + sYear; break;  // Status is 'equal'
            case "": sYearDir = sDirOther + sYear; break;       // Unknown status??
            default: sYearDir = sDirFinal + sYear; break;       // Includes: unique, largest
          }
          // Check directory
          if (!Directory.Exists(sYearDir)) { Directory.CreateDirectory(sYearDir); }
          // Copy all to the 'final' directory
          sTarget = Path.GetFullPath(sYearDir + "/" + oLog.name);
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
    /// doHeaderCsv -- Create a header for the CSV output
    /// </summary>
    /// <returns></returns>
    public bool doHeaderCsv() {
      try {
        StringBuilder sbThis = new StringBuilder();
        sbThis.Append("Name\t");
        sbThis.Append("Cmdi\t");
        sbThis.Append("Genres\t");
        sbThis.Append("MovieId\t");
        sbThis.Append("ImdbId\t");
        sbThis.Append("MovieKind\t");
        sbThis.Append("MovieLanguages\t");
        sbThis.Append("MovieName\t");
        sbThis.Append("imdbRating\t");
        sbThis.Append("imdbVotes\t");
        sbThis.Append("MovieYear\t");
        sbThis.Append("ReleaseName\t");
        sbThis.Append("SubDate\t");
        sbThis.Append("SubId\t");
        sbThis.Append("SubLanguage\t");
        sbThis.Append("SubLicType\t");
        sbThis.Append("SubStatusLink\t");
        sbThis.Append("SubAvailable\t");
        sbThis.Append("SubSentences\t");
        sbThis.Append("SubStatus\t");
        sbThis.Append("SubUserClass\t");
        sbThis.Append("SubUserId\t");
        sbThis.Append("SubUserPseudo\t");
        sbThis.Append("SubWords\n");
        String sLine = sbThis.ToString();
        File.AppendAllText(this.sCsvFile, sLine, Encoding.UTF8);

        // Return positively
        return true;
       } catch (Exception ex) {
        errHandle.DoError("cmdConv/doHeaderCsv", ex);
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
                sbThis.Append(ndxWork.InnerText);
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
    public String stSentences;    // Subtitle/Statistics/nSentences
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
