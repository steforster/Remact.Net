
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
    #region Identification, fields
    /// <summary>
      /// Detailed information about this client. May be a RemactPortClient&lt;TOC&gt; object containing application specific "OutputContext".
    /// </summary>
    public RemactPortClient ClientIdent {get; private set;}

    /// <summary>
    /// The last request id received in a response from the connected service.
    /// It is used to calculate outstandig responses.
    /// </summary>
    protected int LastRequestIdReceived;

    /// <summary>
    /// Detailed information about the connected service. Contains a "UserContext" object for free use by the client application.
    /// </summary>
    public RemactPortService ServiceIdent {get; private set;}

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
    private Dictionary<int, RemactMessage> m_OutstandingRequests; // TODO does not support multithreaded clients


    #endregion
    //----------------------------------------------------------------------------------------------
    #region Constructor, connection state, linking and disconnecting

    /// <summary>
    /// Create the proxy for a remote service.
    /// </summary>
    /// <param name="clientIdent">Link this RemactPortClient to the remote service.</param>
    internal RemactClient (RemactPortClient clientIdent)
    {
      m_OutstandingRequests = new Dictionary<int, RemactMessage>();
      ClientIdent = clientIdent;
      ServiceIdent = new RemactPortService(); // not yet defined
      ServiceIdent.IsServiceName = true;
      ServiceIdent.PassResponsesTo (ClientIdent); // ServiceIdent.PostInput will send to our client
    }


    /// <summary>
    /// <para>Connect this Client to a service identified by the serviceName parameter.</para>
    /// <para>The correct serviceHost and TCP port will be looked up at a Remact.CatalogService identified by parameter catalogHost.</para>
    /// </summary>
    /// <param name="serviceName">A unique name of the service. This service may run on any host that has been registered at the Remact.CatalogService.</param>
    /// <param name="clientConfig">Plugin your own client configuration instead of RemactDefaults.ClientConfiguration.</param>
    internal void LinkToService(string serviceName, IClientConfiguration clientConfig = null)
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
    internal void LinkToService(Uri websocketUri, IClientConfiguration clientConfig = null)
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
    /// A client is connected after the ServiceConnectResponse has been received.
    /// </summary>
    public bool IsConnected    { get { return m_protocolClient != null 
                                           && m_boFirstResponseReceived
                                           && !m_boTimeout
                                           && m_protocolClient.PortState == PortState.Ok; }}

    /// <summary>
    /// A client is disconnected after construction, after a call to Disconnect() or AbortCommunication()
    /// </summary>
    public bool IsDisconnected { get { return !m_boConnecting && m_protocolClient == null && !m_boTimeout;}}

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
                    Remact_ActorInfo_ClientDisconnectNotification(new ActorInfo(ClientIdent));
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
            RaLog.Exception("cannot abort Remact connection", ex, ClientIdent.Logger);
        }
      
        m_protocolClient = null;
        m_boConnecting = false;
        m_boFirstResponseReceived = false;
        m_boTimeout = false;
        ServiceIdent.m_isOpen = false; // internal, from ServiceIdent to ClientIdent
        ClientIdent.m_isOpen = false; // internal, from RemactPortClient to RemactClient

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
    #region Connect


    /// <summary>
    /// Accept the binding configuration provided when linking the RemactPortClient or set in RemactDefaults.ClientConfiguration.
    /// </summary>
    /// <param name="serviceUri">The URI to connect to. Parts of the URI may be changed depending on the binding configuration.</param>
    /// <param name="forCatalog">True, when the connection is to a Remact.Catalog.</param>
    protected internal void DoClientConfiguration(ref Uri serviceUri, bool forCatalog)
    {
        if (m_ClientConfig == null)
        {
            m_ClientConfig = RemactConfigDefault.Instance;
        }
        m_ClientConfig.DoClientConfiguration(m_protocolClient, ref serviceUri, forCatalog);
    }


    /// <summary>
    /// Connect or reconnect output to the previously linked partner.
    /// </summary>
    /// <returns>A task. When this task is run to completion, the task.Result corresponds to IsOpen.</returns>
    public Task<bool> TryConnect()
    {
        var tcs = new TaskCompletionSource<bool>();
        try
        {
            if (!(IsDisconnected || IsFaulted))
            {
                throw new InvalidOperationException("cannot connect " + ClientIdent.Name + ", state = " + OutputState);
            }

            ServiceIdent.IsMultithreaded = ClientIdent.IsMultithreaded;
            ServiceIdent.TryConnect(); // internal, from ServiceIdent to ClientIdent
            ClientIdent.PickupSynchronizationContext();
            ClientIdent.m_isOpen = true; // internal, from RemactPortClient to RemactClient
            m_boFirstResponseReceived = false;
            m_boTimeout = false;
            m_boConnecting = true;

            if (!_connectViaCatalog)
            {
                return OpenConnectionAsync(tcs, m_RequestedServiceUri);
            }

            if (RemactCatalogClient.IsDisabled)
            {
                throw new InvalidOperationException("cannot open " + ClientIdent.Name + ", RemactCatalogClient is disabled");
            }

            Task<RemactMessage<ActorInfo>> task = RemactCatalogClient.Instance.LookupInput(m_ServiceNameToLookup);
            task.ContinueWith(t =>
            {
                if (t.Status != TaskStatus.RanToCompletion)
                {
                    EndOfConnectionTries(tcs, "failed when asking catalog service.", t.Exception);
                    return;
                }

                ServiceIdent.UseDataFrom(t.Result.Payload);
                if (ClientIdent.TraceSend)
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
                    RaLog.Info(t.Result.CltRcvId, "ServiceAddressResponse: " + t.Result.Payload.Uri + s, ClientIdent.Logger);
                }

                m_addressesTried = 0;
                TryOpenNextServiceIpAddress(tcs, null); // try first address
            });
        }
        catch (Exception ex)
        {
            EndOfConnectionTries(tcs, "exception in TryConnect.", ex); // enter 'faulted' state when eg. configuration is incorrect
        }
        
        return tcs.Task;
    }// TryConnect


    // Tries to open one of the IP addresses of the service. Is running on user- or threadpool thread
    private bool TryOpenNextServiceIpAddress(TaskCompletionSource<bool> tcs, Exception error)
    {
        if (m_boFirstResponseReceived)
        {
            return EndOfConnectionTries(tcs, null, null); // successful !
        }

        if (error != null)
        {
            if (ServiceIdent.AddressList == null) return EndOfConnectionTries(tcs, "one address tried." , error); // connect without lookup a catalog

            m_addressNumber++; // connection failed, next time try next address. 
            if (m_addressesTried > ServiceIdent.AddressList.Count) return EndOfConnectionTries(tcs, "all addresses tried." , error);
            if (!m_boConnecting) return EndOfConnectionTries(tcs, "wrong state." , error);
            m_addressesTried++;
        }

        UriBuilder b = new UriBuilder(ServiceIdent.Uri);
        if (m_addressNumber <= 0 || ServiceIdent.AddressList == null || m_addressNumber > ServiceIdent.AddressList.Count)
        {
            m_addressNumber = 0; // the hostname
            b.Host = ServiceIdent.HostName;
        }
        else
        {
            b.Host = ServiceIdent.AddressList[m_addressNumber - 1].ToString(); // an IP address
        }

        b.Host = b.Uri.DnsSafeHost;
        OpenConnectionAsync(tcs, b.Uri);
        return true;
    }

    
    // Open the connection to the service, running on user- or threadpool thread
    private Task<bool> OpenConnectionAsync(TaskCompletionSource<bool> tcs, Uri uri)
    {
        m_RequestedServiceUri = NormalizeHostName(uri);
        m_protocolClient = new WampClient(m_RequestedServiceUri);
        // TODO: Let now the library user change binding and security credentials.
        // By default RemactDefaults.OnClientConfiguration is called.
        var websocketUri = m_RequestedServiceUri;
        DoClientConfiguration(ref websocketUri, forCatalog: false);
        ServiceIdent.PrepareServiceName(websocketUri);

        m_protocolClient.OpenAsync(new OpenAsyncState {Tcs = tcs}, this);
        // Callback to OnOpenCompleted when channel has been opened locally (no TCP connection opened on mono).
        return tcs.Task; // Connecting now
    }


    // Eventhandler, running on threadpool thread, sent from m_protocolClient.
    void IRemactProtocolDriverCallbacks.OnOpenCompleted(OpenAsyncState state)
    {
        if (ClientIdent.IsMultithreaded)
        {
            OnOpenCompletedOnUserThread(state); // Test1.ClientNoSync, CatalogClient
        }
        else if (ClientIdent.SyncContext == null)
        {
            RaLog.Error("Remact", "no synchronization context to open " + ClientIdent.Name, ClientIdent.Logger);
            OnOpenCompletedOnUserThread(state);
        }
        else
        {
            ClientIdent.SyncContext.Post(OnOpenCompletedOnUserThread, state);
        }
    }


    // Eventhandler, running on user thread.
    private void OnOpenCompletedOnUserThread(object obj)
    {
        var state = (OpenAsyncState) obj;
        if (m_protocolClient == null)
        {
            EndOfConnectionTries(state.Tcs, "output was disconnected.", new ObjectDisposedException("RemactClient"));
            return;
        }

        try
        {
            if (state.Error != null)
            {   
                TryOpenNextServiceIpAddress (state.Tcs, state.Error); // failed opening when using the current IP address
            }
            else
            {
                var task = Remact_ActorInfo_ClientConnectRequest(new ActorInfo(ClientIdent));
                task.ContinueWith(t =>
                    {
                        if (t.Status != TaskStatus.RanToCompletion)
                        {
                            EndOfConnectionTries(state.Tcs, "failed when sending ClientConnectRequest.", t.Exception);
                            return;
                        }

                        if (t.Result.Payload.IsServiceName && t.Result.Payload.IsOpen)
                        { // First message received from Service
                            t.Result.Payload.Uri = ServiceIdent.Uri; // keep the Uri stored here (maybe IP address instead of hostname used)
                            ServiceIdent.UseDataFrom (t.Result.Payload);
                            ClientIdent.OutputClientId = t.Result.Payload.ClientId; // defined by server
                            t.Result.ClientId = t.Result.Payload.ClientId;
                            if (ClientIdent.TraceConnect) RaLog.Info(t.Result.CltRcvId, ServiceIdent.ToString("Connected  svc", 0), ClientIdent.Logger);

                            m_boConnecting = false;
                            m_boFirstResponseReceived = true; // IsConnected --> true !
                            RemactCatalogClient.Instance.AddClient(this);
                            EndOfConnectionTries(state.Tcs, null, null); // ok
                        }
                        else
                        {
                            EndOfConnectionTries(state.Tcs, "unexpeced ClientConnectResponse.", new InvalidOperationException("unexpected message from service: "+t.Result.ToString()));
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
            RaLog.Exception("Remact cannot connect '" + ClientIdent.Name + "', " + reason, ex, ClientIdent.Logger);
            tcs.SetException(ex);
        }
        else
        {
            tcs.SetResult(true);
        }

        //try
        //{
        //    ClientIdent.DefaultInputHandler(rsp); // pass the negative feedback from catalog or real service to the application
        //}
        //catch (Exception ex)
        //{
        //    RaLog.Exception("Connect failure message to " + ClientIdent.Name + " cannot be handled by application", ex, ClientIdent.Logger);
        //}
        return false;
    }


    #endregion
    //----------------------------------------------------------------------------------------------
    #region IRemactService implementation

    public Task<RemactMessage<ActorInfo>> Remact_ActorInfo_ClientConnectRequest(ActorInfo client)
    {
        client.IsOpen = true;
        RemactMessage sentMessage;
        bool traceSend = ClientIdent.TraceSend;
        if (ClientIdent.TraceConnect)
        {
            ClientIdent.TraceSend = false;
        }

        ClientIdent.OutputClientId = 0;
        ClientIdent.LastRequestIdSent = 9;
        LastRequestIdReceived = 9;
        var task = ClientIdent.Ask<ActorInfo>(RemactService.ConnectMethodName, client, out sentMessage, throwException: false);

        if (ClientIdent.TraceConnect)
        {
            ClientIdent.TraceSend = traceSend;
            string serviceAddr = GetSetServiceAddress();
            RaLog.Info(sentMessage.CltSndId, string.Concat("Connecting svc: '", serviceAddr, "'"), ClientIdent.Logger);
        }
        return task;
    }

    public void Remact_ActorInfo_ClientDisconnectNotification(ActorInfo client)
    {
        client.IsOpen = false;
        bool traceSend = ClientIdent.TraceSend;
        ClientIdent.TraceSend = ClientIdent.TraceConnect;
        var msg = new RemactMessage(ClientIdent, ClientIdent.OutputClientId, 0, // creates a notification
                                   ServiceIdent, RemactService.DisconnectMethodName, client, null);
        PostInput(msg);
        ClientIdent.TraceSend = traceSend;
        Thread.Sleep(30);
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region IRemactProtocolDriverCallbacks implementation and incoming messages


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


    private bool TryGetResponseMessage(int id, out RemactMessage msg)
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
        m_OutstandingRequests = new Dictionary<int, RemactMessage>();

        foreach (var msg in copy.Values)
        {
            var lower = new LowerProtocolMessage
            {
                Type = RemactMessageType.Error,
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

            RemactMessage msg;

            switch (lower.Type)
            {
                case RemactMessageType.Response:
                    {
                        if (!TryGetResponseMessage(lower.RequestId, out msg))
                        {
                            RaLog.Warning(ClientIdent.Name, "skipped unexpected response with id " + lower.RequestId);
                            return;
                        }
                    }
                    break;

                case RemactMessageType.Error:
                    {
                        if (!TryGetResponseMessage(lower.RequestId, out msg))
                        {
                            msg = new RemactMessage(ServiceIdent, ClientIdent.OutputClientId, lower.RequestId,
                                                   ClientIdent, string.Empty, lower.Payload);
                        }
                    }
                    break;

                default:
                    {
                        msg = new RemactMessage(ServiceIdent, ClientIdent.OutputClientId, 0,
                                               ClientIdent, string.Empty, lower.Payload);
                    }
                    break;
            }

            msg.MessageType = lower.Type;
            msg.Payload = lower.Payload;

            if (!m_boTimeout)
            {
                var m = msg.Payload as IExtensibleRemactMessage;
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
            throw new InvalidOperationException("Remact: TryConnect of '"+ClientIdent.Name+"' has not been called to pick up the synchronization context.");
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
    public void PostInput (RemactMessage msg)
    {
        if (m_boTimeout || m_protocolClient == null || m_protocolClient.PortState != PortState.Ok)
        {
            throw new InvalidOperationException("Remact: Output of '" + ClientIdent.Name + "' is not open. Cannot send message.");
        }
            
        // PostInput() may be used during connection buildup as well
        if (ClientIdent.TraceSend) RaLog.Info(msg.CltSndId, msg.ToString(), ClientIdent.Logger);

        RemactMessage lost;
        if (m_OutstandingRequests.TryGetValue(msg.RequestId, out lost))
        {
            m_OutstandingRequests.Remove(msg.RequestId);
            RaLog.Error(lost.CltSndId, "response was never received");
        }

        m_OutstandingRequests.Add(msg.RequestId, msg);
        m_protocolClient.MessageFromClient(msg);
    }

    /// <summary>
    /// Gets the Uri of a linked service.
    /// </summary>
    public Uri Uri {get{return ServiceIdent.Uri;}}


    #endregion
  }
}
