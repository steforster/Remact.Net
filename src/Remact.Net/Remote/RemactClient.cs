
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.ServiceModel;
using System.Net;            // Dns
using System.Threading;
using System.ComponentModel; // AsyncOperation
using Newtonsoft.Json.Linq;
using Remact.Net.Protocol;
using Remact.Net.Protocol.Wamp;

namespace Remact.Net.Remote
{
  /// <summary>
  /// <para>Client class to handle a remote service.</para>
  /// <para>Requests are sent asynchronously.</para>
  /// <para>Responses are asynchroniously received on the same thread as the request was sent</para>
  /// <para>(only when sent from a thread with message queue (as WinForms), but not when sent from a threadpool-thread).</para>
  /// </summary>
  internal class RemactClient : IRemoteActor, IRemactProtocolDriverCallbacks
  {
    //----------------------------------------------------------------------------------------------
    #region Properties
    /// <summary>
    /// Detailed information about this client. May be a ActorOutput&lt;TOC&gt; object containing application specific "OutputContext".
    /// </summary>
    public    ActorOutput       ClientIdent            {get; private set;}

    /// <summary>
    /// The last request id received in a response from the connected service.
    /// It is used to calculate outstandig responses.
    /// </summary>
    protected int               LastRequestIdReceived;

    /// <summary>
    /// Detailed information about the connected service. Contains a "UserContext" object for free use by the client application.
    /// </summary>
    public    ActorInput        ServiceIdent           {get; private set;}

    /// <summary>
    /// The lower level client.
    /// </summary>
    internal IRemactProtocolDriverService m_protocolClient; // internal protected is not allowed ?!
    
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
    protected Uri  m_RequestedServiceUri;

    /// <summary>
    /// The plugin provided by the library user or RemactDefaults.ClientConfiguration
    /// </summary>
    protected IActorOutputConfiguration m_ClientConfig;

    /// <summary>
    /// True, when connecting or connected to catalog service, not to the original service.
    /// </summary>
    protected bool m_boTemporaryCatalogConn;

    /// <summary>
    /// True, when connecting and not yet connected.
    /// </summary>
    protected bool m_boConnecting;

    /// <summary>
    /// The number of addresses tried to connect already.
    /// </summary>
    protected int  m_addressesTried;

    /// <summary>
    /// The tried address. 0 = hostname, 1 = first IP address, AddressList.Count = last IP address.
    /// </summary>
    protected int  m_addressNumber;

    /// <summary>
    /// True, when first response from original service received.
    /// </summary>
    protected bool m_boFirstResponseReceived;

    /// <summary>
    /// The default message handler to use, when connected to the target service.
    /// </summary>
    protected MessageHandler m_DefaultInputHandlerForApplication;

    /// <summary>
    /// The hostname of the catalog service.
    /// </summary>
    protected string m_CatalogHostToLookup;

    /// <summary>
    /// The TCP port of the catalog.
    /// </summary>
    protected int m_RemactCatalogPort;
    
    /// <summary>
    /// True, when traces of the connect process to the target service should be written.
    /// </summary>
    protected bool m_TraceConnectBefore;

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Constructors, linking and connecting

    /// <summary>
    /// Create the proxy for a remote service.
    /// </summary>
    /// <param name="clientIdent">Link this ActorOutput to the remote service.</param>
    internal RemactClient (ActorOutput clientIdent)
    {
      m_DefaultInputHandlerForApplication = clientIdent.DefaultInputHandler;
      ClientIdent = clientIdent;
      ServiceIdent = new ActorInput(); // not yet defined
      ServiceIdent.IsServiceName = true;
      ServiceIdent.PassResponsesTo (ClientIdent); // ServiceIdent.PostInput will send to our client
    }


    /// <summary>
    /// <para>Connect this Client to a service identified by the serviceName parameter.</para>
    /// <para>The correct serviceHost and TCP port will be looked up at a Remact.CatalogService identified by parameter catalogHost.</para>
    /// </summary>
    /// <param name="catalogHost">The HostName, where the Remact.CatalogService is running. This may be the 'localhost'.
    ///    <para>By default TCP port 40000 is used for Remact.CatalogService, but you can specify another TCP port for the catalog eg. "host:3333"</para></param>
    /// <param name="serviceName">A unique name of the service. This service may run on any host that has been registered at the Remact.CatalogService.</param>
    /// <param name="clientConfig">Plugin your own client configuration instead of RemactDefaults.ClientConfiguration.</param>
    internal void LinkToService(string catalogHost, string serviceName, IActorOutputConfiguration clientConfig = null)
    {
        m_RemactCatalogPort = RemactConfigDefault.Instance.CatalogPort;
        m_CatalogHostToLookup = catalogHost;
        try
        {
            int i = catalogHost.LastIndexOf(':');
            if (i > 0)
            {
                m_CatalogHostToLookup = catalogHost.Substring(0, i);
                m_RemactCatalogPort = Convert.ToInt32(catalogHost.Substring(i + 1));
            }
        }
        catch
        {
        }
        m_CatalogHostToLookup = NormalizeHostName(m_CatalogHostToLookup);
        m_ServiceNameToLookup = serviceName;
        ServiceIdent.PrepareServiceName(m_CatalogHostToLookup, m_ServiceNameToLookup);
    }// LinkToService (URI)


    /// <summary>
    /// Link this ClientIdent to a remote service. No lookup at Remact.Catalog is needed as we know the TCP portnumber.
    /// </summary>
    /// <param name="websocketUri">The uri of the remote service.</param>
    /// <param name="clientConfig">Plugin your own client configuration instead of RemactDefaults.ClientConfiguration.</param>
    internal void LinkToService(Uri websocketUri, IActorOutputConfiguration clientConfig = null)
    {
      // this link method does not read the App.config file (it is running on mono also).
      if (!IsDisconnected) Disconnect ();
      m_CatalogHostToLookup = null;
      websocketUri = NormalizeHostName(websocketUri);
      m_RequestedServiceUri = websocketUri;
    }

    private Uri NormalizeHostName( Uri uri )
    {
      if( uri.IsLoopback )
      {
        UriBuilder b = new UriBuilder (uri);
        b.Host = Dns.GetHostName(); // concrete name of localhost -- needed for Mono!
        return b.Uri;
      }
      return uri;
    }

    private string NormalizeHostName( string host )
    {
      if( host.ToLower() == "localhost" )
      {
        return Dns.GetHostName(); // concrete name of localhost -- needed for Mono!
      }
      return host;
    }

    /// <summary>
    /// Accept the binding configuration provided when linking the ActorOutput or set in RemactDefaults.ClientConfiguration.
    /// </summary>
    /// <param name="serviceUri">The URI to connect to. Parts of the URI may be changed depending on the binding configuration.</param>
    /// <param name="forCatalog">True, when the connection is to a Remact.Catalog.</param>
    protected internal void DoClientConfiguration(ref Uri serviceUri, bool forCatalog)
    {
        if( m_ClientConfig == null )
        {
            m_ClientConfig = RemactConfigDefault.Instance;
        }
        m_ClientConfig.DoClientConfiguration(m_protocolClient, ref serviceUri, forCatalog);
    }


    /// <summary>
    /// Connect or reconnect output to the previously linked partner.
    /// </summary>
    /// <returns>false, when the connection may not be started.</returns>
    public virtual bool TryConnect()
    {
        if (!(IsDisconnected || IsFaulted)) return true;  // already connected or connecting

        try
        {
            if (m_CatalogHostToLookup != null)
            {
                // connect to catalog first
                var uri = new Uri("http://" + m_CatalogHostToLookup + ':' + m_RemactCatalogPort + "/" + RemactConfigDefault.WsNamespace + "/" + RemactConfigDefault.Instance.CatalogServiceName);
                TryConnectVia(uri, OnResponseFromCatalogService, toCatalog: true);
                return true;
            }
            else
            {
                // do not connect to catalog
                LinkToService(m_RequestedServiceUri);
                m_protocolClient = new WampClient(m_RequestedServiceUri);
                // Let now the library user change binding and security credentials.
                // By default RemactDefaults.OnClientConfiguration is called.
                var websocketUri = m_RequestedServiceUri;
                DoClientConfiguration(ref websocketUri, forCatalog:false);
                ServiceIdent.PrepareServiceName(websocketUri);
                ClientIdent.PickupSynchronizationContext();
                OpenConnectionToService();
                return true; // Connecting now
            }
        }
        catch (Exception ex)
        {
            RaLog.Exception("Cannot open Remact connection(3)", ex, ClientIdent.Logger);
            m_boTimeout = true; // enter 'faulted' state when eg. configuration is incorrect
            return false;
        }
    }// TryConnect


    /// <summary>
    /// <para>Connect this client to a catalog or to the requested service, without configuration from App.config file.</para>
    /// </summary>
    /// <param name="websocketUri">fully specified URI of the service</param>
    /// <param name="viaResponseHandler">The callback method when a response arrives</param>
    /// <param name="toCatalog">True, when the connection to a catalog is made.</param>
    internal virtual void TryConnectVia(Uri websocketUri, MessageHandler viaResponseHandler, bool toCatalog)
    {
      if (!IsDisconnected) Disconnect ();
      try
      {
          if (toCatalog)
          {
              m_TraceConnectBefore = ClientIdent.TraceConnect;
              ClientIdent.TraceConnect = ClientIdent.TraceSend;
              m_boTemporaryCatalogConn = true;
              ServiceIdent.Uri = null; // yet unknown
          }
          else if( m_boTemporaryCatalogConn )
          {
              ClientIdent.TraceConnect = m_TraceConnectBefore;
              m_boTemporaryCatalogConn = false;
          }

          ClientIdent.DefaultInputHandler = viaResponseHandler;
          websocketUri = NormalizeHostName(websocketUri);
          m_RequestedServiceUri = websocketUri;
          m_protocolClient = new WampClient(websocketUri);
          // Let now the library user change binding and security credentials.
          // By default RemactDefaults.OnClientConfiguration is called.
          DoClientConfiguration(ref websocketUri, toCatalog); // TODO: changes in uri are not reflected in a new m_protocolClient
          OpenConnectionToService();
      }
      catch (Exception ex)
      {
          RaLog.Exception("Cannot open Remact connection(2)", ex, ClientIdent.Logger);
          m_boTimeout = true; // enter 'faulted' state when eg. configuration is incorrect
      }
    }// TryConnectVia


    /// <summary>
    /// A client is connected after the ServiceConnectResponse has been received.
    /// </summary>
    public bool IsConnected    { get { return m_protocolClient != null 
                                           && m_boFirstResponseReceived
                                           && !m_boTimeout
                                           && m_protocolClient.PortState == PortState.Ok; }}

    /// <summary>
    /// A client is disconnected after construction, after a call to Disconnect() or AbortCommunication()
    /// </summary>
    public bool IsDisconnected { get { return m_protocolClient == null 
                                       ||   (!m_boConnecting && !m_boFirstResponseReceived && !m_boTimeout);}}

    /// <summary>
    /// A client is in Fault state when a connection cannot be kept open or a timeout has passed.
    /// </summary>
    public bool IsFaulted      { get { return m_boTimeout
                                       ||    (m_protocolClient != null
                                           && m_protocolClient.PortState == PortState.Faulted);}}

    /// <summary>
    /// Returns the number of requests that have not received a response by the service.
    /// </summary>
    public int  OutstandingResponsesCount
    { get {
        if (m_protocolClient != null){
           return m_protocolClient.OutstandingResponsesCount;
        }else{ return 0;}
    }}


    /// <summary>
    /// Same as Disconnect.
    /// </summary>
    public void Dispose()
    {
      Disconnect();
    }


    //--------------------
    /// <summary>
    /// <para>Send Disconnect messages to service if possible . Go from any state to Disconnected state.</para>
    /// <para>Makes it possible to restart the client with TryConnect.</para>
    /// </summary>
    public void Disconnect()
    {
        try
        {
            if (IsConnected)
            {
                try
                {
                    RemactCatalogClient.Instance().RemoveClient(this);
                    SendDisconnectMessage();
                }
                catch
                {
                }
            }
        
            if (m_protocolClient != null) 
            {
                m_protocolClient.Dispose();
                RemactCatalogClient.Instance().RemoveClient (this);
                //TraceState("Abortd");
            }
        }
        catch (Exception ex)
        {
            RaLog.Exception("Cannot abort Remact connection", ex, ClientIdent.Logger);
        }
      
        m_protocolClient = null;
        m_boConnecting = false;
        m_boFirstResponseReceived = false;
        m_boTimeout = false;
        m_boTemporaryCatalogConn  = false;
        ServiceIdent.m_Connected = false; // internal, from ServiceIdent to ClientIdent
        ClientIdent.m_Connected = false; // internal, from ActorOutput to RemactClient

    }// Disconnect

    
    /// <summary>
    /// internal: Overloaded by ClientAsyncAwait
    /// </summary>
    internal protected virtual void SendDisconnectMessage()
    {
        bool traceSend = ClientIdent.TraceSend;
        ClientIdent.TraceSend = ClientIdent.TraceConnect;
        SendOut (new ActorInfo (ClientIdent, ActorInfo.Use.ClientDisconnectRequest));
        ClientIdent.TraceSend = traceSend;
        Thread.Sleep(30);
    }


    /// <summary>
    /// <para>Abort all messages, go from any state to Disconnected state.</para>
    /// <para>Makes it possible to restart the client with TryConnect.</para>
    /// </summary>
    public void AbortCommunication()
    {
      m_boTimeout = true;
      Disconnect();
    }// AbortCommunication


    #endregion
    //----------------------------------------------------------------------------------------------
    #region Message handling

    /// <summary>
    /// Trace internal state of this client
    /// </summary>
    /// <param name="mark">6 char mark: Opened, Abortd, ...</param>
    public void TraceState (string mark)
    {
      if (m_protocolClient != null)
      {
        RaLog.Info("RemactClt", "["+mark.PadRight(6)+"] "+ ClientIdent.Name+"["+ClientIdent.OutputClientId+"]"
                    +", PortState=" + m_protocolClient.PortState.ToString()
                    , ClientIdent.Logger );
      }
    }// TraceState
    

    /// <summary>
    /// Connect this Client to the prepared m_protocolClient
    /// </summary>
    //  Running on the user thread
    private void OpenConnectionToService()
    {
        ClientIdent.OutputClientId = 0;
        ClientIdent.LastRequestIdSent = 9;
        LastRequestIdReceived = 9;
        m_boFirstResponseReceived = false;
        m_boTimeout = false;
        m_boConnecting = true;
        if (ServiceIdent.Uri == null) ServiceIdent.PrepareServiceName(m_protocolClient.ServiceUri);
        ServiceIdent.IsMultithreaded = ClientIdent.IsMultithreaded;
        ServiceIdent.TryConnect(); // internal, from ServiceIdent to ClientIdent

        ClientIdent.PickupSynchronizationContext();
        ClientIdent.m_Connected = true; // internal, from ActorOutput to RemactClient
        ActorMessage msg = new ActorMessage(ClientIdent, ClientIdent.OutputClientId, ClientIdent.NextRequestId, 
                                             ServiceIdent, null, null);
        m_protocolClient.OpenAsync(msg, this); 
        // Callback to OnOpenCompleted when channel has been opened locally (no TCP connection opened on mono).
    }// OpenConnectionToService


    // Eventhandler, running on user thread, sent from m_protocolClient.
    void IRemactProtocolDriverCallbacks.OnOpenCompleted(object obj)
    {
        ActorMessage request = obj as ActorMessage;
        request.DestinationLambda = null;
        request.SourceLambda = null;
      
        try
        {
            if (request.Payload != null)
            {   // error while opening
                request.Type = ActorMessageType.Error;
                request.Source = ServiceIdent;
                if( ServiceIdent.AddressList != null )
                {
                    ClientIdent.DefaultInputHandler(request); // pass the negative feedback from real service to the handler in this class
                }
                else
                {
                    EndOfConnectionTries(request); // enter 'faulted' state when eg. catalog service not running
                }
            }
            else
            {
                string serviceAddr = GetSetServiceAddress();
                request.Payload = new ActorInfo(ClientIdent, ActorInfo.Use.ClientConnectRequest);
                request.PayloadType = typeof(ActorInfo).FullName;
  
                if (ClientIdent.TraceConnect) {
                    if (m_boTemporaryCatalogConn) RaLog.Info(request.CltSndId, string.Concat("Temporary connecting .....: '", serviceAddr, "'"), ClientIdent.Logger);
                    else RaLog.Info(request.CltSndId, string.Concat("Connecting svc: '", serviceAddr, "'"), ClientIdent.Logger);
                }

                // send first connection request on user thread --> response will be received on this thread also
                m_protocolClient.MessageFromClient(request);
            }
        }
        catch (Exception ex)
        {
            request.Type = ActorMessageType.Error;
            request.Source = ServiceIdent;
            request.Payload = new ErrorMessage(ErrorMessage.Code.CouldNotStartConnect, ex);
            EndOfConnectionTries(request); // enter 'faulted' state when eg. configuration is incorrect
        }
    }// OnOpenCompleted


    /// <summary>
    /// Called before opening a connection. Prepares endpointaddress for tracing.
    /// </summary>
    /// <returns>string representation of the endpoint address.</returns>
    protected string GetSetServiceAddress()
    {
#if !MONO
        // Anonymous Uri: http://schemas.microsoft.com/2005/12/ServiceModel/Addressing/Anonymous
        EndpointAddress address = m_ServiceReference.InnerChannel.LocalAddress;
        if (address.IsAnonymous)
        { // basicHttpBinding
          //  ClientIdent.Uri = new Uri ("http://"+ClientIdent.HostName+"/"+RemactConfig.WsNamespace
          //                              +string.Format ("/{0}/{1}", ClientIdent.AppIdentification, ClientIdent.Name));
        }
        else
        { // wsDualHttpBinding
            ClientIdent.Uri = address.Uri;
        }
        ServiceIdent.Uri = m_ServiceReference.Endpoint.ListenUri;
        return m_ServiceReference.Endpoint.ListenUri.ToString();
#else
        // mono:
        // ClientIdent.Uri = new Uri ("http://"+ClientIdent.HostName+"/"+RemactDefaults.WsNamespace
        //                              +string.Format ("/{0}/{1}", ClientIdent.AppIdentification, ClientIdent.Name));
        ServiceIdent.Uri = m_protocolClient.ServiceUri;
        return m_protocolClient.ServiceUri.ToString();
#endif
    }

    Uri IRemactProtocolDriverCallbacks.ClientUri { get { return ClientIdent.Uri; } }

    // sent from m_protocolClient
    void IRemactProtocolDriverCallbacks.MessageFromService(ActorMessage message)
    {
        if (ClientIdent.IsMultithreaded || ClientIdent.SyncContext == null)
        {
            throw new NotImplementedException("must sync to user thread");
        }
        else
        {
            message.Source = ServiceIdent;
            message.Destination = ClientIdent;
            message.ClientId = ClientIdent.OutputClientId;
            ClientIdent.SyncContext.Post(OnIncomingMessageOnActorThread, message);
        }
    }

    private void OnIncomingMessageOnActorThread(object obj)
    {
        try
        {
            var message = (ActorMessage)obj;
            // TODO m_boTimeout    = x.timeout;

            if (!m_boTimeout)
            {
                var m = message.Payload as IExtensibleActorMessage;
                if (!ClientIdent.IsMultithreaded)
                {
                    if (m != null) m.BoundSyncContext = SynchronizationContext.Current;
                }
                if (m != null) m.IsSent = true;

                if (message.IsResponse)
                {
                    LastRequestIdReceived = message.RequestId;
                    if (message.PayloadType == typeof(ActorInfo).FullName)
                    {
                        ActorInfo actorInfo;
                        if (message.TryConvertPayload(out actorInfo)
                         && HandleActorInfo(message, actorInfo))
                        {
                            return;
                        }
                    }
                }
            }

            ClientIdent.DispatchMessage(message);
        }
        catch (Exception ex)
        {
            RaLog.Exception( "Message for " + ClientIdent.Name + " cannot be handled by application", ex, ClientIdent.Logger );
        }
    }// OnIncomingMessageOnActorThread


    /// <summary>
    /// This function is normally only used internally by OnRequestCompleted. It checks whether the response has to be handled by application code.
    /// </summary>
    /// <param name="result">received response.</param>
    /// <param name="rsp">response is of type ActorInfo.</param>
    /// <returns>True if response has been handled internally; False, when response must be handled by application</returns>
    protected bool HandleActorInfo(ActorMessage result, ActorInfo rsp)
    {
        if (rsp.Usage == ActorInfo.Use.ServiceConnectResponse)
        { // First message received from Service
            rsp.Uri = ServiceIdent.Uri; // keep the Uri used to request the message (maybe IP address instead of hostname used)
            ServiceIdent.UseDataFrom (rsp);
            ClientIdent.OutputClientId = rsp.ClientId; // defined by server
            result.ClientId = rsp.ClientId;
            OnConnectMessage (result);
        }
        else if (rsp.Usage == ActorInfo.Use.ServiceDisconnectResponse)
        {
            RaLog.Info( result.CltRcvId, rsp.ToString(), ClientIdent.Logger ); // this is unexpected, response is swallowed by service
            return true;
        }
        else if (rsp.Usage == ActorInfo.Use.ServiceAddressResponse
              || rsp.Usage == ActorInfo.Use.ServiceEnableResponse
              || rsp.Usage == ActorInfo.Use.ServiceDisableResponse)
        {
          // service address management
        }
        else
        {
            RaLog.Error( result.CltRcvId, "Unknown use of " + rsp.ToString(), ClientIdent.Logger );
        }

        return false; // Message must be handled by application
    }


    // Implements the connect message handling for RemactClientAsync.
    // 1. call from Remact.CatalogService
    // 2. call from looked up service
    // the same message will be sent to OnResponseFromCatalogService or to application later on.
    internal void OnConnectMessage(ActorMessage id)
    {
        if (m_CatalogHostToLookup == null || ServiceIdent.Name == m_ServiceNameToLookup)
        {
            RemactCatalogClient.Instance ().AddClient (this);
            m_boFirstResponseReceived = true; // IsConnected --> true !
            m_boConnecting = false;
            if( ClientIdent.TraceConnect ) RaLog.Info( id.CltRcvId, ServiceIdent.ToString( "Connected  svc", 0 ), ClientIdent.Logger );
            //TraceState("Opened");
        }
    }


    // Response callback from Remact.CatalogService
    private void OnResponseFromCatalogService(ActorMessage rsp)
    {
        ActorInfo svcRsp = rsp.Payload as ActorInfo;
        if (svcRsp != null && svcRsp.Usage == ActorInfo.Use.ServiceConnectResponse)
        {
            if (ClientIdent.TraceSend) RaLog.Info(rsp.CltRcvId, "Temporary connected catalog: '" + svcRsp.Name + "' on '" + svcRsp.HostName + "'", ClientIdent.Logger);
            ActorPort lookup = new ActorPort();
            lookup.HostName = m_CatalogHostToLookup;
            lookup.Name = m_ServiceNameToLookup;
            lookup.IsServiceName = true;
            ActorInfo req = new ActorInfo(lookup, ActorInfo.Use.ServiceAddressRequest);
            SendOut(req); // lookup the service URI (especially the TCP port)
        }
        else if (svcRsp != null && svcRsp.Usage == ActorInfo.Use.ServiceAddressResponse)
        {
            ServiceIdent.UseDataFrom( svcRsp );
            if( ClientIdent.TraceSend )
            {
                string s = string.Empty;
                if( svcRsp.AddressList != null )
                {
                    string delimiter = ", IP-adresses = ";
                    foreach( var adr in svcRsp.AddressList )
                    {
                        s = string.Concat( s, delimiter, adr.ToString() );
                        delimiter = ", ";
                    }
                }
                RaLog.Info( rsp.CltRcvId, "ServiceAddressResponse: " + svcRsp.Uri + s, ClientIdent.Logger );
            }
            m_addressesTried = 0;
            OnConnectionResponseFromService( null ); // try first address
        }
        else
        {
            ErrorMessage err = rsp.Payload as ErrorMessage;
            if (err != null)
            {
                //RaLog.Warning (rsp.CltRcvId, "Catalog "+rsp.ToString());
                if (err.Error == ErrorMessage.Code.ServiceNotRunning)
                {
                    err.Error = ErrorMessage.Code.CatalogServiceNotRunning;
                }
            }
            else
            {
                RaLog.Error( rsp.CltRcvId, "Receiving unexpected response from Remact.CatalogService: " + rsp.ToString(), ClientIdent.Logger );
                rsp.Payload = new ErrorMessage(ErrorMessage.Code.CouldNotConnectCatalog,
                                                    "Unexpected response from Remact.CatalogService");
            }
            EndOfConnectionTries(rsp); // failed at catalog
        }
    }// OnResponseFromCatalogService


    // Response callback from real service
    private void OnConnectionResponseFromService( ActorMessage rsp )
    {
        if( m_boFirstResponseReceived )
        {
            EndOfConnectionTries( rsp ); // successful !
            return;
        }

        if( rsp != null )
        {
            m_addressNumber++; // connection failed, next time try next address. 
            var err = rsp.Payload as ErrorMessage;
            if( m_addressesTried > ServiceIdent.AddressList.Count // all addresses tried
             || err == null       // wrong response
             || !m_boConnecting ) // wrong state
            {
                EndOfConnectionTries( rsp ); // failed
                return;
            }
            m_addressesTried++;
        }

        UriBuilder b = new UriBuilder( ServiceIdent.Uri );
        if( m_addressNumber <= 0 || m_addressNumber > ServiceIdent.AddressList.Count )
        {
            m_addressNumber = 0; // the hostname
            b.Host = ServiceIdent.HostName;
        }
        else
        {
            b.Host = ServiceIdent.AddressList[m_addressNumber - 1].ToString(); // an IP address
        }
        b.Host = b.Uri.DnsSafeHost;
        TryConnectVia(b.Uri, OnConnectionResponseFromService, toCatalog: false);
    }


    private void EndOfConnectionTries( ActorMessage rsp )
    {
        m_boTimeout = !m_boFirstResponseReceived; // Fault state when not correct response in OnConnectMessage
        if( m_boTemporaryCatalogConn )
        {
            ClientIdent.TraceConnect = m_TraceConnectBefore;
        }

        ClientIdent.DefaultInputHandler = m_DefaultInputHandlerForApplication;
        try
        {
            ClientIdent.DefaultInputHandler(rsp); // pass the negative or positive feedback from catalog or real service to the application
        }
        catch( Exception ex )
        {
            RaLog.Exception( "Connect message to " + ClientIdent.Name + " cannot be handled by application", ex, ClientIdent.Logger );
        }

        if( m_boTimeout && m_boTemporaryCatalogConn )
        {
          ServiceIdent.PrepareServiceName( m_CatalogHostToLookup, m_ServiceNameToLookup ); // prepare for next connect try
        }
        m_boTemporaryCatalogConn = false;
    }


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
        if (IsConnected)    return PortState.Ok;
        if (IsDisconnected) return PortState.Disconnected;
        if (IsFaulted)      return PortState.Faulted;
        return PortState.Connecting;
      }
      set
      {
        if (value == PortState.Connecting || value == PortState.Ok)
        {
          if (ClientIdent.IsMultithreaded || ClientIdent.SyncContext != null)
          {
            TryConnect();
          }
          else
          {
            throw new Exception("Remact: TryConnect of '"+ClientIdent.Name+"' has not been called to pick up the synchronization context.");
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

    private void OpenConnectionToService(object dummy)
    {
        OpenConnectionToService(); // 2. try on the right sync context
    }

    /// <summary>
    /// Post a request to the input of the remote partner. It will be sent over the network.
    /// Called from ClientIdent, when SendOut a message to remote partner.
    /// </summary>
    /// <param name="request">A <see cref="ActorMessage"/></param>
    public virtual void PostInput (ActorMessage request)
    {
        ErrorMessage err = null;
        if (!IsFaulted && ClientIdent.OutputClientId > 0) // Send() may be used during connection buildup as well
        {
            try
            {
                if (ClientIdent.TraceSend) RaLog.Info(request.CltSndId, request.ToString(), ClientIdent.Logger);
                m_protocolClient.MessageFromClient(request);
            }
            catch (Exception ex)
            {
                err = new ErrorMessage (ErrorMessage.Code.CouldNotStartSend, ex);
            }
        }
        else
        {
            err = new ErrorMessage (ErrorMessage.Code.NotConnected, "Cannot send");
        }

        if (err != null)
        {
            request.SendResponseFrom(ServiceIdent, err, null);
        }
    }

    /// <summary>
    /// the intuitive action is to send the message to remote service.
    /// </summary>
    /// <param name="msg">A <see cref="ActorMessage"/></param>
    public void SendOut (ActorMessage msg)
    {
      PostInput(msg);
    }

    /// <summary>
    /// <para>Send a message to the service. Do not wait here for the response.</para>
    /// <para>The AsyncResponseHandler is called on the same thread,</para>
    /// <para>when a response or errormessage arrives or a timeout has passed.</para>
    /// </summary>
    /// <param name="request">The message to send.</param>
    public ActorMessage SendOut(object request)
    {
      return SendOut (request, null);
    }

    /// <summary>
    /// <para>Send a message to the service. Do not wait here for the response.</para>
    /// <para>The response is asynchronously passed to the extension method "On", normally used as asyncResponseHandler.</para>
    /// <para>If the sending thread has a message queue, the response is executed by the same thread as the send operation was.</para>
    /// <para>If the response could not be handled by the On-extension methods, the default MessageHandler passed to TryConnect() is called.</para>
    /// <para>Example:</para>
    /// <para>Send (request, rsp => rsp.On&lt;ReadyMessage>(idle => {do something with idle message 'idle'})</para>
    /// <para>.On&lt;ErrorMessage>(err => {do something with error message 'err'}));</para>
    /// </summary>
    /// <param name="request">The message to send.</param>
    /// <param name="asyncResponseHandler"><see cref="ActorMessageExtensions.On{T}(ActorMessage, Action{T})"/></param>
    public ActorMessage SendOut(object request, AsyncResponseHandler asyncResponseHandler)
    {
        ActorMessage id = new ActorMessage(ClientIdent, ClientIdent.OutputClientId, ClientIdent.NextRequestId,
                                           ServiceIdent, null, request, asyncResponseHandler);
        PostInput  (id);
        return id;
    }

    /// <summary>
    /// Gets the Uri of a linked service.
    /// </summary>
    public Uri Uri {get{return ServiceIdent.Uri;}}

    #endregion
  }
}
