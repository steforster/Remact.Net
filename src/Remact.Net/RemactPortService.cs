
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Net;                  // Dns
using Remact.Net.Remote;
using System.Threading.Tasks;

namespace Remact.Net
{
    //----------------------------------------------------------------------------------------------
    #region == class RemactPortService ==

    /// <summary>
    /// <para>Actors can connect <see cref="RemactPortProxy"/>'s to this service.</para>
    /// <para>The service is the destination of request messages and the source of response messages.</para>
    /// </summary>
    public class RemactPortService : RemactPort, IRemactPortService
    {
        #region Constructor

        /// <summary>
        /// <para>Creates a service port for an actor. The port can be opened later on for local access or network access.</para>
        /// </summary>
        /// <param name="name">The unique name of this service.</param>
        /// <param name="defaultRequestHandler">The method to be called when a request is not handled by the <see cref="RemactPort.InputDispatcher"/>. See <see cref="MessageHandler"/>.</param>
        public RemactPortService(string name, MessageHandler defaultRequestHandler)
             : base(name, defaultRequestHandler)
        {
            IsServiceName = true;
        }// CTOR1

        /// <summary>
        /// <para>Creates an input port without handler method for internal purpose.</para>
        /// </summary>
        /// <param name="name">The unique name of this service.</param>
        internal RemactPortService(string name)
            : base(name, (MessageHandler)null)
        {
            IsServiceName = true;
        }// CTOR2



        #endregion
        //----------------------------------------------------------------------------------------------
        #region Destination linking, service creation

        private RemactPortProxy m_Anonymous; // each service may have one anonymous client carrying one TSC (sender context)
        private RemactService m_MyInputService;

        /// <summary>
        /// Link this input to the network. Remote clients will be able to connect to this service after Open() has been called.
        /// When this method is not called, the service is accessible application internally only.
        /// </summary>
        /// <param name="serviceName">A service name must be unique in the plant. Null: do not change the current name.</param>
        /// <param name="tcpPort">The TCP port for the service or 0, when automatic port allocation will be used.</param>
        /// <param name="publishToCatalog">True(=default): The servicename will be published to the Remact.Catalog on localhost.</param>
        /// <param name="serviceConfig">Plugin your own service configuration instead of RemactDefaults.ServiceConfiguration.</param>
        public void LinkInputToNetwork(string serviceName = null, int tcpPort = 0, bool publishToCatalog = true,
                                        IServiceConfiguration serviceConfig = null)
        {
            if (serviceName != null) this.Name = serviceName;
            this.IsServiceName = true;
            if (publishToCatalog)
            {
                try
                {
                    AddressList = new List<string>();
                    var host = Dns.GetHostEntry(this.HostName);
                    foreach (var adr in host.AddressList)
                    {
                        if (!adr.IsIPv6LinkLocal && !adr.IsIPv6Multicast)
                        {
                            AddressList.Add(adr.ToString());
                        }
                    }
                }
                catch { }
            }

            m_MyInputService = new RemactService(this, tcpPort, publishToCatalog, serviceConfig); // sets this.Uri. SenderContext is set into client stubs on connecting.
        }

        /// <summary>
        /// When the input is linked to network, BasicService provides some informations about the RemactService.
        /// </summary>
        public RemactService BasicService { get { return m_MyInputService; } }

        /// <summary>
        /// When true: ConnectAsync() must be called (will open the service host)
        /// </summary>
        public bool MustOpenInput { get { return m_MyInputService != null && !m_MyInputService.IsOpen; } }


        /// <summary>
        /// <para>Gets or sets the state of the incoming connection from the network to the service.</para>
        /// <para>May be called from any thread.</para>
        /// <para>Setting InputStateFromNetwork to PortState.Ok or PortState.Connecting reconnects a previously disconnected link.</para>
        /// <para>These states may be set only after an initial call to ConnectAsync from the actors internal thread.</para>
        /// <para>Setting other states will disconnect the RemactService from network.</para>
        /// </summary>
        /// <returns>A <see cref="PortState"/></returns>
        public PortState InputStateFromNetwork
        {
            get
            {
                if (m_MyInputService != null) return m_MyInputService.InputStateFromNetwork;
                if (m_isOpen) return PortState.Ok;
                return PortState.Unlinked;
            }
            set { }
        }

        /// <summary>
        /// <para>Check client connection-timeouts, should be called periodically.</para>
        /// </summary>
        /// <returns>True, when a client state has changed</returns>
        public bool DoPeriodicTasks()
        {
            bool changed = false;
            if (m_MyInputService != null) changed = m_MyInputService.DoPeriodicTasks();
            return changed;
        }

        private RemactPortProxy GetAnonymousProxy()
        {
            if (m_Anonymous == null)
            {
                m_Anonymous = new RemactPortProxy("anonymous");
                m_Anonymous.IsMultithreaded = true;
                m_Anonymous.LinkToService(this);
                m_Anonymous.ConnectAsync();
            }
            return m_Anonymous;
        }

        /// <summary>
        /// The event is risen, when a client is connected to this service.
        /// The response to the RemactMessage is sent by the subsystem. No further response is required. 
        /// </summary>
        public event MessageHandler OnInputConnected;

        /// <summary>
        /// The event is risen, when a client is disconnected from this service.
        /// The response to the RemactMessage is sent by the subsystem. No further response is required. 
        /// </summary>
        public event MessageHandler OnInputDisconnected;


        #endregion
        //----------------------------------------------------------------------------------------------
        #region IRemotePort implementation

        /// <summary>
        /// Opens the service for incomming (network) connections (same as ConnectAsync).
        /// The method is accessible only by the owner of this RemactPortService object. No interface exposes the method.
        /// Open picks up the synchronization context and must be called on the receiving thread only!
        /// A <see cref="ActorInfo"/> message is received, when the connection is established.
        /// The connect-process runs asynchronous and does involve an address registration at the Remact.Net.CatalogApp (when CatalogClient is not disabled).
        /// </summary>
        public void Open()
        {
            ConnectAsync();
        }

        /// <inheritdoc />
        public override Task<bool> ConnectAsync()
        {
            bool ok = true;
            PickupSynchronizationContext();

            if (m_MyInputService != null && !m_MyInputService.IsOpen)
            {
                ok = m_MyInputService.OpenService();
            }
            m_isOpen = ok;
            return Task.FromResult(m_isOpen);
        }

        /// <summary>
        /// Close the incoming network connection.
        /// </summary>
        public override void Disconnect()
        {
            if (m_MyInputService != null) m_MyInputService.Disconnect();
            base.Disconnect();
        }

        // may be called on any thread
        internal virtual object GetNewSenderContext()
        {
            return null; // is overridden by RemactPortService<TSC>
        }

        #endregion
        //----------------------------------------------------------------------------------------------
        #region Message dispatching


        /// <summary>
        /// Anonymous sender: Threadsafe enqueue payload at the receiving partner. No response is expected.
        /// </summary>
        /// <param name="payload">The message to enqueue.</param>
        public void PostFromAnonymous(object payload)
        {
            var proxy = GetAnonymousProxy();
            proxy.Notify(null, payload);
        }


        /// <summary>
        /// Message is passed to users connect/disconnect event handler, may be overloaded and call a MessageHandler;TSC>
        /// </summary>
        /// <param name="msg">RemactMessage containing Payload and Source.</param>
        /// <returns>True when handled.</returns>
        protected override bool OnConnectDisconnect(RemactMessage msg)
        {
            if (msg.DestinationMethod == RemactService.ConnectMethodName)
            {
                if (OnInputConnected != null) OnInputConnected(msg); // optional event
            }
            else if (msg.DestinationMethod == RemactService.DisconnectMethodName)
            {
                if (OnInputDisconnected != null) OnInputDisconnected(msg); // optional event
            }
            else
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Log an incoming message (service side).
        /// </summary>
        protected override void LogIncoming(RemactMessage msg)
        {
            RaLog.Info(msg.SvcRcvId, msg.ToString(), Logger);
        }


        #endregion
        //----------------------------------------------------------------------------------------------
        #region InputClientList

        // used in RemactService TODO move it
        internal List<RemactPortClient> InputClientList;
        private object m_inputClientLock = new Object();

        /// <summary>
        /// Add a local or remote RemactPort.
        /// </summary>
        /// <param name="client">the local RemactPortClient</param>
        internal void TryAddClient(RemactPortClient client)
        {
            lock (m_inputClientLock)
            {
                if (InputClientList == null) InputClientList = new List<RemactPortClient>(10);
                if (InputClientList.Contains(client)) return; // already added
                InputClientList.Add(client);
            }
        }


        /// <summary>
        /// Remove a local or remote RemactPort while Disconnecting.
        /// </summary>
        /// <param name="client">the local RemactPortClient</param>
        internal void TryRemoveClient(RemactPortClient client)
        {
            lock (m_inputClientLock)
            {
                if (InputClientList == null) return;
                int n = InputClientList.IndexOf(client);
                if (n < 0) return; // already removed
                InputClientList.RemoveAt(n);
            }
        }

        #endregion
    }// class RemactPortService


    #endregion
    //----------------------------------------------------------------------------------------------
    #region == class RemactPortService<TSC> ==

    /// <summary>
    /// <para>This class represents an incoming connection from a client to an actor (service).</para>
    /// <para>It is the destination of requests and contains additional data representing the session and the sending actor (client).</para>
    /// </summary>
    /// <typeparam name="TSC">Additional data (source context) representing the communication session and the sending actor.</typeparam>
    public class RemactPortService<TSC> : RemactPortService where TSC : class, new()
    {
        /// <summary>
        /// The event is risen, when a client is connected to this service.
        /// The response to the RemactMessage is sent the subsystem. No further response is required. 
        /// </summary>
        public new event MessageHandler<TSC> OnInputConnected;


        /// <summary>
        /// The event is risen, when a client is disconnected from this service.
        /// The response to the RemactMessage is sent by the subsystem. No further response is required. 
        /// </summary>
        public new event MessageHandler<TSC> OnInputDisconnected;


        private MessageHandler<TSC> m_defaultTscInputHandler;


        /// <summary>
        /// Creates a RemactPortService using a handler method with TSC object for each client.
        /// </summary>
        /// <param name="name">The application internal name of this service or client</param>
        /// <param name="defaultRequestHandler">The method to be called when a request is not handled by the <see cref="RemactPort.InputDispatcher"/>. See <see cref="MessageHandler{TSC}"/>.</param>
        public RemactPortService(string name, MessageHandler<TSC> defaultRequestHandler)
            : base(name)
        {
            DefaultInputHandler = OnDefaultInput;
            m_defaultTscInputHandler = defaultRequestHandler;
        }


        // called when linking output or adding a client partner
        internal override object GetNewSenderContext()
        {
            return new TSC(); // create default SenderContext. It will be stored on the Source partner.
        }


        /// <summary>
        /// Message is passed to users connect/disconnect event handler.
        /// </summary>
        /// <param name="msg">RemactMessage containing Payload and Source.</param>
        /// <returns>True when handled.</returns>
        protected override bool OnConnectDisconnect(RemactMessage msg)
        {
            TSC senderCtx = GetSenderContext(msg);

            if (msg.DestinationMethod == RemactService.ConnectMethodName)
            {
                if (OnInputConnected != null) OnInputConnected(msg, senderCtx); // optional event
            }
            else if (msg.DestinationMethod == RemactService.DisconnectMethodName)
            {
                if (OnInputDisconnected != null) OnInputDisconnected(msg, senderCtx); // optional event
            }
            else
            {
                return false;
            }

            return true;
        }


        internal static TSC GetSenderContext(RemactMessage msg)
        {
            // We are peer   : SendingP is a RemactPortClient. It has only one Output and therefore only one (our) SenderContext. 
            // We are service: SendingP is the client proxy (RemactServiceUser). It has our SenderContext.
            // We NEVER are client : SendingP is ServiceIdent of RemactClient. It's SenderContext is the same as its ClientIdent.SenderContext. 
            TSC senderCtx = null;
            var sender = msg.Source as RemactPortClient;
            if (sender != null)
            {
                senderCtx = sender.SenderCtx as TSC;  // base does not create a new ctx
            }

            //if (senderCtx == null && msg.Source.Uri == null) // anonymous partner
            //{
            //    senderCtx = GetAnonymousSenderContext();
            //}
            return senderCtx;
        }


        /// <summary>
        /// Message is passed to users default handler.
        /// </summary>
        /// <param name="msg">RemactMessage containing Payload and Source.</param>
        private Task OnDefaultInput(RemactMessage msg)
        {
            TSC senderCtx = GetSenderContext(msg);

            if (m_defaultTscInputHandler != null)
            {
                return m_defaultTscInputHandler(msg, senderCtx); // MessageHandlerC> delegate
            }
            else
            {
                RaLog.Error("Remact", "Unhandled request: " + msg.Payload, Logger);
            }
            return null; // completed synchronously
        }
    }// class RemactPortService<TSC>

    #endregion
}