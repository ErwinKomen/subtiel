using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using opsubcrp.util;

namespace opsubcrp {
  /* -------------------------------------------------------------------------------------
   * Name:  oscMain
   * Goal:  entry point of command-line "open subtitle corpus" program
   *        Convert existing open subtitle files into basic .folia.xml files 
   * History:
   * 25/jan/2016 ERK Created
     ------------------------------------------------------------------------------------- */
  class oscMain {
    // =================== My own static variables =======================================
    static ErrHandle errHandle = new ErrHandle();
    static String[] arInput;   // Array of input files
    static String strOutDir;   // Output directory
    // =================== Local variables ===============================================
    // Command-line entry point + argument handling
    static void Main(string[] args) {
      String sInput = "";       // Input file or dir
      String sOutput = "";      // Output directory
      String sDict = "";        // Movie dictionary
      bool bIsDebug = false;    // Debugging
      bool bForce = false;      // Force

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
              case "o": // Output directory
                sOutput = args[++i];
                break;
              case "m": // Movie dictionary
                sDict = args[++i];
                break;
              case "f": // Force
                bForce = true;
                break;
              case "d": // Debugging
                bIsDebug = true;
                break;
            }
          } else {
            // Throw syntax error and leave
            SyntaxError("1 - i=" + i + " args=" + args.Length + " argCurrent=[" + sArg + "]"); return;
          }
        }
        // Check presence of input/output
        if (sInput == "" || sOutput == "") { SyntaxError("2"); return; }
        // Check existence of output dir
        if (!Directory.Exists(sOutput)) {
          // Create the output directory
          Directory.CreateDirectory(sOutput);
        }
        // Initialize the main entry point for the conversion
        opsConv objConv = new opsConv();

        // Load the movie dictionary
        if (!objConv.loadMovieDictionary(sDict)) {
          errHandle.DoError("Main", "Could not load movie dictionary from [" + sDict + "]");
          return;
        }

        // Initialise the Treebank Xpath functions, which may make use of tb:matches()
        util.XPathFunctions.conTb.AddNamespace("tb", util.XPathFunctions.TREEBANK_EXTENSIONS);

        // Check if the input is a directory or file
        if (Directory.Exists(sInput)) {
          WalkDirectoryTree(sInput, "*.gz", sInput, sOutput, bForce, bIsDebug, ref objConv);
        } else {
          // Show we don't have input file
          errHandle.DoError("Main", "Cannot find input file(s) in: " + sInput);
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
    /// <param name="sOutput"></param>
    /// <param name="bForce"></param>
    /// <param name="bIsDebug"></param>
    /// <param name="objConv"></param>
    static void WalkDirectoryTree(String sStartDir, String sFilter, String sInput, 
      String sOutput, bool bForce, bool bIsDebug, ref opsConv objConv) {
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
        // Walk all files in this directory
        foreach (String sFile in arFiles) {
          // Parse this input file to the output directory
          if (!objConv.ConvertOneOpsToFolia(sFile, sInput, sOutput, bForce, bIsDebug)) {
            errHandle.DoError("Main", "Could not convert file [" + sFile + "]");
            return;
          }
        }

        // Now find all the subdirectories under this directory.
        arSubDirs = Directory.GetDirectories(sStartDir);
        // Walk all directories
        foreach (String sDirName in arSubDirs) {
          // Resursive call for each subdirectory.
          WalkDirectoryTree(sDirName, sFilter, sInput, sOutput, bForce, bIsDebug, ref objConv);
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
      Console.WriteLine("Syntax: opsubcrp -i inputDir -o outputDir [-d] \n" +
        "\n\n\tNote: output directory must differ from input one\n");
    }

  }
}
