
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Net;            // Dns
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using Remact.Net.Contracts;

namespace Remact.Net.Remote
{
    /// <summary>
    /// <para>Client class to handle a remote service.</para>
    /// <para>Requests are sent asynchronously.</para>
    /// <para>Responses are asynchroniously received on the same thread as the request was sent</para>
    /// <para>(only when sent from a thread with message queue (as WinForms), but not when sent from a threadpool-thread).</para>
    /// </summary>
    internal class RemactClient : IRemotePort, IRemactProtocolDriverToClient, IRemactService
    {
        //----------------------------------------------------------------------------------------------
        #region Identification, fields
        /// <summary>
        /// Detailed information about this client. May be a RemactPortClient&lt;TOC&gt; object containing application specific "OutputContext".
        /// </summary>
        public RemactPortClient PortClient { get; private set; }

        /// <summary>
        /// The last request id received in a response from the connected service.
        /// It is used to calculate outstandig responses.
        /// </summary>
        protected int LastRequestIdReceived;

        /// <summary>
        /// Detailed information about the connected service. Contains a "UserContext" object for free use by the client application.
        /// </summary>
        public RemactPortProxy PortProxy { get; private set; }

        /// <summary>
        /// The lower level client.
        /// </summary>
        internal IRemactProtocolDriverToService m_protocolClient; // internal protected is not allowed ?!

        /// <summary>
        /// <para>Set m_boTimeout to true, when the connect operation fails or some errormessages are received.</para>
        /// <para>Sets the client into Fault state.</para>
        /// </summary>
        protected bool m_boTimeout;

        /// <summary>
        /// The original service name (unique in plant), not the catalog service.
        /// </summary>
        protected string m_ServiceNameToLookup;

        /// <summary>
        /// URI of next service to connect, can be the catalog service.
        /// </summary>
        protected Uri m_RequestedServiceUri;

        /// <summary>
        /// The plugin provided by the library user or RemactDefaults.ClientConfiguration
        /// </summary>
        protected IClientConfiguration m_ClientConfig;

        /// <summary>
        /// True, when connecting or connected to catalog service, not to the original service.
        /// </summary>
        private bool _connectViaCatalog;

        /// <summary>
        /// True, when connecting and not yet connected.
        /// </summary>
        protected bool m_boConnecting;

        /// <summary>
        /// The number of addresses tried to connect already.
        /// </summary>
        protected int m_addressesTried;

        /// <summary>
        /// The tried address. 0 = hostname, 1 = first IP address, AddressList.Count = last IP address.
        /// </summary>
        protected int m_addressNumber;

        /// <summary>
        /// True, when first response from original service received.
        /// </summary>
        protected bool m_boFirstResponseReceived;

        /// <summary>
        /// Outstanding requests, key = request ID.
        /// </summary>
        private Dictionary<int, RemactMessage> _OutstandingRequests;

        private readonly object _lock;


        #endregion
        //----------------------------------------------------------------------------------------------
        #region Constructor, connection state, linking and disconnecting

        /// <summary>
        /// Create the proxy for a remote service.
        /// </summary>
        /// <param name="portProxy">Link this RemactPortProxy to the remote service.</param>
        /// <param name="portClient">The internally used client identification.</param>
        internal RemactClient(RemactPortProxy portProxy, RemactPortClient portClient)
        {
            _OutstandingRequests = new Dictionary<int, RemactMessage>();
            _lock = new object();
            PortClient = portClient;
            PortProxy = portProxy;
            PortProxy.IsServiceName = true;
        }


        /// <summary>
        /// <para>Connect this Client to a service identified by the serviceName parameter.</para>
        /// <para>The correct serviceHost and TCP port will be looked up at a Remact.CatalogService identified by parameter catalogHost.</para>
        /// </summary>
        /// <param name="serviceName">A unique name of the service. This service may run on any host that has been registered at the Remact.CatalogService.</param>
        /// <param name="clientConfig">Plugin your own client configuration instead of RemactDefaults.ClientConfiguration.</param>
        internal void LinkToRemoteService(string serviceName, IClientConfiguration clientConfig = null)
        {
            _connectViaCatalog = true;
            m_ClientConfig = clientConfig;
            m_ServiceNameToLookup = serviceName;
            PortProxy.PrepareServiceName(RemactConfigDefault.Instance.CatalogHost, m_ServiceNameToLookup);
        }


        /// <summary>
        /// Link this ClientIdent to a remote service. No lookup at Remact.Catalog is needed as we know the TCP portnumber.
        /// </summary>
        /// <param name="websocketUri">The uri of the remote service.</param>
        /// <param name="clientConfig">Plugin your own client configuration instead of RemactDefaults.ClientConfiguration.</param>
        internal void LinkToRemoteService(Uri websocketUri, IClientConfiguration clientConfig = null)
        {
            // this link method does not read the App.config file (it is running on mono also).
            if (!IsDisconnected) Disconnect();
            _connectViaCatalog = false;
            m_ClientConfig = clientConfig;
            m_RequestedServiceUri = NormalizeHostName(websocketUri);
        }

        private Uri NormalizeHostName(Uri uri)
        {
            if (uri.IsLoopback)
            {
                UriBuilder b = new UriBuilder(uri);
                b.Host = Dns.GetHostName(); // concrete name of localhost -- needed for Mono!
                return b.Uri;
            }
            return uri;
        }

        private string NormalizeHostName(string host)
        {
            if (host.ToLower() == "localhost")
            {
                return Dns.GetHostName(); // concrete name of localhost -- needed for Mono!
            }
            return host;
        }


        /// <summary>
        /// A client is connected after the ServiceConnectResponse has been received.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return m_protocolClient != null
                    && m_boFirstResponseReceived
                    && !m_boTimeout
                    && m_protocolClient.PortState == PortState.Ok;
            }
        }

        /// <summary>
        /// A client is disconnected after construction, after a call to Disconnect() or AbortCommunication()
        /// </summary>
        public bool IsDisconnected { get { return !m_boConnecting && m_protocolClient == null && !m_boTimeout; } }

        /// <summary>
        /// A client is in Fault state when a connection cannot be kept open or a timeout has passed.
        /// </summary>
        public bool IsFaulted
        {
            get
            {
                return m_boTimeout
                || (m_protocolClient != null
                    && m_protocolClient.PortState == PortState.Faulted);
            }
        }

        /// <summary>
        /// Returns the number of requests that have not received a response by the service.
        /// </summary>
        public int OutstandingResponsesCount { get { return _OutstandingRequests.Count; } }


        /// <summary>
        /// Same as Disconnect.
        /// </summary>
        public void Dispose()
        {
            Disconnect();
        }


        /// <summary>
        /// <para>Send Disconnect messages to service if possible . Go from any state to Disconnected state.</para>
        /// <para>Makes it possible to restart the client with ConnectAsync.</para>
        /// </summary>
        public void Disconnect()
        {
            try
            {
                if (IsConnected)
                {
                    try
                    {
                        RemactCatalogClient.Instance.RemoveClient(this);
                        Remact_ActorInfo_ClientDisconnectNotification(new ActorInfo(PortClient));
                    }
                    catch
                    {
                    }
                }

                if (m_protocolClient != null)
                {
                    m_protocolClient.Dispose();
                    RemactCatalogClient.Instance.RemoveClient(this);
                }
            }
            catch (Exception ex)
            {
                RaLog.Exception("cannot abort Remact connection", ex, PortProxy.Logger);
            }

            m_protocolClient = null;
            m_boConnecting = false;
            m_boFirstResponseReceived = false;
            m_boTimeout = false;
            PortProxy.m_isOpen = false; // internal, from Proxy to RemactClient
            PortClient.m_isOpen = false; // internal, from Client to Proxy

        }// Disconnect


        /// <summary>
        /// <para>Abort all messages, go from any state to Disconnected state.</para>
        /// <para>Makes it possible to restart the client with ConnectAsync.</para>
        /// </summary>
        public void AbortCommunication()
        {
            m_boTimeout = true;
            Disconnect();
        }


        #endregion
        //----------------------------------------------------------------------------------------------
        #region Connect


        /// <summary>
        /// Connect or reconnect output to the previously linked partner.
        /// </summary>
        /// <returns>A task. When this task is run to completion, the task.Result corresponds to IsOpen.</returns>
        public Task<bool> ConnectAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            try
            {
                if (!(IsDisconnected || IsFaulted))
                {
                    throw new InvalidOperationException("cannot connect " + PortClient.Name + ", state = " + OutputState);
                }

                PortClient.PickupSynchronizationContext();
                PortClient.m_isOpen = true;
                m_boFirstResponseReceived = false;
                m_boTimeout = false;
                m_boConnecting = true;

                if (!_connectViaCatalog)
                {
                    return OpenConnectionAsync(tcs, m_RequestedServiceUri);
                }

                if (RemactCatalogClient.IsDisabled)
                {
                    throw new InvalidOperationException("cannot open " + PortClient.Name + ", RemactCatalogClient is disabled");
                }

                Task<RemactMessage<ActorInfo>> task = RemactCatalogClient.Instance.LookupService(m_ServiceNameToLookup);
                task.ContinueWith(t =>
                {
                    if (t.Status != TaskStatus.RanToCompletion)
                    {
                        EndOfConnectionTries(tcs, "failed when asking catalog service.", t.Exception);
                        return;
                    }

                    PortProxy.UseDataFrom(t.Result.Payload);
                    if (PortProxy.TraceSend)
                    {
                        string s = string.Empty;
                        if (t.Result.Payload.AddressList != null)
                        {
                            string delimiter = ", IP-addresses = ";
                            foreach (var adr in t.Result.Payload.AddressList)
                            {
                                s = string.Concat(s, delimiter, adr.ToString());
                                delimiter = ", ";
                            }
                        }
                        RaLog.Info(t.Result.CltRcvId, "ServiceAddressResponse: " + t.Result.Payload.Uri + s, PortProxy.Logger);
                    }

                    m_addressesTried = 0;
                    TryOpenNextServiceIpAddress(tcs, null); // try first address
                });
            }
            catch (Exception ex)
            {
                EndOfConnectionTries(tcs, "exception in ConnectAsync.", ex); // enter 'faulted' state when eg. configuration is incorrect
            }

            return tcs.Task;
        }


        // Tries to open one of the IP addresses of the service. Is running on user- or threadpool thread
        private bool TryOpenNextServiceIpAddress(TaskCompletionSource<bool> tcs, Exception error)
        {
            if (m_boFirstResponseReceived)
            {
                return EndOfConnectionTries(tcs, null, null); // successful !
            }

            if (error != null)
            {
                if (PortProxy.AddressList == null) return EndOfConnectionTries(tcs, "one address tried.", error); // connect without lookup a catalog

                m_addressNumber++; // connection failed, next time try next address. 
                if (m_addressesTried > PortProxy.AddressList.Count) return EndOfConnectionTries(tcs, "all addresses tried.", error);
                if (!m_boConnecting) return EndOfConnectionTries(tcs, "wrong state.", error);
                m_addressesTried++;
            }

            UriBuilder b = new UriBuilder(PortProxy.Uri);
            if (m_addressNumber <= 0 || PortProxy.AddressList == null || m_addressNumber > PortProxy.AddressList.Count)
            {
                m_addressNumber = 0; // the hostname
                b.Host = PortProxy.HostName;
            }
            else
            {
                b.Host = PortProxy.AddressList[m_addressNumber - 1].ToString(); // an IP address
            }

            b.Host = b.Uri.DnsSafeHost;
            OpenConnectionAsync(tcs, b.Uri);
            return true;
        }


        // Open the connection to the service, running on user- or threadpool thread
        private Task<bool> OpenConnectionAsync(TaskCompletionSource<bool> tcs, Uri uri)
        {
            var websocketUri = m_RequestedServiceUri = NormalizeHostName(uri);
            // TODO: Let now the library user change binding and security credentials.
            // By default RemactDefaults.OnClientConfiguration is called.
            if (m_ClientConfig == null)
            {
                m_ClientConfig = RemactConfigDefault.Instance;
            }
            m_protocolClient = m_ClientConfig.DoClientConfiguration(ref websocketUri, forCatalog: false);
            PortProxy.PrepareServiceName(websocketUri);

            m_protocolClient.OpenAsync(new OpenAsyncState { Tcs = tcs }, this);
            // Callback to OnOpenCompleted when channel has been opened locally (no TCP connection opened on mono).
            return tcs.Task; // Connecting now
        }


        // Eventhandler, running on threadpool thread, sent from m_protocolClient.
        // No parallel connection attempts and no messages to the user. Therefore, we remain on the threadpool thread.
        void IRemactProtocolDriverToClient.OnOpenCompleted(OpenAsyncState state)
        {
            if (m_protocolClient == null)
            {
                EndOfConnectionTries(state.Tcs, "output was disconnected.", new ObjectDisposedException("RemactClient"));
                return;
            }

            try
            {
                if (state.Error != null)
                {
                    TryOpenNextServiceIpAddress(state.Tcs, state.Error); // failed opening when using the current IP address
                }
                else
                {
                    var task = Remact_ActorInfo_ClientConnectRequest(new ActorInfo(PortClient));
                    task.ContinueWith(t =>
                        {
                            if (t.Status != TaskStatus.RanToCompletion)
                            {
                                EndOfConnectionTries(state.Tcs, "failed when sending ClientConnectRequest.", t.Exception);
                                return;
                            }

                            if (t.Result.Payload.IsServiceName && t.Result.Payload.IsOpen)
                            { // First message received from Service
                            t.Result.Payload.Uri = PortProxy.Uri; // keep the Uri stored here (maybe IP address instead of hostname used)
                            PortProxy.UseDataFrom(t.Result.Payload);
                                PortClient.ClientId = t.Result.Payload.ClientId; // defined by server
                            t.Result.ClientId = t.Result.Payload.ClientId;
                                if (PortProxy.TraceConnect) RaLog.Info(t.Result.CltRcvId, PortProxy.ToString("Connected  svc", 0), PortProxy.Logger);

                                m_boConnecting = false;
                                m_boFirstResponseReceived = true; // IsConnected --> true !
                            RemactCatalogClient.Instance.AddClient(this);
                                EndOfConnectionTries(state.Tcs, null, null); // ok
                        }
                            else
                            {
                                EndOfConnectionTries(state.Tcs, "unexpeced ClientConnectResponse.", new InvalidOperationException("unexpected message from service: " + t.Result.ToString()));
                            }
                        });
                }
            }
            catch (Exception ex)
            {
                EndOfConnectionTries(state.Tcs, "exception in OnOpenCompleted.", ex); // enter 'faulted' state when eg. configuration is incorrect
            }
        }// OnOpenCompleted


        private bool EndOfConnectionTries(TaskCompletionSource<bool> tcs, string reason, Exception ex)
        {
            m_boTimeout = !m_boFirstResponseReceived;

            if (m_boTimeout)
            {
                if (ex == null) ex = new OperationCanceledException(reason);
                RaLog.Exception("Remact cannot connect '" + PortClient.Name + "', " + reason, ex, PortProxy.Logger);
                tcs.SetException(ex);
            }
            else
            {
                tcs.SetResult(true);
            }

            return false;
        }


        #endregion
        //----------------------------------------------------------------------------------------------
        #region IRemactService implementation

        public Task<RemactMessage<ActorInfo>> Remact_ActorInfo_ClientConnectRequest(ActorInfo client)
        {
            client.IsOpen = true;
            bool traceSend = PortProxy.TraceSend;
            if (PortProxy.TraceConnect)
            {
                PortProxy.TraceSend = false;
            }

            bool multithreaded = PortProxy.IsMultithreaded;
            PortProxy.IsMultithreaded = true;

            PortClient.ClientId = 0;
            PortProxy.LastRequestIdSent = 9;
            PortProxy.m_isOpen = true; // internal, from Proxy to RemactClient
            LastRequestIdReceived = 9;
            RemactMessage sentMessage;
            var task = PortProxy.SendReceiveAsync<ActorInfo>(RemactService.ConnectMethodName, client, out sentMessage, throwException: false);

            PortProxy.IsMultithreaded = multithreaded;
            if (PortProxy.TraceConnect)
            {
                PortProxy.TraceSend = traceSend;
                PortProxy.Uri = m_protocolClient.ServiceUri; // Prepares endpointaddress for logging
                RaLog.Info(sentMessage.CltSndId, string.Concat("Connecting svc: '", PortProxy.Uri, "'"), PortProxy.Logger);
            }
            return task;
        }

        public void Remact_ActorInfo_ClientDisconnectNotification(ActorInfo client)
        {
            client.IsOpen = false;
            bool traceSend = PortProxy.TraceSend;
            PortProxy.TraceSend = PortProxy.TraceConnect;
            var msg = new RemactMessage(PortProxy, RemactService.DisconnectMethodName, client, RemactMessageType.Notification, null);
            PostInput(msg);
            PortProxy.TraceSend = traceSend;
            Thread.Sleep(30);
        }

        #endregion
        //----------------------------------------------------------------------------------------------
        #region IRemactProtocolDriverCallbacks implementation and incoming messages


        private bool TryGetResponseMessage(LowerProtocolMessage lower, out RemactMessage msg)
        {
            lock (_lock)
            {
                if (!_OutstandingRequests.TryGetValue(lower.RequestId, out msg))
                {
                    return false;
                }

                _OutstandingRequests.Remove(lower.RequestId);
            }

            msg.DestinationLambda = msg.SourceLambda;
            msg.SourceLambda = null;
            msg.Source = PortProxy;
            msg.Destination = PortClient;
            msg.ClientId = PortClient.ClientId;

            msg.Payload = lower.Payload;
            msg.SerializationPayload = lower.SerializationPayload;
            msg.MessageType = lower.Type;
            return true;
        }


        Uri IRemactProtocolDriverToClient.ClientUri { get { return PortClient.Uri; } }


        // sent from m_protocolClient
        void IRemactProtocolDriverToClient.OnServiceDisconnect()
        {
            if (PortProxy.IsMultithreaded || PortProxy.SyncContext == null)
            {
                OnServiceDisconnectOnActorThread(null);
            }
            else
            {
                PortProxy.SyncContext.Post(OnServiceDisconnectOnActorThread, null);
            }
        }


        private void OnServiceDisconnectOnActorThread(object dummy)
        {
            m_boTimeout = true;
            var copy = _OutstandingRequests;
            _OutstandingRequests = new Dictionary<int, RemactMessage>();

            foreach (var msg in copy.Values)
            {
                var lower = new LowerProtocolMessage
                {
                    Type = RemactMessageType.Error,
                    RequestId = msg.RequestId,
                    Payload = new ErrorMessage(ErrorCode.CouldNotSend, "web socket disconnected")
                };

                OnIncomingMessageOnActorThread(lower);
            }
        }

        // sent from m_protocolClient
        void IRemactProtocolDriverToClient.OnMessageToClient(LowerProtocolMessage msg)
        {
            if (PortProxy.IsMultithreaded || PortProxy.SyncContext == null)
            {
                OnIncomingMessageOnActorThread(msg);
            }
            else
            {
                PortProxy.SyncContext.Post(OnIncomingMessageOnActorThread, msg);
            }
        }


        private void OnIncomingMessageOnActorThread(object obj)
        {
            try
            {
                var lower = (LowerProtocolMessage)obj;

                RemactMessage msg;

                switch (lower.Type)
                {
                    case RemactMessageType.Response:
                        {
                            if (!TryGetResponseMessage(lower, out msg))
                            {
                                RaLog.Warning(PortClient.Name, "skipped unexpected response with id " + lower.RequestId, PortProxy.Logger);
                                return;
                            }
                        }
                        break;

                    case RemactMessageType.Error:
                        {
                            if (!TryGetResponseMessage(lower, out msg))
                            {
                                msg = new RemactMessage(PortClient, lower.DestinationMethod, lower.Payload, RemactMessageType.Error, null);
                                msg.SerializationPayload = lower.SerializationPayload;
                                msg.RequestId = lower.RequestId;
                            }
                        }
                        break;

                    default:
                        {
                            msg = new RemactMessage(PortClient, lower.DestinationMethod, lower.Payload, RemactMessageType.Notification, null);
                            msg.SerializationPayload = lower.SerializationPayload;
                            msg.RequestId = lower.RequestId;
                            msg.MessageType = lower.Type; // initially set to notification as we do not want to increment our clients requestId
                        }
                        break;
                }

                if (!m_boTimeout)
                {
                    var m = msg.Payload as IExtensibleRemactMessage;
                    if (!PortProxy.IsMultithreaded)
                    {
                        if (m != null) m.BoundSyncContext = SynchronizationContext.Current;
                    }
                    if (m != null) m.IsSent = true;

                    if (msg.DestinationMethod == null) msg.DestinationMethod = string.Empty;
                }

                PortProxy.DispatchMessage(msg);
            }
            catch (Exception ex)
            {
                RaLog.Exception("Message for " + PortClient.Name + " cannot be handled by application", ex, PortProxy.Logger);
            }
        }// OnIncomingMessageOnActorThread


        #endregion
        //----------------------------------------------------------------------------------------------
        #region IRemoteActor implementation

        /// <summary>
        /// Gets or sets the state of the outgoing connection. May be called on any thread.
        /// </summary>
        /// <returns>A <see cref="PortState"/></returns>
        public PortState OutputState
        {
            get
            {
                if (IsConnected) return PortState.Ok;
                if (IsDisconnected) return PortState.Disconnected;
                if (IsFaulted) return PortState.Faulted;
                return PortState.Connecting;
            }

            set
            {
                if (value == PortState.Connecting || value == PortState.Ok)
                {
                    if (PortProxy.IsMultithreaded || PortProxy.SyncContext != null)
                    {
                        ConnectAsync();
                    }
                    else
                    {
                        throw new InvalidOperationException("Remact: ConnectAsync of '" + PortClient.Name + "' has not been called to pick up the synchronization context.");
                    }
                }
                else if (value == PortState.Faulted)
                {
                    AbortCommunication();
                }
                else
                {
                    Disconnect();
                }
            }
        }


        /// <summary>
        /// Post a request to the input of the remote partner. It will be sent over the network.
        /// Called from ClientIdent, when SendOut a message to remote partner.
        /// </summary>
        /// <param name="msg">A <see cref="RemactMessage"/></param>
        public void PostInput(RemactMessage msg)
        {
            if (m_boTimeout || m_protocolClient == null || m_protocolClient.PortState != PortState.Ok)
            {
                throw new InvalidOperationException("Remact: Output of '" + PortClient.Name + "' is not open. Cannot send message.");
            }

            // PostInput() may be used during connection buildup as well
            if (PortProxy.TraceSend) RaLog.Info(msg.CltSndId, msg.ToString(), PortProxy.Logger);

            lock (_lock)
            {
                RemactMessage lost;
                if (_OutstandingRequests.TryGetValue(msg.RequestId, out lost))
                {
                    _OutstandingRequests.Remove(msg.RequestId);
                    RaLog.Error(lost.CltSndId, "response was never received", PortProxy.Logger);
                }

                _OutstandingRequests.Add(msg.RequestId, msg);
            }

            m_protocolClient.MessageToService(new LowerProtocolMessage(msg));
        }

        /// <summary>
        /// Gets the Uri of a linked service.
        /// </summary>
        public Uri Uri { get { return PortProxy.Uri; } }


        #endregion
    }
}
