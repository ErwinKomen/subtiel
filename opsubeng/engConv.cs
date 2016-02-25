using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using opsub;
using System.Xml;
using System.Text.RegularExpressions;

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
      String sMovieIdNL = "";   // Dutch   open-subtitles movie id
      String sMovieIdEN = "";   // English open-subtitles movie id
      String sFileIn = "";      // Input subtitle .xml file
      String sFileEn = "";      // English .folia.xml output file
      String sFileTr = "";      // A .folia.xml file that contains the translation in <complexalignments>

      try {
        // Validate
        if (!File.Exists(sFileCmdi)) return false;
        // Get the open subtitle id's
        sMovieIdNL = getMovieId(ndxLinkGrp.Attributes["toDoc"].Value);
        sMovieIdEN = getMovieId(ndxLinkGrp.Attributes["fromDoc"].Value);
        // Find the Dutch .folia.xml.gz file (converted FoLiA format)
        sFileIn = getDutchFolia(sMovieIdNL);
        String[] arNL = Directory.GetFiles(sDirInput, sFileIn + ".gz", SearchOption.AllDirectories);
        // If we don't have this input, then return empty-handed
        if (arNL.Length == 0) return false;
        // Find the English .folia.xml.gz file (converted FoLiA format)

        // Create English folia.xml

        // Determine the output file

        return true;
      } catch (Exception ex) {
        errHandle.DoError("engConv/ConvertOneEngToFolia", ex);
        return false;
      }
    }

    /// <summary>
    /// getMovieId -- retrieve the movie id number from the given string
    /// </summary>
    /// <param name="sPath"></param>
    /// <returns></returns>
    private String getMovieId(String sPath) {
      String sBack = "";

      try {
        // Start by taking the last part of the path
        sBack = Path.GetFileName(sPath);
        // Find the underscore
        int iUnd = sBack.IndexOf("_");
        if (iUnd > 0) {
          sBack = sBack.Substring(0, iUnd);
        }
        // Return what we found
        return sBack;
      } catch (Exception ex) {
        errHandle.DoError("engConv/ConvertOneEngToFolia", ex);
        return "";
      }
    }

    /// <summary>
    /// getDutchFolia -- Find the .folia.xml.gz belonging to the movie-id
    /// 
    /// </summary>
    /// <param name="sMovieId"></param>
    /// <returns></returns>
    private String getDutchFolia(String sMovieId) {
      String sBack = "";

      try {
        // Think of file name
        int iMovieId = Convert.ToInt32(sMovieId);
        sBack = "S-O_" + iMovieId.ToString("D8") + ".folia.xml";

        // Return what we found
        return sBack;
      } catch (Exception ex) {
        errHandle.DoError("engConv/getDutchFolia", ex);
        return "";
      }
    }

  }
}
