
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Threading.Tasks;

namespace Remact.Net.Remote
{
    /// <summary>
    /// <para>Class used on service side.</para>
    /// <para>Represents a connected client.</para>
    /// <para>Has the possibility to forward responses, notifications and requests to the client.</para>
    /// </summary>
    public class RemactServiceUser : IRemotePort
    {
        //----------------------------------------------------------------------------------------------
        #region Properties

        /// <summary>
        /// <para>Detailed information about the client using this service.</para>
        /// <para>Contains the "SenderCtx" object that may be used freely by the service application.</para>
        /// <para>Output from service side is linked to this RemactPortClient.</para>
        /// </summary>
        internal RemactPortClient PortClient { get; private set; }

        /// <summary>
        /// Client ID used on this service.
        /// </summary>
        public int ClientId { get { return PortClient.ClientId; } }

        private RemactPortService _serviceIdent;

        /// <summary>
        /// Set to 0 when a message has been received or sent. Incremented by milliseconds in TestNotificationChannel().
        /// </summary>
        internal int ChannelTestTimer;
        internal object ClientAccessLock = new Object();

        private IRemactProtocolDriverToClient _protocolCallback;
        private bool m_boTimeout;

        #endregion
        //----------------------------------------------------------------------------------------------
        #region Constructor + Methods

        /// <summary>
        /// Internally called to create a RemactServiceUser object
        /// </summary>
        /// <param name="serviceIdent">client using this service</param>
        public RemactServiceUser(RemactPortService serviceIdent)
        {
            _serviceIdent = serviceIdent;
            PortClient = new RemactPortClient()
            {
                SvcUser = this,
                LinkedPort = this,  // the local client posts notifications to us, we will pass it to the remote client
                ServiceIdent = serviceIdent,
                SenderCtx = serviceIdent.GetNewSenderContext(),
                TraceConnect = serviceIdent.TraceConnect,
                TraceSend = serviceIdent.TraceSend,
                TraceReceive = serviceIdent.TraceReceive,
                Logger = serviceIdent.Logger,
            };

        }// CTOR

        /// <summary>
        /// Sets the protocol driver instance to use for this connection from service to client.
        /// </summary>
        /// <param name="protocolCallback">The protocol driver to use.</param>
        public void SetCallbackHandler(IRemactProtocolDriverToClient protocolCallback)
        {
            _protocolCallback = protocolCallback;
        }

        internal void UseDataFrom(ActorInfo remoteClient, int clientId)
        {
            PortClient.UseDataFrom(remoteClient);
            PortClient.ClientId = clientId;
            UriBuilder uri = new UriBuilder(PortClient.Uri);
            uri.Scheme = _serviceIdent.Uri.Scheme; // the service's Uri scheme (e.g. http)
            PortClient.Uri = uri.Uri;
        }

        internal void SetConnected()
        {
            PortClient.SyncContext = _serviceIdent.SyncContext;
            PortClient.ManagedThreadId = _serviceIdent.ManagedThreadId;
            PortClient.m_isOpen = true;
            m_boTimeout = false;
            if (PortClient.Uri == null && _protocolCallback != null)
            {
                PortClient.Uri = _protocolCallback.ClientUri;
                PortClient.Name = "anonymous";
            }
        }

        /// <summary>
        /// Shutdown the outgoing connection. Send a disconnect message to the partner.
        /// </summary>
        public void Disconnect()
        {
            // TODO close the webSocket in case a remote connection is present.
            PortClient.Disconnect();
        }

        /// <summary>
        /// Is the client connected ?
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return PortClient.m_isOpen && !m_boTimeout && _protocolCallback != null;
            }
        }

        /// <summary>
        /// Was there an error that disconnected the client ?
        /// </summary>
        public bool IsFaulted { get { return m_boTimeout; } }

        /// <summary>
        /// Used for tracing messages from/to this client.
        /// </summary>
        public string ClientMark { get { return string.Format("{0}/{1}[{2}]", PortClient.HostName, PortClient.Name, PortClient.ClientId); } }

        /// <summary>
        /// Internally called by DoPeriodicTasks(), notifies idle messages and disconnects the client connection in case of failure.
        /// </summary>
        /// <returns>True, when connection state has changed.</returns>
        public bool TestChannel(int i_nMillisecondsPassed)
        {
            if (_protocolCallback != null)
            {
                try
                {
                    if (PortClient.TimeoutSeconds > 0
                     && ChannelTestTimer > ((PortClient.TimeoutSeconds + i_nMillisecondsPassed) * 400)) // 2 messages per TimeoutSeconds-period
                    {
                        PortClient.Notify(RemactService.KeepAliveMethodName, new ReadyMessage());
                    }
                }
                catch (Exception ex)
                {
                    RaLog.Exception("Cannot test '" + ClientMark + "' from service", ex, PortClient.Logger);
                    m_boTimeout = true;
                    return true; // changed
                }
            }

            if (IsConnected)
            {
                if (PortClient.TimeoutSeconds > 0 && ChannelTestTimer > PortClient.TimeoutSeconds * 1000)
                {
                    m_boTimeout = true; // Client has not sent a request for longer than the specified timeout
                    Disconnect();
                    return true; // changed
                }

                ChannelTestTimer += i_nMillisecondsPassed; // increment at end, to give a chance to recover after longer debugging breakpoints
            }
            return false;
        }// TestChannel


        /// <summary>
        /// Internally called on service shutdown or timeout on notification channel
        /// </summary>
        internal void AbortNotificationChannel()
        {
            if (_protocolCallback == null) return;

            try
            {
                Disconnect();
                _protocolCallback.OnServiceDisconnect();
                _protocolCallback = null;
            }
            catch (Exception ex)
            {
                _protocolCallback = null;
                RaLog.Exception("Cannot abort '" + ClientMark + "' from service", ex, PortClient.Logger);
            }
        }


        /// <summary>
        /// Returns the number of notification responses, that have not been sent yet.
        /// </summary>
        public int OutstandingResponsesCount
        {
            get
            {
                //if (m_NotifyList != null) return m_NotifyList.Notifications.Count;
                return 0;
            }
        }

        #endregion
        //----------------------------------------------------------------------------------------------
        #region IRemoteActor implementation

        /// <summary>
        /// Dummy implementation. Client stub is always connected to the service.
        /// </summary>
        /// <returns>true</returns>
        Task<bool> IRemotePort.ConnectAsync()
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// Send a response, error or notification to the remote client belonging to this client-stub.
        /// </summary>
        /// <param name="msg">A <see cref="RemactMessage"/>.</param>
        public void PostInput(RemactMessage msg)
        {
            if (_protocolCallback == null)
            {
                RaLog.Warning("RemactService", "Closed channel to " + ClientMark, PortClient.Logger);
            }
            else
            {
                var lower = new LowerProtocolMessage
                {
                    Type = msg.MessageType,
                    RequestId = msg.RequestId,
                    Payload = msg.Payload
                };

                _protocolCallback.OnMessageToClient(lower);
            }
        }

        /// <summary>
        /// Gets the Uri of a linked client.
        /// </summary>
        public Uri Uri { get { return PortClient.Uri; } }


        #endregion
        //----------------------------------------------------------------------------------------------
    }
}
