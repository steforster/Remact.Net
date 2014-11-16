
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using Alchemy.Classes;
using Remact.Net.Remote;

namespace Remact.Net.Protocol.JsonRpc
{
    /// <summary>
    /// Implements the protocol level for a JSON-RPC server. See http://www.jsonrpc.org/specification.
    /// Uses the MsgPack-cli serializer.
    /// </summary>
    public class JsonRpcMsgPackClientStub : JsonRpcMsgPackDriver, IRemactProtocolDriverCallbacks
    {
        private UserContext _wsChannel;

        /// <summary>
        /// Constructor for a stub instance that connects from service to client.
        /// </summary>
        /// <param name="svcUser">The RemactServiceUser instance for this connection.</param>
        /// <param name="serviceIdent">The service identification.</param>
        /// <param name="requestHandler">The interface for callbacks to the service.</param>
        /// <param name="websocketChannel">The Alchemy channel.</param>
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
        }


        #region IRemactProtocolDriverCallbacks implementation

        /// <inheritdoc/>
        public Uri ClientUri { get { return new Uri("ws://"+_wsChannel.ClientAddress.ToString()); } }

        /// <inheritdoc/>
        public void OnServiceDisconnect()
        {
            _wsChannel = null;
        }

        /// <inheritdoc/>
        public void OnOpenCompleted(OpenAsyncState state)
        {
            throw new NotImplementedException();
        }


        /// <inheritdoc/>
        public void OnMessageFromService(LowerProtocolMessage msg)
        {
            SendMessage(msg);
        }

        /// <inheritdoc/>
        protected override void IncomingMessageNotDeserializable(int id, string errorDesc)
        {
            var error = new ErrorMessage(ErrorCode.ReqestNotDeserializableOnService, errorDesc);
            SendError(id, error);
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
