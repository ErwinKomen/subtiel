using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using System.Threading.Tasks;
using System.Reflection;
using opsubRpc;

namespace omdb {
  class omdbapi {
    private String sApiStart = "http://www.omdbapi.com/?i=@tt&plot=short&r=xml";
    private int iBufSize = 1024;
    private ErrHandle errHandle;
    /*
    private String[] arField = { "title", "year", "rated", "released", "runtime", "genre", "director",
    "writer", "actors", "plot", "language", "country", "awards", "imdbRating", "imdbVotes", "type"};
    */
    // ================= CLASS INITIALIZER ============================================
    public omdbapi(ErrHandle oErr) {
      this.errHandle = oErr;
    }

    /* -------------------------------------------------------------------------------------
     * Name:        getInfo
     * Goal:        Use the 'omdbapi' site to retrieve information on the movie with the indicated @imdbId
     * Parameters:  sImdbId     - The ImdbId of the movie we are interested in
     * History:
     * 24/feb/2016 ERK Created
       ------------------------------------------------------------------------------------- */
    public MovieInfo getInfo(String sImdbId) {

      try {
        // Create the object we will return
        MovieInfo oBack = new MovieInfo();
        // Create the imdbid string: "tt" + exactly 7 numbers, possibly prepended by '0'
        int iImdbId = Convert.ToInt32(sImdbId);
        sImdbId = "tt" + iImdbId.ToString("D7"); 

        // Create the request string
        String sRequest = sApiStart.Replace("@tt", sImdbId);
        // Make the request
        WebRequest request = WebRequest.Create(sRequest);
        request.Method = "GET";
        WebResponse response = request.GetResponse();
        StringBuilder sbReply = new StringBuilder();
        // Process the result
        using (Stream strResponse = response.GetResponseStream())
        using (StreamReader rdThis = new StreamReader(strResponse)) {
          Char[] readBuff = new Char[iBufSize];
          int iCount = rdThis.Read(readBuff, 0, iBufSize);
          while (iCount > 0) {
            // Append the information to the stringbuilder
            sbReply.Append(new String(readBuff, 0, iCount));
            // Make a follow-up request
            iCount = rdThis.Read(readBuff, 0, iBufSize);
          }
        }
        // Convert the XML reply to a processable object
        XmlDocument pdxReply = new XmlDocument();
        pdxReply.LoadXml(sbReply.ToString());
        // Get to the information
        XmlNode ndxInfo = pdxReply.SelectSingleNode("./descendant-or-self::movie");
        if (ndxInfo == null) return null;
        // Fill the object we will return
        // Iterate over all the properties of object 'MovieInfo' 
        //    NOTE: they have to be implemented as 'properties' of the class...
        foreach(PropertyInfo prop in typeof(MovieInfo).GetProperties()) {
          // Set the value of oBack's property using the information in the Xml node
          prop.SetValue(oBack, ndxInfo.Attributes[prop.Name].Value);
        }

        // Read the reply as a 
        return oBack;
      } catch (Exception ex) {
        errHandle.DoError("oprConv/getInfo", ex);
        return null;
      }
    }
  }
  
}
