
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;         // IPEndPoint
using System.Net.Sockets; // Socket
using System.Threading;   // SynchronizationContext
#if !BEFORE_NET40
    using System.Threading.Tasks;
#endif
using Alchemy;
using Alchemy.Classes;
using Remact.Net.Protocol;
using Remact.Net.Protocol.Wamp;



namespace Remact.Net.Internal
{
    /// <summary>
    /// <para>Adds the following features to a Alchemy.WebSocketServer:</para>
    /// <para>- registered with Remact.Catalog (default = 'ws://localhost:40000/').</para>
    /// <para>- automatic TCP port assignement</para>
    /// <para>- handling of new connected clients</para>
    /// <para>- coordinated shutdown of all Services</para>
    /// </summary>
    public class WebSocketService : WcfBasicService
    {
        //----------------------------------------------------------------------------------------------
        #region == Constructors and Destructors ==

        /// <summary>
        /// <para>Initializes a new instance of the WebSocketService class.</para>
        /// <para>The service is uniquely identified by the service name.</para>
        /// </summary>
        /// <param name="serviceIdent">This ActorInput is linked to network.</param>
        /// <param name="tcpPort">The TCP port for the service or 0, when automatic port allocation may be used.</param>
        /// <param name="publishToRouter">True(=default): The servicename will be published to the Remact.Catalog on localhost.</param>
        /// <param name="serviceConfig">Plugin your own service configuration instead of RemactDefaults.ServiceConfiguration.</param>
        internal WebSocketService(ActorInput serviceIdent, int tcpPort = 0, bool publishToRouter = true,
                                  IActorInputConfiguration serviceConfig = null )
            : base (serviceIdent, /*firstClient=*/1, /*maxClients=*/20)
        {
            _tcpPort = tcpPort;
            _publishToRouter = publishToRouter;
            _serviceConfig = serviceConfig;
            if( _serviceConfig == null )
            {
                _serviceConfig = RemactDefaults.Instance;
            }
        }// CTOR
    

        /// <summary>
        /// <para>Shutdown this service and release all attached resources</para>
        /// <para>Send service disable message to Remact.Catalog if possible</para>
        /// </summary>
        internal override void Disconnect()
        {
            try
            {
                if (_wsServer != null)
                {
                    base.AbortUserNotificationChannels();
                    try
                    {
                        if (_tcpPort == ms_nSharedTcpPort && ms_nSharedTcpPortCount > 0) 
                        {
                            ms_nSharedTcpPortCount--;
                            _tcpPort = 0;
                        }
                        _wsServer.Stop();
                        _wsServer.Dispose();
                    }
                    catch
                    {
                    }

                    _wsServer = null;
                }
        
                WcfRouterClient.Instance().RemoveService (this); // send disable message to WcfRouterService

                if( base.ServiceIdent.Uri != null ) RaTrc.Info( "Wcf", "Closed service " + base.ServiceIdent.Uri, base.ServiceIdent.Logger );
                                               else RaTrc.Info( "Wcf", "Closed service " + base.ServiceIdent.Name, base.ServiceIdent.Logger );
                base.Disconnect();
            }
            catch (Exception ex)
            {
                RaTrc.Exception( "Svc: Error while closing the service", ex, base.ServiceIdent.Logger );
            }
        }// Disconnect
    
    
        #endregion
        //----------------------------------------------------------------------------------------------
        #region == Properties and public methods ==

        private int                      _tcpPort;
        private bool                     _publishToRouter;
        private IActorInputConfiguration _serviceConfig;
        private WebSocketServer          _wsServer;

        private static int ms_nSharedTcpPort;
        private static int ms_nSharedTcpPortCount;
        private static object ms_Lock = new Object();
        private static Dictionary<Uri, WebSocketService> ms_serviceMap = new Dictionary<Uri, WebSocketService>(20);

        /// <summary>
        /// Returns true, when service is ready to receive requests.
        /// </summary>
        public bool IsOpen { get { return _wsServer != null; } }


        /// <summary>
        /// Gets or sets the state of the incoming service connection from the network.
        /// </summary>
        /// <returns>A <see cref="PortState"/></returns>
        public PortState InputStateFromNetwork
        {
            get
            {
                if (_wsServer == null) return PortState.Disconnected;
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
                if (_wsServer != null) Disconnect();
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
                    // Set URI before the first request arrives (as in WcfBaseService). 
                    // The URI will be sent to WcfRouter for registration.
                    Uri uri = new Uri ("ws://"
                        + base.ServiceIdent.HostName     // initialized with Dns.GetHostName()
                        +":"+_tcpPort
                        +"/"+RemactDefaults.WsNamespace+"/"+base.ServiceIdent.Name);// ServiceName, not the ServiceType

                    // Open the ServiceHost to start listening for messages.
                    _wsServer = new WebSocketServer()
                    {
                        Port = _tcpPort,
                        SubProtocols = new string[] { "wamp" },
                        OnConnected = OnClientConnected,
                    };

                    // Add the dynamically created endpoint. And let the library user add binding and security credentials.
                    // By default RemactDefaults.DoServiceConfiguration is called.
                    // TODO _wsServer should be configured
                    _serviceConfig.DoServiceConfiguration(null, ref uri, /*isRouter=*/false);
                    base.ServiceIdent.Uri = uri;
                //}
                //else
                //{
                //    // Set configured URI so it can be sent to WcfRouter for registration.
                //    // TODO: ServiceIdent.Name is used as identification in WcfRouter - should it be changed now ???
                //    //       or should the Uri be changed / created from different fields ???
                //    UriBuilder uri = new UriBuilder (m_ServiceHost.Description.Endpoints[0].ListenUri);
                //    uri.Host = base.ServiceIdent.HostName; // initialized with Dns.GetHostName(), replaces "localhost"
                //    base.ServiceIdent.Uri = uri.Uri;
                //}
        
                lock (ms_Lock)
                {
                    ms_serviceMap [base.ServiceIdent.Uri] = this; // adds new key if not present yet
                }

                _wsServer.Start();

                if (_publishToRouter)
                {
                    // Start registering on WcfRouter
                    WcfRouterClient.Instance().AddService(this);
                }
        
                // The service can now be accessed, but must be registered.
                RaTrc.Info( "Wcf", "Opened service " + base.ServiceIdent.Uri, base.ServiceIdent.Logger );
                return true;
            }
            catch (Exception ex)
            {
                RaTrc.Exception( "could not open " + base.ServiceIdent.Name, ex, base.ServiceIdent.Logger );
                base.LastAction = ex.Message;
            }
            return false;
        }// OpenService

        
        private void OnClientConnected(UserContext context)
        {
            var svcUser = new WcfBasicServiceUser(ServiceIdent);
            var handler = new InternalMultithreadedServiceNet40(this, svcUser);
            var wampProxy = new WampClientProxy(svcUser.ClientIdent, ServiceIdent, handler, context);
            svcUser.SetCallbackHandler(wampProxy);
        }

        // TODO OnDisconnected

        #endregion
        //----------------------------------------------------------------------------------------------
        #region == Internal multithreaded service class for .NET framework 4.0 and Mono ==


        #if !BEFORE_NET40
        /// <summary>
        /// This is the Service Entrypoint. It dispatches requests and returns a response.
        /// </summary>
        private class InternalMultithreadedServiceNet40 : IRemactProtocolDriverService
        {
            private WcfBasicService _service;
            private WcfBasicServiceUser _svcUser;

            public InternalMultithreadedServiceNet40(WcfBasicService service, WcfBasicServiceUser svcUser)
            {
                _service = service;
                _svcUser = svcUser;
            }

            /// <summary>
            /// Occurs when a WampClientProxy calls a service.
            /// </summary>
            void IRemactProtocolDriverService.MessageFromClient(ActorMessage message)
            {
                object response = null;
                try
                {
                    // unlike WCF, a channel oriented protocol has the _svcUser before the first ActorInfo message
                    // TODO CheckBasicResponse should not return another svcUser
                    var tempSvcUser = _svcUser;
                    response = _service.CheckBasicResponse(message, ref tempSvcUser);

                    // multithreaded access, several requests may run in parallel. They are scheduled for execution on the right synchronization context.
                    if( response != null )
                    {
                        var connectMsg = response as ActorInfo;
                        if (connectMsg != null) // no error and connected
                        {
                            var reqCopy = new ActorMessage(message.Source, message.ClientId, message.RequestId, message.Payload, message.SourceLambda);
                            reqCopy.Response = reqCopy; // do not send a ReadyMessage
                            reqCopy.Destination = message.Destination;
                            var task = DoRequestAsync( reqCopy ); // call event OnInputConnected or OnInputDisconnected on the correct thread.
                            if (connectMsg.Usage != ActorInfo.Use.ServiceDisconnectResponse)
                            {
                                var dummy = task.Result; // blocking wait!
                            }
                            // return the original response.
                        }
                    }
                    else
                    {
                        var task = DoRequestAsync(message);
                        message = task.Result; // blocking wait!
                        // Response and optional notifications have been returned to the client already
                        return;
                    }
                }
                catch( Exception ex )
                {
                    RaTrc.Exception(message.SvcRcvId, ex, _service.ServiceIdent.Logger);
                    response = new ErrorMessage( ErrorMessage.Code.UnhandledExceptionOnService, ex );
                }
                message.SendResponse(response);
            }



            private Task<ActorMessage> DoRequestAsync( ActorMessage msg )
            {
                var tcs = new TaskCompletionSource<ActorMessage>();

                if( msg.Destination.IsMultithreaded
                    || msg.Destination.SyncContext == null
                    || msg.Destination.ManagedThreadId == Thread.CurrentThread.ManagedThreadId )
                { // execute request on the calling thread or multi-threaded
                #if !BEFORE_NET45
                    id.Input.DispatchMessageAsync( id )
                        .ContinueWith((t)=>
                            tcs.SetResult( id )); // when finished the first task: finish tcs and let the original request thread return the response.
                #else
                    msg.Destination.DispatchMessage( msg );
                    tcs.SetResult( msg );
                #endif
                }
                else
                {
                    Task.Factory.StartNew(() => 
                        msg.Destination.SyncContext.Post( obj =>
                        {   // execute task on the specified thread after passing the delegate through the message queue...
                            try
                            {
                    #if !BEFORE_NET45
                                id.Input.DispatchMessageAsync( id )        // execute request async on the thread bound to the Input
                                    .ContinueWith((t)=>
                                        tcs.SetResult( id )); // when finished the first task: finish tcs and let the original request thread return the response.
                    #else
                                msg.Destination.DispatchMessage( msg );// execute request synchronously on the thread bound to the Destination
                                tcs.SetResult( msg );
                    #endif
                            }
                            catch( Exception ex )
                            {
                                RaTrc.Exception( "ActorMessage to " + msg.Destination.Name + " cannot be handled by application", ex );
                                tcs.SetException( ex );
                            }
                        }, null )); // obj
                }

                return tcs.Task;
            }

            void       IRemactProtocolDriverService.OpenAsync(ActorMessage message, IRemactProtocolDriverCallbacks callback) { }
            Uri        IRemactProtocolDriverService.ServiceUri { get { return null; } }
            ReadyState IRemactProtocolDriverService.ReadyState { get { return ReadyState.Connected; } }
            string     IRemactProtocolDriverService.ReadyStateAsString { get { return ReadyState.Connected.ToString(); } }
            void       IRemactProtocolDriverService.Dispose() { }

        }//class InternalMultithreadedServiceNet40
        #endif // !BEFORE_NET40


        #endregion
    }
    //----------------------------------------------------------------------------------------------
}
