
// Copyright (c) https://github.com/steforster/Remact.Net

using Alchemy;
using Alchemy.Classes;
using System.IO;
using Remact.Net.Remote;

namespace Remact.Net.Json.Msgpack.Alchemy
{
    /// <summary>
    /// Implements some common protocol level methods for a client.
    /// </summary>
    public class ProtocolDriverClientHelper
    {
        private WebSocketClient _wsClient;
        private IRemactProtocolDriverToClient _callback;
        private bool _faulted;
        private bool _disposed;
        private OnEventDelegate _onReceiveAction;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="wsClient">An Alchemy client.</param>
        public ProtocolDriverClientHelper(WebSocketClient wsClient)
        {
            _wsClient = wsClient;
        }

        #region IRemactProtocolDriverToService implementation

        /// <inheritdoc/>
        public PortState PortState 
        { 
            get 
            {
                if (_faulted)
                {
                    return PortState.Faulted;
                }
                else if (_wsClient.ReadyState == WebSocketClient.ReadyStates.CONNECTING)
                {
                    return PortState.Connecting;
                }
                else if (_wsClient.ReadyState == WebSocketClient.ReadyStates.OPEN)
                {
                    return PortState.Ok;
                }
                else
                {
                    return PortState.Disconnected;
                }
            } 
        }

        /// <summary>
        /// Opens the connection to the service.
        /// </summary>
        /// <param name="state">The state is passed to OnOpenCompleted.</param>
        /// <param name="callback">Called when the open has finished or messages have been received.</param>
        /// <param name="onReceiveAction">Called by the web socket client or service, when a message has been received.</param>
        public void OpenAsync(OpenAsyncState state, IRemactProtocolDriverToClient callback, OnEventDelegate onReceiveAction)
        {
            _callback = callback;
            _onReceiveAction = onReceiveAction;
            _wsClient.OnConnected = OnConnected;
            _wsClient.OnDisconnect = OnConnectFailure;
            _wsClient.BeginConnect(state);
        }

        /// <summary>
        /// The alchemy user context for this client. Null when not connected.
        /// </summary>
        public UserContext UserContext { get; private set; }

        private void OnConnected(UserContext context)
        {
            var state = (OpenAsyncState)context.Data;
            state.Error = null;
            if (_wsClient.ReadyState != WebSocketClient.ReadyStates.OPEN)
            {
                _faulted = true;
                state.Error = new IOException("WebSocketClient not open.");
            }
            else
            {
                context.SetOnReceive(_onReceiveAction);
                context.SetOnDisconnect(OnDisconnect);
                UserContext = context;
            }

            _callback.OnOpenCompleted(state);
        }

        private void OnConnectFailure(UserContext context)
        {
            _faulted = true;
            UserContext = null;
            var state = (OpenAsyncState)context.Data;
            state.Error = context.LatestException;
            _callback.OnOpenCompleted(state);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                _disposed = true;
                UserContext = null;
                if (_wsClient != null) _wsClient.Disconnect();
            }
            catch { }
        }

        #endregion
        #region Alchemy callbacks


        // Connect failure or disposing context 
        private void OnDisconnect(UserContext context)
        {
            _faulted = true;
            if (_disposed)
            {
                return;
            }

            UserContext = null;
            _callback.OnServiceDisconnect();
        }

        #endregion
    }
}