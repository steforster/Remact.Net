
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Net;                  // Dns
using Remact.Net;

namespace Remact.Net.Internal
{
  //----------------------------------------------------------------------------------------------
  #region == interface IWcfBasicPartner ==

  /// <summary>
  /// WcfPartners, Services and Clients must implement this interface.
  /// It is used for library internal purposes mainly.
  /// </summary>
  public interface IWcfBasicPartner
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
    /// Send a request message to the partner on the outgoing connection.
    /// At least a ReadyMessage will asynchronously be received through 'PostInput', when the partner has processed the request.
    /// ** Usage **
    /// Internal:             Send a message to the connected partner running on another thread synchronization context.
    /// ActorOutput (client): Send a request to the connected remote service.
    /// Serviceside:          Sender.SendOut() send a request from client-proxy to the internal service.
    /// </summary>
    /// <param name="id">A <see cref="Request"/>the 'Sender' property references the sending partner, where the response is expected.</param>
    void SendOut (Request id); // TODO is it needed anymore ????

    /// <summary>
    /// Post a request or response message to the input of this partner.
    /// ** Usage **
    /// Internal:                                  Post a message into this partners input queue.
    /// ActorOutput (client):                      Post a response into this clients input queue.
    /// ActorOutput.m_OutputClient (server-proxy): Send a request to the connected remote service.
    /// Serviceside:                               Sender.PostInput() sends a response from client-stub to the remote client.
    /// </summary>
    /// <param name="id">A <see cref="Request"/> the 'Sender' property references the sending partner.</param>
    void PostInput (Request id);

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