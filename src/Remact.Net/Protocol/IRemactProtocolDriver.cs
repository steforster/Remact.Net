
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using Newtonsoft.Json.Linq;

namespace Remact.Net.Protocol
{
    /// <summary>
    /// Interface for protocol level clients (client side, lower level).
    /// and also interface for service handlers (server side, higher level).
    /// </summary>
    public interface IRemactProtocolDriverService
    {
        /// <summary>
        /// Gets the endpoint uri of the service
        /// </summary>
        Uri ServiceUri { get; }

        /// <summary>
        /// Gets the connection ready state.
        /// </summary>
        ReadyState ReadyState { get; }

        /// <summary>
        /// Gets the connection ready state as string.
        /// </summary>
        string     ReadyStateAsString { get; }

        /// <summary>
        /// Opens the connection.
        /// </summary>
        /// <param name="message">A request message to return error info.</param>
        /// <param name="callback">Callback handler for OnOpenCompleted and MessageFromService.</param>
        void OpenAsync(ActorMessage message, IRemactProtocolDriverCallbacks callback);

        /// <summary>
        /// Occurs when a client calls a service.
        /// </summary>
        void MessageFromClient(ActorMessage message);

        /// <summary>
        /// Closes connection and frees resources.
        /// </summary>
        void Dispose();
    }

    /// <summary>
    /// The connection ready state.
    /// </summary>
    public enum ReadyState
    {
        Closed,
        Connected,
        Faulted
    }

    /// <summary>
    /// Interface for RemactClient (client side, higher level).
    /// and also interface for client proxies (server side, lower protocol level).
    /// </summary>
    public interface IRemactProtocolDriverCallbacks
    {
        /// <summary>
        /// Occurs when the WebSocketClient could connect to the server.
        /// </summary>
        /// <param name="response">response is of type 'ActorMessage'. response.Payload contains information in case of error.</param>
        void OnOpenCompleted(object response);

        /// <summary>
        /// Gets the endpoint uri of the client
        /// </summary>
        string ClientAddress { get; }

        /// <summary>
        /// Occurs when a call result is returned from service, when the service sends a notification or an error message.
        /// </summary>
        /// <param name="message">The result of a call, when message.Type is ActorMessageType.Response. 
        /// In this case, the type of message.Payload corresponds to the return type of the called method. Payload is null for void methods.
        /// </param>
        void MessageFromService(ActorMessage message);
    }
}