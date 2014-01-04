
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;  // List
using Remact.Net;

namespace Remact.Net.Internal
{
  /// <summary>
  /// <para>This message payload contains a list of ActorInfo payloads.</para>
  /// <para>It is used by the routers to exchange informations.</para>
  /// </summary>
  public class WcfPartnerListMessage
  {
    /// <summary>
    /// List of services in a plant.
    /// </summary>
    public List<ActorInfo> Item;

    /// <summary>
    /// Create a WcfPartnerListMessage.
    /// </summary>
    public WcfPartnerListMessage ()
    {
      Item = new List<ActorInfo> (20);
    }
  }
}
