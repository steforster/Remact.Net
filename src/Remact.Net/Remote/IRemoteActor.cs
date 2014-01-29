
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Net;                  // Dns
using Remact.Net;

namespace Remact.Net.Remote
{
  //----------------------------------------------------------------------------------------------
  #region == interface IRemoteActor ==

  /// <summary>
  /// Services and Clients must implement this interface.
  /// It is used for library internal purposes mainly.
  /// </summary>
  public interface IRemoteActor
  {
    /// <summary>
    /// Connect or reconnect output to the previously linked partner.
    /// </summary>
    /// <returns>false, when the connection may not be started.</returns>
    bool TryConnect();

    /// <summary>
    /// Shutdown the outgoing remote connection. Send a disconnect message to the partner.
    /// Close the incoming network connection.
    /// </summary>
    void Disconnect();

    /// <summary>
    /// Post a request or response message to the input of this partner.
    /// ** Usage **
    /// Remote:                                  Post a message into this partners input queue.
    /// ActorOutput (client):                      Post a response into this clients input queue.
    /// ActorOutput.m_OutputClient (server-proxy): Send a request to the connected remote service.
    /// Serviceside:                               Source.PostInput() sends a response from client-stub to the remote client.
    /// </summary>
    /// <param name="msg">A <see cref="ActorMessage"/> the 'Source' property references the sending partner.</param>
    void PostInput(ActorMessage msg);

    /// <summary>
    /// Universal resource identifier for the service or client.
    /// </summary>
    Uri  Uri {get;}
    
    /// <summary>
    /// The number of requests not yet responded by the service connected to this output.
    /// </summary>
    int OutstandingResponsesCount { get; }
  };

  #endregion
}// namespace