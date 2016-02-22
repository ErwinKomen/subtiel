using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using opsub;
using System.Xml;

namespace opsubeng {
  class engConv {
    // ================================ LOCAL VARIABLES ===========================================
    private ErrHandle errHandle = null;         // Error handler of calling
    private opsub.util.xmlTools oTools = null;  // 
    private String sDirInput = "";              // 
    private String sDirOutput = "";             // Output directory

    // ==================== CLASS INITIALISATION ==================================================
    public engConv(ErrHandle oErr) {
      this.errHandle = oErr;
      this.oTools = new opsub.util.xmlTools(oErr);
    }
    // ==================== GET/SET routines ======================================================
    public String input { set { sDirInput = value; } }
    public String output { set { sDirOutput = value; } }
    // ============================================================================================

    /* -------------------------------------------------------------------------------------
     * Name:        ConvertOneEngToFolia
     * Goal:        Create a .folia.xml file with metadata for the indicated input file
     *              Note: the .cmdi file will be placed in the same directory as FileIn
     * Parameters:  sFileIn     - File to be processed
     *              bForce      - Create result, even though it exists already
     *              bIsDebug    - Debugging mode on or off
     * History:
     * 1/feb/2016 ERK Created
       ------------------------------------------------------------------------------------- */
    public bool ConvertOneEngToFolia(XmlNode ndxLinkGrp, bool bForce, bool bDebug) {
      String sFileCmdi = "";    // A .cmdi.xml file belonging to the Dutch .folia.xml file
      String sFileIn = "";      // Input subtitle .xml file
      String sFileEn = "";      // English .folia.xml output file
      String sFileTr = "";      // A .folia.xml file that contains the translation in <complexalignments>

      try {
        // Validate
        if (!File.Exists(sFileCmdi)) return false;
        // Find the English input file

        // Determine the output file

        return true;
      } catch (Exception ex) {
        errHandle.DoError("engConv/ConvertOneEngToFolia", ex);
        return false;
      }
    }
  }
}
