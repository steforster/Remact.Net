
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.ServiceModel;
using System.Collections.Generic;
using System.Text;
using System.Net;         // IPEndPoint
using System.Net.Sockets; // Socket
using System.Threading;   // SynchronizationContext
#if !BEFORE_NET40
    using System.Threading.Tasks;
#endif


namespace SourceForge.AsyncWcfLib.Basic
{
  /// <summary>
  /// <para>Adds the following features to a WCF service:</para>
  /// <para>- registered with WcfRouterService (default = 'http://localhost:40000/AsyncWcfLib/RouterService').</para>
  /// <para>- automatic TCP port assignement</para>
  /// <para>- automatic handling of the ServiceHost</para>
  /// <para>- coordinated shutdown of all ServiceAssistants</para>
  /// </summary>
  public class WcfServiceAssistant: WcfBasicService
  {
    //----------------------------------------------------------------------------------------------
    #region == Constructors and Destructors ==

    /// <summary>
    /// <para>Initializes a new instance of the WcfServiceAssistant class.</para>
    /// <para>The service is uniquely identified by the service name.</para>
    /// </summary>
    /// <param name="serviceIdent">This WcfPartners input is linked to network.</param>
    /// <param name="tcpPort">The TCP port for the service or 0, when automatic port allocation may be used.</param>
    /// <param name="publishToRouter">True(=default): The servicename will be published to the WcfRouter on localhost.</param>
    /// <param name="serviceConfig">Plugin your own service configuration instead of WcfDefault.ServiceConfiguration.</param>
    internal WcfServiceAssistant(ActorInput serviceIdent, int tcpPort = 0, bool publishToRouter = true,
                                 IWcfServiceConfiguration serviceConfig = null )
      : base (serviceIdent, /*firstClient=*/1, /*maxClients=*/20)
    {
        m_nTcpPort = tcpPort;
        m_boPublishToRouter = publishToRouter;
        m_WcfServiceConfig = serviceConfig;
        if( m_WcfServiceConfig == null )
        {
            m_WcfServiceConfig = WcfDefault.Instance;
        }
    }// CTOR
    

    /// <summary>
    /// <para>Shutdown this service and release all attached resources</para>
    /// <para>(ServiceHost, RouterClient + WcfRouterService entry)</para>
    /// <para>Send service disable message to WcfRouterService if possible</para>
    /// </summary>
    internal override void Disconnect()
    {
      try
      {
        if (m_ServiceHost != null)
        {
          base.AbortUserNotificationChannels();
          if (m_ServiceHost.State == CommunicationState.Opened)
          {
            try
            {
              if (m_nTcpPort == ms_nSharedTcpPort && ms_nSharedTcpPortCount > 0) 
              {
                ms_nSharedTcpPortCount--;
                m_nTcpPort = 0;
              }
              m_ServiceHost.Close (TimeSpan.FromMilliseconds(100));
              m_ServiceHost = null;
            }
            catch
            {
            }
          }

          if (m_ServiceHost != null)
          {
            m_ServiceHost.Abort();
            m_ServiceHost = null;
          }
        }
        
        WcfRouterClient.Instance().RemoveService (this); // send disable message to WcfRouterService

        if( base.ServiceIdent.Uri != null ) WcfTrc.Info( "Wcf", "Closed service " + base.ServiceIdent.Uri, base.ServiceIdent.Logger );
                                       else WcfTrc.Info( "Wcf", "Closed service " + base.ServiceIdent.Name, base.ServiceIdent.Logger );
        base.Disconnect();
      }
      catch (Exception ex)
      {
          WcfTrc.Exception( "Svc: Error while closing the service", ex, base.ServiceIdent.Logger );
      }
    }// Disconnect
    
    
    #endregion
    //----------------------------------------------------------------------------------------------
    #region == Public Types, Properties and Methods ==

    /// <summary>
    /// Returns true, when service is ready to receive requests.
    /// </summary>
    public bool IsOpen {get{return m_ServiceHost != null && m_ServiceHost.State == CommunicationState.Opened;}}

    private int                      m_nTcpPort;
    private bool                     m_boPublishToRouter;
    private IWcfServiceConfiguration m_WcfServiceConfig;

    private static int ms_nSharedTcpPort;
    private static int ms_nSharedTcpPortCount;
    private static object ms_Lock = new Object();
    private static Dictionary<Uri, WcfServiceAssistant> ServiceMap
              = new Dictionary<Uri, WcfServiceAssistant>(20);

    /// <summary>
    /// Gets or sets the state of the incoming service connection from the network.
    /// </summary>
    /// <returns>A <see cref="WcfState"/></returns>
    public WcfState InputStateFromNetwork
    {
      get
      {
        if (m_ServiceHost == null) return WcfState.Disconnected;
        if (m_ServiceHost.State == CommunicationState.Opened) return WcfState.Ok;
        return WcfState.Faulted;
      }
      set
      {
        if (value == WcfState.Ok || value == WcfState.Connecting)
        {
          if (!IsOpen) OpenService ();
        }
        else
        {
          Disconnect();
        }
      }
    }

    /// <summary>
    /// <para>** IsMultithreaded==FALSE **  = default on ActorPort</para>
    /// <para>Create and open a ServiceHost running a threadsafe IWcfBasicContractSync-singleton service.</para>
    /// <para>These services must be very fast and may only access memory. No file- or database-access and no other synchronous calls are allowed.</para>
    /// <para>Calls to the message handler are made from the same thread (synchronization context) that is used now to open the service.</para>
    /// <para>An exception is thrown, when your opening thread has no message queue.</para>
    /// <para></para>
    /// <para>** IsMultithreaded==TRUE **</para>
    /// <para>Create and open a ServiceHost running a IWcfBasicContractSync instance for each session in parallel.</para>
    /// <para>These services may be relativly slow, when accessing files, databases or doing other synchronous calls.</para>
    /// <para>Calls to the message handler are made from different threads, several clients may run in parallel</para>
    /// <para>but only one thread at a time is accessing the client and user context.</para>
    /// <para></para>
    /// <para></para>
    /// <para>When there exists no [service name="ConcreteTypeOfServiceInstance"] entry in the App.config file,</para>
    /// <para>or the entry has no endpoint (apart from a possible "mex" endpoint),</para>
    /// <para>the WcfServiceAssistant creates a standard service URI containig the next free TCP port and the service name.</para>
    /// <para>E.g. "http://host:1234/AsyncWcfLib/ServiceName"</para>
    /// <para>It registeres the service with WcfRouterService, so clients can find the dynamically generated TCP port.</para>
    /// </summary>
    /// <returns>true if successfully open</returns>
    internal bool OpenService()
    {
      try
      {
        if (m_ServiceHost != null) m_ServiceHost.Abort();
        
        // attach implementation to servicehost and read existing endpoints from App.config file:
        // <service name="TypeName">,  TypeName = e.g. "Company.Product.Service.ServiceClass"
        // so it is possible to add the mex endpoint:
        //  <service behaviorConfiguration="IldServiceBehavior"... 
        //    <endpoint address="mex" binding="mexHttpBinding" name="mex" contract="IMetadataExchange" />
        //  <behaviors>
        //    <serviceBehaviors>
        //      <behavior name="IldServiceBehavior">
        //        <!-- To avoid disclosing metadata information, 
        //        set the value below to false and remove the metadata endpoint above before deployment -->
        //        <serviceMetadata httpGetEnabled="True"/>

#if BEFORE_NET40
        // for .NET 3.5: synchronous service
        if (ServiceIdent.IsMultithreaded)
        {
            m_ServiceHost = new ServiceHost(typeof(InternalMultithreadedService));
        }
        else
        {
            // we open a singleton service, always using the same synchronization context 
            m_ServiceHost = new ServiceHost(new InternalSinglethreadedService(this));
        }
#else
        // for .NET 4.0 and Mono: async service, but not using async await keywords
        m_ServiceHost = new ServiceHost( typeof( InternalMultithreadedServiceNet40 ) ); 
#endif

        // Do we have to add a dynamically generated endpoint ?
        if (m_ServiceHost.Description.Endpoints.Count == 0
        || (m_ServiceHost.Description.Endpoints.Count == 1 && m_ServiceHost.Description.Endpoints[0].Name.ToLower() == "mex"))
        {
          if (m_nTcpPort == 0)
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
              m_nTcpPort = endpoint.Port;
#endif
              socket.Close(); // socket.Shutdown is not allowed as we are not yet connected
            }
#if !MONO
            m_nTcpPort = ms_nSharedTcpPort;
            ms_nSharedTcpPortCount++;
#endif
          }

          // Set URI before the first request arrives (as in WcfBaseService). 
          // The URI will be sent to WcfRouter for registration.
          Uri uri = new Uri ("http://"
                + base.ServiceIdent.HostName     // initialized with Dns.GetHostName()
                +":"+m_nTcpPort
                +"/"+WcfDefault.WsNamespace+"/"+base.ServiceIdent.Name);// ServiceName, not the ServiceType

          // Add the dynamically created endpoint. And let the library user add binding and security credentials.
          // By default WcfDefault.DoServiceConfiguration is called.
          m_WcfServiceConfig.DoServiceConfiguration( m_ServiceHost, ref uri, /*isRouter=*/false );
          base.ServiceIdent.Uri = uri;
        }
        else
        {
          // Set configured URI so it can be sent to WcfRouter for registration.
          // TODO: ServiceIdent.Name is used as identification in WcfRouter - should it be changed now ???
          //       or should the Uri be changed / created from different fields ???
          UriBuilder uri = new UriBuilder (m_ServiceHost.Description.Endpoints[0].ListenUri);
          uri.Host = base.ServiceIdent.HostName; // initialized with Dns.GetHostName(), replaces "localhost"
          base.ServiceIdent.Uri = uri.Uri;
        }
        
        lock (ms_Lock)
        {
            ServiceMap [base.ServiceIdent.Uri] = this; // adds new key if not present yet
        }

        // Open the ServiceHost to start listening for messages.
        m_ServiceHost.Open();

        if (m_boPublishToRouter)
        {
            // Start registering on WcfRouter
            WcfRouterClient.Instance().AddService(this);
        }
        
        // The service can now be accessed, but must be registered.
        WcfTrc.Info( "Wcf", "Opened service " + base.ServiceIdent.Uri, base.ServiceIdent.Logger );
        return true;
      }
      catch (Exception ex)
      {
          WcfTrc.Exception( "could not open " + base.ServiceIdent.Name, ex, base.ServiceIdent.Logger );
          base.LastAction = ex.Message;
      }
      return false;
    }// OpenService
    
    #endregion
    //----------------------------------------------------------------------------------------------
    #region == Internal Fields and Methods ==

    private  ServiceHost            m_ServiceHost = null;

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == Internal singlethreaded service class ==

#if BEFORE_NET40
    /// <summary>
    /// This is the Service Entrypoint. It dispatches requests and returns a response.
    /// </summary>
    [ServiceBehavior (InstanceContextMode = InstanceContextMode.Single,
                      ConcurrencyMode     = ConcurrencyMode.Multiple, // when async-await is used, multiple interleaving operations are executed on the same thread.
                      UseSynchronizationContext = true)]

    private class InternalSinglethreadedService: IWcfBasicContractSync
    {
      private WcfServiceAssistant m_myServiceAssistant;
       
      // CTOR
      public InternalSinglethreadedService (WcfServiceAssistant myAssistant)
      {
        m_myServiceAssistant = myAssistant;
      }

  #if !MONO
      // The legacy IWcfBasicServiceSync entrypoint
      public IWcfMessage WcfRequest (IWcfMessage msg, ref WcfReqIdent id)
      {
          id.Message = msg; // Message is not a [DataMember] of WcfReqIdent.
          try
          {
              WcfBasicServiceUser svcUser;
              var response = m_myServiceAssistant.CheckBasicResponse(id, out svcUser);
              if (response != null)
              {
                  if (response is WcfPartnerMessage) // no error
                  {
                      // call event OnInputConnected or OnInputDisconnected
                      id.Input.DispatchMessage(id);// execute request synchronously on this thread
                  }
              }
              else
              {
                  svcUser.StartNewRequest(id);
                  id.Input.DispatchMessage(id);// execute request synchronously on this thread
                  response = svcUser.GetNotificationsAndResponse(ref id);
              }
              id.Message = response;
          }
          catch (Exception ex)
          {
              WcfTrc.Exception(id.SvcRcvId, ex, m_myServiceAssistant.ServiceIdent.Logger);
              id.Message = new WcfErrorMessage(WcfErrorMessage.Code.UnhandledExceptionOnService, ex);
              if (id.Sender != null) id.SendId = ++id.Sender.LastSentId;
          }
          return id.Message;
      }
  #else
      // Mono seems not to respect the UseSynchronizationContext attribute of a singleton service!

      // The legacy IWcfBasicServiceSync entrypoint
      public IWcfMessage WcfRequest (IWcfMessage msg, ref WcfReqIdent o_reqIdent)
      {
          o_reqIdent.Message = msg;
          var id = o_reqIdent;
          try
          {
              // execute synchronously on the specified thread after passing the delegate through the message queue...
              m_myServiceAssistant.ServiceIdent.SyncContext.Send (DoRequest, id);
              // our thread has been blocked until the other has returned the response
          }
          catch( Exception ex )
          {
              WcfTrc.Exception( id.SvcRcvId, ex, m_myServiceAssistant.ServiceIdent.Logger );
              id.Message = new WcfErrorMessage( WcfErrorMessage.Code.UnhandledExceptionOnService, ex );
              if( id.SendingP != null ) id.SendId = ++id.SendingP.LastSentId;
          }

          o_reqIdent = id;
          return o_reqIdent.Message;
      }

      private void DoRequest( object obj )
      {
          var id = obj as WcfReqIdent; // contains reference to response
          try
          {
            WcfBasicServiceUser svcUser;
            var response = m_myServiceAssistant.CheckBasicResponse( id, out svcUser );
            if( response != null )
            {
                if( response is WcfPartnerMessage ) // no error
                {
                    // call event OnInputConnected or OnInputDisconnected
                    id.Input.DispatchMessage (id);// execute request synchronously on this thread
                }
            }
            else
            {
                svcUser.StartNewRequest( id );
                id.Input.DispatchMessage (id);// execute request synchronously on this thread
                response = svcUser.GetNotificationsAndResponse( ref id );
            }
            id.Message = response;
          }
          catch (Exception ex)
          {
              WcfTrc.Exception( id.SvcRcvId, ex, m_myServiceAssistant.ServiceIdent.Logger );
              id.Message = new WcfErrorMessage( WcfErrorMessage.Code.UnhandledExceptionOnService, ex );
              if( id.SendingP != null ) id.SendId = ++id.SendingP.LastSentId;
          }
          // obj and id contain reference to response
      }
  #endif
    }//class InternalSinglethreadedService
#endif // BEFORE_NET40


    #endregion
    //----------------------------------------------------------------------------------------------
    #region == Internal multithreaded service class ==

#if BEFORE_NET40
    /// <summary>
    /// This is the Service Entrypoint. It dispatches requests and returns a response.
    /// Synchronization, see "Programming WCF Services" by Juval Löwi, 
    /// chapters "Resources and Services", "Resource Synchronization Context", "Service Synchronization Context"
    /// </summary>
    [ServiceBehavior( InstanceContextMode = InstanceContextMode.PerSession,
                      ConcurrencyMode = ConcurrencyMode.Multiple, // any difference to 'Single', when UseSynchronizationContext is true ?
                      UseSynchronizationContext = false)]
                    //UseSynchronizationContext = true )] // async-await allows interleving operations on one thread.

    private class InternalMultithreadedService: IWcfBasicContractSync
    {
      private WcfServiceAssistant m_myServiceAssistant = null;

      // The legacy IWcfBasicServiceSync entrypoint
      public IWcfMessage WcfRequest (IWcfMessage msg, ref WcfReqIdent id)
      {
          // before V3.0 Message was not a [DataMember] of WcfReqIdent. Now, msg is transfered two times, when sending through this legacy interface!
          id.Message = msg;
          IWcfMessage response = null;
          try
          {
              WcfBasicServiceUser svcUser;
              lock( ms_Lock )
              {
                  // first request of a session ?
                  if( m_myServiceAssistant == null )
                  {
                      m_myServiceAssistant = InputDispatcher.ServiceMap [OperationContext.Current.Channel.LocalAddress.Uri];
                  }

                  response = m_myServiceAssistant.CheckBasicResponse( id, out svcUser );
              }

              // multithreaded access, several clients may run in parallel but only one thread at a time may access the client and user context.
              lock( svcUser.ClientAccessLock )
              {
                  if( response != null )
                  {
                      if( response is WcfPartnerMessage ) // no error
                      {
                          // call event OnInputConnected or OnInputDisconnected
                          id.Input.DispatchMessage( id );// execute synchronously on this thread
                      }
                  }
                  else
                  {
                      svcUser.StartNewRequest( id );
                      id.Input.DispatchMessage( id );// execute request synchronously on this thread
                      response = svcUser.GetNotificationsAndResponse( ref id );
                  }
              }
          }
          catch( Exception ex )
          {
              WcfTrc.Exception( id.SvcRcvId, ex, m_myServiceAssistant.ServiceIdent.Logger );
              response = new WcfErrorMessage( WcfErrorMessage.Code.UnhandledExceptionOnService, ex );
              if (id.Sender != null) id.SendId = ++id.Sender.LastSentId;
          }

          id.Message = response;
          return id.Message;
      }
    }//class InternalMultithreadedService
#endif // BEFORE_NET40


    #endregion
    //----------------------------------------------------------------------------------------------
    #region == Internal multithreaded service class for .NET framework 4.0 and Mono ==


#if !BEFORE_NET40
    /// <summary>
    /// This is the Service Entrypoint. It dispatches requests and returns a response.
    /// Synchronization, see "Programming WCF Services" by Juval Löwi, 
    /// chapters "Resources and Services", "Resource Synchronization Context", "Service Synchronization Context"
    /// </summary>
    [ServiceBehavior( InstanceContextMode = InstanceContextMode.PerSession,
                      ConcurrencyMode = ConcurrencyMode.Multiple,
                      UseSynchronizationContext = false )] // every request comes on its own threadpool thread.

    private class InternalMultithreadedServiceNet40 : IWcfBasicContractSync
    {
        private WcfServiceAssistant m_myServiceAssistant = null;

        // The legacy IWcfBasicServiceSync entrypoint
        public IWcfMessage WcfRequest( IWcfMessage msg, ref WcfReqIdent id )
        {
            // before V3.0 Message was not a [DataMember] of WcfReqIdent. Now, msg is transfered two times, when sending through this legacy interface!
            id.Message = msg;
            IWcfMessage response = null;
            try
            {
                WcfBasicServiceUser svcUser;
                lock( ms_Lock )
                {
                    // first request of a session ?
                    if( m_myServiceAssistant == null )
                    {
                        m_myServiceAssistant = ServiceMap[OperationContext.Current.Channel.LocalAddress.Uri];
                    }

                    response = m_myServiceAssistant.CheckBasicResponse( id, out svcUser );
                }

                // multithreaded access, several requests may run in parallel. They are scheduled for execution on the right synchronization context.
                if( response != null )
                {
                    var connectMsg = response as WcfPartnerMessage;
                    if (connectMsg != null) // no error and connected
                    {
                        id.Sender.LastSentId--; // correct for reqCopy
                        var reqCopy = new WcfReqIdent(id.Sender, id.ClientId, id.RequestId, id.Message, id.SourceLambda);
                        reqCopy.Response = reqCopy; // do not send a WcfIdleMessage
                        reqCopy.Input = id.Input;
                        var task = DoRequestAsync( reqCopy ); // call event OnInputConnected or OnInputDisconnected on the correct thread.
                        if (connectMsg.Usage != WcfPartnerMessage.Use.ServiceDisconnectResponse)
                        {
                            var dummy = task.Result; // blocking wait!
                        }
                        // return the original response.
                    }
                }
                else
                {
                    svcUser.StartNewRequest( id );
                    var task = DoRequestAsync( id );
                    id = task.Result; // blocking wait!
                    response = svcUser.GetNotificationsAndResponse( ref id ); // changes id
                }
            }
            catch( Exception ex )
            {
                WcfTrc.Exception( id.SvcRcvId, ex, m_myServiceAssistant.ServiceIdent.Logger );
                response = new WcfErrorMessage( WcfErrorMessage.Code.UnhandledExceptionOnService, ex );
                if (id.Sender != null) id.SendId = ++id.Sender.LastSentId;
            }

            id.Message = response;
            return id.Message;
        }



        private Task<WcfReqIdent> DoRequestAsync( WcfReqIdent id )
        {
            var tcs = new TaskCompletionSource<WcfReqIdent>();

            if( id.Input.IsMultithreaded
             || id.Input.SyncContext == null
             || id.Input.ManagedThreadId == Thread.CurrentThread.ManagedThreadId )
            { // execute request on the calling thread or multi-threaded
            #if !BEFORE_NET45
                id.Input.DispatchMessageAsync( id )
                  .ContinueWith((t)=>
                      tcs.SetResult( id )); // when finished the first task: finish tcs and let the original request thread return the response.
            #else
                id.Input.DispatchMessage( id );
                tcs.SetResult( id );
            #endif
            }
            else
            {
                Task.Factory.StartNew(() => 
                    id.Input.SyncContext.Post( obj =>
                    {   // execute task on the specified thread after passing the delegate through the message queue...
                        try
                        {
                #if !BEFORE_NET45
                            id.Input.DispatchMessageAsync( id )        // execute request async on the thread bound to the Input
                              .ContinueWith((t)=>
                                  tcs.SetResult( id )); // when finished the first task: finish tcs and let the original request thread return the response.
                #else
                            id.Input.DispatchMessage( id );// execute request synchronously on the thread bound to the Input
                            tcs.SetResult( id );
                #endif
                        }
                        catch( Exception ex )
                        {
                            WcfTrc.Exception( "Request to " + id.Input.Name + " cannot be handled by application", ex );
                            tcs.SetException( ex );
                        }
                    }, null )); // obj
            }

            return tcs.Task;
        }

    }//class InternalMultithreadedServiceNet40
#endif // !BEFORE_NET40


  #endregion
  }//class WcfServiceAssistant
  //----------------------------------------------------------------------------------------------
}//namespace
