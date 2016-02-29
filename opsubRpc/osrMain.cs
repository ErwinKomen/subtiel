using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using omdb;

namespace opsubRpc {
  /* -------------------------------------------------------------------------------------
   * Name:  osrMain
   * Goal:  entry point of command-line "open subtitle RPC" program
   *        Find meta-data information for open subtitle files 
   * History:
   * 1/feb/2016 ERK Created
     ------------------------------------------------------------------------------------- */
  class osrMain {
    // =================== My own static variables =======================================
    static ErrHandle errHandle = new ErrHandle();
    // =================== Local variables ===============================================
    static List<SubInstance> lSubInst = new List<SubInstance>();
    // Command-line entry point + argument handling
    static void Main(string[] args) {
      String sInput = "";       // Input file or dir
      String sOutput = "/scratch/ekomen/out/";      // Output directory, if specified
      String sLanguage = "dut";                     // This is the language abbreviation used in [osrMovie.cs] for sBaseUrl
      String sDict = "";        // Movie dictionary
      bool bIsDebug = false;    // Debugging
      bool bForce = false;      // Force
      bool bOview = false;      // Make overview or not
      bool bSkip = false;       // Skip everything that has *not* been made
      String sAction = "cmdi";  // Type of action to be taken

      try {
        // Check command-line options
        for (int i = 0; i < args.Length; i++) {
          // get this argument
          String sArg = args[i];
          if (sArg.StartsWith("-")) {
            // Check out the arguments
            switch (sArg.Substring(1)) {
              case "i": // Input file or directory with .folia.xml files
                sInput = args[++i];
                break;
              case "f": // Force
                bForce = true;
                break;
              case "s": // Skip
                bSkip = true;
                break;
              case "m": // Movie dictionary   -- Tab-separated list from opensubtitles.org
                sDict = args[++i];
                break;
              case "o": // Output directory
                sOutput = args[++i];
                break;
              case "h": // Calculate hashes and add them to existing .cmdi.xml files
                sAction = "hash";
                break;
              case "v": // Make an overview
                bOview = true;
                break;
              case "d": // Debugging
                bIsDebug = true;
                break;
              case "l": // Language (three letter code)
                sLanguage = args[++i];
                break;
            }
          } else {
            // Throw syntax error and leave
            SyntaxError("1 - i=" + i + " args=" + args.Length + " argCurrent=[" + sArg + "]"); return;
          }
        }
        // Check presence of input/output
        if (sInput == "" ) { SyntaxError("2"); return; }

        // Initialize the main entry point for the conversion
        oprConv objConv = new oprConv(errHandle);
        osrMovie objMovie = new osrMovie(errHandle, sLanguage);
        omdbapi objOmdb = new omdbapi(errHandle);

        // Set directory for conversion
        objConv.dirRoot(sOutput);

        // Load the movie dictionary
        if (!objConv.loadMovieDictionary(sDict)) {
          errHandle.DoError("Main", "Could not load movie dictionary from [" + sDict + "]");
          return;
        }

        // Initialise the Treebank Xpath functions, which may make use of tb:matches()
        util.XPathFunctions.conTb.AddNamespace("tb", util.XPathFunctions.TREEBANK_EXTENSIONS);

        // Check if the input is a directory or file
        if (Directory.Exists(sInput)) {
          WalkDirectoryTree(sInput, "*.folia.xml.gz", sInput, bForce, bSkip, bIsDebug, sAction, 
            ref objConv, ref objMovie);
        } else {
          // Show we don't have input file
          errHandle.DoError("Main", "Cannot find input file(s) in: " + sInput);
        }
        // Calculate for each file which others are close to it
        // - try to determine the license information for the best matching .cmdi.xml files
        // - add some more meta-information to the .cmdi.xml files
        objConv.findDuplicates(ref lSubInst, 3, ref objOmdb);

        // Create an overview - if required
        if (bOview) {
          String sOview = objConv.getDistanceOview();
          // Save it in a standard file
          String sFileCsv = Path.GetDirectoryName(sInput) + "/oview.csv";
          File.WriteAllText(sFileCsv, sOview);
        }
        // Exit the program
        Console.WriteLine("Ready");
      } catch (Exception ex) {
        errHandle.DoError("Main", ex); // Provide standard error message
        throw;
      }
    }

    /// <summary>
    /// WalkDirectoryTree --
    ///     Recursively walk the directory starting with @sStartDir
    ///     Execute conversion on any .gz file encountered using @objConv
    /// </summary>
    /// <param name="sStartDir"></param>
    /// <param name="sFilter"></param>
    /// <param name="sInput"></param>
    /// <param name="bForce"></param>
    /// <param name="bIsDebug"></param>
    /// <param name="sAction">The action to be taken: "cmdi", "hash"</param>
    /// <param name="objConv"></param>
    /// <param name="objMovie"></param>
    /// <param name="objOmdb"></param>
    static void WalkDirectoryTree(String sStartDir, String sFilter, String sInput, bool bForce, 
      bool bSkip, bool bIsDebug, String sAction, ref oprConv objConv, ref osrMovie objMovie) {
      String[] arFiles = null;
      String[] arSubDirs = null;

      // Exclude 'raw'
      if (sStartDir.Contains("/raw/") || sStartDir.Contains("\\raw\\")) return;
      // First, process all the files directly under this folder
      try {
        arFiles = Directory.GetFiles(sStartDir, sFilter);
      }
      // This is thrown if even one of the files requires permissions greater
      // than the application provides.
      catch (UnauthorizedAccessException e) {
        // Only give warning
        errHandle.Status(e.Message);
      } catch (System.IO.DirectoryNotFoundException e) {
        errHandle.Status(e.Message);
      }

      // Check if all is valid
      if (arFiles != null) {
        bSkip = true;
        // Walk all files in this directory
        foreach (String sFile in arFiles) {
          // What we do here depends on the action identified
          switch(sAction) {
            case "cmdi": 
              // Parse this input file to the output directory
              if (!objConv.ConvertOneOpsToCmdi(sFile, ref objMovie, bForce, bIsDebug)) {
                errHandle.DoError("Main", "Could not convert file [" + sFile + "]");
                return;
              }
              break;
            case "hash":
              if (bSkip) {
                if (!objConv.HarvestHashFromCmdi(sFile, ref lSubInst)) {
                  errHandle.DoError("Main", "Could not harvest hash for file [" + sFile + "]");
                  return;
                }
              } else { 
                // Calculate the HASH of this .folia.xml file, and put it into the existing CMDI
                if (!objConv.CalculateHashToCmdi(sFile, ref lSubInst, bIsDebug)) {
                  errHandle.DoError("Main", "Could not calculate hash for file [" + sFile + "]");
                  return;
                }
              }
              break;
          }
        }

        // Now find all the subdirectories under this directory.
        arSubDirs = Directory.GetDirectories(sStartDir);
        // Walk all directories
        foreach (String sDirName in arSubDirs) {
          // Resursive call for each subdirectory.
          WalkDirectoryTree(sDirName, sFilter, sInput, bForce, bSkip, bIsDebug, sAction, ref objConv, ref objMovie);
        }
      }
    }


    /* -------------------------------------------------------------------------------------
     * Name:  SyntaxError
     * Goal:  Show simple syntax error message to the user
     * History:
     * 25/jan/2016 ERK Created
       ------------------------------------------------------------------------------------- */
    static void SyntaxError(String sChk) {
      Console.WriteLine("Syntax: opsubRpc -i inputDir -o outputDir [-d] \n" +
        "\n\n\tNote: output directory must differ from input one\n");
    }



  }

  class SubInstance {
    public String name;     // The name of the subtitle file without dir and extension(s)
    public String file;     // Full location of the subtitle .folia.xml file
    public UInt64 simhash;  // The 64-bit similarity hash
    public int words;       // Number of words
    public int sents;       // Number of sentences
    public List<int> lDup;  // List of duplicates to this one
    public String license;  // The license code for this one
    public String details;  // Details that can help identify the license
    public String sIdMovie; // The idmovie (should be known)
    public String sImdbId;  // The @imdb number of this movie
    public SubInstance(String sFile, UInt64 iSimHash, int iWords, int iSents, String sIdMovie, String sImdbId) {
      this.file = sFile;
      this.name = Path.GetFileNameWithoutExtension(sFile);
      this.simhash = iSimHash;
      this.words = iWords;
      this.sents = iSents;
      this.lDup = new List<int>();
      this.license = "";
      this.details = "";
      this.sIdMovie = sIdMovie;
      this.sImdbId = sImdbId;
    }
    public void addDuplicate(int iDup) {
      this.lDup.Add(iDup);
    }
    // ======================= Getters and setters ===============
    public String idmovie { get { return this.sIdMovie; } set { this.sIdMovie = value; } }
    public String imdbid { get { return this.sImdbId; } set { this.sImdbId = value; } }
  }
}
