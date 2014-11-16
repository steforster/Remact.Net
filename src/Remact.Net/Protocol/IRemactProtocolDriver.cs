
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
        /// The state of the connection to the service.
        /// </summary>
        PortState PortState { get; }

        /// <summary>
        /// Opens the connection to the service.
        /// </summary>
        /// <param name="state">The state is passed to OnOpenCompleted.</param>
        /// <param name="callback">Called when the open has finished or messages have been received.</param>
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

    /// <summary>
    /// Asynchronous state is passed ny the protocol driver from method OpenAsync to OnOpenCompleted.
    /// </summary>
    public class OpenAsyncState
    {
        /// <summary>
        /// The task completion source.
        /// </summary>
        public TaskCompletionSource<bool> Tcs;

        /// <summary>
        /// The exception in case of error while opening.
        /// </summary>
        public Exception Error;
    }

    /// <summary>
    /// Message content used by the protocol drivers.
    /// </summary>
    public class LowerProtocolMessage
    {
        /// <summary>
        /// The message type.
        /// </summary>
        public RemactMessageType Type;

        /// <summary>
        /// The request id matches request and response message pairs.
        /// </summary>
        public int RequestId;

        /// <summary>
        /// The name of the destination mezhod, used for notifications or requests.
        /// </summary>
        public string DestinationMethod;

        /// <summary>
        /// The strong typed message payload to serialize.
        /// </summary>
        public object Payload;

        /// <summary>
        /// The message payload when received as a dynamically deserializable type.
        /// </summary>
        public ISerializationPayload SerializationPayload;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public LowerProtocolMessage()
        {}

        /// <summary>
        /// Creates an instance from a RemactMessage.
        /// </summary>
        /// <param name="msg">The RemactMessage.</param>
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