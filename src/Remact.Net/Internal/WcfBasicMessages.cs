
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Runtime.Serialization;// DataContract
using System.Collections.Generic;  // List
using Remact.Net;

namespace Remact.Net.Internal
{
  //----------------------------------------------------------------------------------------------
  #region == class WcfPartnerListMessage ==

  /// <summary>
  /// <para>This message contains a list of WcfPartnerMessages.</para>
  /// <para>It is used by the routers to exchange informations.</para>
  /// </summary>
  [DataContract (Namespace=RemactDefaults.WsNamespace)]

  public class WcfPartnerListMessage: WcfMessage
  {
    /// <summary>
    /// List of services in a plant.
    /// </summary>
    [DataMember]
    public List<ActorMessage> Item;

    /// <summary>
    /// Create a WcfPartnerListMessage.
    /// </summary>
    public WcfPartnerListMessage ()
    {
      Item = new List<ActorMessage> (20);
    }

    #if MONO
    public static new IEnumerable<Type> z_GetKnownTypeList()  {return WcfMessage.z_GetKnownTypeList();}
    #endif
  }// WcfPartnerListMessage

  #endregion
  //----------------------------------------------------------------------------------------------
}// namespace
