
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Threading.Tasks;

namespace Remact.Net.Remote
{
    //----------------------------------------------------------------------------------------------
    #region == interface IRemoteActor ==

    /// <summary>
    /// Services and Clients must implement this interface.
    /// </summary>
    internal interface IRemotePort
    {
        /// <summary>
        /// Connect or reconnect output to the previously linked partner.
        /// </summary>
        /// <returns>A task. When this task is run to completion, the task.Result is either true or an exception.</returns>
        Task<bool> TryConnect();

        /// <summary>
        /// Shutdown the outgoing remote connection. Send a disconnect message to the partner.
        /// Close the incoming network connection.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Post a request or response message to the input of this partner.
        /// ** Usage **
        /// Remote:                                  Post a message into this partners input queue.
        ///   RemactPortClient:                      Post a response into this clients input queue.
        ///   RemactPortClient.m_OutputClient (server-proxy): Send a request to the connected remote service.
        /// Serviceside:                             Source.PostInput() sends a response from client-stub to the remote client.
        /// </summary>
        /// <param name="msg">A <see cref="RemactMessage"/> the 'Source' property references the sending partner.</param>
        void PostInput(RemactMessage msg);

        /// <summary>
        /// Universal resource identifier for the service or client.
        /// </summary>
        Uri Uri { get; }

        /// <summary>
        /// The number of requests not yet responded by the service connected to this output.
        /// </summary>
        int OutstandingResponsesCount { get; }
    };

    #endregion
}