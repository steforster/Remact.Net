
// Copyright (c) 2014, github.com/steforster/Remact.Net

// Event-based Asynchronous Pattern:
// ms-help://MS.VSCC.v90/MS.MSDNQTR.v90.en/dv_fxadvance/html/792aa8da-918b-458e-b154-9836b97735f3.htm

using System;
using System.ServiceModel;
using System.Net;            // Dns
using System.Threading;
using System.ComponentModel; // AsyncOperation

namespace SourceForge.AsyncWcfLib.Basic
{
  /// <summary>
  /// <para>Base class of WcfClientAsync to connect to a WCF service.</para>
  /// <para>Requests are sent asynchronious.</para>
  /// <para>Responses are asynchroniously received on the same thread as the request was sent</para>
  /// <para>(only when sent from a thread with message queue (as WinForms), but not when sent from a threadpool-thread).</para>
  /// <para>This class uses a auto-generated service reference 'WcfBasicClient'.</para>
  /// <para>TSC is the TypeofUserContext in ClientIdent and ServiceIdent.</para>
  /// <para>We accept only reference types as TSC. This allows to modify user context when receiving a message.</para>
  /// <para>Specify WcfBasicClientAsync&lt;object>, when you do not need the user context.</para>
  /// </summary>
  public class WcfBasicClientAsync: IWcfBasicPartner
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
    protected uint              LastRequestIdReceived;

    /// <summary>
    /// The last send id received in a response from the connected service.
    /// It is used to detect missing messages from a remote service.
    /// </summary>
    protected uint              LastSendIdReceived;

    /// <summary>
    /// Detailed information about the connected service. Contains a "UserContext" object for free use by the client application.
    /// </summary>
    public    ActorInput        ServiceIdent           {get; private set;}

    /// <summary>
    /// Auto-generated class from WcfRouterService - mex endpoint. Used when opening the connection.
    /// </summary>
    internal WcfBasicClient     m_ServiceReference; // internal protected is not allowed ?!
    
    /// <summary>
    /// <para>Set m_boTimeout to true, when the connect operation fails or some errormessages are received.</para>
    /// <para>Sets the client into Fault state.</para>
    /// </summary>
    protected bool              m_boTimeout;

    private   InstanceContext   m_InstanceContext;

    /// <summary>
    /// The original service name (unique in plant), not the router.
    /// </summary>
    protected string m_ServiceNameToLookup;

    /// <summary>
    /// URI of next service to connect, can be the router.
    /// </summary>
    protected Uri  m_RequestedServiceUri;

    /// <summary>
    /// The plugin provided by the library user or WcfDefault.ClientConfiguration
    /// </summary>
    protected IWcfClientConfiguration m_WcfClientConfig;

    /// <summary>
    /// True, when connecting or connected to router, not to the original service.
    /// </summary>
    protected bool m_boTemporaryRouterConn;

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
    protected WcfMessageHandler m_DefaultInputHandlerForApplication;

    /// <summary>
    /// The hostname of the router.
    /// </summary>
    protected string m_RouterHostToLookup;

    /// <summary>
    /// The TCP port of the router.
    /// </summary>
    protected int m_WcfRouterPort;
    
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
    /// <param name="clientName">Unique identification of the client inside an application.</param>
    /// <param name="defaultResponseHandler">The method to be called for responses that have not otherwise been handled.</param>
    internal WcfBasicClientAsync (string clientName, WcfMessageHandler defaultResponseHandler)
    {
      m_DefaultInputHandlerForApplication = defaultResponseHandler;
      ClientIdent = new ActorOutput(clientName, defaultResponseHandler);
      ServiceIdent           = new ActorInput(); // not yet defined
      ServiceIdent.IsServiceName = true;
      ServiceIdent.PassResponsesTo (ClientIdent); // ServiceIdent.PostInput will send to our client
      //ServiceIdent.SetSenderContext (ClientIdent.GetNewOrExistingSenderContext ()); // use only one instance of user data
      m_InstanceContext      = new InstanceContext (this);
    }// CTOR1


    /// <summary>
    /// Create the proxy for a remote service.
    /// </summary>
    /// <param name="clientIdent">Link this ActorOutput to the remote service.</param>
    internal WcfBasicClientAsync (ActorOutput clientIdent)
    {
      m_DefaultInputHandlerForApplication = clientIdent.DefaultInputHandler;
      ClientIdent = clientIdent;
      ServiceIdent           = new ActorInput(); // not yet defined
      ServiceIdent.IsServiceName = true;
      ServiceIdent.PassResponsesTo (ClientIdent); // ServiceIdent.PostInput will send to our client
      //ServiceIdent.SetSenderContext (ClientIdent.GetNewOrExistingSenderContext ()); // use only one instance of user data
      m_InstanceContext      = new InstanceContext (this);
    }// CTOR 2


    /// <summary>
    /// Link this ClientIdent to a remote service. No lookup at WcfRouter is needed as we know the TCP portnumber.
    /// </summary>
    /// <param name="serviceUri">The uri of the remote service.</param>
    /// <param name="clientConfig">Plugin your own client configuration instead of WcfDefault.ClientConfiguration.</param>
    internal void LinkToService(Uri serviceUri, IWcfClientConfiguration clientConfig = null)
    {
      // this link method does not read the App.config file (it is running on mono also).
      if (!IsDisconnected) Disconnect ();
      m_RouterHostToLookup = null;
      serviceUri = NormalizeHostName( serviceUri );
      m_RequestedServiceUri = serviceUri;
      m_ServiceReference = new WcfBasicClient (new BasicHttpBinding(), new EndpointAddress (serviceUri), this);
      // Let now the library user change binding and security credentials.
      // By default WcfDefault.OnClientConfiguration is called.
      DoClientConfiguration( ref serviceUri, /*forRouter=*/false );
      ServiceIdent.PrepareServiceName (serviceUri);
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
    /// Accept the binding configuration provided when linking the ActorOutput or set in WcfDefault.ClientConfiguration.
    /// </summary>
    /// <param name="serviceUri">The URI to connect to. Parts of the URI may be changed depending on the binding configuration.</param>
    /// <param name="forRouter">True, when the connection is to a WcfRouter.</param>
    protected internal void DoClientConfiguration( ref Uri serviceUri, bool forRouter )
    {
        if( m_WcfClientConfig == null )
        {
            m_WcfClientConfig = WcfDefault.Instance;
        }
        m_WcfClientConfig.DoClientConfiguration( m_ServiceReference, ref serviceUri, forRouter );
    }


    /// <summary>
    /// <para>Connect this Client to a service identified by the serviceName parameter.</para>
    /// <para>The correct serviceHost and TCP port will be looked up at a WcfRouterService identified by parameter routerHost.</para>
    /// </summary>
    /// <param name="routerHost">The HostName, where the WcfRouterService is running. This may be the 'localhost'.
    ///    <para>By default TCP port 40000 is used for WcfRouterService, but you can specify another TCP port for the router eg. "host:3333"</para></param>
    /// <param name="serviceName">A unique name of the service. This service may run on any host that has been registered at the WcfRouterService.</param>
    /// <param name="clientConfig">Plugin your own client configuration instead of WcfDefault.ClientConfiguration.</param>
    internal void LinkToService(string routerHost, string serviceName, IWcfClientConfiguration clientConfig = null)
    {
        m_WcfRouterPort = WcfDefault.Instance.RouterPort;
        m_RouterHostToLookup = routerHost;
        try
        {
            int i = routerHost.LastIndexOf(':');
            if (i > 0)
            {
                m_RouterHostToLookup = routerHost.Substring(0, i);
                m_WcfRouterPort = Convert.ToInt32(routerHost.Substring(i + 1));
            }
        }
        catch
        {
        }
        m_RouterHostToLookup  = NormalizeHostName( m_RouterHostToLookup );
        m_ServiceNameToLookup = serviceName;
        ServiceIdent.PrepareServiceName(m_RouterHostToLookup, m_ServiceNameToLookup);
    }// LinkToService (RouterService lookup)


    //--------------------
    /// <summary>
    /// <para>Connect this client to a service, using the ClientName for endpoint name entry in App.config file </para>
    /// <para>as in:</para>
    /// <para>(system.serviceModel></para>
    /// <para>  (client></para>
    /// <para>    (endpoint address="http://localhost:40000/AsyncWcfLib/RouterService/"</para>
    /// <para>      binding="basicHttpBinding" bindingConfiguration="" contract="AsyncWcfLib.ClientContract"</para>
    /// <para>      name="RouterClient"></para>
    /// <para>    (/endpoint></para>
    /// <para>  (/client></para>
    /// <para>(/system.serviceModel></para>
    /// </summary>
    internal void TryConnectConfiguredEndpoint()
    {
      if (!IsDisconnected) Disconnect ();
      try
      {
        m_RouterHostToLookup = null;
        m_ServiceReference = new WcfBasicClient(ClientIdent.Name, this); // config-name
        m_RequestedServiceUri = NormalizeHostName( m_ServiceReference.Endpoint.Address.Uri );
        m_boTemporaryRouterConn = false;
        OpenConnectionToService();
      }
      catch (Exception ex)
      {
          WcfTrc.Exception( "Cannot open Wcf connection(1)", ex, ClientIdent.Logger );
          m_boTimeout = true; // enter 'faulted' state when eg. configuration is incorrect
      }
    }// TryConnectConfiguredEndpoint


    /// <summary>
    /// <para>Connect this client to a router or to the requested service, without configuration from App.config file.</para>
    /// </summary>
    /// <param name="endpointUri">fully specified URI of the service</param>
    /// <param name="viaResponseHandler">The callback method when a response arrives</param>
    /// <param name="toRouter">True, when the connection to a router is made.</param>
    internal virtual void TryConnectVia( Uri endpointUri, WcfMessageHandler viaResponseHandler, bool toRouter )
    {
      if (!IsDisconnected) Disconnect ();
      try
      {
          if (toRouter)
          {
              m_TraceConnectBefore = ClientIdent.TraceConnect;
              ClientIdent.TraceConnect = ClientIdent.TraceSend;
              m_boTemporaryRouterConn = true;
              ServiceIdent.Uri = null; // yet unknown
          }
          else if( m_boTemporaryRouterConn )
          {
              ClientIdent.TraceConnect = m_TraceConnectBefore;
              m_boTemporaryRouterConn = false;
          }

          ClientIdent.DefaultInputHandler = viaResponseHandler;
          endpointUri = NormalizeHostName( endpointUri );
          m_RequestedServiceUri = endpointUri;
          m_ServiceReference = new WcfBasicClient (new BasicHttpBinding(), new EndpointAddress (endpointUri), this);
          // Let now the library user change binding and security credentials.
          // By default WcfDefault.OnClientConfiguration is called.
          DoClientConfiguration( ref endpointUri, toRouter ); // TODO: changes in uri are not reflected in a new m_ServiceReference
          OpenConnectionToService();
      }
      catch (Exception ex)
      {
          WcfTrc.Exception( "Cannot open Wcf connection(2)", ex, ClientIdent.Logger );
          m_boTimeout = true; // enter 'faulted' state when eg. configuration is incorrect
      }
    }// TryConnectVia


    /// <summary>
    /// A client is connected after the ServiceConnectResponse has been received.
    /// </summary>
    public bool IsConnected    { get { return m_ServiceReference != null 
                                           && m_boFirstResponseReceived
                                           && !m_boTimeout
                                           && m_ServiceReference.State == CommunicationState.Opened; }}

    /// <summary>
    /// A client is disconnected after construction, after a call to Disconnect() or AbortCommunication()
    /// </summary>
    public bool IsDisconnected { get { return m_ServiceReference == null 
                                       ||   (!m_boConnecting && !m_boFirstResponseReceived && !m_boTimeout);}}

    /// <summary>
    /// A client is in Fault state when a connection cannot be kept open or a timeout has passed.
    /// </summary>
    public bool IsFaulted      { get { return m_boTimeout
                                       ||    (m_ServiceReference != null
                                           && m_ServiceReference.State == CommunicationState.Faulted); }}

    /// <summary>
    /// Returns the number of requests that have not received a response by the service.
    /// </summary>
    public int  OutstandingResponsesCount
    { get {
        if (ClientIdent.LastRequestIdSent >= LastRequestIdReceived){
           return (int)(ClientIdent.LastRequestIdSent - LastRequestIdReceived);
        }else{ return 1;} 
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
            WcfRouterClient.Instance().RemoveClient(this);
            SendDisconnectMessage();
            m_ServiceReference.Close();// Dispose
            m_ServiceReference = null;
          }
          catch
          {
          }
        }
        
        if (m_ServiceReference != null) 
        {
          if (m_ServiceReference.State != CommunicationState.Created && m_ServiceReference.State != CommunicationState.Faulted)
          {
            m_ServiceReference.Abort(); // Dispose
          }
          WcfRouterClient.Instance ().RemoveClient (this);
          //TraceState("Abortd");
        }
      }
      catch (Exception ex)
      {
          WcfTrc.Exception( "Cannot abort Wcf connection", ex, ClientIdent.Logger );
      }
      
      m_ServiceReference = null;
      m_boConnecting = false;
      m_boFirstResponseReceived = false;
      m_boTimeout = false;
      m_boTemporaryRouterConn  = false;
      ServiceIdent.m_Connected = false; // internal, from ServiceIdent to ClientIdent
      ClientIdent.m_Connected  = false; // internal, from ActorOutput to WcfBasicClientAsync

    }// Disconnect

    
    /// <summary>
    /// internal: Overloaded by ClientAsyncAwait
    /// </summary>
    internal protected virtual void SendDisconnectMessage()
    {
        SendOut (new WcfPartnerMessage (ClientIdent, WcfPartnerMessage.Use.ClientDisconnectRequest));
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
      if (m_ServiceReference != null)
      {
        WcfTrc.Info("WcfClt", "["+mark.PadRight(6)+"] "+ ClientIdent.Name+"["+ClientIdent.OutputClientId+"]"
                    +", SessionId=" +m_ServiceReference.InnerChannel.SessionId
                    +", ServiceReference="+m_ServiceReference.State
                    +", InnerChannel="+m_ServiceReference.InnerChannel.State
                 // +", InnerDuplexChannel="+m_ServiceReference.InnerDuplexChannel.State
                    +", InstanceContext="+m_InstanceContext.State
                    +", IncomingChannels="+m_InstanceContext.IncomingChannels.Count
                    +", OutgoingChannels="+m_InstanceContext.OutgoingChannels.Count
                    , ClientIdent.Logger );
      }
    }// TraceState
    

    /// <summary>
    /// Connect this Client to the prepared m_ServiceReference
    /// </summary>
    //  Running on the user thread
    private void OpenConnectionToService()
    {
      ClientIdent.OutputClientId    =  0;
      ClientIdent.LastRequestIdSent = 10;
      LastSendIdReceived     =  0; // first message after connect is expected with SendId=1
      LastRequestIdReceived  = 10;
      m_boFirstResponseReceived = false;
      m_boTimeout = false;
      m_boConnecting = true;
      if (ServiceIdent.Uri == null) ServiceIdent.PrepareServiceName(m_ServiceReference.Endpoint.Address.Uri);
      ServiceIdent.IsMultithreaded = ClientIdent.IsMultithreaded;
      ServiceIdent.TryConnect(); // internal, from ServiceIdent to ClientIdent

      ClientIdent.PickupSynchronizationContext();
      ClientIdent.m_Connected = true; // internal, from ActorOutput to WcfBasicClientAsync
      ClientIdent.LastSentId = 0; // ++ = 1 = connect
      WcfReqIdent id = new WcfReqIdent (ClientIdent, ClientIdent.OutputClientId, ++ClientIdent.LastRequestIdSent, null, null);
      m_ServiceReference.WcfOpenAsync (this, id); 
      // Callback to OnOpenCompleted when channel has been opened locally (no TCP connection opened on mono).
    }// OpenConnectionToService


    // Eventhandler, running on user thread, dispatched from m_ServiceReference.
    internal void OnOpenCompleted (object idObj)
    {
      WcfReqIdent id = idObj as WcfReqIdent;
      id.DestinationLambda = null; // our call has been reached.
      id.SourceLambda      = null;
      
      try
      {
          if (id.Message != null)
          {   // error while opening
              id.IsResponse = true;
              id.Sender = ServiceIdent;
              if( ServiceIdent.AddressList != null )
              {
                  ClientIdent.DefaultInputHandler( id ); // pass the negative feedback from real service to the handler in this class
              }
              else
              {
                  EndOfConnectionTries( id ); // enter 'faulted' state when eg. router not running
              }
          }
          else
          {
              string serviceAddr = GetSetServiceAddress();
              id.Message = new WcfPartnerMessage (ClientIdent, WcfPartnerMessage.Use.ClientConnectRequest);
  
              if (ClientIdent.TraceConnect) {
                  if( m_boTemporaryRouterConn ) WcfTrc.Info( id.CltSndId, string.Concat( "Temporary connecting .....: '", serviceAddr, "'" ), ClientIdent.Logger );
                                           else WcfTrc.Info( id.CltSndId, string.Concat( "Connecting svc: '", serviceAddr, "'" ), ClientIdent.Logger );
              }
              // send first connection request on user thread --> response will be received on this thread also
              m_ServiceReference.WcfRequestAsync (id);
          }
      }
      catch (Exception ex)
      {
          id.IsResponse = true;
          id.Sender = ServiceIdent;
          id.Message = new WcfErrorMessage (WcfErrorMessage.Code.CouldNotStartConnect, ex);
          EndOfConnectionTries (id); // enter 'faulted' state when eg. configuration is incorrect
      }
    }// OnOpened


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
          //  ClientIdent.Uri = new Uri ("http://"+ClientIdent.HostName+"/"+WcfDefault.WsNamespace
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
        // ClientIdent.Uri = new Uri ("http://"+ClientIdent.HostName+"/"+WcfDefault.WsNamespace
        //                              +string.Format ("/{0}/{1}", ClientIdent.AppIdentification, ClientIdent.Name));
        ServiceIdent.Uri = m_ServiceReference.Endpoint.Address.Uri;
        return m_ServiceReference.Endpoint.Address.ToString ();
#endif
    }


    //------------------------------
    // End of async Send/Receive process, running on user thread
    internal void OnRequestCompleted (object data)
    {
        OnRequestCompleted(data as WcfBasicClient.ReceivingState, ClientIdent.DispatchMessage);
    }

    internal void OnRequestCompleted(WcfBasicClient.ReceivingState x, WcfMessageHandler responseHandler)
    {
      try
      {
        IWcfMessage rsp = x.idRcv.Message;
        m_boTimeout    = x.timeout;
        // not streamed data, for tracing:
        x.idRcv.IsResponse = true;
        x.idRcv.Sender = ServiceIdent;
        x.idRcv.Input  = ClientIdent;

        if (!m_boTimeout)
        {
          WcfNotifyResponse multi = rsp as WcfNotifyResponse;
          if (multi != null)
          {
            WcfReqIdent idNfy = new WcfReqIdent (ServiceIdent, x.idRcv.ClientId, 0, null, x.idSnd.SourceLambda);
            idNfy.IsResponse  = true;
            idNfy.Input       = ClientIdent;
            foreach (IWcfMessage p in multi.Notifications)
            {
              idNfy.Message = p;
              OnWcfNotificationFromService(idNfy, responseHandler);
            }
            x.idRcv.Message = multi.Response;
          }

          rsp = CheckResponse (x.idRcv, x.idSnd, false);
        }

        if (rsp != null)
        {
          x.idSnd.Message       = rsp;
          x.idSnd.IsResponse    = true;
          x.idSnd.ClientId      = x.idRcv.ClientId;
          x.idSnd.RequestId     = x.idRcv.RequestId;
          x.idSnd.SendId        = x.idRcv.SendId;
          x.idSnd.Sender        = ServiceIdent;
          LastRequestIdReceived = x.idRcv.RequestId;
          if (ClientIdent.TraceReceive) WcfTrc.Info(x.idSnd.CltRcvId, x.idSnd.ToString(), ClientIdent.Logger);
          responseHandler(x.idSnd);
        }
      }
      catch (Exception ex)
      {
          WcfTrc.Exception( "Response to " + ClientIdent.Name + " cannot be handled by application", ex, ClientIdent.Logger );
      }
    }// OnRequestCompleted


    /// <summary>
    /// Handling of unexpected notification messages (not requested messages).
    /// </summary>
    /// <param name="notification">The unexpected message.</param>
    /// <param name="responseHandler">The application message handler for unexpected messages.</param>
    protected void OnWcfNotificationFromService(WcfReqIdent notification, WcfMessageHandler responseHandler)
    {
      try
      {
        //if (!m_boNotificationReceived)
        //{
        //  m_boNotificationReceived = true;
        //  TraceState("Notify");
        //}
        if (notification.ClientId != ClientIdent.OutputClientId)
        {
            WcfTrc.Error( notification.CltRcvId, string.Format( "Notification with wrong ClientId = {0}", notification.ClientId ), ClientIdent.Logger );
        }

        //if (notification.RequestId != 0)
        //{
        //  WcfTrc.Error (notification.CltRcvId, string.Format ("Notification with wrong RequestId = {0}", notification.RequestId));
        //}

        //if (LastSendIdReceived == uint.MaxValue) LastSendIdReceived = 10;
        ++LastSendIdReceived; // check it at last message of multi response
        //if (notification.SendId != ++LastSendIdReceived)
        //{
        //  WcfTrc.Warning (notification.CltRcvId, string.Format ("Expected SendId = {0}, got notification with {1}", LastSendIdReceived, notification.SendId));
        //  LastSendIdReceived = notification.SendId;
        //}

        var m = notification.Message as IExtensibleWcfMessage;
        if (!ClientIdent.IsMultithreaded)
        {
            if( m != null ) m.BoundSyncContext = SynchronizationContext.Current;
        }
        if( m != null ) m.IsSent = true;
        if( ClientIdent.TraceReceive ) WcfTrc.Info( notification.CltRcvId, notification.ToString(), ClientIdent.Logger );

        responseHandler(notification);
      }
      catch (Exception ex)
      {
          WcfTrc.Exception( "Notification to " + ClientIdent.Name + " cannot be handled by application", ex, ClientIdent.Logger );
      }
    }// OnWcfNotificationFromService


    /// <summary>
    /// This function is normally only used internally by OnRequestCompleted. It checks whether the response has to be handled by application code.
    /// </summary>
    /// <param name="result">received response</param>
    /// <param name="req">original request containing response handlers</param>
    /// <param name="cancelled">handshake has been aborted</param>
    /// <returns>null if response has been handled internally, !null, when response must be handled by application</returns>
    protected IWcfMessage CheckResponse (WcfReqIdent result, WcfReqIdent req, bool cancelled)
    {
      result.Sender = ServiceIdent;

      if (result.ClientId != ClientIdent.OutputClientId && ClientIdent.OutputClientId != 0)
      {
          WcfTrc.Error( req.CltRcvId, string.Format( "Received wrong ClientId = {0}", result.ClientId ), ClientIdent.Logger );
      }

      if (req.ClientId != ClientIdent.OutputClientId)
      {
          WcfTrc.Error( req.CltRcvId, string.Format( "Request should have ClientId = {0}", ClientIdent.OutputClientId ), ClientIdent.Logger );
      }

      if (result.RequestId != req.RequestId)
      {
          WcfTrc.Error( req.CltRcvId, string.Format( "Received wrong RequestId = {0}", result.RequestId ), ClientIdent.Logger );
      }

      if ( result.SendId != LastSendIdReceived + 1)
     //&&!(result.SendId == LastSendIdReceived && result.Message is WcfErrorMessage)) // no need for errormessages.MakeResponseTo (req)
      {
          WcfTrc.Warning( req.CltRcvId, string.Format( "Expected SendId = {0}, received = {1}", LastSendIdReceived + 1, result.SendId ), ClientIdent.Logger );
      }
      LastSendIdReceived = result.SendId;

      var m = result.Message as IExtensibleWcfMessage;
      if (!ClientIdent.IsMultithreaded)
      {
          if( m != null ) m.BoundSyncContext = SynchronizationContext.Current;
      }
      if( m != null ) m.IsSent = true;

      WcfPartnerMessage rsp = result.Message as WcfPartnerMessage;
      
      if (rsp != null)
      {
        if (rsp.Usage == WcfPartnerMessage.Use.ServiceConnectResponse)
        { // First message received from Service
          rsp.Uri = ServiceIdent.Uri; // keep the Uri used to request the message (maybe IP address instead of hostname used)
          ServiceIdent.UseDataFrom (rsp);
          ClientIdent.OutputClientId = result.ClientId; // defined by server
          OnConnectMessage (result);
        }
        else if (rsp.Usage == WcfPartnerMessage.Use.ServiceDisconnectResponse)
        {
            WcfTrc.Info( result.CltRcvId, rsp.ToString(), ClientIdent.Logger );//"Disconnected svc",0));
            return null;
        }
        else if (rsp.Usage == WcfPartnerMessage.Use.ServiceAddressResponse
              || rsp.Usage == WcfPartnerMessage.Use.ServiceEnableResponse
              || rsp.Usage == WcfPartnerMessage.Use.ServiceDisableResponse)
        {
          // service address management
        }
        else
        {
            WcfTrc.Error( result.CltRcvId, "Unknown use of " + rsp.ToString(), ClientIdent.Logger );
        }
      }
      else if (cancelled)
      {
          WcfTrc.Error( result.CltRcvId, string.Format( "Request cancelled, SendId = {0}", result.SendId ), ClientIdent.Logger );
          return null;
      }

      return result.Message; // Message must be handled by application
    }// CheckResponse


    // Implements the connect message handling for WcfClientAsync.
    // 1. call from WcfRouterService
    // 2. call from looked up service
    // the same message will be sent to OnWcfResponseFromRouterService or to application later on.
    internal void OnConnectMessage(WcfReqIdent id)
    {
        if (m_RouterHostToLookup == null || ServiceIdent.Name == m_ServiceNameToLookup)
        {
            WcfRouterClient.Instance ().AddClient (this);
            m_boFirstResponseReceived = true; // IsConnected --> true !
            m_boConnecting = false;
            if( ClientIdent.TraceConnect ) WcfTrc.Info( id.CltRcvId, ServiceIdent.ToString( "Connected  svc", 0 ), ClientIdent.Logger );
            //TraceState("Opened");
        }
    }


    // Response callback from WcfRouterService
    private void OnWcfResponseFromRouterService(WcfReqIdent rsp)
    {
        WcfPartnerMessage svcRsp = rsp.Message as WcfPartnerMessage;
        if (svcRsp != null && svcRsp.Usage == WcfPartnerMessage.Use.ServiceConnectResponse)
        {
            if( ClientIdent.TraceSend ) WcfTrc.Info( rsp.CltRcvId, "Temporary connected router: '" + svcRsp.Name + "' on '" + svcRsp.HostName + "'", ClientIdent.Logger );
            ActorPort lookup = new ActorPort();
            lookup.HostName = m_RouterHostToLookup;
            lookup.Name = m_ServiceNameToLookup;
            lookup.IsServiceName = true;
            WcfPartnerMessage req = new WcfPartnerMessage(lookup, WcfPartnerMessage.Use.ServiceAddressRequest);
            SendOut(req); // lookup the service URI (especially the TCP port)
        }
        else if (svcRsp != null && svcRsp.Usage == WcfPartnerMessage.Use.ServiceAddressResponse)
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
                WcfTrc.Info( rsp.CltRcvId, "ServiceAddressResponse: " + svcRsp.Uri + s, ClientIdent.Logger );
            }
            m_addressesTried = 0;
            OnConnectionResponseFromService( null ); // try first address
        }
        else
        {
            WcfErrorMessage err = rsp.Message as WcfErrorMessage;
            if (err != null)
            {
                //WcfTrc.Warning (rsp.CltRcvId, "Router "+rsp.ToString());
                if (err.Error == WcfErrorMessage.Code.ServiceNotRunning)
                {
                    err.Error = WcfErrorMessage.Code.RouterNotRunning;
                }
            }
            else
            {
                WcfTrc.Error( rsp.CltRcvId, "Receiving unexpected response from WcfRouterService: " + rsp.ToString(), ClientIdent.Logger );
                rsp.Message = new WcfErrorMessage(WcfErrorMessage.Code.CouldNotConnectRouter,
                                                    "Unexpected response from WcfRouterService");
            }
            EndOfConnectionTries( rsp ); // failed at router
        }
    }// OnWcfResponseFromRouterService


    // Response callback from real service
    private void OnConnectionResponseFromService( WcfReqIdent rsp )
    {
        if( m_boFirstResponseReceived )
        {
            EndOfConnectionTries( rsp ); // successful !
            return;
        }

        if( rsp != null )
        {
            m_addressNumber++; // connection failed, next time try next address. 
            var err = rsp.Message as WcfErrorMessage;
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
        TryConnectVia( b.Uri, OnConnectionResponseFromService, toRouter:false );
    }


    private void EndOfConnectionTries( WcfReqIdent rsp )
    {
        m_boTimeout = !m_boFirstResponseReceived; // Fault state when not correct response in OnConnectMessage
        if( m_boTemporaryRouterConn )
        {
            ClientIdent.TraceConnect = m_TraceConnectBefore;
        }
        ClientIdent.DefaultInputHandler = m_DefaultInputHandlerForApplication;
        try
        {
            ClientIdent.DefaultInputHandler( rsp ); // pass the negative or positive feedback from router or real service to the application
        }
        catch( Exception ex )
        {
            WcfTrc.Exception( "Connect message to " + ClientIdent.Name + " cannot be handled by application", ex, ClientIdent.Logger );
        }

        if( m_boTimeout && m_boTemporaryRouterConn )
        {
          ServiceIdent.PrepareServiceName( m_RouterHostToLookup, m_ServiceNameToLookup ); // prepare for next connect try
        }
        m_boTemporaryRouterConn = false;
    }


    #endregion
    //----------------------------------------------------------------------------------------------
    #region IWcfBasicPartner implementation

    /// <summary>
    /// Gets or sets the state of the outgoing connection. May be called on any thread.
    /// </summary>
    /// <returns>A <see cref="WcfState"/></returns>
    public WcfState OutputState
    {
      get
      {
        if (IsConnected)    return WcfState.Ok;
        if (IsDisconnected) return WcfState.Disconnected;
        if (IsFaulted)      return WcfState.Faulted;
        return WcfState.Connecting;
      }
      set
      {
        if (value == WcfState.Connecting || value == WcfState.Ok)
        {
          if (ClientIdent.IsMultithreaded || ClientIdent.SyncContext != null)
          {
            TryConnect();
          }
          else
          {
            throw new Exception("AsyncWcfLib: TryConnect of '"+ClientIdent.Name+"' has not been called to pick up the synchronization context.");
          }
        }
        else if (value == WcfState.Faulted)
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
    /// Connect or reconnect output to the previously linked partner.
    /// </summary>
    /// <returns>false, when the connection may not be started.</returns>
    public virtual bool TryConnect()
    {
        if (!(IsDisconnected || IsFaulted)) return true;  // already connected or connecting

        try
        {
            if (m_RouterHostToLookup != null)
            {
                // connect to router first
                var uri = new Uri("http://" + m_RouterHostToLookup + ':' + m_WcfRouterPort + "/" + WcfDefault.WsNamespace + "/" + WcfDefault.Instance.RouterServiceName);
                TryConnectVia (uri, OnWcfResponseFromRouterService, toRouter:true );
                return true;
            }
            else
            {
                // do not connect to router
                LinkToService( m_RequestedServiceUri );
                ClientIdent.PickupSynchronizationContext();
                OpenConnectionToService();
                return true; // Connecting now
            }
        }
        catch (Exception ex)
        {
            WcfTrc.Exception( "Cannot open Wcf connection(3)", ex, ClientIdent.Logger );
            m_boTimeout = true; // enter 'faulted' state when eg. configuration is incorrect
            return false;
        }
    }// TryConnect


    private void OpenConnectionToService(object dummy)
    {
        OpenConnectionToService(); // 2. try on the right sync context
    }

    /// <summary>
    /// Post a request to the input of the remote partner. It will be sent over the network.
    /// Called from ClientIdent, when SendOut a message to remote partner.
    /// </summary>
    /// <param name="id">A <see cref="WcfReqIdent"/></param>
    public virtual void PostInput (WcfReqIdent id)
    {
      WcfErrorMessage err = null;

      if (!IsFaulted && ClientIdent.OutputClientId > 0) // Send() may be used during connection buildup as well
      {
        try
        {
          if( ClientIdent.TraceSend ) WcfTrc.Info( id.CltSndId, id.ToString(), ClientIdent.Logger );
          // send operation is threadsave. May be executed on any thread. Response is routed to id.Sender.SyncContext.
          m_ServiceReference.WcfRequestAsync (id);
        }
        catch (Exception ex)
        {
          err = new WcfErrorMessage (WcfErrorMessage.Code.CouldNotStartSend, ex);
        }
      }
      else
      {
        err = new WcfErrorMessage (WcfErrorMessage.Code.NotConnected, "Cannot send");
      }

      if (err != null)
      {
        id.SendResponseFrom (ServiceIdent, err, null);
      }
    }

    /// <summary>
    /// the intuitive action is to send the message to remote service.
    /// </summary>
    /// <param name="id">A <see cref="WcfReqIdent"/></param>
    public void SendOut (WcfReqIdent id)
    {
      PostInput(id);
    }

    /// <summary>
    /// <para>Send a message to the service. Do not wait here for the response.</para>
    /// <para>The OnWcfMessageReceivedDelegate is called on the same thread,</para>
    /// <para>when a response or errormessage arrives or a timeout has passed.</para>
    /// </summary>
    /// <param name="request">The message to send.</param>
    public WcfReqIdent SendOut (IWcfMessage request)
    {
      return SendOut (request, null);
    }

    /// <summary>
    /// <para>Send a message to the service. Do not wait here for the response.</para>
    /// <para>The response is asynchronously passed to the extension method "On", normally used as asyncResponseHandler.</para>
    /// <para>If the sending thread has a message queue, the response is executed by the same thread as the send operation was.</para>
    /// <para>If the response could not be handled by the On-extension methods, the default OnWcfMessageReceivedDelegate passed to TryConnect() is called.</para>
    /// <para>Example:</para>
    /// <para>Send (request, rsp => rsp.On&lt;WcfIdleMessage>(idle => {do something with idle message 'idle'})</para>
    /// <para>.On&lt;WcfErrorMessage>(err => {do something with error message 'err'}));</para>
    /// </summary>
    /// <param name="request">The message to send.</param>
    /// <param name="asyncResponseHandler"><see cref="WcfExtensionMethods.On&lt;T>(WcfReqIdent,Action&lt;T>)"/></param>
    public WcfReqIdent SendOut (IWcfMessage request, AsyncResponseHandler asyncResponseHandler)
    {
      if (ClientIdent.LastRequestIdSent == uint.MaxValue) ClientIdent.LastRequestIdSent = 10;
      WcfReqIdent id = new WcfReqIdent (ClientIdent, ClientIdent.OutputClientId, ++ClientIdent.LastRequestIdSent, request, asyncResponseHandler);
      PostInput  (id);
      return id;
    }

    /// <summary>
    /// Gets the Uri of a linked service.
    /// </summary>
    public Uri Uri {get{return ServiceIdent.Uri;}}

    #endregion
  }//   class WcfBasicClientAsync
}// namespace
