
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;// DataContract
using System.Net;                  // Dns
using System.Threading;            // SynchronizationContext
using Remact.Net.Internal;
#if !BEFORE_NET45
    using System.Threading.Tasks;
#endif

namespace Remact.Net
{
  //----------------------------------------------------------------------------------------------
  #region == class ActorInput ==

  /// <summary>
  /// <para>This class represents a communication partner (service).</para>
  /// <para>It is the destination of a request message and the source of a response message.</para>
  /// </summary>
  public class ActorInput: ActorPort, IActorInput, IRemoteActor
  {
    #region Constructor

    /// <summary>
    /// <para>Creates an input port for an actor running in the current application on the local host.</para>
    /// </summary>
    /// <param name="name">The unique name of this service.</param>
    /// <param name="requestHandler">The method to be called when a request is received.</param>
    public ActorInput (string name, MessageHandler requestHandler)
         : base (name, requestHandler)
    {
        IsServiceName = true;
    }// CTOR1

    /// <summary>
    /// <para>Creates an input port without handler method for internal purpose.</para>
    /// </summary>
    /// <param name="name">The unique name of this service.</param>
    internal ActorInput(string name)
        : base(name, (MessageHandler)null)
    {
        IsServiceName = true;
    }// CTOR2

#if !BEFORE_NET45
    /// <summary>
    /// <para>Creates an awaitable input port for an actor running in the current application on the local host.</para>
    /// </summary>
    /// <param name="name">The unique name of this service.</param>
    /// <param name="requestHandlerAsync">The awaitable method to be called when a request is received.</param>
    public ActorInput( string name, ActorMessageHandlerAsync requestHandlerAsync )
        : base(name, requestHandlerAsync)
    {
        IsServiceName = true;
    }// CTOR3
#endif

    
    /// <summary>
    /// <para>Creates a service proxy. Used internally by the client.</para>
    /// </summary>
    internal ActorInput ()
        : base()
    {
        IsServiceName = true;
    }// default CTOR


    #endregion
    //----------------------------------------------------------------------------------------------
    #region Destination linking, service creation

    private ActorOutput          m_Anonymous; // each input may have one anonymous partner carrying one TSC (sender context)
    private RemactService      m_MyInputService;

    // prepare for tracing of connect-process
    internal void PrepareServiceName (string routerHost, string serviceName)
    {
      IsServiceName = true;
      HostName = routerHost;
      Name = serviceName;
      Uri = new Uri("routed://" + routerHost + "/" + RemactConfigDefault.WsNamespace + "/" + serviceName);
    }

    // prepare for tracing of connect-process
    internal void PrepareServiceName (Uri uri)
    {
      IsServiceName = true;
      HostName = uri.Host;
      Name = uri.AbsolutePath;
      Uri = uri;
    }

    /// <summary>
    /// Default = false. When set to true: Disable router client, no input of this application will publish its service name to the Remact.Catalog.
    /// </summary>
    public static bool DisableRouterClient
    {
      get { return RemactCatalogClient.Instance ().DisableRouterClient; }
      set { RemactCatalogClient.Instance ().DisableRouterClient = value; }
    }

    /// <summary>
    /// Link this input to the network. Remote clients will be able to connect to this service after Open() has been called.
    /// When this method is not called, the service is accessible application internally only.
    /// </summary>
    /// <param name="serviceName">The unique name of the service or null, when this partners name is equal to the servicename. </param>
    /// <param name="tcpPort">The TCP port for the service or 0, when automatic port allocation will be used.</param>
    /// <param name="publishToRouter">True(=default): The servicename will be published to the Remact.Catalog on localhost.</param>
    /// <param name="serviceConfig">Plugin your own service configuration instead of RemactDefaults.ServiceConfiguration.</param>
    public void LinkInputToNetwork( string serviceName = null, int tcpPort = 0, bool publishToRouter = true,
                                    IActorInputConfiguration serviceConfig = null )
    {
      if (serviceName != null) this.Name = serviceName;
      this.IsServiceName = true;
      if( publishToRouter )
      {
          try
          {
              AddressList = new List<string>();
              var host = Dns.GetHostEntry( this.HostName );
              foreach( var adr in host.AddressList )
              {
                  if( !adr.IsIPv6LinkLocal && ! adr.IsIPv6Multicast )
                  {
                      AddressList.Add( adr.ToString() );
                  }
              }
          }
          catch { }
      }

      m_MyInputService = new RemactService( this, tcpPort, publishToRouter, serviceConfig ); // sets this.Uri. SenderContext is set into client stubs on connecting.
    }

    /// <summary>
    /// When the input is linked to network, BasicService provides some informations about the RemactService.
    /// </summary>
    public RemactService BasicService { get { return m_MyInputService; } }

    /// <summary>
    /// When true: TryConnect() must be called (will open the service host)
    /// </summary>
    public bool MustOpenInput { get { return m_MyInputService != null && !m_MyInputService.IsOpen; } }


    /// <summary>
    /// <para>Gets or sets the state of the incoming connection from the network to the service.</para>
    /// <para>May be called from any thread.</para>
    /// <para>Setting InputStateFromNetwork to PortState.Ok or PortState.Connecting reconnects a previously disconnected link.</para>
    /// <para>These states may be set only after an initial call to TryConnect from the actors internal thread.</para>
    /// <para>Setting other states will disconnect the RemactService from network.</para>
    /// </summary>
    /// <returns>A <see cref="PortState"/></returns>
    public PortState InputStateFromNetwork 
    { get{
        if (m_MyInputService != null) return m_MyInputService.InputStateFromNetwork;
        if( m_Connected ) return PortState.Ok;
        return PortState.Unlinked;
      } 
      set{} 
    }

    /// <summary>
    /// <para>Check client connection-timeouts, should be called periodically.</para>
    /// </summary>
    /// <returns>True, when a client state has changed</returns>
    public bool DoPeriodicTasks ()
    {
      bool changed = false;
      if (m_MyInputService != null) changed = m_MyInputService.DoPeriodicTasks();
      return changed;
    }

    private ActorOutput GetAnonymousPartner ()
    {
      if (m_Anonymous == null)
      {
        m_Anonymous = new ActorOutput ("anonymous");
        m_Anonymous.IsMultithreaded = true;
        m_Anonymous.LinkOutputTo(this);
        m_Anonymous.TryConnect();
      }
      return m_Anonymous;
    }

    /// <summary>
    /// The event is risen, when a client is connected to this service.
    /// The response to the ActorMessage is sent by the subsystem. No further response is required. 
    /// </summary>
    public event MessageHandler OnInputConnected;
    
    /// <summary>
    /// The event is risen, when a client is disconnected from this service.
    /// The response to the ActorMessage is sent by the subsystem. No further response is required. 
    /// </summary>
    public event MessageHandler OnInputDisconnected;


    #endregion
    //----------------------------------------------------------------------------------------------
    #region IRemoteActor implementation

    /// <summary>
    /// Opens the service for incomming connections (same as TryConnect).
    /// The method is accessible only by the owner of this ActorInput object. No interface exposes the method.
    /// - Incoming connections from network: Opens a RemactService.
    /// Open picks up the synchronization context and must be called on the receiving thread only!
    /// A ActorInfo message is received, when the connection is established.
    /// The connect-process runs asynchronous and does involve an address registration at the Remact.Catalog (when CatalogClient is not disabled).
    /// </summary>
    public void Open()
    {
        TryConnect();
    }

    /// <inheritdoc />
    public bool TryConnect()
    {
        bool ok = true;
        PickupSynchronizationContext();

        if( m_MyInputService != null && !m_MyInputService.IsOpen )
        {
            ok = m_MyInputService.OpenService();
        }
        m_Connected = ok;
        return ok;
    }

    /// <summary>
    /// Close the incoming network connection.
    /// </summary>
    public override void Disconnect ()
    {
      if (m_MyInputService != null) m_MyInputService.Disconnect();
      base.Disconnect();
    }

    // may be called on any thread
    internal virtual object GetNewSenderContext()
    {
        return null; // is overridden by ActorInput<TSC>
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Message dispatching

    /// <summary>
    /// Returns 0 for inputs.
    /// </summary>
    public int OutstandingResponsesCount
    { get {
        return 0;
    }}

#if !BEFORE_NET45

    /// <summary>
    /// VS2012 Version:
    /// Switch to the actors thread of this input and process the request there.
    /// At least a ReadyMessage will asynchronously be passed to the Task-return value.
    /// </summary>
    /// <param name="id">The request to send.</param>
    /// <returns>The asynchronous response</returns>
    public Task<ActorMessage> SendReceiveAsync( ActorMessage id )
    {
        // see  TA.docx: Workloads : IO bound
        var tcs = new TaskCompletionSource<ActorMessage>();
        id.SourceLambda = ( rsp ) =>
            {
                tcs.TrySetResult( rsp ); // response has been received
                return null;
            };
        
        PostInput( id ); // switch synchronization context and process message
        
        return tcs.Task; // return awaitable task
    }

#endif

    /// <summary>
    /// Anonymous sender: Threadsafe enqueue payload at the receiving partner. No response is expected.
    /// </summary>
    /// <param name="payload">The message to enqueue.</param>
    public void PostInput(object payload)
    {
        PostInputFrom(null, payload, null);
    }

    /// <summary>
    /// Used internally: Threadsafe enqueue message at the receiving partner (must be implemented to guide the compiler)
    /// </summary>
    /// <param name="message">The message to send.</param>
    public new void PostInput(ActorMessage message)
    {
        base.PostInput(message);
    }

    /// <summary>
    /// Used internally: Threadsafe enqueue payload at the receiving partner.
    /// </summary>
    /// <param name="sender">The source partner sending the message <see cref="ActorPort"/>. Its default message handler will receive the response.</param>
    /// <param name="payload">The message to enqueue.</param>
    public void PostInputFrom(ActorOutput sender, object payload)
    {
        PostInputFrom(sender, payload, null);
    }

    /// <summary>
    /// Used internally: Threadsafe enqueue payload at the receiving partner.
    /// </summary>
    /// <param name="sender">The source partner sending the message <see cref="ActorPort"/></param>
    /// <param name="payload">The message to enqueue.</param>
    /// <param name="responseHandler">The lambda expression executed at the source partner, when a response arrives.</param>
    public void PostInputFrom(ActorOutput sender, object payload, AsyncResponseHandler responseHandler)
    {
      if (sender == null)
      {
        sender = GetAnonymousPartner();
        if (responseHandler != null)
        {
          throw new Exception ("Remact: No response supported when sending from anonymous partner");
        }
      }
      else
      {
        int threadId = Thread.CurrentThread.ManagedThreadId;
        if (sender.SyncContext == null)
        {
          sender.ManagedThreadId = threadId;
          sender.SyncContext = SynchronizationContext.Current;    // set on first send operation
        }
        else if (sender.ManagedThreadId != threadId)
        {
          throw new Exception ("Remact: wrong thread synchronization context when posting from '"+sender.Name+"'");
        }
      }

      ActorMessage msg = new ActorMessage(sender, 0, sender.NextRequestId,
                                          this, payload.GetType().FullName, payload, responseHandler);
      base.PostInput (msg); // Message is posted into the message queue
    }


    /// <summary>
    /// Message is passed to users connect/disconnect event handler, may be overloaded and call a MessageHandler;TSC>
    /// </summary>
    /// <param name="msg">ActorMessage containing Payload and Source.</param>
    /// <param name="info">The ActorInfo payload.</param>
    /// <returns>True when handled.</returns>
    protected override bool OnConnectDisconnect (ActorMessage msg, ActorInfo info)
    {
        if (info.Usage == ActorInfo.Use.ClientConnectRequest)
        {
            if (OnInputConnected != null) OnInputConnected(msg); // optional event
        }
        else if (info.Usage == ActorInfo.Use.ClientDisconnectRequest)
        {
            if (OnInputDisconnected != null) OnInputDisconnected(msg); // optional event
        }
        else
        {
            return false;
        }
        return true;
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region InputClientList

    // used in RemactService (TODO move it)
    internal List<ActorOutput> InputClientList;
    private  object            m_inputClientLock = new Object();

    /// <summary>
    /// Add a local or remote ActorPort
    /// </summary>
    /// <param name="clt">the local ActorOutput</param>
    internal void AddInputClient (ActorOutput clt)
    {
      lock (m_inputClientLock)
      {
        if (InputClientList == null) InputClientList = new List<ActorOutput>(10);
        InputClientList.Add (clt);
      }
    }


    /// <summary>
    /// Remove a local or remote ActorPort while Disconnecting.
    /// </summary>
    /// <param name="clt">the local ActorOutput</param>
    internal void RemoveInputClient (ActorOutput clt)
    {
      lock (m_inputClientLock)
      {
        if (InputClientList == null) return;
        int n = InputClientList.IndexOf (clt);
        if (n < 0) return; // already removed
        InputClientList.RemoveAt (n);
      }
    }

    #endregion
  }// class ActorInput


  #endregion
  //----------------------------------------------------------------------------------------------
  #region == class ActorInput<TSC> ==

  /// <summary>
  /// <para>This class represents an incoming connection from a client to an actor (service).</para>
  /// <para>It is the destination of requests and contains additional data representing the session and the sending actor (client).</para>
  /// </summary>
  /// <typeparam name="TSC">Additional data (sender context) representing the communication session and the sending actor.</typeparam>
  public class ActorInput<TSC>: ActorInput where TSC: class, new ()
  {
    /// <summary>
    /// The event is risen, when a client is connected to this service.
    /// The response to the ActorMessage is sent the subsystem. No further response is required. 
    /// </summary>
    public new event MessageHandler<TSC> OnInputConnected;


    /// <summary>
    /// The event is risen, when a client is disconnected from this service.
    /// The response to the ActorMessage is sent by the subsystem. No further response is required. 
    /// </summary>
    public new event MessageHandler<TSC> OnInputDisconnected;


    private MessageHandler<TSC> m_defaultTscInputHandler;


    /// <summary>
    /// Creates a ActorInput using a handler method with TSC object for each client.
    /// </summary>
    /// <param name="name">The application internal name of this service or client</param>
    /// <param name="requestHandler">The method to be called when a request is received. See <see cref="MessageHandler{TSC}"/>.</param>
    public ActorInput(string name, MessageHandler<TSC> requestHandler)
        : base(name)
    {
      DefaultInputHandler      = OnDefaultInput;
      m_defaultTscInputHandler = requestHandler;
    }


    // called when linking output or adding a client partner
    internal override object GetNewSenderContext()
    {
      return new TSC(); // create default SenderContext. It will be stored on the Source partner.
    }

    
    /// <summary>
    /// Message is passed to users connect/disconnect event handler.
    /// </summary>
    /// <param name="msg">ActorMessage containing Payload and Source.</param>
    /// <param name="info">The ActorInfo payload.</param>
    /// <returns>True when handled.</returns>
    protected override bool OnConnectDisconnect (ActorMessage msg, ActorInfo info)
    {
        TSC senderCtx = GetSenderContext(msg);

        if (info.Usage == ActorInfo.Use.ClientConnectRequest)
        {
            if (OnInputConnected != null) OnInputConnected(msg, senderCtx); // optional event
        }
        else if (info.Usage == ActorInfo.Use.ClientDisconnectRequest)
        {
            if (OnInputDisconnected != null) OnInputDisconnected(msg, senderCtx); // optional event
        }
        else
        {
            return false;
        }
        return true;
    }


    internal static TSC GetSenderContext (ActorMessage msg)
    {
        // We are peer   : SendingP is an ActorOutput. It has only one Output and therefore only one (our) SenderContext. 
        // We are service: SendingP is the client  proxy (RemactServiceUser). It has our SenderContext.
        // We NEVER are client : SendingP is ServiceIdent of RemactClient. It's SenderContext is the same as its ClientIdent.SenderContext. 
        TSC senderCtx = null;
        var sender = msg.Source as ActorOutput;
        if (sender != null)
        {
            senderCtx = sender.GetSenderContext() as TSC;  // base does not create a new ctx
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
    /// <param name="msg">ActorMessage containing Payload and Source.</param>
    private void OnDefaultInput(ActorMessage msg)
    {
        TSC senderCtx = GetSenderContext(msg);

        if (m_defaultTscInputHandler != null)
        {
            m_defaultTscInputHandler(msg, senderCtx); // MessageHandlerC> delegate
        } 
        else 
        {
            RaLog.Error("Remact", "Unhandled request: " + msg.Payload, Logger);
        }
    }
  }// class ActorInput<TSC>

  #endregion
}// namespace