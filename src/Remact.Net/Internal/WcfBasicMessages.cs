
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Runtime.Serialization;// DataContract
using System.Collections.Generic;  // List
using SourceForge.AsyncWcfLib;

namespace SourceForge.AsyncWcfLib.Basic
{
  //----------------------------------------------------------------------------------------------
  #region == class WcfPartnerListMessage ==

  /// <summary>
  /// <para>This message contains a list of WcfPartnerMessages.</para>
  /// <para>It is used by the routers to exchange informations.</para>
  /// </summary>
  [DataContract (Namespace=WcfDefault.WsNamespace)]

  public class WcfPartnerListMessage: WcfMessage
  {
    /// <summary>
    /// List of services in a plant.
    /// </summary>
    [DataMember]
    public List<WcfPartnerMessage> Item;

    /// <summary>
    /// Create a WcfPartnerListMessage.
    /// </summary>
    public WcfPartnerListMessage ()
    {
      Item = new List<WcfPartnerMessage> (20);
    }

    #if MONO
    public static new IEnumerable<Type> z_GetKnownTypeList()  {return WcfMessage.z_GetKnownTypeList();}
    #endif
  }// WcfPartnerListMessage

  #endregion
  //----------------------------------------------------------------------------------------------
  #region == class WcfNotifyResponse ==

  /// <summary>
  /// <para>A service may send a 'WcfNotifyResponse' instead of a normal response.</para>
  /// <para>The 'WcfNotifyResponse' contains the normal response plus some messages not expected by the client.</para>
  /// <para>This is a polling type replacement of the more cumbersome 'DualHttpBinding'.</para>
  /// <para>This message is used internally and returned by calling 'WcfBasicServiceUser.NotificationsAndResponse(rsp)'.</para>  
  /// </summary>
  [DataContract (Namespace=WcfDefault.WsNamespace)]

  internal class WcfNotifyResponse: WcfMessage
  {
    /// <summary>
    /// The notification messages not requested by the client are received first.
    /// </summary>
    [DataMember] public List<IWcfMessage> Notifications;
    
    /// <summary>
    /// The message requested by the client is received last.
    /// </summary>
    [DataMember] public IWcfMessage       Response;

    /// <summary>
    /// Create an empty notify message
    /// </summary>
    public WcfNotifyResponse()
    {
      Notifications = new List<IWcfMessage>(10);
      Response      = null;
    }// CTOR
    
    #if MONO
    public static new IEnumerable<Type> z_GetKnownTypeList()  {return WcfMessage.z_GetKnownTypeList();}
    #endif
  }// class WcfNotifyResponse

  #endregion
}// namespace
