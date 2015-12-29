
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using Remact.Net.Remote;
using Remact.Net.Bms1Serializer;
using Remact.Net.TcpStream;

namespace Remact.Net.Plugin.Bms.Tcp
{
    /// <summary>
    /// Implements the protocol level for a BMS client. See https://github.com/steforster/bms1-binary-message-stream-format.
    /// </summary>
    public class BmsProtocolClient : BmsProtocolDriver, IRemactProtocolDriverToService
    {
        private TcpStreamClient _tcpClient;
        private bool _connecting;
        private bool _faulted;

        /// <summary>
        /// Constructor for a client that connects to a service.
        /// </summary>
        /// <param name="tcpUri">The uri of the service.</param>
        public BmsProtocolClient(Uri tcpUri)
        {
            ServiceUri = tcpUri;
            _tcpClient = new TcpStreamClient();
        }

        #region IRemactProtocolDriverService proxy implementation

        /// <inheritdoc/>
        public Uri ServiceUri { get; private set; }

        /// <inheritdoc/>
        public PortState PortState
        {
            get
            {
                if (_faulted)
                {
                    return PortState.Faulted;
                }
                else if (_connecting)
                {
                    return PortState.Connecting;
                }
                else if (_tcpClient.IsConnected)
                {
                    return PortState.Ok;
                }
                else
                {
                    return PortState.Disconnected;
                }
            }
        }

        /// <inheritdoc/>
        public void OpenAsync(OpenAsyncState state, IRemactProtocolDriverToClient callback)
        {
            _connecting = true;
            InitOnClientSide(callback);
            var task = _tcpClient.ConnectAsync(ServiceUri, OnMessageReceived, OnDisconnect);
            task.ContinueWith((t) => 
                {
                    _connecting = false;
                    if (t.Exception != null)
                    {
                        state.Error = t.Exception;
                        _faulted = true;
                    }
                    callback.OnOpenCompleted(state);
                });
        }

        /// <inheritdoc/>
        public void MessageToService(LowerProtocolMessage msg)
        {
            SendMessage(msg, ServiceUri.AbsolutePath, _tcpClient.OutputStream);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            _faulted = true;
            if (disposing)
            {
                _tcpClient.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion
        #region Socket callbacks


        protected override Func<IBms1Reader, object> FindDeserializerByDestination(string destinationMethod)
        {
            // TODO search dispatcher, then BmsProtocolConfig.Instance.FindDeserializerByObjectType(objectType) 
            throw new NotImplementedException();
        }

        /// <summary>
        /// Occurs, when connection failes or is disposed.
        /// </summary>
        /// <param name="channel">The client channel.</param>
        private void OnDisconnect(TcpStreamChannel channel)
        {
            _faulted = true;
            // TODO
        }

        #endregion
    }
}