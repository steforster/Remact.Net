
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Threading.Tasks;

namespace Remact.Net.Protocol
{
    /// <summary>
    /// Interface for protocol level clients (client side, lower level outgoing interface).
    /// and also interface for service handlers (server side, higher level incoming interface).
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
        /// <param name="state">The state will be returned.</param>
        /// <param name="callback">Callback handler for OnOpenCompleted and MessageFromService.</param>
        void OpenAsync(OpenAsyncState state, IRemactProtocolDriverCallbacks callback);

        /// <summary>
        /// Occurs when a client calls a service.
        /// </summary>
        void MessageFromClient(RemactMessage message);

        /// <summary>
        /// Closes connection and frees resources.
        /// </summary>
        void Dispose();
    }

    /// <summary>
    /// Interface for RemactClient (client side, higher level incoming interface).
    /// and also interface for client proxies (server side, lower protocol level outgoing interface).
    /// </summary>
    public interface IRemactProtocolDriverCallbacks
    {
        /// <summary>
        /// Occurs when the WebSocketClient has finished connecting to the server.
        /// </summary>
        /// <param name="state">contains information in case of error.</param>
        void OnOpenCompleted(OpenAsyncState state);

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

    public class OpenAsyncState
    {
        public TaskCompletionSource<bool> Tcs;
        public Exception Error;
    }

    public class LowerProtocolMessage
    {
        public RemactMessageType Type;
        public int RequestId; // when receiving responses
        public string DestinationMethod; // when receiving notifications or requests
        public object Payload;
        public ISerializationPayload SerializationPayload;

        public LowerProtocolMessage()
        {}

        public LowerProtocolMessage(RemactMessage msg)
        {
            Type = msg.MessageType;
            RequestId = msg.RequestId;
            DestinationMethod = msg.DestinationMethod;
            Payload = msg.Payload;
            SerializationPayload = msg.SerializationPayload;
        }
    }
}