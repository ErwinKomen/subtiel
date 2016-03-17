using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Data;
using System.Net;
// using Newtonsoft.Json;
using System.Threading.Tasks;
using omdb;

namespace opsubRpc {
  class oprConv {
    // ================================ LOCAL VARIABLES ===========================================
    private ErrHandle errHandle = null;
    private static string CLARIN_CMDI = "http://www.clarin.eu/cmd/";
    private String sCmdiXsd = "http://erwinkomen.ruhosting.nl/xsd/SUBTIEL.xsd";
    private Dictionary<String, String> dicMovie = new Dictionary<string, string>();
    private String sDicSource = "";   // Source file for [dicMovie]
    private bool bInit = false;
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
    private String sDirRoot = "/scratch/ekomen/out/";        // Directory under which all should be kept
    private List<MovieHash> lstSimHash = new List<MovieHash>();
    public oprConv(ErrHandle oErr) {
      this.errHandle = oErr;
      this.oTools = new util.xmlTools(oErr);
      lstSimHash.Clear();
    }
    // ========================= GETTERS/SETTERS ===========================================
    public void dirRoot(String sDir) {
      this.sDirRoot = sDir;
      if (!sDirRoot.EndsWith("/") && !sDirRoot.EndsWith("\\"))
        sDirRoot = sDirRoot+"/";
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

        // Check if this is a symbolic link
        if (File.GetAttributes(sFileIn).HasFlag(FileAttributes.ReparsePoint) || 
          !util.General.CanReadFile(sFileIn)) {
          // The input file is a symbolic link --> skip it, because it should already have been done
          return true;
        }

        // Get the output directory
        String sDirOut = Path.GetDirectoryName(sFileIn);

        // Determine file names
        String sFileInXml = sFileIn.Replace(".gz", "");
        String sFileCmdi = sFileIn.Replace(".folia.xml.gz", ".cmdi.xml");

        // Do we need to continue?
        if (!bForce && File.Exists(sFileCmdi)) {
          errHandle.Status("OpsToCmdi skips: " + sFileInXml);
          return true;
        }

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
          XmlNodeList ndxList = null; XmlNode ndxMovie = null;
          if (objMovie.getInformation(sIdMovie, ref ndxList, ref ndxMovie)) {
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
            // ============= DEBUGGING ===
            if (!bHaveInfo && sIdMovie == "36797") {
              int iDebug = 1;
            }
            // Validate
            if (bHaveInfo) {
              if (sIdMovie == "36797") {
                int iDebug = 1;
              }
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
              // Additional information from the MOVIE
              String sSeriesName = oTools.getXmlChildValue(ref ndxMovie, "SeriesName");
              String sSeriesRootName = oTools.getXmlChildValue(ref ndxMovie, "SeriesRootName");
              String sEpisodeName = oTools.getXmlChildValue(ref ndxMovie, "EpisodeName");
              String sMoviePlot = oTools.getXmlChildValue(ref ndxMovie, "MoviePlot");
              XmlNode ndxAKA = ndxMovie.SelectSingleNode("./descendant::MovieAKA");
              // Possibly get information from other places
              if (sMovieYear == "") sMovieYear = oTools.getXmlChildValue(ref ndxMovie, "MovieYear");
              if (sMovieKind == "") sMovieKind = oTools.getXmlChildValue(ref ndxMovie, "MovieKind");
              if (sSeriesSeason == "") sSeriesSeason = oTools.getXmlChildValue(ref ndxMovie, "SeriesSeason");
              if (sSeriesEpisode == "") sSeriesEpisode = oTools.getXmlChildValue(ref ndxMovie, "SeriesEpisode");
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
              oSubt.Movie.Plot = sMoviePlot;
              // (2a') add one or more alternative name parts
              oSubtiel.Components.SUBTIEL.Movie.AltNameList = new CMDComponentsSUBTIELMovieAltNameList();
              if (ndxAKA != null) {
                // Find alternative names
                List<String> lstAlt = new List<string>();
                while (ndxAKA != null) {
                  lstAlt.Add(ndxAKA.InnerText);
                  // Find next name
                  ndxAKA = ndxAKA.SelectSingleNode("./following-sibling::MovieAKA");
                }
                // Add this list of names
                oSubt.Movie.AltNameList.AltName = lstAlt.ToArray();
              }
              // (2b) Should we add a Series part?
              if (sSeriesSeason != "" || sSeriesEpisode != "" || sSeriesImdbParent != "") {
                // Add a Series part
                oSubtiel.Components.SUBTIEL.Movie.Series = new CMDComponentsSUBTIELMovieSeries();
                oSubt.Movie.Series.Name = sSeriesName;
                oSubt.Movie.Series.RootName = sSeriesRootName;
                oSubt.Movie.Series.Season = new CMDComponentsSUBTIELMovieSeriesSeason();
                oSubt.Movie.Series.Season.Value = sSeriesSeason;
                oSubt.Movie.Series.Season.Name = "";
                oSubt.Movie.Series.Episode = new CMDComponentsSUBTIELMovieSeriesEpisode();
                oSubt.Movie.Series.Episode.Value = sSeriesEpisode;
                oSubt.Movie.Series.Episode.Name = sEpisodeName;
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
              oSubt.Subtitle.textHash = "";             // similarity hash
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
              oSubtiel.Components.SUBTIEL.Subtitle.Statistics = new CMDComponentsSUBTIELSubtitleStatistics();
              oSubt.Subtitle.Statistics.nSentences = 0;
              oSubt.Subtitle.Statistics.nWords = 0;

              oSubtiel.Components.SUBTIEL.Subtitle.StatusInfo = new CMDComponentsSUBTIELSubtitleStatusInfo();
              oSubt.Subtitle.StatusInfo.link = "none";

              // Serialize into output
              var serializer = new System.Xml.Serialization.XmlSerializer(typeof(CMD));
              using (var stream = new StreamWriter(sFileCmdi))
                serializer.Serialize(stream, oSubtiel);

 
            } else {
              errHandle.Status("OpsToCmdi no information for: " + sFileInXml);
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
     *              bSkip       - Skip anything that has *not* been made
     *              bIsDebug    - Debugging mode on or off
     * History:
     * 8/feb/2016 ERK Created
       ------------------------------------------------------------------------------------- */
    public bool CalculateHashToCmdi(String sFileIn, ref List<SubInstance> lSubInst, 
      bool bIsDebug) {
      try {
        // Validate
        if (!File.Exists(sFileIn)) return false;

        // Check if this is a symbolic link
        if (File.GetAttributes(sFileIn).HasFlag(FileAttributes.ReparsePoint) ||
          !util.General.CanReadFile(sFileIn)) {
          // The input file is a symbolic link --> skip it, because it should already have been done
          return true;
        }


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

        // Prepare statistics information
        String sHash = ""; int iWords = 0; int iSents = 0;

        // Decompress input .gz file
        if (!util.General.DecompressFile(sFileIn, sFileInXml)) return false;

        // Calculate the hash and other information
        List<String> lStat = new List<string>();
        if (!oTools.getXmlStats(sFileInXml, ref sHash, lStat, ref iWords, ref iSents)) {
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
              "nSentences", Convert.ToString(iSents), "child");
          }
          // Add stat info
          XmlNode ndxInfo = ndxSubtitle.SelectSingleNode("./child::f:StatusInfo", nsFolia);
          if (ndxInfo != null) {
            // Remove the existing one
            ndxInfo.RemoveAll();
            ndxSubtitle.RemoveChild(ndxInfo);
          }
          // Add this information
          XmlNode ndxStatusInfo = oTools.AddXmlChild(ndxSubtitle, "StatusInfo",
            "link", "none", "attribute",
            "status", "", "attribute");
          // Add all the evidence
          int iEvidId = 1;
          for (int i=0;i<lStat.Count;i++) {
            XmlNode ndxEvid = oTools.AddXmlChild(ndxStatusInfo, "Evidence",
              "EvidenceId", Convert.ToString( iEvidId), "attribute");
            ndxEvid.InnerText = lStat[i];
          iEvidId += 1;
          }
          // Save the file
          pdxCmdi.Save(sFileCmdi);
          // Retrieve idmovie and imdbid
          String sIdMovie = "";
          String sImdbId = "";
          XmlNode ndxIdMovie = pdxCmdi.SelectSingleNode("./descendant::f:MovieId", nsFolia);
          XmlNode ndxImdb = pdxCmdi.SelectSingleNode("./descendant::f:ImdbId", nsFolia);
          if (ndxIdMovie != null) sIdMovie = ndxIdMovie.InnerText;
          if (ndxImdb != null) sImdbId = ndxImdb.InnerText;
          // Show what has happened
          errHandle.Status("CalculateHashToCmdi: add [" + sHash + "] to " + sFileCmdi);
          // Add the filename + hash to the list
          lstSimHash.Add(new MovieHash(Convert.ToUInt64(sHash), sFileInXml + ".txt"));
          // Also add it to another list
          lSubInst.Add(new SubInstance(sFileInXml, Convert.ToUInt64(sHash), iWords, iSents, sIdMovie, sImdbId));
        }

        // Remove the xml file again (the .gz file stays)
        File.Delete(sFileInXml);

        return true;
      } catch (Exception ex) {
        errHandle.DoError("oprConv/CalculateHashToCmdi", ex);
        return false;
      }
    }


    /* -------------------------------------------------------------------------------------
      * Name:        HarvestHashFromCmdi
      * Goal:        Get the hash and other data from the cmdi
      * Parameters:  sFileIn     - File to be processed
      *              lSubInst    - one subtitle instance
      * History:
      * 10/feb/2016 ERK Created
        ------------------------------------------------------------------------------------- */
    public bool HarvestHashFromCmdi(String sFileIn, ref List<SubInstance> lSubInst) {
      try {
        // Determine file names
        String sFileCmdi = sFileIn.Replace(".folia.xml.gz", ".cmdi.xml");

        // Do we need to continue?
        if (!File.Exists(sFileCmdi)) {
          // If the CMDI file does not exist, we cannot adapt it
          // It is not really an error, but we should give a warning
          errHandle.Status("HarvestHashFromCmdi: skipping non-existent [" + sFileCmdi + "]");
          return true;
        }

        // Prepare statistics information
        String sHash = ""; int iWords = 0; int iSents = 0;
        // Read the CMDI
        XmlDocument pdxCmdi = new XmlDocument();
        pdxCmdi.Load(sFileCmdi);
        // Get correct namespace manager
        XmlNamespaceManager nsFolia = new XmlNamespaceManager(pdxCmdi.NameTable);
        nsFolia.AddNamespace("f", pdxCmdi.DocumentElement.NamespaceURI);
        oTools.SetXmlDocument(pdxCmdi, pdxCmdi.DocumentElement.NamespaceURI);
        // Zoek het <Subtitle> element 
        XmlNode ndxSubtitle = pdxCmdi.SelectSingleNode("./descendant::f:Subtitle", nsFolia);
        if (ndxSubtitle != null) {
          String sIdMovie = "";
          String sImdbId = "";
          // Kijk of er al een <textHash> element is
          XmlNode ndxTextHash = ndxSubtitle.SelectSingleNode("./child::f:textHash", nsFolia);
          XmlNode ndxWords = ndxSubtitle.SelectSingleNode("./descendant::f:nWords", nsFolia);
          XmlNode ndxSents = ndxSubtitle.SelectSingleNode("./descendant::f:nSentences", nsFolia);
          if (ndxSents == null) {
            ndxSents = ndxSubtitle.SelectSingleNode("./descendant::f:nSents", nsFolia);
            // Does this one exist?
            if (ndxSents != null) {
              // Then change the name to what it should be
              XmlNode ndxNew = oTools.AddXmlChild(ndxSents.ParentNode, "nSentences", "", ndxSents.InnerText, "text");
              // Remove the old one
              ndxNew.ParentNode.RemoveChild(ndxSents);
              ndxSents = ndxNew;
            }
          }
          XmlNode ndxIdMovie = pdxCmdi.SelectSingleNode("./descendant::f:MovieId", nsFolia);
          XmlNode ndxImdb = pdxCmdi.SelectSingleNode("./descendant::f:ImdbId", nsFolia);
          if (ndxTextHash != null && ndxWords != null && ndxSents != null) {
            sHash = ndxTextHash.InnerText;
            iWords = Convert.ToInt32(ndxWords.InnerText);
            iSents = Convert.ToInt32(ndxSents.InnerText);
            sIdMovie = ndxIdMovie.InnerText;
            sImdbId = ndxImdb.InnerText;
            
            // Also add it to another list
            lSubInst.Add(new SubInstance(sFileCmdi, Convert.ToUInt64(sHash), iWords, iSents, sIdMovie, sImdbId));
          } else {
            // This is not really an error, but we should give a warning
            errHandle.Status("HarvestHashFromCmdi: skipping no-hash-having [" + sFileCmdi + "]");
            return true;
          }
        }

        return true;
      } catch (Exception ex) {
        errHandle.DoError("oprConv/HarvestHashFromCmdi", ex);
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
    public bool findDuplicates(ref List<SubInstance> lSubInst, int iGoodHd, ref omdbapi objOmdb) {
      try {
        // Need to have a hash analyzer object
        util.SimHashAnalyzer oSim = new util.SimHashAnalyzer();
        // Walk through the list of instances
        for (int i=0;i<lSubInst.Count;i++) {
          // Walk through all other instances that could match up with me
          for (int j=0;j< i;j++) {
            // Check if the idmove or the imdbid is similar between <i,j>
            if (lSubInst[i].sIdMovie == lSubInst[j].sIdMovie ||
                lSubInst[i].sImdbId == lSubInst[j].sImdbId) {
              // Get the hamming distance between items <i,j>
              int iHdist = oSim.GetHammingDistance(lSubInst[i].simhash, lSubInst[j].simhash);
              // Is this on or below the threshold?
              if (iHdist <= iGoodHd) {
                // So this is probably a duplicate of me -- add it
                lSubInst[i].addDuplicate(j);
                // DO NOT add me to the list of the other one --> EXTINCT
                // lSubInst[j].addDuplicate(i);
                // DO add me to the list of the other one (otherwise order of occurrance plays a role)
                lSubInst[j].addDuplicate(i);
              }
            }
          }
        }

        // Walk through all the instances again
        for (int i = 0; i < lSubInst.Count; i++) {
          String sTargetDir = sDirRoot;   // Directory where we will store the result

          // Get this instance
          SubInstance oOrg = lSubInst[i];

          // Initialisations
          String sLink = "";
          List<String> lSimilar = new List<string>();
          List<String> lEvid = new List<string>();

          // Does this one have duplicates?
          if (oOrg.lDup.Count>0) {
            // Find the first longest text both in words and sentences
            int iWords = oOrg.words;
            int iSents = oOrg.sents;
            int iLongest = -1;
            bool bEqual = true;    // Assume copies are NOT equal...
            for (int j=0;j<oOrg.lDup.Count;j++) {
              SubInstance oThis = lSubInst[oOrg.lDup[j]];
              if (oThis.words> iWords && oThis.sents >= iSents) {
                // Adapt the new maximum
                iWords = oThis.words;
                iSents = oThis.sents;
                iLongest = j;
              }
              // Check for inequality
              if (oThis.words != iWords || oThis.sents != iSents) bEqual = false;
            }
            // Check what the longest is; that's the most 'original'
            if (iLongest<0) {
              // Are they equal?
              if (bEqual) {
                // The [oOrg] is the longest
                oOrg.license = "equal";
                sLink = "list";
              } else {
                // The [oOrg] is the longest
                oOrg.license = "largest";
                sLink = "this";
              }
              // Create a list of similar ones
              for (int j = 0; j < oOrg.lDup.Count; j++) {
                // aSimilar.Add(lSubInst[oOrg.lDup[j]].name);
                lSimilar.Add(lSubInst[oOrg.lDup[j]].name);
              }
            } else {
              // Another one is the longest
              oOrg.license = "copy";
              sLink = lSubInst[iLongest].name;
            }
          } else {
            // This is a unique subtitle file
            oOrg.license = "unique";
            sLink = "none";
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
            // Get the movie id
            XmlNode ndxMovieId = pdxCmdi.SelectSingleNode("./descendant::f:MovieId", nsFolia);
            String sIdMovie = ndxMovieId.InnerText;
            // Get the list of languages
            String sSubLangs = "";
            if (!getSubtitleLanguages(sIdMovie, ref sSubLangs)) return false;
            // Remove any available list
            XmlNode ndxAvailableList = ndxSubtitle.SelectSingleNode("./child::f:AvailableList", nsFolia);
            if (ndxAvailableList != null) {
              ndxAvailableList.RemoveAll();
              ndxSubtitle.RemoveChild(ndxAvailableList);
              XmlNode ndxTmp = ndxSubtitle.SelectSingleNode("./child::f:languageAvailable", nsFolia);
              if (ndxTmp != null) {
                ndxTmp.RemoveAll();
                ndxSubtitle.RemoveChild(ndxTmp);
              }
            }

            // Process the languages available list:
            XmlNode ndxLngAvail = ndxSubtitle.SelectSingleNode("./child::f:languageAvailable", nsFolia);
            if (ndxLngAvail == null) {
              ndxLngAvail = oTools.AddXmlChild(ndxSubtitle, "languageAvailable", "", sSubLangs, "text");
            } else {
              // Check if this string is already in there
              if (!ndxLngAvail.InnerText.Contains(sSubLangs)) {
                // Add the string
                ndxLngAvail.InnerText = ndxLngAvail.InnerText + " " + sSubLangs;
              }
            }
            // Check if there is a statusinfo child
            XmlNode ndxStatusInfo = ndxSubtitle.SelectSingleNode("./child::f:StatusInfo", nsFolia);
            if (ndxStatusInfo == null) {
              // Create such a child
              ndxStatusInfo = oTools.AddXmlChild(ndxSubtitle, "StatusInfo", 
                "status", "", "attribute",
                "link", "", "attribute");
            } else {
              // Get any status info evidence there is
              XmlNode ndxEvid = ndxStatusInfo.SelectSingleNode("./child::f:Evidence", nsFolia);
              while (ndxEvid != null) {
                lEvid.Add(ndxEvid.InnerText);
                ndxEvid = ndxEvid.SelectSingleNode("./following-sibling::f:Evidence", nsFolia);
              }
            }
            // Add the information into the status info node
            ndxStatusInfo.Attributes["status"].Value = oOrg.license;
            ndxStatusInfo.Attributes["link"].Value = sLink;
            // Remove any previous links that might be still in here
            XmlNode ndxWork = ndxStatusInfo.SelectSingleNode("./child::f:Similar", nsFolia);
            while (ndxWork != null) {
              XmlNode ndxRemove = ndxWork;
              ndxWork = ndxWork.SelectSingleNode("./following-sibling::f:Similar", nsFolia);
              // Remove worknode contents
              ndxRemove.RemoveAll();
              // Remove the worknode itself
              ndxStatusInfo.RemoveChild(ndxRemove);
            }
            // Add any links
            for (int j=0;j<lSimilar.Count;j++) {
              XmlNode ndxSimi = oTools.AddXmlChild(ndxStatusInfo, "Similar",
                "SimilarId", Convert.ToString(j+1), "attribute");
              ndxSimi.InnerText = Path.GetFileNameWithoutExtension( lSimilar[j]);
            }
            // Try to find out more information, depending on the status we have found
            switch (oOrg.license) {
              case "copy":
              case "largest":
              case "equal":
              case "unique":
                bool bCopyright = false;
                bool bTranslated = false;
                bool bDownload = false;
                String sDetails = "";
                // Walk through all evidence
                for (int j=0; j<lEvid.Count;j++) {
                  // Get this evidence
                  String sEvid = lEvid[j].ToLower();
                  // Check for RIP information
                  // "*vertaald*|*vertaling*|*ondertiteling*|*bewerkt*|*ripped*|*download*|*copyright*"
                  if (util.General.DoLike(sEvid, "*ripped*|*copyright*")) {
                    // Add the copyright information as evidence if it is necessary
                    if (bTranslated || bDownload || !bCopyright) sDetails = sEvid;
                    bCopyright = true;
                  } else if (util.General.DoLike(sEvid, "*vertaald*|*vertaling*|*ondertiteling*|*bewerkt*")) {
                    // Double check the text of the evidence
                    if (util.General.DoLike(sEvid, "*broadcast text*|*bti *")) {
                      // This is 'stolen' from BTI or its predecessor
                      bCopyright = true;
                      sEvid = "BTI: " + sEvid;
                    } else {
                      bTranslated = true;
                    }
                    if (sDetails == "" || bDownload) sDetails = sEvid;
                  } else if (util.General.DoLike(sEvid, "*download*")) {
                    bDownload = true;
                    if (sDetails == "") sDetails = sEvid;
                  }
                }
                // Additional check
                XmlNode ndxUserClass = ndxSubtitle.SelectSingleNode("./descendant::f:Author/child::f:UserClass", nsFolia);
                if (ndxUserClass != null) {
                  String sUserClass = ndxUserClass.InnerText.ToLower();
                  if (sUserClass == "subtranslator") {
                    bTranslated = true;
                    sDetails = "userclass=SubTranslator";
                  }
                }
                // We should be able to determine the license information
                String sLicense = "";
                if (bCopyright)
                  sLicense = "copyright";
                else if (bTranslated)
                  sLicense = "translation";
                else if (bDownload)
                  sLicense = "download";
                else
                  sLicense = "unknown";
                // Find the location where we are going to put this information
                XmlNode ndxLicenseType = ndxSubtitle.SelectSingleNode("./descendant::f:LicenseType", nsFolia);
                ndxLicenseType.InnerText = sLicense;
                XmlNode ndxLicenseDetails = ndxSubtitle.SelectSingleNode("./descendant::f:LicenseDetails", nsFolia);
                ndxLicenseDetails.InnerText = sDetails;
                // Adapt the target directory
                XmlNode ndxYear = pdxCmdi.SelectSingleNode("./descendant::f:Year", nsFolia);
                XmlNode ndxImdbId = pdxCmdi.SelectSingleNode("./descendant::f:ImdbId", nsFolia);
                String sYear = ndxYear.InnerText;
                String sImdbId = ndxImdbId.InnerText;
                // Determine the target directory...
                // WAS: sTargetDir += oOrg.license + "/" + sLicense + "/";
                if (sYear != "")
                  sTargetDir += sYear + "/";
                else
                  sTargetDir += "unknown/";
                if (sImdbId != "") sTargetDir += sImdbId + "/";
                break;
              default:
                // No further license determination is needed, since this is a copy 
                break;
            }
          }
          // Zoek het <Movie> element 
          XmlNode ndxMovie = pdxCmdi.SelectSingleNode("./descendant::f:Movie", nsFolia);
          if (ndxMovie != null) {
            // Movie information needs to be gathered *ALWAYS*
            XmlNode ndxImdbId = ndxMovie.SelectSingleNode("./child::f:ImdbId", nsFolia);
            String sImdbId = ndxImdbId.InnerText;
            // Get the movie information
            MovieInfo oInfo = objOmdb.getInfo(sImdbId);
            if (oInfo == null) {
              // Not sure what to do now
              int iError = 1;
              errHandle.Status("findDuplicates: could not get information for imdb="+sImdbId);
            } else {
              // (1) Add the runtime information
              if (!addOneInfo(ndxMovie, nsFolia, "Runtime", oInfo.runtime)) return false;
              // (2) Add the COUNTRY information
              if (!addMultiInfo(ndxMovie, nsFolia, "Country", oInfo.country)) return false;
              // (3) Add the GENRE information
              if (!addMultiInfo(ndxMovie, nsFolia, "Genre", oInfo.genre)) return false;
              // (4) Add the LANGUAGE information
              if (!addMultiInfo(ndxMovie, nsFolia, "Language", oInfo.language)) return false;
              // (5) Add the DIRECTOR information
              if (!addMultiInfo(ndxMovie, nsFolia, "Director", oInfo.director)) return false;
              // (6) Add the WRITER information
              if (!addMultiInfo(ndxMovie, nsFolia, "Writer", oInfo.writer)) return false;
              // (7) Add the ACTOR information
              if (!addMultiInfo(ndxMovie, nsFolia, "Actor", oInfo.actors)) return false;
              // (8) Add other information: rated, released, plot, awards, imdbRating, imdbVotes
              if (!addOneInfo(ndxMovie, nsFolia, "Rated", oInfo.rated)) return false;
              if (!addOneInfo(ndxMovie, nsFolia, "Released", oInfo.released)) return false;
              if (!addOneInfo(ndxMovie, nsFolia, "Plot", oInfo.plot)) return false;
              if (!addOneInfo(ndxMovie, nsFolia, "Awards", oInfo.awards)) return false;
              if (!addOneInfo(ndxMovie, nsFolia, "imdbRating", oInfo.imdbRating)) return false;
              if (!addOneInfo(ndxMovie, nsFolia, "imdbVotes", oInfo.imdbVotes.Replace(",", ""))) return false;
              // (9) Look for the <Series>...
              XmlNode ndxSeries = pdxCmdi.SelectSingleNode("./descendant::f:Series", nsFolia);
              if (ndxSeries != null) {
                // Get the nodes we are interested in
                XmlNode ndxSeason = ndxSeries.SelectSingleNode("./child::f:Season", nsFolia);
                XmlNode ndxEpisode = ndxSeries.SelectSingleNode("./child::f:Episode", nsFolia);
                XmlNode ndxParent = ndxSeries.SelectSingleNode("./child::f:ParentImdbId", nsFolia);
                MovieInfo oParent = null;
                if (ndxParent != null && ndxParent.InnerText != "") {
                  String sParentImdbId = ndxParent.InnerText;
                  oParent = objOmdb.getInfo(sParentImdbId);
                }
                // Add the season/episode information
                if (ndxSeason != null && ndxEpisode != null) {
                  if (oParent== null) {
                    oTools.AddAttribute(ndxSeason, "Name", "");
                    oTools.AddAttribute(ndxEpisode, "Name", "");
                  } else {
                    oTools.AddAttribute(ndxSeason, "Name", "");
                    oTools.AddAttribute(ndxEpisode, "Name", "");
                  }
                }
              }
            }
          }
          // Save the adapted CMDI
          pdxCmdi.Save(sFileCmdi);

          // Create the target directory if it does not exist yet
          if (!Directory.Exists(sTargetDir)) {
            Directory.CreateDirectory(sTargetDir);
          }
          // Get the file name
          String sName = Path.GetFileNameWithoutExtension(sFileCmdi).Replace(".cmdi", "");
          String sSrc = Path.GetDirectoryName(sFileCmdi);
          if (!sSrc.EndsWith("/") && !sSrc.EndsWith("\\")) sSrc += "/";
          // Copy the CMDI
          File.Copy(sFileCmdi, sTargetDir + sName + ".cmdi.xml", true);
          // Copy the folia
          sName = sName + ".folia.xml.gz";
          File.Copy(sSrc + sName, sTargetDir + sName, true);
          // Show where we are
          errHandle.Status("copying:\t" + oOrg.name + "\t" + oOrg.license + "\t" + sLink + "\t" + sTargetDir);

        }


        // Return positively
        return true;
      } catch (Exception ex) {
        errHandle.DoError("oprConv/findDuplicates", ex);
        return false;
      }
    }

    /* -------------------------------------------------------------------------------------
      * Name:        addOneInfo
      * Goal:        Add information as <List> + items under [ndxMovie]
      * History:
      * 24/feb/2016 ERK Created
        ------------------------------------------------------------------------------------- */
    private bool addOneInfo(XmlNode ndxMovie, XmlNamespaceManager nsFolia, String sType, String sValue) {
      try {
        XmlNode ndxTarget = ndxMovie.SelectSingleNode("./child::f:" + sType, nsFolia);
        if (ndxTarget == null) {
          // Add a new child
          ndxTarget = oTools.AddXmlChild(ndxMovie, sType);
        }
        // Adapt the value of the child
        ndxTarget.InnerText = sValue;
        // Return positively
        return true;
      } catch (Exception ex) {
        errHandle.DoError("oprConv/addOneInfo", ex);
        return false;
      }
    }

    /* -------------------------------------------------------------------------------------
      * Name:        addMultiInfo
      * Goal:        Add information as <List> + items under [ndxMovie]
      * History:
      * 24/feb/2016 ERK Created
        ------------------------------------------------------------------------------------- */
    private bool addMultiInfo(XmlNode ndxMovie, XmlNamespaceManager nsFolia, String sType, String sList) {
      try {
        XmlNode ndxList = ndxMovie.SelectSingleNode("./child::f:"+sType+"List", nsFolia);
        if (ndxList == null) {
          ndxList = oTools.AddXmlChild(ndxMovie, sType + "List");
        } else {
          // Remove any previous CHILDREN
          ndxList.RemoveAll();
        }
        String[] arList = sList.Split(',');
        for (int j = 0; j < arList.Length; j++) {
          // Find out what the item is
          String sOneItem = arList[j].Trim();
          oTools.AddXmlChild(ndxList, sType,
            sType + "Id", Convert.ToString(j + 1), "attribute",
            "", sOneItem, "text");
        }

        return true;
      } catch (Exception ex) {
        errHandle.DoError("oprConv/addMultiInfo", ex);
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

    /// <summary>
    /// getSubtitleLanguages --
    ///     Get the available subtitle languages from the @sIdMovie
    ///     
    /// </summary>
    /// <param name="sIdMovie"></param>
    /// <param name="sLanguages"></param>
    /// <returns>boolean</returns>
    private bool getSubtitleLanguages(String sIdMovie, ref String sLanguageList) {
      // String sLanguageList;
      try {
        // Locate the movie's informatin through its id
        if (!bInit) return false;
        // Locate the movie's name
        if (dicMovie.ContainsKey(sIdMovie)) {
          dicMovie.TryGetValue(sIdMovie, out sLanguageList);
        } else {
          sLanguageList = "(no languages)";
        }

        // Return positively
        return true;
      } catch (Exception ex) {
        errHandle.DoError("getSubtitleLanguages", ex); // Provide standard error message
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
            if (arPart.Length > 2) {
              // Get the id and the name
              String sIdMovie = arPart[0];
              String sLanguageList = arPart[2].Trim();
              // Possibly take off leading and ending quotation marks
              if (sLanguageList.StartsWith("\"") && sLanguageList.EndsWith("\"")) {
                sLanguageList = sLanguageList.Substring(1, sLanguageList.Length - 2).Trim();
              }
              // Does this item already exist?
              if (dicMovie.ContainsKey(sIdMovie)) {
                // Check if it is already there
                if (!dicMovie[sIdMovie].Contains(sLanguageList)) {
                  // Add the language list information to what is available already
                  dicMovie[sIdMovie] += sLanguageList;
                }
              } else {
                // Store it in the dictionary
                dicMovie.Add(sIdMovie, sLanguageList);
              }
            }
          }
        }
        // Set the dicsource value
        this.sDicSource = Path.GetFileNameWithoutExtension(sFileIn);

        // Return positively
        bInit = true;
        return true;
      } catch (Exception ex) {
        errHandle.DoError("loadMovieDictionary", ex); // Provide standard error message
        return false;
      }
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
