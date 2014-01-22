using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Remact.Net;

namespace Test2.Contracts
{
  /// <summary>
  /// 
  /// </summary>
  public class Test2Req
  {
    //----------------------------------------------------------------------------------------------
    #region Message Data Members
    
    /// <summary>
    /// z_RequestCode is public but used internally only! Access 'RequestCode' instead!
    /// Reason: http://msdn.microsoft.com/en-us/library/bb924412%28v=VS.100%29.aspx
    /// 'RequestCode' is stramed as int in order to make it reverse compatible to older communication partners
    /// </summary>
    [JsonProperty]
    private uint  z_requestcode = 0; // ERequestCode
    
    #endregion
    //----------------------------------------------------------------------------------------------
    #region Enums
    
    /// <summary>
    /// Demonstrate how to make enum fields forward/backward compatible
    /// </summary>
    public enum ERequestCode
    {
      Unknown = 0,
      Normal,
      Emergency,
      // Later versions may add a new code here
      Unused
    }

    [JsonIgnore]
    public ERequestCode RequestCode
    {get{
      if (z_requestcode >= (uint)ERequestCode.Unused) return  ERequestCode.Unknown; // a old service receives an new, unknown code
                                                 else return (ERequestCode)z_requestcode;
    }}
    
    #endregion
    //----------------------------------------------------------------------------------------------
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the Test2Req class.
    /// </summary>
    public Test2Req (ERequestCode code)
    {
      z_requestcode = (uint)code;
    }

    #endregion
  }
}
