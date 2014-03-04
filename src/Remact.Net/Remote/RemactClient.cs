
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Net;            // Dns
using System.Threading;
using System.ComponentModel; // AsyncOperation
using Newtonsoft.Json.Linq;
using Remact.Net.Protocol;
using Remact.Net.Protocol.Wamp;
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
  internal class RemactClient : IRemoteActor, IRemactProtocolDriverCallbacks, IRemactService
  {
    //----------------------------------------------------------------------------------------------
    #region Properties
    /// <summary>
    /// Detailed information about this client. May be a ActorOutput&lt;TOC&gt; object containing application specific "OutputContext".
    /// </summary>
    public ActorOutput ClientIdent {get; private set;}

    /// <summary>
    /// The last request id received in a response from the connected service.
    /// It is used to calculate outstandig responses.
    /// </summary>
    protected int LastRequestIdReceived;

    /// <summary>
    /// Detailed information about the connected service. Contains a "UserContext" object for free use by the client application.
    /// </summary>
    public ActorInput ServiceIdent {get; private set;}

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
    private bool _connectViaCatalog;

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
    /// Outstanding requests, key = request ID.
    /// </summary>
    private Dictionary<int, ActorMessage> m_OutstandingRequests; // TODO does not support multithreaded clients


    #endregion
    //----------------------------------------------------------------------------------------------
    #region Constructors, linking and connecting

    /// <summary>
    /// Create the proxy for a remote service.
    /// </summary>
    /// <param name="clientIdent">Link this ActorOutput to the remote service.</param>
    internal RemactClient (ActorOutput clientIdent)
    {
      m_OutstandingRequests = new Dictionary<int, ActorMessage>();
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
    internal void LinkToService(string serviceName, IActorOutputConfiguration clientConfig = null)
    {
        _connectViaCatalog = true;
        m_ClientConfig = clientConfig;
        m_ServiceNameToLookup = serviceName;
        ServiceIdent.PrepareServiceName(RemactConfigDefault.Instance.CatalogHost, m_ServiceNameToLookup);
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
      _connectViaCatalog = false;
      m_ClientConfig = clientConfig;
      m_RequestedServiceUri = NormalizeHostName(websocketUri);
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
            ServiceIdent.IsMultithreaded = ClientIdent.IsMultithreaded;
            ServiceIdent.TryConnect(); // internal, from ServiceIdent to ClientIdent
            ClientIdent.PickupSynchronizationContext();
            ClientIdent.m_Connected = true; // internal, from ActorOutput to RemactClient

            if (!_connectViaCatalog)
            {
                return ConnectToRemoteInput(m_RequestedServiceUri);
            }

            if (RemactCatalogClient.Instance.DisableCatalogClient)
            {
                throw new InvalidOperationException("cannot open " + ClientIdent.Name + ", RemactCatalogClient is disabled");
            }

            if (!RemactCatalogClient.Instance.IsConnected)
            {
                Thread.Sleep(100); // initial connection
            }

            if (!RemactCatalogClient.Instance.IsConnected)
            {
                return false;
            }

            // TODO Timeout and Fault messages
            Task<ActorMessage<ActorInfo>> task = RemactCatalogClient.Instance.LookupInput(m_ServiceNameToLookup);
            task.ContinueWith(t =>
            {
                try
                {
                    if (ClientIdent.TraceSend) RaLog.Info(t.Result.CltRcvId, "Received response from catalog: '" + t.Result.Source.Name + "' on '" + t.Result.Source.HostName + "'", ClientIdent.Logger);
                    ServiceIdent.UseDataFrom(t.Result.Payload);
                    if (ClientIdent.TraceSend)
                    {
                        string s = string.Empty;
                        if (t.Result.Payload.AddressList != null)
                        {
                            string delimiter = ", IP-adresses = ";
                            foreach (var adr in t.Result.Payload.AddressList)
                            {
                                s = string.Concat(s, delimiter, adr.ToString());
                                delimiter = ", ";
                            }
                        }
                        RaLog.Info(t.Result.CltRcvId, "ServiceAddressResponse: " + t.Result.Payload.Uri + s, ClientIdent.Logger);
                    }
                    m_addressesTried = 0;
                    OnConnectionResponseFromService(null); // try first address
                }
                catch (ActorException<ErrorMessage> ex)
                {
                    //RaLog.Warning (rsp.CltRcvId, "Catalog "+rsp.ToString());
                    if (ex.ActorMessage.Payload.Error == ErrorMessage.Code.ServiceNotRunning)
                    {
                        ex.ActorMessage.Payload.Error = ErrorMessage.Code.CatalogServiceNotRunning;
                    }
                    EndOfConnectionTries(ex.ActorMessage); // failed at catalog
                }
                catch (ActorException ex)
                {
                    RaLog.Error(ex.ActorMessage.CltRcvId, "Receiving unexpected response from Remact.CatalogService: " + ex.ActorMessage.ToString(), ClientIdent.Logger);
                    ex.ActorMessage.Payload = new ErrorMessage(ErrorMessage.Code.CouldNotConnectCatalog, "Unexpected response from Remact.CatalogService");
                    EndOfConnectionTries(ex.ActorMessage); // failed at catalog
                }
            });

            return true;
        }
        catch (Exception ex)
        {
            RaLog.Exception("Cannot open Remact connection(3)", ex, ClientIdent.Logger);
            m_boTimeout = true; // enter 'faulted' state when eg. configuration is incorrect
            return false;
        }
    }// TryConnect


    public virtual bool ConnectToRemoteInput(Uri uri)
    {
        m_RequestedServiceUri = NormalizeHostName(uri);
        m_protocolClient = new WampClient(m_RequestedServiceUri);
        // Let now the library user change binding and security credentials.
        // By default RemactDefaults.OnClientConfiguration is called.
        var websocketUri = m_RequestedServiceUri;
        DoClientConfiguration(ref websocketUri, forCatalog: false);
        ServiceIdent.PrepareServiceName(websocketUri);

        ClientIdent.OutputClientId = 0;
        ClientIdent.LastRequestIdSent = 9;
        LastRequestIdReceived = 9;
        m_boFirstResponseReceived = false;
        m_boTimeout = false;
        m_boConnecting = true;
        ActorMessage msg = new ActorMessage(ClientIdent, ClientIdent.OutputClientId, ClientIdent.NextRequestId,
                                            ServiceIdent, null, null);
        m_protocolClient.OpenAsync(msg, this);
        // Callback to OnOpenCompleted when channel has been opened locally (no TCP connection opened on mono).
        return true; // Connecting now
    }


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
    public int  OutstandingResponsesCount  { get { return m_OutstandingRequests.Count; }}


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
                    RemactCatalogClient.Instance.RemoveClient(this);
                    Remact_ActorInfo_ClientDisconnectNotification(new ActorInfo(ClientIdent, ActorInfo.Use.ClientDisconnectNotification));
                }
                catch
                {
                }
            }
        
            if (m_protocolClient != null) 
            {
                m_protocolClient.Dispose();
                RemactCatalogClient.Instance.RemoveClient (this);
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
        ServiceIdent.m_Connected = false; // internal, from ServiceIdent to ClientIdent
        ClientIdent.m_Connected = false; // internal, from ActorOutput to RemactClient

    }// Disconnect

    
    /// <summary>
    /// <para>Abort all messages, go from any state to Disconnected state.</para>
    /// <para>Makes it possible to restart the client with TryConnect.</para>
    /// </summary>
    public void AbortCommunication()
    {
      m_boTimeout = true;
      Disconnect();
    }


    #endregion
    //----------------------------------------------------------------------------------------------
    #region Message handling

 
    // Eventhandler, running on threadpool thread, sent from m_protocolClient.
    void IRemactProtocolDriverCallbacks.OnOpenCompleted(ActorMessage request)
    {
        if (m_protocolClient == null)
        {
            return;
        }

        if (ClientIdent.IsMultithreaded)
        {
            OnOpenCompletedOnUserThread(request); // Test1.ClientNoSync, CatalogClient
        }
        else if (ClientIdent.SyncContext == null)
        {
            RaLog.Error("Remact", "No synchronization context to open " + ClientIdent.Name, ClientIdent.Logger);
            OnOpenCompletedOnUserThread(request);
        }
        else
        {
            ClientIdent.SyncContext.Post(OnOpenCompletedOnUserThread, request);
        }
    }


    // Eventhandler, running on user thread.
    private void OnOpenCompletedOnUserThread(object obj)
    {
        ActorMessage msg = obj as ActorMessage;
        msg.DestinationLambda = null;
        msg.SourceLambda = null;
      
        try
        {
            if (msg.Payload != null)
            {   // error while opening
                msg.Type = ActorMessageType.Error;
                msg.Source = ServiceIdent;
                if( ServiceIdent.AddressList != null )
                {
                    ClientIdent.DefaultInputHandler(msg); // pass the negative feedback from real service to the handler in this class
                }
                else
                {
                    EndOfConnectionTries(msg); // enter 'faulted' state when eg. catalog service not running
                }
            }
            else
            {
                var task = Remact_ActorInfo_ClientConnectRequest(new ActorInfo(ClientIdent, ActorInfo.Use.ClientConnectRequest));
                task.ContinueWith(t =>
                    {
                        if (t.Result.Payload.Usage == ActorInfo.Use.ServiceConnectResponse)
                        { // First message received from Service
                            t.Result.Payload.Uri = ServiceIdent.Uri; // keep the Uri used to request the message (maybe IP address instead of hostname used)
                            ServiceIdent.UseDataFrom (t.Result.Payload);
                            ClientIdent.OutputClientId = t.Result.Payload.ClientId; // defined by server
                            t.Result.ClientId = t.Result.Payload.ClientId;
                            RemactCatalogClient.Instance.AddClient(this);
                            m_boFirstResponseReceived = true; // IsConnected --> true !
                            m_boConnecting = false;
                            if (ClientIdent.TraceConnect) RaLog.Info(t.Result.CltRcvId, ServiceIdent.ToString("Connected  svc", 0), ClientIdent.Logger);
                        }
                        else
                        {
                            RaLog.Error( t.Result.CltRcvId, "unexpeced connect response: " + t.Result.ToString(), ClientIdent.Logger );
                        }
                    });
            }
        }
        catch (Exception ex)
        {
            msg.Type = ActorMessageType.Error;
            msg.Source = ServiceIdent;
            msg.Payload = new ErrorMessage(ErrorMessage.Code.CouldNotStartConnect, ex);
            EndOfConnectionTries(msg); // enter 'faulted' state when eg. configuration is incorrect
        }
    }// OnOpenCompleted


    #region IRemactService implementation

    public Task<ActorMessage<ActorInfo>> Remact_ActorInfo_ClientConnectRequest(ActorInfo actorOutput)
    {
        ActorMessage sentMessage;
        var task = ClientIdent.Ask<ActorInfo>(string.Concat(ActorInfo.MethodNamePrefix, "ClientConnectRequest"), actorOutput, out sentMessage, throwException: false);

        if (ClientIdent.TraceConnect)
        {
            string serviceAddr = GetSetServiceAddress();
            RaLog.Info(sentMessage.CltSndId, string.Concat("Connecting svc: '", serviceAddr, "'"), ClientIdent.Logger);
        }
        return task;
    }

    public void Remact_ActorInfo_ClientDisconnectNotification(ActorInfo actorOutput)
    {
        bool traceSend = ClientIdent.TraceSend;
        ClientIdent.TraceSend = ClientIdent.TraceConnect;
        var msg = new ActorMessage(ClientIdent, ClientIdent.OutputClientId, 0, // creates a notification
                                   ServiceIdent, string.Concat(ActorInfo.MethodNamePrefix, "ClientDisconnectNotification"), actorOutput, null);
        PostInput(msg);
        ClientIdent.TraceSend = traceSend;
        Thread.Sleep(30);
    }

    #endregion

      
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


    private bool TryGetResponseMessage(int id, out ActorMessage msg)
    {
        if (!m_OutstandingRequests.TryGetValue(id, out msg))
        {
            return false;
        }

        m_OutstandingRequests.Remove(id);
        msg.DestinationLambda = msg.SourceLambda;
        msg.SourceLambda = null;
        msg.Source = ServiceIdent;
        msg.Destination = ClientIdent;
        msg.ClientId = ClientIdent.OutputClientId;
        return true;
    }


    Uri IRemactProtocolDriverCallbacks.ClientUri { get { return ClientIdent.Uri; } }


    // sent from m_protocolClient
    void IRemactProtocolDriverCallbacks.OnServiceDisconnect()
    {
        if (ClientIdent.IsMultithreaded || ClientIdent.SyncContext == null)
        {
            OnServiceDisconnectOnActorThread(null);
        }
        else
        {
            ClientIdent.SyncContext.Post(OnServiceDisconnectOnActorThread, null);
        }
    }


    private void OnServiceDisconnectOnActorThread(object obj)
    {
        m_boTimeout = true;
        var copy = m_OutstandingRequests;
        m_OutstandingRequests = new Dictionary<int, ActorMessage>();

        foreach (var msg in copy.Values)
        {
            var lower = new LowerProtocolMessage
            {
                Type = ActorMessageType.Error,
                RequestId = msg.RequestId,
                Payload = new ErrorMessage(ErrorMessage.Code.CouldNotSend, "web socket disconnected")
            };

            OnIncomingMessageOnActorThread(lower);
        }
    }

    // sent from m_protocolClient
    void IRemactProtocolDriverCallbacks.OnMessageFromService(LowerProtocolMessage msg)
    {
        if (ClientIdent.IsMultithreaded || ClientIdent.SyncContext == null)
        {
            OnIncomingMessageOnActorThread(msg);
        }
        else
        {
            ClientIdent.SyncContext.Post(OnIncomingMessageOnActorThread, msg);
        }
    }


    private void OnIncomingMessageOnActorThread(object obj)
    {
        try
        {
            var lower = (LowerProtocolMessage)obj;

            ActorMessage msg;

            switch (lower.Type)
            {
                case ActorMessageType.Response:
                    {
                        if (!TryGetResponseMessage(lower.RequestId, out msg))
                        {
                            RaLog.Warning(ClientIdent.Name, "skipped unexpected response with id " + lower.RequestId);
                            return;
                        }
                    }
                    break;

                case ActorMessageType.Error:
                    {
                        if (!TryGetResponseMessage(lower.RequestId, out msg))
                        {
                            msg = new ActorMessage(ServiceIdent, ClientIdent.OutputClientId, lower.RequestId,
                                                   ClientIdent, string.Empty, lower.Payload);
                        }
                    }
                    break;

                default:
                    {
                        msg = new ActorMessage(ServiceIdent, ClientIdent.OutputClientId, 0,
                                               ClientIdent, string.Empty, lower.Payload);
                    }
                    break;
            }

            msg.Type = lower.Type;
            msg.Payload = lower.Payload;

            if (!m_boTimeout)
            {
                var m = msg.Payload as IExtensibleActorMessage;
                if (!ClientIdent.IsMultithreaded)
                {
                    if (m != null) m.BoundSyncContext = SynchronizationContext.Current;
                }
                if (m != null) m.IsSent = true;

                if (msg.DestinationMethod == null) msg.DestinationMethod = string.Empty;
            }

            ClientIdent.DispatchMessage(msg);
        }
        catch (Exception ex)
        {
            RaLog.Exception( "Message for " + ClientIdent.Name + " cannot be handled by application", ex, ClientIdent.Logger );
        }
    }// OnIncomingMessageOnActorThread



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
        ConnectToRemoteInput(b.Uri);
    }


    private void EndOfConnectionTries( ActorMessage rsp )
    {
        m_boTimeout = !m_boFirstResponseReceived; // Fault state when not correct response in OnConnectMessage

        try
        {
            ClientIdent.DefaultInputHandler(rsp); // pass the negative or positive feedback from catalog or real service to the application
        }
        catch( Exception ex )
        {
            RaLog.Exception( "Connect message to " + ClientIdent.Name + " cannot be handled by application", ex, ClientIdent.Logger );
        }
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


    /// <summary>
    /// Post a request to the input of the remote partner. It will be sent over the network.
    /// Called from ClientIdent, when SendOut a message to remote partner.
    /// </summary>
    /// <param name="msg">A <see cref="ActorMessage"/></param>
    public void PostInput (ActorMessage msg)
    {
        ErrorMessage err = null;
        if (!IsFaulted && m_protocolClient != null) // PostInput() may be used during connection buildup as well
        {
            try
            {
                if (ClientIdent.TraceSend) RaLog.Info(msg.CltSndId, msg.ToString(), ClientIdent.Logger);

                ActorMessage lost;
                if (m_OutstandingRequests.TryGetValue(msg.RequestId, out lost))
                {
                    m_OutstandingRequests.Remove(msg.RequestId);
                    RaLog.Error(lost.CltSndId, "response was never received");
                }

                m_OutstandingRequests.Add(msg.RequestId, msg);
                m_protocolClient.MessageFromClient(msg);
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
            msg.SendResponseFrom(ServiceIdent, err, null);
        }
    }

    /// <summary>
    /// Gets the Uri of a linked service.
    /// </summary>
    public Uri Uri {get{return ServiceIdent.Uri;}}


    #endregion
  }
}
