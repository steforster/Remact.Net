
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Threading.Tasks;

namespace Remact.Net.Remote
{
    /// <summary>
    /// Outgoing interface for protocol level clients (e.g. JsonRpcMsgPackClient) called by RemactClient.
    /// and also incoming interface for services (e.g. MultithreadedServiceNet40) called by lower level client stub.
    /// </summary>
    public interface IRemactProtocolDriverToService : IDisposable
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
        void OpenAsync(OpenAsyncState state, IRemactProtocolDriverToClient callback);

        /// <summary>
        /// Occurs when a client calls a service.
        /// <param name="msg">The message in form of a LowerProtocolMessage. 
        /// In case of an incoming message (server side), the Payload and the SerializationPayload is a JsonToken.
        /// The Payload is converted later on to the request type of the called method.</param>
        /// </summary>
        void MessageToService(LowerProtocolMessage msg);
    }

    /// <summary>
    /// Incoming interface for RemactClient (called by lower level protocol client like JsonRpcMsgPackClient).
    /// and also outgoing interface for client stubs on server side (called by higher level RemactServiceUser).
    /// </summary>
    public interface IRemactProtocolDriverToClient
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
        /// In case of an incoming message (client side), the Payload and the SerializationPayload is a JsonToken.
        /// The Payload is converted later on to the return type of the called method.
        /// The return Payload is null for void methods.</param>
        void OnMessageToClient(LowerProtocolMessage msg);

        /// <summary>
        /// Occurs when the service disconnects from client. Closes and Disposes the socket.
        /// </summary>
        void OnServiceDisconnect();
    }

    /// <summary>
    /// Asynchronous state is passed by the protocol driver from method OpenAsync to OnOpenCompleted.
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