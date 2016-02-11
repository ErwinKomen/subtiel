using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using CookComputing.XmlRpc;

public struct SumAndDiffValue 
{
  public int sum; 
  public int difference; 
}

[XmlRpcUrl("http://api.opensubtitles.org/xml-rpc")]

public interface ISumAndDiff {
  [XmlRpcMethod]
  SumAndDiffValue SumAndDifference(int x, int y);
} 
