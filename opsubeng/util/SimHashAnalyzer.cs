using System;
using System.Collections.Generic;
using System.Text;
using SimHashAlgorithm.Interfaces;

namespace SimHashAlgorithm.Interfaces {
  public interface ITokeniser {
    #region Public Methods and Operators

    IEnumerable<string> Tokenise(string input);

    #endregion
  }
  public interface IAnalyser {
    #region Public Methods and Operators

    float GetLikenessValue(string needle, string haystack);

    #endregion
  }

}


namespace opsub.util {
  class SimHashAnalyzer {
    #region Constants and Fields

    private const int HashSize = 64; // Was; 32;

    #endregion

    #region Public Methods and Operators

    public float GetLikenessValue(string needle, string haystack) {
      var needleSimHash = this.DoCalculateSimHash(needle);
      var hayStackSimHash = this.DoCalculateSimHash(haystack);
      return (HashSize - GetHammingDistance(needleSimHash, hayStackSimHash)) / (float)HashSize;
    }

    /// <summary>
    /// GetLikenessValue -- Compute the normalized distance between two 64-bit hashes
    /// </summary>
    /// <param name="iNeedle"></param>
    /// <param name="iHayStack"></param>
    /// <returns></returns>
    public float GetLikenessValue(UInt64 iNeedle, UInt64 iHayStack) {
      return (HashSize - GetHammingDistance(iNeedle, iHayStack)) / (float)HashSize;
    }

    #endregion

    #region Methods

    /// <summary>
    /// DoHashTokens -- convert an array of 'string' tokens into an array of 64 bit hashes
    /// </summary>
    /// <param name="tokens"></param>
    /// <returns></returns>
    private static IEnumerable<UInt64> DoHashTokens(IEnumerable<string> tokens) {
      var hashedTokens = new List<UInt64>();
      foreach (string token in tokens) {
        // Compute a 64-bit hash from this string token
        UInt64 hThis = GetInt64HashCode(token); // Convert.ToUInt64(token.GetHashCode());
        // Add this hash to the list
        hashedTokens.Add(hThis);
      }
      return hashedTokens;
    }

    /// <summary>
    /// Return unique Int64 value for input string
    /// </summary>
    /// <param name="strText"></param>
    /// <returns></returns>
    private static UInt64 GetInt64HashCode(string strText) {
      UInt64 hashCode = 0;
      if (!string.IsNullOrEmpty(strText)) {
        //Unicode Encode Covering all characterset
        byte[] byteContents = Encoding.Unicode.GetBytes(strText);
        System.Security.Cryptography.SHA256 hash = new System.Security.Cryptography.SHA256CryptoServiceProvider();
        byte[] hashText = hash.ComputeHash(byteContents);
        //32Byte hashText separate
        //hashCodeStart = 0~7  8Byte
        //hashCodeMedium = 8~23  8Byte
        //hashCodeEnd = 24~31  8Byte
        //and Fold
        UInt64 hashCodeStart = BitConverter.ToUInt64(hashText, 0);
        UInt64 hashCodeMedium = BitConverter.ToUInt64(hashText, 8);
        UInt64 hashCodeEnd = BitConverter.ToUInt64(hashText, 24);
        hashCode = hashCodeStart ^ hashCodeMedium ^ hashCodeEnd;
      }
      return (hashCode);
    }

    public int GetHammingDistance(UInt64 firstValue, UInt64 secondValue) {
      UInt64 hammingBits = firstValue ^ secondValue;
      int hammingValue = 0;
      for (int i = 0; i < HashSize; i++) {
        if (IsBitSet(hammingBits, i)) {
          hammingValue += 1;
        }
      }
      return hammingValue;
    }

    private static bool IsBitSet(UInt64 b, int pos) {
      UInt64 iOne = 1;
      return (b & (iOne << pos)) != 0;
    }

    /// <summary>
    /// DoCalculateSimHash -- find the 64-bit hash for the input string
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public UInt64 DoCalculateSimHash(string input) {
      // ITokeniser tokeniser = new OverlappingStringTokeniser(5, 3);
      // ITokeniser tokeniser = new OverlappingStringTokeniser(4, 3);
      // ITokeniser tokeniser = new OverlappingStringTokeniser(5, 4);
      ITokeniser tokeniser = new StringToWordTokeniser(3);
      var hashedtokens = DoHashTokens(tokeniser.Tokenise(input));
      // var hashedtokens = DoHashTokens(tokeniser.Tokenise(input.ToLower()));

      var vector = new int[HashSize];
      for (var i = 0; i < HashSize; i++) {
        vector[i] = 0;
      }

      foreach (var value in hashedtokens) {
        for (var j = 0; j < HashSize; j++) {
          if (IsBitSet(value, j)) {
            vector[j] += 1;
          } else {
            vector[j] -= 1;
          }
        }
      }

      UInt64 fingerprint = 0;
      UInt64 iOne = 1;
      for (var i = 0; i < HashSize; i++) {
        if (vector[i] > 0) {
          fingerprint += iOne << i;
        }
      }
      return fingerprint;
    }

    #endregion
  }
}
