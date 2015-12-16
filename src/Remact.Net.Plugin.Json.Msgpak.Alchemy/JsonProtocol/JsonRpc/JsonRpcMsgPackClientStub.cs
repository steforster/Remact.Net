
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using Alchemy.Classes;
using Remact.Net.Remote;

namespace Remact.Net.Plugin.Json.Msgpack.Alchemy
{
    /// <summary>
    /// Implements the protocol level for a JSON-RPC server. See http://www.jsonrpc.org/specification.
    /// Uses the MsgPack-cli serializer.
    /// </summary>
    public class JsonRpcMsgPackClientStub : JsonRpcNewtonsoftMsgPackDriver, IRemactProtocolDriverToClient
    {
        private UserContext _wsChannel;

        /// <summary>
        /// Constructor for a stub instance that connects from service to client.
        /// </summary>
        /// <param name="requestHandler">The interface for callbacks to the service.</param>
        /// <param name="websocketChannel">The Alchemy channel.</param>
        public JsonRpcMsgPackClientStub(IRemactProtocolDriverToService requestHandler, UserContext websocketChannel)
        {
            _wsChannel = websocketChannel;
                //OnSend = OnSend,
            _wsChannel.SetOnReceive(OnReceived);
                //OnConnect = OnConnect,
                //OnConnected = OnConnected,
            _wsChannel.SetOnDisconnect(OnDisconnect);

            InitOnServiceSide(requestHandler);
        }


        #region IRemactProtocolDriverCallbacks implementation

        /// <inheritdoc/>
        public Uri ClientUri { get { return new Uri("ws://"+_wsChannel.ClientAddress.ToString()); } }

        /// <inheritdoc/>
        public void OnServiceDisconnect()
        {
            if (_wsChannel != null)
            {
                _wsChannel.Disconnect();
                _wsChannel = null;
            }
        }

        /// <inheritdoc/>
        public void OnOpenCompleted(OpenAsyncState state)
        {
            throw new NotImplementedException();
        }


        /// <inheritdoc/>
        public void OnMessageToClient(LowerProtocolMessage msg)
        {
            SendMessage(msg, _wsChannel);
        }


        #endregion
        #region Alchemy callbacks

        /// <summary>
        /// Occurs, when connection failes or context is disposed.
        /// </summary>
        /// <param name="context"></param>
        private void OnDisconnect(UserContext context)
        {
            // TODO
        }

        #endregion
    }
}
