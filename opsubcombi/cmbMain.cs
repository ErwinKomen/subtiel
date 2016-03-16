using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using opsub;

namespace opsubcombi {
  /* -------------------------------------------------------------------------------------
   * Name:  cmbMain
   * Goal:  entry point of command-line "open subtitle Combi" program
   *        Harvest, combine, distribute FoLiA and cmdi files
   * History:
   * 14/mar/2016 ERK Created
     ------------------------------------------------------------------------------------- */
  class cmbMain {
    // =================== My own static variables =======================================
    static ErrHandle errHandle = new ErrHandle();
    // =================== Local variables ===============================================
    static List<String> lstFolia = null; // List of .folia.xml.gz files in the [sFolia] directory
    static List<String> lstCmdi = null;  // List of .cmdi.xml files in the [sCmdi] directory
    // Command-line entry point + argument handling
    static void Main(string[] args) {
      String sLanguage = "";    // Which language to take (two-letter code 'en' or 'nl')
      String sFolia = "";       // Base directory to take (e.g. /vol/tensusers/ekomen)
      String sSubtiel = "";     // Base directory for the output (e.g: /vol/tensusers/ekomen/subtiel)
      String sCmdi = "";        // Base directory for CMDI files
      bool bIsDebug = false;    // Debugging

      try {
        // Check command-line options
        for (int i = 0; i < args.Length; i++) {
          // get this argument
          String sArg = args[i];
          if (sArg.StartsWith("-")) {
            // Check out the arguments
            switch (sArg.Substring(1)) {
              case "f": // Root directory under which the .folia.xml.gz files are located
                sFolia = Path.GetFullPath(args[++i]);
                break;
              case "c": // Root directory where the CMDI files are located
                sCmdi = Path.GetFullPath(args[++i]);
                break;
              case "o": // Root directory where the output files are going to be stored
                sSubtiel = Path.GetFullPath(args[++i]);
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
        if (sFolia == "" || !Directory.Exists(sFolia)) { SyntaxError("No (valid) base directory for FoLiA input"); return; }
        if (sCmdi == "" || !Directory.Exists(sCmdi)) { SyntaxError("No (valid) cmdi directory"); return; }
        if (sSubtiel == "") { SyntaxError("No subtiel directory"); return; }
        // If the target directory is not there, create it
        if (!Directory.Exists(sSubtiel)) {
          Directory.CreateDirectory(sSubtiel);
        }

        // Initialise the Treebank Xpath functions, which may make use of tb:matches()
        XPathFunctions.conTb.AddNamespace("tb", XPathFunctions.TREEBANK_EXTENSIONS);

        // Create a new instance of the combination class
        cmbConv oConv = new cmbConv(errHandle);
        oConv.output = sSubtiel;

        // find .cmdi.xml files
        errHandle.Status("Finding .cmdi.xml files...");
        lstCmdi = Directory.GetFiles(sCmdi, "*.cmdi.xml", SearchOption.AllDirectories).ToList();

        // Convert the list of files into a dictionary
        oConv.cmdi(lstCmdi);

        // Find .folia.xml.gz files
        errHandle.Status("Finding directories in " + sFolia);
        // Walk all directories
        errHandle.Status("Processing .folia.xml.gz files...");
        // Resursive call for each subdirectory.
        WalkDirectoryTree(sFolia, "*.folia.xml.gz", ref oConv);

        // Save the harvested information to an xml file
        if (!oConv.harvestSaveXml( sSubtiel + "/harvest.xml")) { 
          errHandle.DoError("Main", "Could not create harvest summary xml file");
          return;
        }

        // Exit the program
        errHandle.Status("Ready");
      } catch (Exception ex) {
        errHandle.DoError("Main", ex); // Provide standard error message
        throw;
      }
    }

    static void WalkDirectoryTree(String sStartDir, String sFilter, ref cmbConv objConv) {
      String[] arFiles = null;
      String[] arSubDirs = null;

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
        // bSkip = true;
        // Walk all files in this directory
        foreach (String sFile in arFiles) {
          // Process this file
          if (!objConv.harvestOneFolia(sFile)) {
            errHandle.DoError("Main", "Could not process file " + sFile);
            return;
          }
        }

        // Now find all the subdirectories under this directory.
        arSubDirs = Directory.GetDirectories(sStartDir);
        // Walk all directories
        foreach (String sDirName in arSubDirs) {
          // Resursive call for each subdirectory.
          WalkDirectoryTree(sDirName, sFilter, ref objConv);
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
      Console.WriteLine(sChk);
      Console.WriteLine("Syntax: opsubcombi -f foliaDir -c cmdiDir -o outputDir -l language [-d] \n" +
        "\n\n\tNote: output directory must differ from input one\n");
    }



  }
}
