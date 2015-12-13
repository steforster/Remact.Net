
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using Remact.Net.Remote;
using Remact.Net.Bms1Serializer;
using Remact.Net.TcpStream;

namespace Remact.Net.Bms.Tcp
{
    /// <summary>
    /// Implements the protocol level for a JSON-RPC server. See http://www.jsonrpc.org/specification.
    /// Uses the MsgPack-cli serializer.
    /// </summary>
    public class BmsProtocolClientStub : BmsProtocolDriver, IRemactProtocolDriverToClient
    {
        private TcpPortManager _portManager;
        private RemactService _remactService;

        /// <summary>
        /// Constructor for a stub instance that is created when a client is accepted on a service.
        /// </summary>
        /// <param name="portManager">The port manager manages services on the shared TCP port.</param>
        public BmsProtocolClientStub(TcpPortManager portManager)
        {
            _portManager = portManager;
            InitOnServiceSide();
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
        #region Socket callbacks

        protected override IRemactProtocolDriverToService FirstMessageReceivedOnServiceSide(string protocolVersion, string servicePath)
        {
            // first message to the service must have the "PV" key-value attribute
            if (protocolVersion != "1.0")
            {
                throw new InvalidOperationException("Not supportet BMS protocol version " + protocolVersion);
            }

            // first message to the service must have the "SID" key-value attribute
            if (!_portManager.TryGetService(servicePath, out _remactService))
            {
                throw new InvalidOperationException("No service '" + servicePath + "' registered on TCP port " + _portManager.TcpListener.ListeningEndPoint);
            }

            var svcUser = new RemactServiceUser(_remactService.ServiceIdent);
            var handler = new MultithreadedServiceNet40(svcUser, _remactService);
            return handler;
        }

        /// <summary>
        /// Occurs, when connection failes or is disposed.
        /// </summary>
        /// <param name="channel">The client channel.</param>
        public void OnDisconnect(TcpStreamChannel channel)
        {
            // TODO
        }

        #endregion
    }
}
