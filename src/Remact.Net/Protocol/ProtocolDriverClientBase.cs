
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Threading;
using System.Collections.Concurrent;
using Alchemy;
using Alchemy.Classes;
using MsgPack.Serialization;
using Remact.Net.Protocol;
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


        public Uri ServiceUri { get; protected set; }
        

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

        // Asynchronous open the connection
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