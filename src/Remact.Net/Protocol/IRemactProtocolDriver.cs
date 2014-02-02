
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
        /// Gets the connection port state.
        /// </summary>
        PortState PortState { get; }

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
        Uri ClientUri { get; }

        /// <summary>
        /// Occurs when a message is received from service.
        /// </summary>
        /// <param name="msg">The message in form of a LowerProtocolMessage. 
        /// In case of Type==Response, the Payload is a JsonToken.
        /// The Payload is converted later on to the return type of the called method.
        /// The return Payload is null for void methods.
        /// </param>
        void OnMessageFromService(LowerProtocolMessage msg);

        /// <summary>
        /// Occurs when the service disconnects from client.
        /// </summary>
        void OnServiceDisconnect();
    }

    public class LowerProtocolMessage
    {
        public ActorMessageType Type;
        public int RequestId;
        public object Payload;
    }
}