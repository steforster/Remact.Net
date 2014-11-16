
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using Alchemy;
using Alchemy.Classes;
using System.IO;

namespace Remact.Net.Protocol
{
    /// <summary>
    /// Implements some common protocol level methods for a client.
    /// </summary>
    public class ProtocolDriverClientBase
    {
        protected WebSocketClient _wsClient;
        protected IRemactProtocolDriverCallbacks _callback;
        protected bool _faulted;
        protected bool _disposed;        
        protected OnEventDelegate _onReceiveAction;

        #region IRemactProtocolDriverService proxy implementation

        /// <inheritdoc/>
        public Uri ServiceUri { get; protected set; }

        /// <inheritdoc/>
        protected PortState BasePortState 
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
        protected void BaseOpenAsync(OpenAsyncState state, IRemactProtocolDriverCallbacks callback)
        {
            _callback = callback;
            _wsClient.OnConnected = OnConnected;
            _wsClient.OnDisconnect = OnConnectFailure;
            _wsClient.BeginConnect(state);
        }

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
            }

            _callback.OnOpenCompleted(state);
        }

        private void OnConnectFailure(UserContext context)
        {
            _faulted = true;
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

            _callback.OnServiceDisconnect();
        }

        #endregion
    }
}