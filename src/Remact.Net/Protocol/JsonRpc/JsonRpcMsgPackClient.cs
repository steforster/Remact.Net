
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Threading;
using System.Collections.Concurrent;
using Alchemy;
using Alchemy.Classes;
using MsgPack.Serialization;
using Remact.Net.Protocol;
using System.IO;

namespace Remact.Net.Protocol.JsonRpc
{
    /// <summary>
    /// Implements the protocol level for a JSON-RPC client. See http://www.jsonrpc.org/specification.
    /// Uses the MsgPack-cli serializer.
    /// </summary>
    public class JsonRpcMsgPackClient : JsonRpcMsgPackClientBase, IRemactProtocolDriverService
    {
        public JsonRpcMsgPackClient(Uri websocketUri)
        {
            ServiceUri = websocketUri;
            _onReceiveAction = OnReceived;
            _wsClient = new WebSocketClient(websocketUri.ToString())
            {
                //OnSend = OnSend,// Message has been dequeued and passed to the socket buffer
                //OnConnect = OnConnect,// TCP socket is connected to the server
                //SubProtocols = new string[]{"wamp"} // null: take all subprotocols
                //TODO Origin = see rfc6455
            };
        }

        #region IRemactProtocolDriverService proxy implementation

        public PortState PortState {get {return BasePortState;}}

        public void OpenAsync(OpenAsyncState state, IRemactProtocolDriverCallbacks callback)
        {
            base.BaseOpenAsync(state, callback);
        }

        public void MessageFromClient(RemactMessage msg)
        {
            SendMessage(new LowerProtocolMessage(msg));
        }

        #endregion
        #region Alchemy callbacks

        private int _lowLevelErrorCount;

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