
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using Remact.Net.Remote;
using System.Threading.Tasks;

namespace Remact.Net
{
    #region == class RemactPortProxy ==

    /// <summary>
    /// <para>The RemactPortProxy is used when an actor sends to a service.</para>
    /// </summary>
    public class RemactPortProxy : RemactPort, IRemactPortProxy
    {
        #region Constructor

        /// <summary>
        /// <para>Creates an output port for an actor.</para>
        /// </summary>
        /// <param name="clientName">The application internal client name of this output port.</param>
        /// <param name="defaultResponseHandler">The method to be called when a response is received and no other handler is applicatable.</param>
        public RemactPortProxy(string clientName, MessageHandler defaultResponseHandler = null)
              : base("unlinkedProxy_" + clientName, defaultResponseHandler)
        {
            m_Client = new RemactPortClient(clientName)
            {
                RedirectIncoming = this,
                ServiceIdent = this,
            };
        }// CTOR1


        #endregion
        //----------------------------------------------------------------------------------------------
        #region Output-linking, service identification

        private RemactPortClient m_Client;
        private RemactPortService m_LocalService; // is null, when connected to remote service
        private RemactClient m_RemoteClient; // is null, when connected to local service

        /// <summary>
        /// Link output to application-internal service.
        /// </summary>
        /// <param name="service">a RemactPortService</param>
        public void LinkToService(IRemactPortService service)
        {
            Disconnect();
            m_LocalService = service as RemactPortService;
            RedirectIncoming = m_LocalService;
            m_RemoteClient = null;
        }

        /// <summary>
        /// Link output to remote service. Look for the service Uri at Remact.Catalog (catalog uri is defined by RemactConfigDefault).
        /// Remact.Catalog may have synchronized its service register with peer catalogs on other hosts.
        /// </summary>
        /// <param name="serviceName">The unique service name.</param>
        /// <param name="clientConfig">Plugin your own client configuration instead of RemactDefaults.Instance.DoClientConfiguration.</param>
        public void LinkOutputToRemoteService(string serviceName, IClientConfiguration clientConfig = null)
        {
            Disconnect();
            m_LocalService = null;
            RedirectIncoming = null;
            if (!string.IsNullOrEmpty(serviceName))
            {
                m_RemoteClient = new RemactClient(this, m_Client);
                m_RemoteClient.LinkToRemoteService(serviceName, clientConfig);
                RedirectIncoming = m_RemoteClient;
            }
        }

        /// <summary>
        /// Link output to remote service. No lookup at Remact.Catalog is needed as we know the romote host and the service TCP portnumber.
        /// </summary>
        /// <param name="serviceUri">The uri of the remote service.</param>
        /// <param name="clientConfig">Plugin your own client configuration instead of RemactDefaults.DoClientConfiguration.</param>
        public void LinkOutputToRemoteService(Uri serviceUri, IClientConfiguration clientConfig = null)
        {
            Disconnect();
            m_LocalService = null;
            RedirectIncoming = null;
            if (serviceUri != null)
            {
                m_RemoteClient = new RemactClient(this, m_Client);
                m_RemoteClient.LinkToRemoteService(serviceUri, clientConfig);
                RedirectIncoming = m_RemoteClient;
            }
        }

        // prepare for tracing of connect-process
        internal void PrepareServiceName(string catalogHost, string serviceName)
        {
            IsServiceName = true;
            HostName = catalogHost;
            Name = serviceName;
            Uri = new Uri("routed://" + catalogHost + "/" + RemactConfigDefault.WsNamespace + "/" + serviceName);
        }

        // prepare for tracing of connect-process
        internal void PrepareServiceName(Uri uri)
        {
            IsServiceName = true;
            HostName = uri.Host;
            Name = uri.AbsolutePath;
            Uri = uri;
        }

        // must be overridden to be able to send messages from the concrete port type.
        internal override RemactMessage NewMessage(string method, object payload, RemactMessageType messageType, AsyncResponseHandler responseHandler)
        {
            return new RemactMessage(this, method, payload, messageType, responseHandler);
        }

        /// <summary>
        /// When true: Sending of requests is possible
        /// </summary>
        public bool IsOutputConnected { get { return PortState.Ok == OutputState; } }

        /// <summary>
        /// When true: ConnectAsync() must be called (first connect or reconnect)
        /// </summary>
        public bool MustConnectOutput { get { PortState s = OutputState; return s == PortState.Disconnected || s == PortState.Faulted; } }

        /// <summary>
        /// ClientIdent is the identification of the client port that is represented by the RemactPortProxy.
        /// </summary>
        public IRemactPort ClientIdent { get { return m_Client; } }

        /// <summary>
        /// The Client for this PortProxy. It is used for callbacks. 
        /// The linked service may ask ClientIdent to send a message. The message is sent to the actor that owns the proxy.
        /// </summary>
        internal RemactPortClient Client { get { return m_Client; } }

        /// <summary>
        /// <para>Gets or sets the state of the outgoing connection.</para>
        /// <para>May be called from any thread.</para>
        /// <para>Setting OutputState to PortState.Ok or PortState.Connecting reconnects a previously disconnected link.</para>
        /// <para>These states may be set only after an initial call to ConnectAsync from the active services internal thread.</para>
        /// <para>Setting other states will disconnect the Remact client from network.</para>
        /// </summary>
        /// <returns>A <see cref="PortState"/></returns>
        public PortState OutputState
        {
            get
            {
                if (m_RemoteClient != null) return m_RemoteClient.OutputState; // proxy for remote actor
                if (RedirectIncoming != null)
                {   // internal actor
                    if (m_isOpen) return PortState.Ok;
                    return PortState.Disconnected;
                }
                return PortState.Unlinked;
            }

            set
            {
                if (m_RemoteClient != null) m_RemoteClient.OutputState = value;
            }
        }

        #endregion
        //----------------------------------------------------------------------------------------------
        #region IRemotePort implementation

        /// <summary>
        /// 'ConnectAsync' opens the outgoing connection to the previously linked partner.
        /// The method is accessible by the owner of this RemactPortClient object only. No interface exposes the method.
        /// ConnectAsync picks up the synchronization context and must be called on the sending thread only!
        /// The connect-process runs asynchronous and may involve an address lookup at the Remact.Catalog.
        /// An ActorInfo message is received, after the connection has been established.
        /// An ErrorMessage is received, when the partner is not reachable.
        /// </summary>
        /// <returns>A task. When this task is run to completion, the task.Result corresponds to IsOpen.</returns>
        public override Task<bool> ConnectAsync()
        {
            PickupSynchronizationContext();
            if (m_RemoteClient != null) return m_RemoteClient.ConnectAsync(); // calls PickupSynchronizationContext and sets m_Connected
            if (m_LocalService == null || RedirectIncoming == null) throw new InvalidOperationException("RemactPortClient is not linked");
            m_LocalService.TryAddClient(m_Client);
            m_isOpen = true;
            return RemactPort.TrueTask;
        }

        /// <summary>
        /// Shutdown the outgoing connection. Send a disconnect message to the partner.
        /// </summary>
        public override void Disconnect()
        {
            if (m_RemoteClient != null) m_RemoteClient.Disconnect();
            if (m_LocalService != null) m_LocalService.TryRemoveClient(m_Client);
            base.Disconnect();
        }


        /// <summary>
        /// The number of requests not yet responded by the service connected to this output.
        /// </summary>
        public override int OutstandingResponsesCount
        {
            get
            {
                if (RedirectIncoming == null) return 0;
                return RedirectIncoming.OutstandingResponsesCount;
            }
        }

        #endregion
    }// class RemactPortProxy


    #endregion
    //----------------------------------------------------------------------------------------------
    #region == class RemactPortProxy<TOC> ==

    /// <summary>
    /// <para>This class represents an outgoing (client) connection to an actor (service).</para>
    /// <para>It is the destination of responses and contains additional data representing the session and the remote service.</para>
    /// </summary>
    /// <typeparam name="TOC">Additional data (output context) representing the communication session and the remote service.</typeparam>
    public class RemactPortProxy<TOC> : RemactPortProxy where TOC : class
    {
        /// <summary>
        /// <para>OutputContext is an object of type TOC defined by the application.</para>
        /// <para>The OutputContext is not sent over the network.</para>
        /// <para>The OutputContext remains untouched by the library. The application may initialize and use it.</para>
        /// </summary>
        public TOC OutputContext
        {
            get
            {
                //if (m_outputCtx == null) m_outputCtx = new TOC();
                return m_outputCtx;
            }

            set
            {
                m_outputCtx = value;
            }
        }

        private TOC m_outputCtx;
        private MessageHandler<TOC> m_defaultTocResponseHandler;


        /// <summary>
        /// <para>Creates an output port for an actor.</para>
        /// </summary>
        /// <param name="name">The application internal name of this output port.</param>
        /// <param name="defaultTocResponseHandler">The method to be called when a response is received and no other handler is applicatable. May be null.</param>
        public RemactPortProxy(string name, MessageHandler<TOC> defaultTocResponseHandler = null)
            : base(name)
        {
            DefaultInputHandler = OnDefaultInput;
            m_defaultTocResponseHandler = defaultTocResponseHandler;
        }


        /// <summary>
        /// Message is passed to users default handler.
        /// </summary>
        /// <param name="msg">RemactMessage containing Payload and Source.</param>
        private void OnDefaultInput(RemactMessage msg)
        {
            if (m_defaultTocResponseHandler != null)
            {
                m_defaultTocResponseHandler(msg, OutputContext); // MessageHandler<TOC> delegate
            }
            else
            {
                RaLog.Error("Remact", "Unhandled response: " + msg.Payload, Logger);
            }
        }

    }// class RemactPortProxy<TOC>
    #endregion
}