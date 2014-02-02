
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Threading;
using System.Collections.Generic;
using Remact.Net.Protocol;

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
    public ActorInput  ServiceIdent {get; private set;}
    
    /// <summary>
    /// The count of known clients of this service (connected or disconnected)
    /// </summary>
    public   int       ClientCount { get { return ServiceIdent.InputClientList.Count - m_UnusedClientCount; } }

    /// <summary>
    /// The count of connected clients of this service
    /// </summary>
    public   int       ConnectedClientCount { get { return m_ConnectedClientCount; } }

    /// <summary>
    /// May be used for tracing of connect/reconnect/disconnect operations.
    /// </summary>L
    public   string    LastAction;
    
    /// <summary>
    /// Default = false. When set to true: Disable catalog client, no service in this application will be published to the Remact.Catalog.
    /// </summary>
    public static bool DisableCatalogClient
    {
      get { return RemactCatalogClient.Instance ().DisableCatalogClient; }
      set { RemactCatalogClient.Instance ().DisableCatalogClient = value; }
    }

    /// <summary>
    /// True if any client has been connected or disconnected. Set to false by DoPeriodicTasks()
    /// </summary>
    internal bool                      HasConnectionStateChanged = true;

    /// <summary>
    /// Internally used by CatalogClient
    /// </summary>
    internal  bool                     IsServiceRegistered;
    
    /// <summary>
    /// Internally used for periodic message to Remact.CatalogService
    /// </summary>
    internal DateTime                  NextEnableMessage;

    private  int                       m_FirstClientId;          // offset, normally = 1
    private  int                       m_UnusedClientCount = 0;  // disconnected clients having RemactDefaults.IsProcessIdUsed
    private  int                       m_ConnectedClientCount = 0;
    private  int                       m_millisPeriodicTask = 0; // systemstart = 0
    private  bool                      m_boCurrentlyCalled;      // to check concurrent calls

    private int                        _tcpPort;
    private bool                       _publishToCatalog;
    private IActorInputConfiguration   _serviceConfig;
    private WebSocketPortManager       _networkPortManager;

    private static int                 ms_nSharedTcpPort;
    private static int                 ms_nSharedTcpPortCount;

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
                if (!IsOpen) OpenService ();
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
    /// <param name="serviceIdent">This ActorInput is linked to network.</param>
    /// <param name="tcpPort">The TCP port for the service or 0, when automatic port allocation may be used.</param>
    /// <param name="publishToCatalog">True(=default): The servicename will be published to the Remact.Catalog on localhost.</param>
    /// <param name="serviceConfig">Plugin your own service configuration instead of RemactDefaults.ServiceConfiguration.</param>
    internal RemactService(ActorInput serviceIdent, int tcpPort = 0, bool publishToCatalog = true,
                              IActorInputConfiguration serviceConfig = null )
        : this (serviceIdent, /*firstClient=*/1, /*maxClients=*/20)
    {
        _tcpPort = tcpPort;
        _publishToCatalog = publishToCatalog;
        _serviceConfig = serviceConfig;
        if( _serviceConfig == null )
        {
            _serviceConfig = RemactConfigDefault.Instance;
        }
    }// CTOR
    

    /// <summary>
    /// Create a RemactService object
    /// </summary>
    /// <param name="serviceIdent">a ActorInput having a unique name for the service.</param>
    /// <param name="firstClientId">client Id's start with this number, normally = 1.</param>
    /// <param name="maxClients">initial capacity of the User List.</param>
    private RemactService (ActorInput serviceIdent, int firstClientId, int maxClients)
    {
      ServiceIdent = serviceIdent;
      ServiceIdent.IsServiceName = true;
      ServiceIdent.InputClientList = new List<ActorOutput> (maxClients);
      if (firstClientId > 0) m_FirstClientId = firstClientId;
                        else m_FirstClientId = 1;
    }// CTOR1


    /// <summary>
    /// Create a RemactService object, used by AsyncRemact.Catalog (only).
    /// </summary>
    /// <param name="serviceName">a unique service name.</param>
    /// <param name="serviceUri">the Uri for the service.</param>
    /// <param name="firstClientId">client Id's start with this number, normally = 1.</param>
    /// <param name="maxClients">initial capacity of the User List.</param>
    internal RemactService(string serviceName, Uri serviceUri, int firstClientId, int maxClients)
    {
      ServiceIdent = new ActorInput (serviceName);
      ServiceIdent.Uri = serviceUri;
      ServiceIdent.IsServiceName = true;
      ServiceIdent.InputClientList = new List<ActorOutput> (maxClients);
      if (firstClientId > 0) m_FirstClientId = firstClientId;
                        else m_FirstClientId = 1;
    }// CTOR2


    /// <summary>
    /// <para>** IsMultithreaded==FALSE **  = default on ActorPort</para>
    /// <para>Create and open a Service running a threadsafe singleton service.</para>
    /// <para>These services must be very fast and may only access memory. No file- or database-access and no other synchronous calls are allowed.</para>
    /// <para>Calls to the message handler are made from the same thread (synchronization context) that is used now to open the service.</para>
    /// <para>An exception is thrown, when your opening thread has no message queue.</para>
    /// <para></para>
    /// <para>** IsMultithreaded==TRUE **</para>
    /// <para>Create and open a Service.</para>
    /// <para>These services may be relativly slow, when accessing files, databases or doing other synchronous calls.</para>
    /// <para>Calls to the message handler are made from different threads, several clients may run in parallel</para>
    /// <para>but only one thread at a time is accessing the client and user context.</para>
    /// <para></para>
    /// <para></para>
    /// <para>When there exists no [service name="ConcreteTypeOfServiceInstance"] entry in the App.config file,</para>
    /// <para>or the entry has no endpoint (apart from a possible "mex" endpoint),</para>
    /// <para>the RemactService creates a standard service URI containig the next free TCP port and the service name.</para>
    /// <para>E.g. "http://host:1234/Remact/ServiceName"</para>
    /// <para>It registeres the service with Remact.CatalogService, so clients can find the dynamically generated TCP port.</para>
    /// </summary>
    /// <returns>true if successfully open</returns>
    internal bool OpenService()
    {
        try
        {
            if (_networkPortManager != null) Disconnect();
/*
            // Do we have to add a dynamically generated endpoint ?
            if (m_ServiceHost.Description.Endpoints.Count == 0
            || (m_ServiceHost.Description.Endpoints.Count == 1 && m_ServiceHost.Description.Endpoints[0].Name.ToLower() == "mex"))
            {
                if (_tcpPort == 0)
                {
                    if (ms_nSharedTcpPort==0 || ms_nSharedTcpPortCount==0)
                    {
                        // Find the next free local TCP-port:
                        Socket     socket   = new Socket (AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        IPEndPoint endpoint = new IPEndPoint (0, 0);   // Local Address, dynamic port assignment
                        socket.Bind (endpoint);
                        endpoint = socket.LocalEndPoint as IPEndPoint; // a free port has been assigned by windows
            #if !MONO
                        ms_nSharedTcpPort = endpoint.Port; // a free dynamic assigned, local port
            #else
                        // Portsharing does not work for Mono, last checked on Mono 2.10.8.1
                        _tcpPort = endpoint.Port;
            #endif
                        socket.Close(); // socket.Shutdown is not allowed as we are not yet connected
                    }
            #if !MONO
                    m_nTcpPort = ms_nSharedTcpPort;
                    ms_nSharedTcpPortCount++;
            #endif
                }
*/
            // Set URI before the first request arrives (as in RemactService). 
                // The URI will be sent to Remact.Catalog for registration.
                Uri uri = new Uri ("ws://"
                    + ServiceIdent.HostName     // initialized with Dns.GetHostName()
                    +":"+_tcpPort
                    +"/"+RemactConfigDefault.WsNamespace+"/"+ServiceIdent.Name);// ServiceName, not the ServiceType

                // Open the ServiceHost to start listening for messages.
                // Add the dynamically created endpoint. And let the library user add binding and security credentials.
                // By default RemactDefaults.DoServiceConfiguration is called.
                _networkPortManager = _serviceConfig.DoServiceConfiguration(this, ref uri, /*isCatalog=*/false);
                ServiceIdent.Uri = uri;
            //}
            //else
            //{
            //    // Set configured URI so it can be sent to Remact.Catalog for registration.
            //    // TODO: ServiceIdent.Name is used as identification in Remact.Catalog - should it be changed now ???
            //    //       or should the Uri be changed / created from different fields ???
            //    UriBuilder uri = new UriBuilder (m_ServiceHost.Description.Endpoints[0].ListenUri);
            //    uri.Host = base.ServiceIdent.HostName; // initialized with Dns.GetHostName(), replaces "localhost"
            //    base.ServiceIdent.Uri = uri.Uri;
            //}
        
            if (_publishToCatalog)
            {
                // Start registering on Remact.Catalog
                RemactCatalogClient.Instance().AddService(this);
            }
        
            // The service can now be accessed, but must be registered.
            RaLog.Info("Remact", "Opened service " + ServiceIdent.Uri, ServiceIdent.Logger);
            return true;
        }
        catch (Exception ex)
        {
            RaLog.Exception( "could not open " + ServiceIdent.Name, ex, ServiceIdent.Logger );
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
        
            RemactCatalogClient.Instance().RemoveService (this); // send disable message to Remact.CatalogService

            if (ServiceIdent.Uri != null) RaLog.Info("Remact", "Closed service " + ServiceIdent.Uri, ServiceIdent.Logger);
                                     else RaLog.Info("Remact", "Closed service " + ServiceIdent.Name, ServiceIdent.Logger);
        }
        catch (Exception ex)
        {
            RaLog.Exception( "Svc: Error while closing the service", ex, ServiceIdent.Logger );
        }
    }// Disconnect


    /// <summary>
    /// <para>Abort all notification connections, do not send any messages.</para>
    /// <para>Should be called before closing the service host.</para>
    /// </summary>
    internal void AbortUserNotificationChannels()
    {
        foreach (ActorOutput clt in ServiceIdent.InputClientList)
        {
            if (clt.SvcUser != null) clt.SvcUser.AbortNotificationChannel();
        }
    }// AbortUserNotificationChannels


    #endregion
    //----------------------------------------------------------------------------------------------
    #region Client connect / disconnect


    // Internally called to create an ActorOutput as client stub.
    internal virtual RemactServiceUser AddNewSvcUser (ActorInfo receivedClientMsg, int index, RemactServiceUser svcUser)
    {
      if (index < 0) // add a new element
      {
        ServiceIdent.InputClientList.Add (null);
        index = ServiceIdent.InputClientList.Count-1;
      }

      if (svcUser == null)
      {
          svcUser = new RemactServiceUser(ServiceIdent); // svcUser not created, when connection has been opened
      }

      svcUser.UseDataFrom (receivedClientMsg, index + m_FirstClientId);
      ServiceIdent.InputClientList[index] = svcUser.ClientIdent;
      return svcUser;
    }


    /// <summary>
    /// Connect / Reconnect a client to this service
    /// </summary>
    /// <param name="client">ActorMessage message</param>
    /// <param name="req">the ActorMessage to be used for responses.</param>
    /// <param name="svcUser">Output the user object containing a "ClientIdent.UserContext" object for free application use</param>
    /// <returns>Service info as response</returns>
    private object ConnectPartner(ActorInfo client, ActorMessage req, ref RemactServiceUser svcUser)
    {
      if (req.ClientId != 0)
      {// Client war schon mal verbunden
        int i = req.ClientId - m_FirstClientId;
        if (i >= 0 && i < ServiceIdent.InputClientList.Count + 100)
        {
          // Nach dem Restart eines Service können sich Clients mit der alten Nummer anmelden
          while (ServiceIdent.InputClientList.Count < i) ServiceIdent.InputClientList.Add (null);

          svcUser = ServiceIdent.InputClientList[i].SvcUser;
          if (svcUser == null)
          {
              svcUser = AddNewSvcUser(client, i, svcUser);
              LastAction = "Reconnect after service restart";
          }
          else if (!client.IsEqualTo (svcUser.ClientIdent))
          {
              RaLog.Warning( req.SvcRcvId, svcUser.ClientIdent.ToString( "ClientId already used", 0 ), ServiceIdent.Logger );
              req.ClientId = 0; // eine neue ID vergeben, kann passieren, wenn Service, aber nicht alle Clients durchgestartet werden
              m_ConnectedClientCount -= 2; // wird sofort 2 mal inkrementiert
          }
          else if (svcUser.IsConnected)
          {
              LastAction = "Reconnect, no disconnect";
              RaLog.Warning( req.SvcRcvId, svcUser.ClientIdent.ToString( LastAction, 0 ), ServiceIdent.Logger );
              //TODO
              svcUser.UseDataFrom(client, req.ClientId);
              m_ConnectedClientCount--; // wird sofort wieder inkrementiert
          }
          else if (svcUser.IsFaulted)
          {
              LastAction = "Reconnect after network failure";
              RaLog.Warning( req.SvcRcvId, svcUser.ClientIdent.ToString( LastAction, 0 ), ServiceIdent.Logger );
              //TODO
              svcUser.UseDataFrom(client, req.ClientId);
              if (RemactConfigDefault.Instance.IsProcessIdUsed (svcUser.ClientIdent.ProcessId)) m_UnusedClientCount--;
          }
          else
          {
              //TODO
              svcUser.UseDataFrom(client, req.ClientId);
              LastAction = "Reconnect after client disconnect";
              if (RemactConfigDefault.Instance.IsProcessIdUsed (svcUser.ClientIdent.ProcessId)) m_UnusedClientCount--;
          }
          m_ConnectedClientCount++;
        }
        else
        {
          ErrorMessage rsp = new ErrorMessage (ErrorMessage.Code.ClientIdNotFoundOnService, "Service cannot find client " + req.ClientId + " to connect");
          RaLog.Error( req.SvcRcvId, rsp.Message, ServiceIdent.Logger );
          LastAction = "ClientId mismatch while connecting";
          return rsp;
        }
      }

      if (req.ClientId == 0)
      {// Client wurde neu gestartet
        int found = ServiceIdent.InputClientList.FindIndex (c => client.IsEqualTo (c));
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

            if( svcUser.IsConnected )
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
      //svcUser.OpenNotificationChannel();
      HasConnectionStateChanged = true;
      
      // reply ServiceIdent
      ActorInfo response = new ActorInfo (ServiceIdent, ActorInfo.Use.ServiceConnectResponse);
      response.ClientId = svcUser.ClientIdent.OutputClientId;
      req.Source   = svcUser.ClientIdent;
      return response;
    }// Connect


    /// <summary>
    /// Mark a client as (currently) disconnected
    /// </summary>
    /// <param name="client">ActorMessage message</param>
    /// <param name="req">the ActorMessage to be used for responses.</param>
    /// <param name="svcUser">Output the user object containing a "ClientIdent.UserContext" object for free application use</param>
    /// <returns>Service info as response</returns>
    private object DisconnectPartner(ActorInfo client, ActorMessage req, ref RemactServiceUser svcUser)
    {
      int i = req.ClientId - m_FirstClientId;
      if (i >= 0 && i < ServiceIdent.InputClientList.Count)
      {
        svcUser = ServiceIdent.InputClientList[i].SvcUser;
        svcUser.ChannelTestTimer = 0;
        //req.CurrentSvcUser = svcUser;
        req.Source = svcUser.ClientIdent;
        HasConnectionStateChanged = true;
        if (client.IsEqualTo (svcUser.ClientIdent))
        {
          svcUser.Disconnect();
          LastAction = "Disconnect";
          if (RemactConfigDefault.Instance.IsProcessIdUsed (svcUser.ClientIdent.ProcessId))
          {
            m_UnusedClientCount++;
            ServiceIdent.InputClientList[i] = null; // will never be used again, the client has been shutdown
          }
          m_ConnectedClientCount--;
        }
        else
        {
          LastAction = "ClientId mismatch while disconnecting";
          RaLog.Error( req.SvcRcvId, LastAction + ": " + client.Uri, ServiceIdent.Logger );
        }
      }
      else
      {
        svcUser = null;
        RaLog.Error( req.SvcRcvId, "Cannot disconnect client" + req.ClientId, ServiceIdent.Logger );
        LastAction = "Disconnect unknown client";
      }

      // Note: This response will not really be sent to the client. The connection is already disconnected.
      //       See ActorPort.PostInput
      ActorInfo response = new ActorInfo (ServiceIdent, ActorInfo.Use.ServiceDisconnectResponse);
      return response;
    }// Disconnect


    /// <summary>
    /// Set client info into the message, call it once for each request to check the connection.
    /// </summary>
    /// <param name="req">ActorMessage message</param>
    /// <param name="svcUser">Output the user object containing a "ClientIdent.UserContext" object for free application use.</param>
    /// <returns>True, when the client has been found. False, when no client has been found and an error message must be generated.</returns>
    internal bool FindPartnerAndCheck (ActorMessage req, ref RemactServiceUser svcUser)
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
      req.Source = svcUser.ClientIdent;
    
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
            LastAction = "Client '" + svcUser.ClientIdent.Uri.ToString() + "' connected";
            if (ServiceIdent.TraceConnect)
            {
                RaLog.Info(req.SvcRcvId, String.Format("{0} to Remact service './{0}'", LastAction, ServiceIdent.Name), ServiceIdent.Logger);
            }
        }

        if (RemactConfigDefault.Instance.IsProcessIdUsed (svcUser.ClientIdent.ProcessId)) 
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
    /// <param name="req">The ActorMessage contains the request. It is used for the response also.</param>
    /// <param name="svcUser">
    /// input: null --> create a new RemactServiceUser as protocol independant client proxy
    ///        not null --> use this RemactServiceUser as protocol independant client proxy
    /// output: the user object contains the "ClientIdent.SenderContext" object for free application use
    /// </param>
    /// <returns><para> null when the response has to be generated by the application.</para>
    ///          <para>!null if the response already has been generated by this class.</para></returns>
    internal object CheckBasicResponse(ActorMessage req, ref RemactServiceUser svcUser)
    {
        if (m_boCurrentlyCalled)
        {
            RaLog.Error ("RemactSvc", "called by multiple threads", ServiceIdent.Logger);
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
               switch (cltReq.Usage)
               {
                   case ActorInfo.Use.ClientConnectRequest:    response = ConnectPartner(cltReq, req, ref svcUser); break;
                   case ActorInfo.Use.ClientDisconnectRequest: response = DisconnectPartner(cltReq, req, ref svcUser); break;
                   default: break;// continue below
               }
           }
        }
      
        if (response == null)
        {
            // no response generated yet (no ActorInfo-message or unknown Usage)
            if (FindPartnerAndCheck (req, ref svcUser))
            {
                LastAction = "ActorMessage";// response must be generated by service-application, request.ClientIdent has been set
            }
            else
            {
                response = new ErrorMessage (ErrorMessage.Code.ClientIdNotFoundOnService, "Service cannot find client " + req.ClientId);
                RaLog.Error( req.SvcRcvId, (response as ErrorMessage).Message, ServiceIdent.Logger );
                LastAction = "ActorMessage from unknown client";
            }
        }

        m_boCurrentlyCalled = false;
        return response;
    }// CheckBasicResponse

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

      for (int i=0; i<ServiceIdent.InputClientList.Count; i++)
      {
        RemactServiceUser u = ServiceIdent.InputClientList[i].SvcUser;
        if (u == null) continue;

        if (u.TestChannel (deltaT))
        { 
          boChange = true;
          if (u.IsFaulted)
          {
              RaLog.Warning("Svc=" + ServiceIdent.Name, u.ClientIdent.ToString("Timeout=" + u.ClientIdent.TimeoutSeconds 
                  + " sec. no message from clt[" + u.ClientIdent.OutputClientId + "]", 0), ServiceIdent.Logger);
            if (RemactConfigDefault.Instance.IsProcessIdUsed(u.ClientIdent.ProcessId))
            {
              m_UnusedClientCount++;
              ServiceIdent.InputClientList[i] = null;// will never be used again, the client has been shutdown
            }
            m_ConnectedClientCount--;
            //u.TraceState("");
          }
        }
        
        if (u.IsConnected) {
          nConnected++;
        } else if (RemactConfigDefault.Instance.IsProcessIdUsed(u.ClientIdent.ProcessId)) {
          nUnused++;
        } 
      }
      m_millisPeriodicTask = millisCurrent;
      
      m_ConnectedClientCount = nConnected;
      m_UnusedClientCount    = nUnused;
      return boChange;
    }// DoPeriodicTasks
      
    
    #endregion
  }
}
