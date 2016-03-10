using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace opsub {
  public class FoliaXmlWriter : XmlTextWriter {
    private string sCurrentStartElement = "";   // Keep continuous track of the current start element
    private bool bSkipEndAttr = false;          // This attribute must be completely skipped
    private bool bChangeLanguage = false;       // Change the <t class='language'> 
    public FoliaXmlWriter(String fileName, Encoding encoding) : base(fileName, encoding) {
      init();
    }
    public FoliaXmlWriter(Stream wrStream, Encoding encoding) : base(wrStream, encoding) {
      init();
    }
    private void init() {
      this.Formatting = Formatting.Indented;
      this.Indentation = 2;
      this.Namespaces = true;
      this.WriteStartDocument();
    }

    /// <summary>
    /// WriteStartElement -- do my own checking here
    /// </summary>
    /// <param name="prefix"></param>
    /// <param name="localName"></param>
    /// <param name="ns"></param>
    public override void WriteStartElement(string prefix, string localName, string ns) {
      // Keep track of current start element
      sCurrentStartElement = localName;
      base.WriteStartElement(prefix, XmlConvert.EncodeLocalName(localName), ns);
    }

    public override void WriteAttributes(XmlReader reader, bool defattr) {
      // Check if 'xmlns' is going to be written
      if (reader.Name == "xmlns") {
        int i = 0;
        // Do not write it out
      } else if (reader.Prefix == "xmlns" && sCurrentStartElement != "FoLiA") {
        int i = 0;
      } else if (sCurrentStartElement == "t") {
        int i = 0;

      } else {
        base.WriteAttributes(reader, defattr);
      }
    }
    public override void WriteStartAttribute(string prefix, string localName, string ns) {
      // Check if 'xmlns' is going to be written
      if (localName == "xmlns") {
        int i = 0;
        bSkipEndAttr = true;
        // Do not write it out
      } else if (prefix == "xmlns" && sCurrentStartElement != "FoLiA") {
        bSkipEndAttr = true;
      } else {
        base.WriteStartAttribute(prefix, localName, ns);
      }
    }
    public override void WriteEndAttribute() {
      if (bSkipEndAttr) {
        bSkipEndAttr = false;
      } else {
        base.WriteEndAttribute();
      }
    }
    public override void WriteString(string text) {
      if (bSkipEndAttr) {
        // Skip this string
      } else {
        base.WriteString(text);
      }
    }
  }
}
