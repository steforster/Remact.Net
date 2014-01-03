using System;
using System.Runtime.Serialization;// DataContract
using SourceForge.AsyncWcfLib;
using System.Collections.Generic;

namespace Test1.Messages
{
  [DataContract (Namespace=WcfDefault.WsNamespace)]
  public class Test1CommandMessage: WcfMessage
  {
    [DataMember] 
    public string Command = string.Empty;
    

    // Constructor
    public Test1CommandMessage (string cmd)
    {
      Command = cmd;
    }
    
    // Register known message types for the deserializer
    public static void AddKnownMessageTypes()
    {
      AddKnownType (typeof (Test1CommandMessage));
      //... add all other messages of this assembly here
    }

    #if MONO
    // a bug in mono (last checked in 2.10.8.1) forces us to write this line in every message type
    public static new IEnumerable<Type> z_GetKnownTypeList()  {return WcfMessage.z_GetKnownTypeList();}
    #endif
  }// Test1CommandMessage
}
