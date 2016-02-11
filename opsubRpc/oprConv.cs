using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Data;
using System.Net;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace opsubRpc {
  class oprConv {
    // ================================ LOCAL VARIABLES ===========================================
    private ErrHandle errHandle = null;
    private static string CLARIN_CMDI = "http://www.clarin.eu/cmd/";
    private String sCmdiXsd = "http://erwinkomen.ruhosting.nl/xsd/SUBTIEL.xsd";
    /*
    private String sCmdiBase = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<CMD xmlns = \"http://www.clarin.eu/cmd/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" CMDVersion=\"1.1\" xsi:schemaLocation=\"http://www.clarin.eu/cmd/ http://catalog.clarin.eu/ds/ComponentRegistry/rest/registry/profiles/clarin.eu:cr1:p_1328259700943/xsd\">" +
        "<Header />" +
        "<Resources>" +
        " <ResourceProxyList />" +
        " <JournalFileProxyList />" +
        " <ResourceRelationList />" +
        "</Resources>" +
        "<Components><SUBTIEL>" + 
        "</SUBTIEL></Components>";
        */
    private String sCmdiXsdText = "";
    private util.xmlTools oTools = null;
    private List<MovieHash> lstSimHash = new List<MovieHash>();
    public oprConv(ErrHandle oErr) {
      this.errHandle = oErr;
      this.oTools = new util.xmlTools(oErr);
      lstSimHash.Clear();
    }
    /* -------------------------------------------------------------------------------------
     * Name:        ConvertOneOpsToCmdi
     * Goal:        Create a .cmdi file with metadata for the indicated input file
     *              Note: the .cmdi file will be placed in the same directory as FileIn
     * Parameters:  sFileIn     - File to be processed
     *              bForce      - Create result, even though it exists already
     *              bIsDebug    - Debugging mode on or off
     * History:
     * 1/feb/2016 ERK Created
       ------------------------------------------------------------------------------------- */
    public bool ConvertOneOpsToCmdi(String sFileIn, ref osrMovie objMovie, bool bForce, bool bIsDebug) {
      try {
        // Validate
        if (!File.Exists(sFileIn)) return false;
        // Get the output directory
        String sDirOut = Path.GetDirectoryName(sFileIn);

        // Determine file names
        String sFileInXml = sFileIn.Replace(".gz", "");
        String sFileCmdi = sFileIn.Replace(".folia.xml.gz", ".cmdi.xml");

        // Do we need to continue?
        if (!bForce && File.Exists(sFileCmdi)) return true;

        // Decompress input .gz file
        if (!util.General.DecompressFile(sFileIn, sFileInXml)) return false;

        // Read the input file's header as xml
        conv.XmlConv oConv = new conv.XmlConv(this.errHandle);
        XmlNode ndxHeader = null; XmlNamespaceManager nsFolia = null;
        if (!oConv.getFoliaHeader(sFileInXml, ref ndxHeader, ref nsFolia)) return false;
        if (ndxHeader != null) {
          // Get the subtitle id and the idmovie
          String sIdSubtitle = ndxHeader.SelectSingleNode("./child::f:meta[@id = 'idsubtitle']", nsFolia).InnerText;
          String sIdMovie = ndxHeader.SelectSingleNode("./child::f:meta[@id = 'idmovie']", nsFolia).InnerText;
          // Check if we already have information from this idmovie
          XmlNodeList ndxList = null;
          if (objMovie.getInformation(sIdMovie, ref ndxList)) {
            bool bHaveInfo = false; XmlNode ndxSubtitle = null;
            // Walk the list to get the correct subtitle production for this movie
            for (int i=0;i<ndxList.Count;i++) {
              // Access this <subtitle> object
              XmlNode ndxSubThis = ndxList[i].SelectSingleNode("./child::IDSubtitle");
              // Got any results?
              if (ndxSubThis != null) {
                // Is this the correct one?
                if (ndxSubThis.InnerText == sIdSubtitle) {
                  // We have the correct one
                  ndxSubtitle = ndxList[i];
                  // Mark this 
                  bHaveInfo = true;
                  // Escape the for-loop
                  break;
                }
              }
            }
            // Validate
            if (bHaveInfo) {
              // Get the information we need
              String sUserId = oTools.getXmlChildValue(ref ndxSubtitle, "UserID");
              String sUserNickName = oTools.getXmlChildValue(ref ndxSubtitle, "UserNickName");
              String sUserClass = oTools.getXmlChildValue(ref ndxSubtitle, "UserClass");
              String sMovieName = oTools.getXmlChildValue(ref ndxSubtitle, "MovieName");
              String sMovieYear = oTools.getXmlChildValue(ref ndxSubtitle, "MovieYear");
              String sMovieImdbId = oTools.getXmlChildValue(ref ndxSubtitle, "MovieImdbID");
              String sMovieReleaseName = oTools.getXmlChildValue(ref ndxSubtitle, "MovieReleaseName");
              String sLanguageName = oTools.getXmlChildValue(ref ndxSubtitle, "LanguageName");
              String sSubDate = oTools.getXmlChildValue(ref ndxSubtitle, "SubDate");
              String sSeriesSeason = oTools.getXmlChildValue(ref ndxSubtitle, "SeriesSeason");
              String sSeriesEpisode = oTools.getXmlChildValue(ref ndxSubtitle, "SeriesEpisode");
              String sSeriesImdbParent = oTools.getXmlChildValue(ref ndxSubtitle, "SeriesIMDBParent");
              String sMovieKind = oTools.getXmlChildValue(ref ndxSubtitle, "MovieKind");
              String sSubTranslator = oTools.getXmlChildValue(ref ndxSubtitle, "SubTranslator");
              String sSubLanguage = oTools.getXmlChildValue(ref ndxSubtitle, "ISO639");
              // Progress
              errHandle.Status("Processing movie " + sIdMovie + " subtitle " + sIdSubtitle);
              // Create the .cmdi information
              var oSubtiel = new CMD();
              // Add header
              oSubtiel.Header = new CMDHeader();
              oSubtiel.Resources = new CMDResources();
              oSubtiel.Resources.JournalFileProxyList = new CMDResourcesJournalFileProxyList();
              oSubtiel.Resources.ResourceProxyList = new CMDResourcesResourceProxyList();
              oSubtiel.Resources.ResourceRelationList = new CMDResourcesResourceRelationList();
              // Access the main component
              oSubtiel.Components = new CMDComponents();
              // Add header
              oSubtiel.Components.SUBTIEL = new CMDComponentsSUBTIEL();
              CMDComponentsSUBTIEL oSubt = oSubtiel.Components.SUBTIEL;
              // (2) add the information above to the correct parts
              // (2a) Populate the MOVIE part
              oSubtiel.Components.SUBTIEL.Movie = new CMDComponentsSUBTIELMovie();
              oSubt.Movie.MovieId = sIdMovie;
              oSubt.Movie.Name = sMovieName;
              oSubt.Movie.Year = sMovieYear;
              oSubt.Movie.ImdbId = sMovieImdbId;
              oSubt.Movie.Kind = sMovieKind;
              // (2b) Should we add a Series part?
              if (sSeriesSeason != "" || sSeriesEpisode != "" || sSeriesImdbParent != "") {
                // Add a Series part
                oSubtiel.Components.SUBTIEL.Movie.Series = new CMDComponentsSUBTIELMovieSeries();
                oSubt.Movie.Series.Season = sSeriesSeason;
                oSubt.Movie.Series.Episode = sSeriesEpisode;
                oSubt.Movie.Series.ParentImdbId = sSeriesImdbParent;
              }
              // (2c) Add a Release part
              oSubtiel.Components.SUBTIEL.Release = new CMDComponentsSUBTIELRelease();
              oSubt.Release.Name = sMovieReleaseName;
              oSubt.Release.countryCode = "";           // To be filled in later
              // (2d) Build the Subtitle part
              oSubtiel.Components.SUBTIEL.Subtitle = new CMDComponentsSUBTIELSubtitle();
              oSubt.Subtitle.SubtitleId = sIdSubtitle;
              oSubt.Subtitle.languageCode = sSubLanguage;
              oSubt.Subtitle.targetCountry = "";        // To be determined later
              oSubt.Subtitle.Date = sSubDate;
              // (2e) Create a licence part
              oSubtiel.Components.SUBTIEL.Subtitle.License = new CMDComponentsSUBTIELSubtitleLicense();
              oSubt.Subtitle.License.LicenseCode = "";  // To be determined
              oSubt.Subtitle.License.LicenseDate = "";  // To be determined
              oSubt.Subtitle.License.LicenseDetails = sSubTranslator;
              String sSubLicense = (sSubTranslator == "") ? "" : "subtranslator";
              oSubt.Subtitle.License.LicenseType = sSubLicense;
              // (2f) Create a Subtitler/Author part
              oSubtiel.Components.SUBTIEL.Subtitle.Author = new CMDComponentsSUBTIELSubtitleAuthor();
              oSubt.Subtitle.Author.Age = "";
              oSubt.Subtitle.Author.Pseudonym = sUserNickName;
              oSubt.Subtitle.Author.Name = "";
              oSubt.Subtitle.Author.UserClass = sUserClass;
              oSubt.Subtitle.Author.UserID = sUserId;
              // (2g) Create a residence place for the author
              oSubtiel.Components.SUBTIEL.Subtitle.Author.ResidencePlace = new CMDComponentsSUBTIELSubtitleAuthorResidencePlace();
              oSubt.Subtitle.Author.ResidencePlace.countryCode = "";
              oSubt.Subtitle.Author.ResidencePlace.Town = "";
              // TODO: calculate hash and statistics...

              // Serialize into output
              var serializer = new System.Xml.Serialization.XmlSerializer(typeof(CMD));
              using (var stream = new StreamWriter(sFileCmdi))
                serializer.Serialize(stream, oSubtiel);

 
            }
          }
        }
        // Remove the xml file again
        File.Delete(sFileInXml);


        return true;
      } catch (Exception ex) {
        errHandle.DoError("oprConv/ConvertOneOpsToCmdi", ex);
        return false;
      }
    }

    /* -------------------------------------------------------------------------------------
     * Name:        CalculateHashToCmdi
     * Goal:        Adapt an existing .cmdi file with the MD5 hash of the text in the .folia.xml file
     * Parameters:  sFileIn     - File to be processed
     *              lSubInst    - one subtitle instance
     *              bIsDebug    - Debugging mode on or off
     * History:
     * 8/feb/2016 ERK Created
       ------------------------------------------------------------------------------------- */
    public bool CalculateHashToCmdi(String sFileIn, ref List<SubInstance> lSubInst, bool bIsDebug) {
      try {
        // Validate
        if (!File.Exists(sFileIn)) return false;
        // Get the output directory
        String sDirOut = Path.GetDirectoryName(sFileIn);

        // Determine file names
        String sFileInXml = sFileIn.Replace(".gz", "");
        String sFileCmdi = sFileIn.Replace(".folia.xml.gz", ".cmdi.xml");

        // Do we need to continue?
        if (!File.Exists(sFileCmdi)) {
          // If the CMDI file does not exist, we cannot adapt it
          // It is not really an error, but we should give a warning
          errHandle.Status("CalculateHashToCmdi: skipping non-existent [" + sFileCmdi + "]");
          return true;
        }

        // Decompress input .gz file
        if (!util.General.DecompressFile(sFileIn, sFileInXml)) return false;

        // Calculate the hash and other information
        String sHash = ""; int iWords = 0; int iSents = 0;
        if (!oTools.getXmlStats(sFileInXml, ref sHash, ref iWords, ref iSents)) {
          errHandle.Status("CalculateHashToCmdi: Could not find statistics for [" + sFileCmdi + "]");
          return false;
        }


        // Read the CMDI
        XmlDocument pdxCmdi = new XmlDocument();
        pdxCmdi.Load(sFileCmdi);
        // Get correct namespace manager
        XmlNamespaceManager nsFolia = new XmlNamespaceManager(pdxCmdi.NameTable);
        nsFolia.AddNamespace("f", pdxCmdi.DocumentElement.NamespaceURI);
        // Zoek het <Subtitle> element 
        XmlNode ndxSubtitle = pdxCmdi.SelectSingleNode("./descendant::f:Subtitle", nsFolia);
        if (ndxSubtitle == null) {
          // There is an error
          errHandle.Status("CalculateHashToCmdi: CMDI file without <Subtitle> section [" + sFileCmdi + "]");
          return false;
        } else {
          // Enable access to this PDX within oTools
          oTools.SetXmlDocument(pdxCmdi, CLARIN_CMDI );
          // Kijk of er al een <textHash> element is
          XmlNode ndxTextHash = ndxSubtitle.SelectSingleNode("./child::f:textHash", nsFolia);
          if (ndxTextHash == null) {
            // Maak zo'n element
            ndxTextHash = oTools.AddXmlChild(ndxSubtitle, "textHash");
          }
          // Set the hash
          ndxTextHash.InnerText = sHash;
          // Kijk of er al een <Statistics> element is
          XmlNode ndxStats = ndxSubtitle.SelectSingleNode("./child::f:Statistics", nsFolia);
          if (ndxStats == null) {
            // Add a statistics element
            oTools.AddXmlChild(ndxSubtitle, "Statistics",
              "nWords", Convert.ToString(iWords), "child",
              "nSents", Convert.ToString(iSents), "child");
          }
          // Save the file
          pdxCmdi.Save(sFileCmdi);
          // Show what has happened
          errHandle.Status("CalculateHashToCmdi: add [" + sHash + "] to " + sFileCmdi);
          // Add the filename + hash to the list
          lstSimHash.Add(new MovieHash(Convert.ToUInt64(sHash), sFileInXml + ".txt"));
          // Also add it to another list
          lSubInst.Add(new SubInstance(sFileInXml, Convert.ToUInt64(sHash), iWords, iSents));
        }

        // Remove the xml file again (the .gz file stays)
        File.Delete(sFileInXml);

        return true;
      } catch (Exception ex) {
        errHandle.DoError("oprConv/ConvertOneOpsToCmdi", ex);
        return false;
      }
    }

    /* -------------------------------------------------------------------------------------
      * Name:        findDuplicates
      * Goal:        Go through the list and find any duplicates
      *              Add this duplicate information to the .cmdi file
      * History:
      * 10/feb/2016 ERK Created
        ------------------------------------------------------------------------------------- */
    public bool findDuplicates(ref List<SubInstance> lSubInst, int iGoodHd) {
      try {
        // Need to have a hash analyzer object
        util.SimHashAnalyzer oSim = new util.SimHashAnalyzer();
        // Walk through the list of instances
        for (int i=0;i<lSubInst.Count;i++) {
          // Walk through all other instances that could match up with me
          for (int j=0;j< i;j++) {
            // Get the hamming distance between items <i,j>
            int iHdist = oSim.GetHammingDistance(lSubInst[i].simhash, lSubInst[j].simhash);
            // Is this on or below the threshold?
            if (iHdist <= iGoodHd) {
              // So this is probably a duplicate of me -- add it
              lSubInst[i].addDuplicate(j);
              // Also add me to the list of the other one
              lSubInst[j].addDuplicate(i);
            }
          }
        }

        // Walk through all the instances again
        for (int i = 0; i < lSubInst.Count; i++) {
          // Get this instance
          SubInstance oOrg = lSubInst[i];

          // ============= DEBUG ===================
          if (oOrg.name.Contains("7546")) {
            int iStop = 1;
          }
          // =======================================

          // Create a JSON object to host details
          Newtonsoft.Json.Linq.JObject oDetails = new Newtonsoft.Json.Linq.JObject();
          // Does this one have duplicates?
          if (oOrg.lDup.Count>0) {
            // Find the first longest text both in words and sentences
            int iWords = oOrg.words;
            int iSents = oOrg.sents;
            int iLongest = -1;
            for (int j=0;j<oOrg.lDup.Count;j++) {
              SubInstance oThis = lSubInst[oOrg.lDup[j]];
              if (oThis.words> iWords && oThis.sents >= iSents) {
                // Adapt the new maximum
                iWords = oThis.words;
                iSents = oThis.sents;
                iLongest = j;
              }
            }
            // Check what the longest is; that's the most 'original'
            if (iLongest<0) {
              // Prepare a list of similars
              Newtonsoft.Json.Linq.JArray aSimilar = new Newtonsoft.Json.Linq.JArray();
              // The [oOrg] is the longest
              oOrg.license = "largest";
              oDetails["link"] = "this";
              // Create a list of similar ones
              for (int j = 0; j < oOrg.lDup.Count; j++) {
                aSimilar.Add(lSubInst[oOrg.lDup[j]].name);
              }
              // Add the list of similar ones
              oDetails["similar"] = aSimilar;
            } else {
              // Another one is the longest
              oOrg.license = "copy";
              oDetails["link"] = lSubInst[iLongest].name;
            }
          } else {
            oOrg.license = "unique";
            oDetails["link"] = "none";
          }
          // Adapt the .cmdi.xml file for this item
          String sFileCmdi = oOrg.file.Replace(".folia.xml", ".cmdi.xml");
          // Do we need to continue?
          if (!File.Exists(sFileCmdi)) {
            // If the CMDI file does not exist, we cannot adapt it
            // It is not really an error, but we should give a warning
            errHandle.Status("findDuplicates: skipping non-existent [" + sFileCmdi + "]");
            return true;
          }
          // Read the CMDI
          XmlDocument pdxCmdi = new XmlDocument();
          pdxCmdi.Load(sFileCmdi);
          oTools.SetXmlDocument(pdxCmdi, CLARIN_CMDI);
          // Get correct namespace manager
          XmlNamespaceManager nsFolia = new XmlNamespaceManager(pdxCmdi.NameTable);
          nsFolia.AddNamespace("f", pdxCmdi.DocumentElement.NamespaceURI);
          // Zoek het <Subtitle> element 
          XmlNode ndxSubtitle = pdxCmdi.SelectSingleNode("./descendant::f:Subtitle", nsFolia);
          if (ndxSubtitle != null) {
            // Check if there is a statusinfo child
            XmlNode ndxStatusInfo = ndxSubtitle.SelectSingleNode("./child::f:StatusInfo", nsFolia);
            if (ndxStatusInfo == null) {
              // Create such a child
              ndxStatusInfo = oTools.AddXmlChild(ndxSubtitle, "StatusInfo", "Status", "", "attribute");
            }
            // Add the information into the status info node
            ndxStatusInfo.Attributes["Status"].Value = oOrg.license;
            // Create a JSON object with status details
            ndxStatusInfo.InnerText = oDetails.ToString(Newtonsoft.Json.Formatting.None);
          }
          // Save the adapted CMDI
          pdxCmdi.Save(sFileCmdi);
        }

        // Return positively
        return true;
      } catch (Exception ex) {
        errHandle.DoError("oprConv/findDuplicates", ex);
        return false;
      }
    }

    /* -------------------------------------------------------------------------------------
      * Name:        getDistanceOview
      * Goal:        Get an overview of the distances between files
      * History:
      * 8/feb/2016 ERK Created
        ------------------------------------------------------------------------------------- */
    public String getDistanceOview() {
      String sBack = "";
      StringBuilder sbThis = new StringBuilder();

      try {
        util.SimHashAnalyzer oSim = new util.SimHashAnalyzer();

        // Compare all similarity hashes with one another
        for (int i=0;i<lstSimHash.Count;i++) {
          for (int j=0;j< i;j++) {
            // Compare items <i,j>
            float fDist = oSim.GetLikenessValue(lstSimHash[i].iSimHash, lstSimHash[j].iSimHash);
            int iHdist = oSim.GetHammingDistance(lstSimHash[i].iSimHash, lstSimHash[j].iSimHash);
            sbThis.AppendLine( fDist + 
              "\t" + iHdist +
              "\t" + lstSimHash[i].sFile + 
              "\t" + lstSimHash[j].sFile +
              "\tfc " + lstSimHash[i].sFile + " " + lstSimHash[j].sFile);
          }
        }
        // Combine
        sBack = sbThis.ToString();
        // Return the result
        return sBack;
      } catch (Exception ex) {
        errHandle.DoError("oprConv/getDistanceOview", ex);
        return "";
      }
    }

    /* -------------------------------------------------------------------------------------
      * Name:        getSubtielDataset
      * Goal:        Create a dataset object that is based on the Subtiel XSD
      * Parameters:  dsThis - the dataset that is returned
      * History:
      * 3/feb/2016 ERK Created
        ------------------------------------------------------------------------------------- */
    private DataSet getSubtielDataset() {
      DataSet dsThis = null;
      String sXsdFile = "subtiel.xsd";
      try {
        // Check if the Subtiel xsd is downloaded
        if (sCmdiXsdText=="") {
          WebClient wcThis = new WebClient();
          wcThis.DownloadFile(sCmdiXsd, sXsdFile);
          // sCmdiXsdText = wcThis.DownloadString(sCmdiXsd);
        }
        dsThis = new DataSet();
        // dsThis.ReadXmlSchema( new System.IO.StringReader(sCmdiXsdText));
        dsThis.ReadXmlSchema(sXsdFile);

        //foreach (DataTable tblThis in dsThis.Tables) {
        //  errHandle.Status("Table = " + tblThis.TableName);
        //}

        return dsThis;
      } catch (Exception ex) {
        errHandle.DoError("oprConv/getSubtielDataset", ex);
        return null;
      }
    }
    private void SubtielProgressChanged(object sender, DownloadProgressChangedEventArgs e) {
      errHandle.Status("Progress = " + e.ProgressPercentage);
    }

    private void SubtielCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e) {
      errHandle.Status("Download completed!");
    }


  }
  class MovieHash {
    public UInt64 iSimHash;  // The sim hash of this movie
    public String sFile;  // The full name of the .txt file of this movie
    public MovieHash(UInt64 iSimHashCode, String sFileFull) {
      this.iSimHash = iSimHashCode;
      this.sFile = sFileFull;
    }
  }
}
