
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Net;
using System.Threading;
using Alchemy;
using Alchemy.Classes;
using MsgPack.Serialization;
using Remact.Net.Contracts;
using Remact.Net.Remote;
using System.IO;

namespace Remact.Net.Protocol.JsonRpc
{
    /// <summary>
    /// Implements the protocol level for a JSON-RPC server. See http://www.jsonrpc.org/specification.
    /// Uses the MsgPack-cli serializer.
    /// </summary>
    public class JsonRpcMsgPackClientStub : JsonRpcMsgPackClientBase
    {
        private UserContext _wsChannel;

        public JsonRpcMsgPackClientStub(RemactServiceUser svcUser, RemactPortService serviceIdent, IRemactProtocolDriverService requestHandler, UserContext websocketChannel)
        {
            _wsChannel = websocketChannel;
                //OnSend = OnSend,
            _wsChannel.SetOnReceive(OnReceived);
                //OnConnect = OnConnect,
                //OnConnected = OnConnected,
            _wsChannel.SetOnDisconnect(OnDisconnect);

            _svcUser = svcUser;
            _serviceIdent = serviceIdent;
            _requestHandler = requestHandler;
            Connected = true;
        }

        public bool Connected { get; private set; }

        public Uri ClientUri { get { return new Uri("ws://"+_wsChannel.ClientAddress.ToString()); } }


        public void OnServiceDisconnect()
        {
            _wsChannel = null;
        }

        #region IRemactProtocolDriverCallbacks implementation


        public void OnOpenCompleted(OpenAsyncState state)
        {
            throw new NotImplementedException();
        }


        /// <inheritdoc/>
        public void OnMessageFromService(LowerProtocolMessage msg)
        {
            SendMessage(msg);
        }


        protected override void IncomingMessageNotDeserializable(int id, string errorDesc)
        {
            var error = new ErrorMessage(ErrorCode.ReqestNotDeserializableOnService, errorDesc);
            SendError(id, error);
        }


        #endregion
        #region Alchemy callbacks

        // Connect failure or disposing context 
        private void OnDisconnect(UserContext context)
        {
            Connected = false; // TODO
        }

        #endregion
    }
}
