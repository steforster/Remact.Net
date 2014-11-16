
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using Remact.Net.Protocol;
using System.Net.Sockets;
using System.Net;

namespace Remact.Net.Remote
{
    /// <summary>
    /// <para>Class used on service side.</para>
    /// <para>Handles and stores all connected clients.</para>
    /// </summary>
    public class RemactService
    {
        //----------------------------------------------------------------------------------------------
        #region Properties

        /// <summary>
        /// Detailed information about this service
        /// </summary>
        public RemactPortService ServiceIdent { get; private set; }

        /// <summary>
        /// The count of known clients of this service (connected or disconnected)
        /// </summary>
        public int ClientCount { get { return ServiceIdent.InputClientList.Count - m_UnusedClientCount; } }

        /// <summary>
        /// The count of connected clients of this service
        /// </summary>
        public int ConnectedClientCount { get { return m_ConnectedClientCount; } }

        /// <summary>
        /// May be used for tracing of connect/reconnect/disconnect operations.
        /// </summary>L
        public string LastAction;

        /// <summary>
        /// True if any client has been connected or disconnected. Set to false by DoPeriodicTasks()
        /// </summary>
        internal bool HasConnectionStateChanged = true;

        /// <summary>
        /// Internally used by CatalogClient
        /// </summary>
        internal bool IsServiceRegistered;

        /// <summary>
        /// Internally used for periodic message to Remact.CatalogService
        /// </summary>
        internal DateTime NextEnableMessage;

        private int m_FirstClientId;          // offset, normally = 1
        private int m_UnusedClientCount = 0;  // disconnected clients having RemactDefaults.IsProcessIdUsed
        private int m_ConnectedClientCount = 0;
        private int m_millisPeriodicTask = 0; // systemstart = 0
        private bool m_boCurrentlyCalled;      // to check concurrent calls

        private int _tcpPort;
        private bool _publishToCatalog;
        private IServiceConfiguration _serviceConfig;
        private WebSocketPortManager _networkPortManager;

        private static int ms_nSharedTcpPort;
        private static int ms_nSharedTcpPortCount;

        internal const string ConnectMethodName = ActorInfo.MethodNamePrefix + "ClientConnectRequest";
        internal const string DisconnectMethodName = ActorInfo.MethodNamePrefix + "ClientDisconnectNotification";
        internal const string KeepAliveMethodName = ActorInfo.MethodNamePrefix + "KeepAliveNotification";

        /// <summary>
        /// Returns true, when service is ready to receive requests.
        /// </summary>
        public bool IsOpen { get { return _networkPortManager != null; } }


        /// <summary>
        /// Gets or sets the state of the incoming service connection from the network.
        /// </summary>
        /// <returns>A <see cref="PortState"/></returns>
        public PortState InputStateFromNetwork
        {
            get
            {
                if (_networkPortManager == null) return PortState.Disconnected;
                return PortState.Ok;
            }

            set
            {
                if (value == PortState.Ok || value == PortState.Connecting)
                {
                    if (!IsOpen) OpenService();
                }
                else
                {
                    Disconnect();
                }
            }
        }

        #endregion
        //----------------------------------------------------------------------------------------------
        #region Constructors / shutdown

        /// <summary>
        /// <para>Initializes a new instance of the RemactService class.</para>
        /// <para>The service is uniquely identified by the service name.</para>
        /// </summary>
        /// <param name="serviceIdent">This RemactPortService is linked to network.</param>
        /// <param name="tcpPort">The TCP port for the service or 0, when automatic port allocation may be used.</param>
        /// <param name="publishToCatalog">True(=default): The servicename will be published to the Remact.Catalog on localhost.</param>
        /// <param name="serviceConfig">Plugin your own service configuration instead of RemactDefaults.ServiceConfiguration.</param>
        internal RemactService(RemactPortService serviceIdent, int tcpPort = 0, bool publishToCatalog = true,
                               IServiceConfiguration serviceConfig = null)
        {
            ServiceIdent = serviceIdent;
            ServiceIdent.IsServiceName = true;
            ServiceIdent.InputClientList = new List<RemactPortClient>(20);
            m_FirstClientId = 1;
            _tcpPort = tcpPort;
            _publishToCatalog = publishToCatalog;
            _serviceConfig = serviceConfig;
            if (_serviceConfig == null)
            {
                _serviceConfig = RemactConfigDefault.Instance;
            }
        }// CTOR


        /// <summary>
        /// Opens the service for communication.
        /// When linked to network, a shared TCP listener port is opened.
        /// When linked to notwork and not CatalogClient.IsDisabled, the service name and uri are registered at the catalog service.
        /// </summary>
        /// <returns>true if successfully open.</returns>
        internal bool OpenService()
        {
            try
            {
                if (_networkPortManager != null) Disconnect();

                if (_tcpPort == 0)
                {
                    if (ms_nSharedTcpPort == 0 || ms_nSharedTcpPortCount == 0)
                    {
                        // Find the next free local TCP-port:
                        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        IPEndPoint endpoint = new IPEndPoint(0, 0);   // Local Address, dynamic port assignment
                        socket.Bind(endpoint);
                        endpoint = socket.LocalEndPoint as IPEndPoint; // a free port has been assigned by windows
                        ms_nSharedTcpPort = endpoint.Port; // a free dynamic assigned, local port
                        socket.Close(); // socket.Shutdown is not allowed as we are not yet connected
                    }
                    _tcpPort = ms_nSharedTcpPort;
                    ms_nSharedTcpPortCount++;
                }

                Uri uri = new Uri("ws://"
                    + ServiceIdent.HostName     // initialized with Dns.GetHostName()
                    + ":" + _tcpPort
                    + "/" + RemactConfigDefault.WsNamespace + "/" + ServiceIdent.Name);// ServiceName, not the ServiceType

                // Let the library user add the service. By default RemactDefaults.DoServiceConfiguration is called.
                _networkPortManager = _serviceConfig.DoServiceConfiguration(this, ref uri, /*isCatalog=*/false);
                ServiceIdent.Uri = uri;

                if (_publishToCatalog)
                {
                    // Start registering on Remact.Catalog
                    RemactCatalogClient.Instance.AddService(this);
                }

                RaLog.Info("Remact", "Opened service " + ServiceIdent.Uri, ServiceIdent.Logger);
                return true;
            }
            catch (Exception ex)
            {
                RaLog.Exception("could not open " + ServiceIdent.Name, ex, ServiceIdent.Logger);
                LastAction = ex.Message;
            }
            return false;
        }// OpenService


        /// <summary>
        /// <para>Shutdown this service and release all attached resources</para>
        /// <para>Send service disable message to Remact.Catalog if possible</para>
        /// </summary>
        internal void Disconnect()
        {
            try
            {
                if (_networkPortManager != null)
                {
                    AbortUserNotificationChannels();
                    try
                    {
                        if (_tcpPort == ms_nSharedTcpPort && ms_nSharedTcpPortCount > 0)
                        {
                            ms_nSharedTcpPortCount--;
                            _tcpPort = 0;
                        }

                        _networkPortManager.RemoveService(ServiceIdent.Uri.AbsolutePath);
                    }
                    catch
                    {
                    }

                    _networkPortManager = null;
                }

                RemactCatalogClient.Instance.RemoveService(this); // send disable message to Remact.CatalogService

                if (ServiceIdent.Uri != null) RaLog.Info("Remact", "Closed service " + ServiceIdent.Uri, ServiceIdent.Logger);
                else RaLog.Info("Remact", "Closed service " + ServiceIdent.Name, ServiceIdent.Logger);
            }
            catch (Exception ex)
            {
                RaLog.Exception("Svc: Error while closing the service", ex, ServiceIdent.Logger);
            }
        }// Disconnect


        /// <summary>
        /// <para>Abort all notification connections, do not send any messages.</para>
        /// <para>Should be called before closing the service host.</para>
        /// </summary>
        internal void AbortUserNotificationChannels()
        {
            foreach (RemactPortClient clt in ServiceIdent.InputClientList)
            {
                if (clt.SvcUser != null) clt.SvcUser.AbortNotificationChannel();
            }
        }


        #endregion
        //----------------------------------------------------------------------------------------------
        #region Client connect / disconnect


        // Internally called to create a RemactServiceUser as client stub.
        internal virtual RemactServiceUser AddNewSvcUser(ActorInfo receivedClientMsg, int index, RemactServiceUser svcUser)
        {
            if (index < 0) // add a new element
            {
                ServiceIdent.InputClientList.Add(null);
                index = ServiceIdent.InputClientList.Count - 1;
            }

            if (svcUser == null)
            {
                svcUser = new RemactServiceUser(ServiceIdent); // svcUser not created, when connection has been opened
            }

            svcUser.UseDataFrom(receivedClientMsg, index + m_FirstClientId);
            ServiceIdent.InputClientList[index] = svcUser.PortClient;
            return svcUser;
        }


        /// <summary>
        /// Connect / Reconnect a client to this service.
        /// </summary>
        private object ConnectPartner(ActorInfo client, RemactMessage req, ref RemactServiceUser svcUser, ref bool connectEvent)
        {
            if (req.ClientId != 0)
            {// Client war schon mal verbunden
                int i = req.ClientId - m_FirstClientId;
                if (i >= 0 && i < ServiceIdent.InputClientList.Count + 100)
                {
                    // Nach dem Restart eines Service können sich Clients mit der alten Nummer anmelden
                    while (ServiceIdent.InputClientList.Count < i) ServiceIdent.InputClientList.Add(null);

                    svcUser = ServiceIdent.InputClientList[i].SvcUser;
                    if (svcUser == null)
                    {
                        svcUser = AddNewSvcUser(client, i, svcUser);
                        LastAction = "Reconnect after service restart";
                    }
                    else if (!client.IsEqualTo(svcUser.PortClient))
                    {
                        RaLog.Warning(req.SvcRcvId, svcUser.PortClient.ToString("ClientId already used", 0), ServiceIdent.Logger);
                        req.ClientId = 0; // eine neue ID vergeben, kann passieren, wenn Service, aber nicht alle Clients durchgestartet werden
                        m_ConnectedClientCount -= 2; // wird sofort 2 mal inkrementiert
                    }
                    else if (svcUser.IsConnected)
                    {
                        LastAction = "Reconnect, no disconnect";
                        RaLog.Warning(req.SvcRcvId, svcUser.PortClient.ToString(LastAction, 0), ServiceIdent.Logger);
                        //TODO
                        svcUser.UseDataFrom(client, req.ClientId);
                        m_ConnectedClientCount--; // wird sofort wieder inkrementiert
                    }
                    else if (svcUser.IsFaulted)
                    {
                        LastAction = "Reconnect after network failure";
                        RaLog.Warning(req.SvcRcvId, svcUser.PortClient.ToString(LastAction, 0), ServiceIdent.Logger);
                        //TODO
                        svcUser.UseDataFrom(client, req.ClientId);
                        if (RemactConfigDefault.Instance.IsProcessIdUsed(svcUser.PortClient.ProcessId)) m_UnusedClientCount--;
                    }
                    else
                    {
                        //TODO
                        svcUser.UseDataFrom(client, req.ClientId);
                        LastAction = "Reconnect after client disconnect";
                        if (RemactConfigDefault.Instance.IsProcessIdUsed(svcUser.PortClient.ProcessId)) m_UnusedClientCount--;
                    }
                    m_ConnectedClientCount++;
                }
                else
                {
                    ErrorMessage rsp = new ErrorMessage(ErrorCode.ClientIdNotFoundOnService, "Service cannot find client " + req.ClientId + " to connect");
                    RaLog.Error(req.SvcRcvId, rsp.Message, ServiceIdent.Logger);
                    LastAction = "ClientId mismatch while connecting";
                    return rsp;
                }
            }

            if (req.ClientId == 0)
            {// Client wurde neu gestartet
                int found = ServiceIdent.InputClientList.FindIndex(c => client.IsEqualTo(c));
                if (found < 0)
                {
                    svcUser = AddNewSvcUser(client, found, svcUser);
                    LastAction = "Connect first time";
                    m_ConnectedClientCount++;
                }
                else
                {
                    if (svcUser != null)
                    {
                        // a new svcUser has been created, when connection has been opened
                        svcUser = AddNewSvcUser(client, found, svcUser);
                    }
                    else
                    {
                        svcUser = ServiceIdent.InputClientList[found].SvcUser;
                        svcUser.UseDataFrom(client, found + m_FirstClientId);
                    }

                    if (svcUser.IsConnected)
                    {
                        LastAction = "Client is reconnecting";
                    }
                    else
                    {
                        LastAction = "Reconnect after client restart";
                        m_ConnectedClientCount++;
                    }
                }
            }

            // Connection state is kept in client object
            svcUser.ChannelTestTimer = 0;
            svcUser.SetConnected();
            HasConnectionStateChanged = true;
            connectEvent = true;

            // reply ServiceIdent
            ActorInfo response = new ActorInfo(ServiceIdent);
            response.ClientId = svcUser.PortClient.ClientId;
            req.Source = svcUser.PortClient;
            return response;
        }// Connect


        /// <summary>
        /// Mark a client as (currently) disconnected
        /// </summary>
        /// <returns>Dummy response.</returns>
        private object DisconnectPartner(ActorInfo client, RemactMessage req, ref RemactServiceUser svcUser, ref bool disconnectEvent)
        {
            int i = req.ClientId - m_FirstClientId;
            if (i >= 0 && i < ServiceIdent.InputClientList.Count)
            {
                svcUser = ServiceIdent.InputClientList[i].SvcUser;
                svcUser.ChannelTestTimer = 0;
                req.Source = svcUser.PortClient;
                HasConnectionStateChanged = true;
                if (client.IsEqualTo(svcUser.PortClient))
                {
                    svcUser.Disconnect();
                    LastAction = "Disconnect";
                    if (RemactConfigDefault.Instance.IsProcessIdUsed(svcUser.PortClient.ProcessId))
                    {
                        m_UnusedClientCount++;
                        ServiceIdent.InputClientList[i] = null; // will never be used again, the client has been shutdown
                    }
                    m_ConnectedClientCount--;
                    disconnectEvent = true;
                }
                else
                {
                    LastAction = "ClientId mismatch while disconnecting";
                    RaLog.Error(req.SvcRcvId, LastAction + ": " + client.Uri, ServiceIdent.Logger);
                }
            }
            else
            {
                svcUser = null;
                RaLog.Error(req.SvcRcvId, "Cannot disconnect client" + req.ClientId, ServiceIdent.Logger);
                LastAction = "Disconnect unknown client";
            }

            // Note: This response will normally not be sent to the client. The disconnect message is a notification.
            var response = new ErrorMessage(ErrorCode.CouldNotDisconnect, LastAction);
            return response;
        }// Disconnect


        /// <summary>
        /// Set client info into the message, call it once for each request to check the connection.
        /// </summary>
        /// <param name="req">RemactMessage.</param>
        /// <param name="svcUser">Output the user object containing a "ClientIdent.UserContext" object for free application use.</param>
        /// <returns>True, when the client has been found. False, when no client has been found and an error message must be generated.</returns>
        internal bool FindPartnerAndCheck(RemactMessage req, ref RemactServiceUser svcUser)
        {
            if (svcUser == null)
            {
                int i = req.ClientId - m_FirstClientId;
                if (i >= 0 && i < ServiceIdent.InputClientList.Count)
                {
                    svcUser = ServiceIdent.InputClientList[i].SvcUser;
                }
                else
                {
                    return false;
                }
            }

            svcUser.ChannelTestTimer = 0;
            req.Source = svcUser.PortClient;

            if (!svcUser.IsConnected)
            {
                svcUser.SetConnected();
                if (req.ClientId > 0)
                {
                    RaLog.Error(req.SvcRcvId, "Reconnect without ConnectRequest, RequestId = " + req.RequestId, ServiceIdent.Logger);
                    LastAction = "Reconnect without ConnectRequest";
                }
                else
                {
                    LastAction = "Client '" + svcUser.PortClient.Uri.ToString() + "' connected without ConnectRequest";
                    if (ServiceIdent.TraceConnect)
                    {
                        RaLog.Info(req.SvcRcvId, String.Format("{0} to service './{0}'", LastAction, ServiceIdent.Name), ServiceIdent.Logger);
                    }
                }

                if (RemactConfigDefault.Instance.IsProcessIdUsed(svcUser.PortClient.ProcessId))
                {
                    m_UnusedClientCount--;
                }
                m_ConnectedClientCount++;
                HasConnectionStateChanged = true;
            }
            return true;
        }// FindPartnerAndCheck


        /// <summary>
        /// Check if response can be generated by library or if an application message is required.
        /// </summary>
        /// <param name="req">The RemactMessage contains the request. It is used for the response also.</param>
        /// <param name="svcUser">
        /// input: null --> create a new RemactServiceUser as protocol independent client proxy
        ///        not null --> use this RemactServiceUser as protocol independent client proxy
        /// output: the user object contains the "ClientIdent.SenderContext" object for free application use
        /// </param>
        /// <param name="connectEvent">Set to true, when the connect method was called.</param>
        /// <param name="disconnectEvent">Set to true, when the disconnect method was called.</param>
        /// <returns><para> null when the response has to be generated by the application.</para>
        ///          <para>!null if the response already has been generated by this class.</para></returns>
        internal object CheckRemactInternalResponse(RemactMessage req, ref RemactServiceUser svcUser, ref bool connectEvent, ref bool disconnectEvent)
        {
            if (m_boCurrentlyCalled)
            {
                RaLog.Error("RemactSvc", "called by multiple threads", ServiceIdent.Logger);
            }
            m_boCurrentlyCalled = true;

            req.Destination = ServiceIdent;
            req.DestinationLambda = null;// make sure to call the DefaultHandler
            object response = null;

            if (req.DestinationMethod == null) req.DestinationMethod = string.Empty;

            if (req.DestinationMethod.StartsWith(ActorInfo.MethodNamePrefix))
            {
                ActorInfo cltReq;
                if (req.TryConvertPayload(out cltReq))
                {
                    req.Payload = cltReq; // use converted payload later on
                    switch (req.DestinationMethod)
                    {
                        case ConnectMethodName: response = ConnectPartner(cltReq, req, ref svcUser, ref connectEvent); break;
                        case DisconnectMethodName: response = DisconnectPartner(cltReq, req, ref svcUser, ref disconnectEvent); break;
                        default: break;// continue below
                    }
                }
            }

            if (response == null)
            {
                // no internal response generated
                if (FindPartnerAndCheck(req, ref svcUser))
                {
                    LastAction = "RemactMessage";// response must be generated by service-application, request.ClientIdent has been set
                }
                else
                {
                    response = new ErrorMessage(ErrorCode.ClientIdNotFoundOnService, "Service cannot find client " + req.ClientId);
                    RaLog.Error(req.SvcRcvId, (response as ErrorMessage).Message, ServiceIdent.Logger);
                    LastAction = "RemactMessage from unknown client";
                }
            }

            m_boCurrentlyCalled = false;
            return response;
        }


        #endregion
        //----------------------------------------------------------------------------------------------
        #region Public methods

        /// <summary>
        /// <para>Check client connection-timeouts, should be called periodically.</para>
        /// </summary>
        /// <returns>True, when a client state has changed</returns>
        public bool DoPeriodicTasks()
        {
            bool boChange = HasConnectionStateChanged;
            HasConnectionStateChanged = false;

            int nConnected = 0;
            int nUnused = 0;
            int millisCurrent = Environment.TickCount;
            int deltaT = millisCurrent - m_millisPeriodicTask;
            if (deltaT < 0 || deltaT > 3600000) deltaT = 0;

            for (int i = 0; i < ServiceIdent.InputClientList.Count; i++)
            {
                RemactServiceUser u = ServiceIdent.InputClientList[i].SvcUser;
                if (u == null) continue;

                if (u.TestChannel(deltaT))
                {
                    boChange = true;
                    if (u.IsFaulted)
                    {
                        RaLog.Warning("Svc=" + ServiceIdent.Name, u.PortClient.ToString("Timeout=" + u.PortClient.TimeoutSeconds
                            + " sec. no message from clt[" + u.PortClient.ClientId + "]", 0), ServiceIdent.Logger);
                        if (RemactConfigDefault.Instance.IsProcessIdUsed(u.PortClient.ProcessId))
                        {
                            m_UnusedClientCount++;
                            ServiceIdent.InputClientList[i] = null;// will never be used again, the client has been shutdown
                        }
                        m_ConnectedClientCount--;
                    }
                }

                if (u.IsConnected)
                {
                    nConnected++;
                }
                else if (RemactConfigDefault.Instance.IsProcessIdUsed(u.PortClient.ProcessId))
                {
                    nUnused++;
                }
            }
            m_millisPeriodicTask = millisCurrent;

            m_ConnectedClientCount = nConnected;
            m_UnusedClientCount = nUnused;
            return boChange;
        }

        #endregion
    }
}
