using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using opsub;

namespace opsubeng {
  /* -------------------------------------------------------------------------------------
   * Name:  oseMain
   * Goal:  Entry point of command-line "open subtitle English" program
   *         This retrieves and processes the English source of a subtitle instance
   * History:
   * 1/feb/2016 ERK Created
     ------------------------------------------------------------------------------------- */
  class oseMain {
    // =================== My own static variables =======================================
    static ErrHandle errHandle = new ErrHandle();
    // =================== Local variables ===============================================
    // Command-line entry point + argument handling
    static void Main(string[] args) {
      String sDutch = "";         // Input directory for DUTCH subtitles
      String sEnglish = "";       // Input directory for ENGLISH subtitles
      String sOutput = "";        // Output directory
      String sParallel = "";      // File in .xml format that contains the parallel between EN-NL
      bool bIsDebug = false;      // Debugging
      bool bForce = false;        // Force
      String sAction = "english"; // Type of action to be taken

      try {
        // Check command-line options
        for (int i = 0; i < args.Length; i++) {
          // get this argument
          String sArg = args[i];
          if (sArg.StartsWith("-")) {
            // Check out the arguments
            switch (sArg.Substring(1)) {
              case "p": // Xml file containing the parallels between Eng and NL
                sParallel = args[++i];
                break;
              case "n": // Input directory for Dutch: .folia.xml and .cmdi.xml files
                sDutch = args[++i];
                break;
              case "e": // Input directory for English: .folia.xml and .cmdi.xml files
                sEnglish = args[++i];
                break;
              case "o": // Top output directory
                sOutput = args[++i];
                break;
              case "f": // Force
                bForce = true;
                break;
              case "a": // Action
                sAction = args[++i].ToLower();
                if (sAction != "english") {
                  SyntaxError("The only action right now is 'english'");
                  return;
                }
                break;
              case "d": // Debugging
                bIsDebug = true;
                break;
              default:
                // Throw syntax error and leave
                SyntaxError("Unknown option: [" + sArg+ "]");
                return;
            }
          } else {
            // Throw syntax error and leave
            SyntaxError("1 - i=" + i + " args=" + args.Length + " argCurrent=[" + sArg + "]"); return;
          }
        }
        // Check presence of input/output
        if (sDutch == "" || sEnglish == "") { SyntaxError("Both dutch and english input must be specified"); return; }
        if (sParallel == "") { SyntaxError("The XML file containing the Dutch-English parallels must be specified"); return; }

        // Check input directory and parallels file
        if (!Directory.Exists(sDutch)) { errHandle.DoError("Main", "Cannot find Dutch input file(s) in: " + sDutch); return; }
        if (!Directory.Exists(sEnglish)) { errHandle.DoError("Main", "Cannot find English input file(s) in: " + sEnglish); return; }
        if (!File.Exists(sParallel)) { errHandle.DoError("Main", "Cannot find parallel file in: " + sParallel); return; }

        // Initialize the main entry point for the conversion
        engConv objConv = new engConv(errHandle);

        // Set directories where input is situated and output should come
        objConv.dutch = sDutch;
        objConv.english = sEnglish;
        objConv.output = sOutput;

        // Initialise the Treebank Xpath functions, which may make use of tb:matches()
        opsub.util.XPathFunctions.conTb.AddNamespace("tb", opsub.util.XPathFunctions.TREEBANK_EXTENSIONS);

        // Start reading the parallels XML
        ParReader oParallel = new ParReader(sParallel, errHandle);
        // Walk all the <linkGrp> elements
        XmlReader rdLinkGrp = null;
        while (oParallel.getNextLinkGrp(ref rdLinkGrp)) {
          // Process this one
          if (!objConv.ConvertOneEngToFolia(rdLinkGrp, bForce, bIsDebug)) {
            errHandle.DoError("Main", "Could not convert English");
            return; }
        }
        /*
        XmlNode ndxLinkGrp = oParallel.getNextLinkGrp();
        while (ndxLinkGrp!= null) {
          // Process this one
          if (!objConv.ConvertOneEngToFolia(ndxLinkGrp, bForce, bIsDebug)) { errHandle.DoError("Main", "Could not convert English"); return; }
          // Go to the next one
          ndxLinkGrp = oParallel.getNextLinkGrp();
        }
        */

        // Exit the program
        errHandle.Status("Ready");
      } catch (Exception ex) {
        errHandle.DoError("Main", ex); // Provide standard error message
        throw;
      }
    }

 
    /* -------------------------------------------------------------------------------------
     * Name:  SyntaxError
     * Goal:  Show simple syntax error message to the user
     * History:
     * 25/jan/2016 ERK Created
       ------------------------------------------------------------------------------------- */
    static void SyntaxError(String sChk) {
      Console.WriteLine("Syntax: opsubeng -n NLinputDir -e ENinputDir -p ParallelXML -o outputDir [-d] [-f] \n" +
        "\n\n\tNote: output directory must differ from input one\n"+
        "Message: " + sChk);
    }

  }
}
