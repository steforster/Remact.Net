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
  public class Test2Rsp
  {
    //----------------------------------------------------------------------------------------------
    #region Payload DataMember

    public List<Test2MessageItem>  Items;
    
    #endregion
    //----------------------------------------------------------------------------------------------
    #region Public Data

    [JsonIgnore]
    public int Index = 0;
    
    #endregion
    //----------------------------------------------------------------------------------------------
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the Test2Rsp class.
    /// </summary>
    public Test2Rsp ()
    {
      Items = new List<Test2MessageItem>(20);
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Public Methods
    
    public void AddItem (string name, int p1, int p2, int p3, string p4)
    {
      Items.Add(new Test2MessageItem (++Index, name, p1, p2, p3, p4));
    }
    
    #endregion
  }// Test2Rsp
  
  
  /// <summary>
  /// One item contained in Test2Rsp. To demonstrate a more complex message type.
  /// </summary>
  public class Test2MessageItem
  {
    //----------------------------------------------------------------------------------------------
    #region Payload DataMember

    public int          Index;
    public string       ItemName;
    public List<object> Parameter;
    
    #endregion
    //----------------------------------------------------------------------------------------------
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the Test2MessageItem class.
    /// </summary>
    public Test2MessageItem (int index, string itemName, params object[] parameterList)
    {
      Index  = index;
      ItemName = itemName;
      if (parameterList != null)
      {
          Parameter = new List<object>(parameterList.Length);

          foreach (object p in parameterList)
          {
              Parameter.Add(p);
          }
      }
      else
      {
          Parameter = new List<object>();
      }
    }

    #endregion

  }// Test2MessageItem
  
}// namespace
