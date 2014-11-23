
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using Alchemy;

namespace Remact.Net.Protocol.JsonRpc
{
    /// <summary>
    /// Implements the protocol level for a JSON-RPC client. See http://www.jsonrpc.org/specification.
    /// Uses the MsgPack-cli serializer. See http://msgpack.org.
    /// </summary>
    public class JsonRpcMsgPackClient : JsonRpcNewtonsoftMsgPackDriver, IRemactProtocolDriverToService
    {
        private ProtocolDriverClientHelper _clientHelper;
        private WebSocketClient _wsClient;

        /// <summary>
        /// Constructor for a client that connects to a service.
        /// </summary>
        /// <param name="websocketUri">The uri of the service.</param>
        public JsonRpcMsgPackClient(Uri websocketUri)
        {
            ServiceUri = websocketUri;
            _wsClient = new WebSocketClient(websocketUri.ToString())
            {
                //OnSend = OnSend,// Message has been dequeued and passed to the socket buffer
                //OnConnect = OnConnect,// TCP socket is connected to the server
                //SubProtocols = new string[]{"wamp"} // null: take all subprotocols
                //TODO Origin = see rfc6455
            };

            _clientHelper = new ProtocolDriverClientHelper(_wsClient);
        }

        #region IRemactProtocolDriverService proxy implementation

        /// <inheritdoc/>
        public Uri ServiceUri { get; private set; }

        /// <inheritdoc/>
        public PortState PortState {get {return _clientHelper.PortState; }}

        /// <inheritdoc/>
        public void OpenAsync(OpenAsyncState state, IRemactProtocolDriverToClient callback)
        {
            InitOnClientSide(_wsClient.Send, callback);
            _clientHelper.OpenAsync(state, callback, OnReceived);
        }

        /// <inheritdoc/>
        public void MessageToService(LowerProtocolMessage msg)
        {
            SendMessage(msg);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _clientHelper.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
        #region Alchemy callbacks

        private int _lowLevelErrorCount;

        /// <inheritdoc/>
        protected override void IncomingMessageNotDeserializable(int id, string errorDesc)
        {
            if (++_lowLevelErrorCount > 100)
            {
                return; // do not respond endless on erronous error messages
            }

            var error = new ErrorMessage(ErrorCode.ResponseNotDeserializableOnClient, errorDesc);
            SendError(id, error);
        }


        #endregion
    }
}