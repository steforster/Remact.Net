
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using Remact.Net.Remote;
using Remact.Net.Bms1Serializer;
using Remact.Net.TcpStream;

namespace Remact.Net.Plugin.Bms.Tcp
{
    /// <summary>
    /// Implements the protocol level for a BMS server. See https://github.com/steforster/bms1-binary-message-stream-format.
    /// </summary>
    public class BmsProtocolClientStub : BmsProtocolDriver, IRemactProtocolDriverToClient
    {
        private TcpPortManager _portManager;
        private TcpStreamChannel _tcpChannel;
        private RemactService _remactService;

        /// <summary>
        /// Constructor for a stub instance that is created when a client is accepted by a service.
        /// </summary>
        /// <param name="portManager">TcpPortManager for this shared TCP port.</param>
        /// <param name="tcpChannel">The newly accepted TCP client channel.</param>
        public BmsProtocolClientStub(TcpPortManager portManager, TcpStreamChannel tcpChannel)
        {
            _portManager = portManager;
            _tcpChannel = tcpChannel;
            InitOnServiceSide();
        }


        #region IRemactProtocolDriverCallbacks implementation

        /// <inheritdoc/>
        public Uri ClientUri { get { return new Uri("net.tcp://" + _tcpChannel.ClientSocket.RemoteEndPoint.ToString()); } }

        /// <inheritdoc/>
        public void OnServiceDisconnect()
        {
            if (_tcpChannel != null)
            {
                _tcpChannel.Dispose();
                _tcpChannel = null;
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
            SendMessage(msg, null, _tcpChannel.OutputStream);
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
            svcUser.SetCallbackHandler(this);
            var handler = new MultithreadedServiceNet40(svcUser, _remactService);
            return handler;
        }

        protected override Func<IBms1Reader, object> FindDeserializerByDestination(string destinationMethod)
        {
            if (destinationMethod.StartsWith(ActorInfo.MethodNamePrefix))
            {
                return ActorInfoExtension.ReadFromBms1Stream;
            }

            var type = _remactService.ServiceIdent.FindPayloadTypeByDestination(destinationMethod);
            if (type != null)
            {
                return BmsProtocolConfig.Instance.FindDeserializerByObjectType(type.FullName);
            }

            throw new NotImplementedException();
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
