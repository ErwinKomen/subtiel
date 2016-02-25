namespace opsub.util {
  #region Libraries
  using System;
  using System.Collections.Generic;
  using SimHashAlgorithm.Interfaces;
  #endregion
  class OverlappingStringTokeniser : ITokeniser {
    #region Constants and Fields
    private readonly ushort chunkSize = 4;
    private readonly ushort overlapSize = 3;
    #endregion

    #region Constructors and Destructors

    public OverlappingStringTokeniser(ushort chunkSize, ushort overlapSize) {
      if (chunkSize <= overlapSize) {
        throw new ArgumentException("Chunck size must be greater than overlap size.");
      }
      this.overlapSize = overlapSize;
      this.chunkSize = chunkSize;
    }

    #endregion

    #region Public Methods and Operators

    public IEnumerable<string> Tokenise(string input) {
      var result = new List<string>();
      int position = 0;
      while (position < input.Length - this.chunkSize) {
        result.Add(input.Substring(position, this.chunkSize));
        position += this.chunkSize - this.overlapSize;
      }
      return result;
    }

    #endregion
  }

  class StringToWordTokeniser : ITokeniser {
    #region Constants and Fields
    private readonly ushort minSize = 4;    // Minimum size of words
    private readonly char[] delimiterChars = { ' ', ',', '.', ':', '\t', '\n', '\r' };
    #endregion

    #region class initialization
    public StringToWordTokeniser(ushort iMinSize) {
      this.minSize = iMinSize;
    }
    #endregion

    #region Public Methods and Operators
    public IEnumerable<string> Tokenise(string input) {
      var result = new List<string>();  // Room for the resulting list of strings

      // Split the text on the basis of the delimiter characters
      string[] words = input.Split(delimiterChars);
      // Fill the result list with words that have the minimal size
      for (int i=0;i<words.Length;i++) {
        string sWord = words[i];
        if (sWord.Length>= minSize) {
          result.Add(sWord);
        }
      }
      return result;
    }

    #endregion
  }
}
