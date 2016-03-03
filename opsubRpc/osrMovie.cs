﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;
using System.Threading.Tasks;

namespace opsubRpc {
  class osrMovie {
    // ==================== LOCAL VARIABLES ====================================
    private ErrHandle errHandle = null;
    private String sBaseUrl = "http://www.opensubtitles.org/nl/search/sublanguageid-dut/";
    private String idMovie; // The idmovie used by opensubtitles for this movie
    private XmlDocument pdxMovie = null;
    // ==================== Class initializer ==================================
    public osrMovie(ErrHandle oErr, String sLanguage) {
      this.errHandle = oErr;
      idMovie = "";
      pdxMovie = new XmlDocument();
      switch (sLanguage) {
        case "dut":
        case "nld":
          // Tjhis is the default, so no action is required
          break;
        default:
          // Switch to the indicated three-letter language
          sBaseUrl = sBaseUrl.Replace("sublanguageid-dut", "sublanguageid-" + sLanguage);
          break;
      }
    }
    // ==================== METHODS ============================================
    public bool getInformation(String sId, ref XmlNodeList ndList, ref XmlNode ndMovie) {
      try {
        // Check if we currently have information on this movie
        if (sId != this.idMovie) {
          int iWaitTime = 1;    // Number of seconds to wait
          // Try get information
          String sUrl = sBaseUrl + "idmovie-" + sId + "/xml";
          String sContent = WebRequestGetData(sUrl);
          while (!sContent.StartsWith ("<") ) {
            // Try again
            sContent = WebRequestGetData(sUrl);
            // Check what we receive back
            if (sContent == null) {
              // Indicate that this is a premature end
              errHandle.Status("getInformation: webrequest time-out");
              return false;
            }
          }
          // Read as XmlDocument
          pdxMovie.LoadXml(sContent);
          // Set the currnet idmovie
          this.idMovie = sId;
        }
        // Set the list of nodes for this move
        ndList = pdxMovie.SelectNodes("./descendant::subtitle");
        ndMovie = pdxMovie.SelectSingleNode("./descendant::Movie");
        return true;
      } catch (Exception ex) {
        errHandle.DoError("osrMoview/getInformation", ex);
        return false;
      }
    }


    /* -------------------------------------------------------------------------------------
     * Name:        WebRequestPostData
     * Goal:        Post a web request and return the string data
     * Parameters:  url       - Address to use
     *              postData  - Input xml to be sent
     * History:
     * 1/feb/2016 ERK Created
       ------------------------------------------------------------------------------------- */
    private static string WebRequestPostData(string url, string postData) {
      System.Net.WebRequest req = System.Net.WebRequest.Create(url);

      req.ContentType = "text/xml";
      req.Method = "POST";

      byte[] bytes = System.Text.Encoding.ASCII.GetBytes(postData);
      req.ContentLength = bytes.Length;

      using (Stream os = req.GetRequestStream()) {
        os.Write(bytes, 0, bytes.Length);
      }

      using (System.Net.WebResponse resp = req.GetResponse()) {
        if (resp == null) return null;

        using (System.IO.StreamReader sr = new System.IO.StreamReader(resp.GetResponseStream())) {
          return sr.ReadToEnd().Trim();
        }
      }
    }

    /* -------------------------------------------------------------------------------------
      * Name:        WebRequestGetData
      * Goal:        Request data at a web address
      * Parameters:  url       - Address to use
      * History:
      * 1/feb/2016 ERK Created
        ------------------------------------------------------------------------------------- */
    private string WebRequestGetData(string url) {
      System.Net.WebRequest req = null;
      System.Net.WebResponse resp = null;
      int MAX_TRIES = 5;

      try {
        req = System.Net.WebRequest.Create(url);

        req.ContentType = "text/xml";
        req.Method = "GET";

        // Try getting a response...
        bool bFound = false;
        int iTry = 0;
        do {
          try {
            // Wait some time
            System.Threading.Thread.Sleep(iTry * 1000);
            if (iTry > 2) {
              errHandle.Status("waiting: " + iTry + "[" + url + "]");
            }
            resp = req.GetResponse();
            bFound = true;
          } catch (Exception ex) {
            bFound = false;
            iTry++;
          }
        } while (!bFound && iTry < MAX_TRIES);
        if (resp == null) return null;

        using (System.IO.StreamReader sr = new System.IO.StreamReader(resp.GetResponseStream())) {
          return sr.ReadToEnd().Trim();
        }
      } catch (Exception ex) {
        errHandle.DoError("osrMoview/WebRequestGetData", ex);
        return null;
      }
    }

  }
}
