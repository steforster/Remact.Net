
// Copyright (c) 2012  AsyncWcfLib.sourceforge.net

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;// DataContract
using System.Net;                  // Dns
using System.Threading;            // SynchronizationContext
using SourceForge.AsyncWcfLib.Basic;
#if !BEFORE_NET45
    using System.Threading.Tasks;
#endif

namespace SourceForge.AsyncWcfLib
{
  //----------------------------------------------------------------------------------------------
  #region == class ActorInput ==

  /// <summary>
  /// <para>This class represents a communication partner (service).</para>
  /// <para>It is the destination of a request message and the source of a response message.</para>
  /// </summary>
  public class ActorInput: ActorPort, IActorInput, IWcfBasicPartner
  {
    #region Constructor

    /// <summary>
    /// <para>Creates an input port for an actor running in the current application on the local host.</para>
    /// </summary>
    /// <param name="name">The unique name of this service.</param>
    /// <param name="requestHandler">The method to be called when a request is received.</param>
    public ActorInput (string name, WcfMessageHandler requestHandler)
         : base (name, requestHandler)
    {
        IsServiceName = true;
    }// CTOR1

    /// <summary>
    /// <para>Creates an input port without handler method for internal purpose.</para>
    /// </summary>
    /// <param name="name">The unique name of this service.</param>
    internal ActorInput(string name)
        : base(name, (WcfMessageHandler)null)
    {
        IsServiceName = true;
    }// CTOR2

#if !BEFORE_NET45
    /// <summary>
    /// <para>Creates an awaitable input port for an actor running in the current application on the local host.</para>
    /// </summary>
    /// <param name="name">The unique name of this service.</param>
    /// <param name="requestHandlerAsync">The awaitable method to be called when a request is received.</param>
    public ActorInput( string name, WcfMessageHandlerAsync requestHandlerAsync )
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
    #region Input linking, service creation

    private ActorOutput          m_Anonymous; // each input may have one anonymous partner carrying one TSC (sender context)
    private WcfServiceAssistant  m_MyInputService;

    // prepare for tracing of connect-process
    internal void PrepareServiceName (string routerHost, string serviceName)
    {
      IsServiceName = true;
      HostName = routerHost;
      Name = serviceName;
      Uri = new Uri("routed://" + routerHost + "/" + WcfDefault.WsNamespace + "/" + serviceName);
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
    /// Default = false. When set to true: Disable router client, no input of this application will publish its service name to the WcfRouter.
    /// </summary>
    public static bool DisableRouterClient
    {
      get { return WcfRouterClient.Instance ().DisableRouterClient; }
      set { WcfRouterClient.Instance ().DisableRouterClient = value; }
    }

    /// <summary>
    /// Link this input to the network. Remote clients will be able to connect to this service after Open() has been called.
    /// When this method is not called, the service is accessible application internally only.
    /// </summary>
    /// <param name="serviceName">The unique name of the service or null, when this partners name is equal to the servicename. </param>
    /// <param name="tcpPort">The TCP port for the service or 0, when automatic port allocation will be used.</param>
    /// <param name="publishToRouter">True(=default): The servicename will be published to the WcfRouter on localhost.</param>
    /// <param name="serviceConfig">Plugin your own service configuration instead of WcfDefault.ServiceConfiguration.</param>
    public void LinkInputToNetwork( string serviceName = null, int tcpPort = 0, bool publishToRouter = true,
                                    IWcfServiceConfiguration serviceConfig = null )
    {
      if (serviceName != null) this.Name = serviceName;
      this.IsServiceName = true;
      if( publishToRouter )
      {
          try
          {
              AddressList = new List<IPAddress>();
              var host = Dns.GetHostEntry( this.HostName );
              foreach( var adr in host.AddressList )
              {
                  if( !adr.IsIPv6LinkLocal && ! adr.IsIPv6Multicast )
                  {
                      AddressList.Add( adr );
                  }
              }
          }
          catch { }
      }
      m_MyInputService = new WcfServiceAssistant( this, tcpPort, publishToRouter, serviceConfig ); // sets this.Uri. SenderContext is set into client stubs on connecting.
    }

    /// <summary>
    /// When the input is linked to network, BasicService provides some informations about the WCF service.
    /// </summary>
    public WcfServiceAssistant BasicService { get { return m_MyInputService; } }

    /// <summary>
    /// When true: TryConnect() must be called (will open the service host)
    /// </summary>
    public bool MustOpenInput { get { return m_MyInputService != null && !m_MyInputService.IsOpen; } }


    /// <summary>
    /// <para>Gets or sets the state of the incoming connection from the network to the service.</para>
    /// <para>May be called from any thread.</para>
    /// <para>Setting InputStateFromNetwork to WcfState.Ok or WcfState.Connecting reconnects a previously disconnected link.</para>
    /// <para>These states may be set only after an initial call to TryConnect from the actors internal thread.</para>
    /// <para>Setting other states will disconnect the WCF service from network.</para>
    /// </summary>
    /// <returns>A <see cref="WcfState"/></returns>
    public WcfState InputStateFromNetwork 
    { get{
        if (m_MyInputService != null) return m_MyInputService.InputStateFromNetwork;
        if( m_Connected ) return WcfState.Ok;
        return WcfState.Unlinked;
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
    /// The response to the WcfReqIdent is sent by AsyncWcfLib. No further response is required. 
    /// </summary>
    public event WcfMessageHandler OnInputConnected;
    
    /// <summary>
    /// The event is risen, when a client is disconnected from this service.
    /// The response to the WcfReqIdent is sent by AsyncWcfLib. No further response is required. 
    /// </summary>
    public event WcfMessageHandler OnInputDisconnected;


    #endregion
    //----------------------------------------------------------------------------------------------
    #region IWcfBasicPartner implementation

    /// <summary>
    /// Opens the service for incomming connections (same as TryConnect).
    /// The method is accessible only by the owner of this ActorInput object. No interface exposes the method.
    /// - Incoming connections from network: Opens a WCF service.
    /// Open picks up the synchronization context and must be called on the receiving thread only!
    /// A WcfPartnerMessage is received, when the connection is established.
    /// The connect-process runs asynchronous and does involve an address registration at the WcfRouter (when RouterClient is not disabled).
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

    /// <summary>
    /// May not be called.
    /// </summary>
    /// <param name="id">A <see cref="WcfReqIdent"/>the 'Sender' property references the sending partner, where the response is expected.</param>
    public void SendOut(WcfReqIdent id)
    {
        throw new Exception("AsyncWcfLib: Input '" + Name + "' cannot SendOut");
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
    /// At least a WcfIdleMessage will asynchronously be passed to the Task-return value.
    /// </summary>
    /// <param name="id">The request to send.</param>
    /// <returns>The asynchronous response</returns>
    public Task<WcfReqIdent> SendReceiveAsync( WcfReqIdent id )
    {
        // see  TA.docx: Workloads : IO bound
        var tcs = new TaskCompletionSource<WcfReqIdent>();
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
    /// Used internally: Threadsafe enqueue message at the receiving partner. No response is expected.
    /// </summary>
    /// <param name="msg">The IWcfMessage to enqueue.</param>
    public void PostInput (IWcfMessage msg)
    {
      PostInputFrom (null, msg, null);
    }

    /// <summary>
    /// Used internally: Threadsafe enqueue message at the receiving partner.
    /// </summary>
    /// <param name="sender">The source partner sending the message <see cref="ActorPort"/>. Its default message handler will receive the response.</param>
    /// <param name="msg">The IWcfMessage to enqueue.</param>
    public void PostInputFrom (ActorOutput sender, IWcfMessage msg)
    {
      PostInputFrom (sender, msg, null);
    }

    /// <summary>
    /// Used internally: Threadsafe enqueue message at the receiving partner.
    /// </summary>
    /// <param name="sender">The source partner sending the message <see cref="ActorPort"/></param>
    /// <param name="msg">The IWcfMessage to enqueue.</param>
    /// <param name="responseHandler">The lambda expression executed at the source partner, when a response arrives.</param>
    public void PostInputFrom (ActorOutput sender, IWcfMessage msg, AsyncResponseHandler responseHandler)
    {
      if (sender == null)
      {
        sender = GetAnonymousPartner();
        if (responseHandler != null)
        {
          throw new Exception ("AsyncWcfLib: No response supported when sending from anonymous partner");
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
          throw new Exception ("AsyncWcfLib: wrong thread synchronization context when posting from '"+sender.Name+"'");
        }
      }

      if (sender.LastRequestIdSent == uint.MaxValue) sender.LastRequestIdSent = 10;
      WcfReqIdent id = new WcfReqIdent (sender, 0, ++sender.LastRequestIdSent, msg, responseHandler);
      PostInput (id); // Message is posted into the message queue
    }


    /// <summary>
    /// Message is passed to users connect/disconnect event handler, may be overloaded and call a WcfMessageHandler&lt;TSC>
    /// </summary>
    /// <param name="id">WcfReqIdent containing Message and Sender.</param>
    /// <param name="msg">The message.</param>
    /// <returns>True when handled.</returns>
    protected override bool OnConnectDisconnect (WcfReqIdent id, WcfPartnerMessage msg)
    {
      if      (msg.Usage == WcfPartnerMessage.Use.ClientConnectRequest)
      {
        if (OnInputConnected != null) OnInputConnected (id); // optional event
      }
      else if (msg.Usage == WcfPartnerMessage.Use.ClientDisconnectRequest)
      {
        if (OnInputDisconnected != null) OnInputDisconnected (id); // optional event
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

    // used in WcfBasicService (TODO move it)
    internal List<ActorOutput> InputClientList;
    private  object            m_inputClientLock = new Object();

    /// <summary>
    /// Add a local or remote ActorPort
    /// </summary>
    /// <param name="clt">the local WcfBasicClientAsync</param>
    internal void AddInputClient (ActorOutput clt)
    {
      lock (m_inputClientLock)
      {
        if (InputClientList == null) InputClientList = new List<ActorOutput>(10);
        InputClientList.Add (clt);
      }
    }// AddClient


    /// <summary>
    /// Remove a local or remote ActorPort while Disconnecting.
    /// </summary>
    /// <param name="clt">the local WcfBasicClientAsync</param>
    internal void RemoveInputClient (ActorOutput clt)
    {
      lock (m_inputClientLock)
      {
        if (InputClientList == null) return;
        int n = InputClientList.IndexOf (clt);
        if (n < 0) return; // already removed
        InputClientList.RemoveAt (n);
      }
    }// RemoveClient

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
    /// The response to the WcfReqIdent is sent by AsyncWcfLib. No further response is required. 
    /// </summary>
    public new event WcfMessageHandler<TSC> OnInputConnected;


    /// <summary>
    /// The event is risen, when a client is disconnected from this service.
    /// The response to the WcfReqIdent is sent by AsyncWcfLib. No further response is required. 
    /// </summary>
    public new event WcfMessageHandler<TSC> OnInputDisconnected;


    private WcfMessageHandler<TSC> m_defaultTscInputHandler;


    /// <summary>
    /// Creates a ActorInput using a handler method with TSC object for each client.
    /// </summary>
    /// <param name="name">The application internal name of this service or client</param>
    /// <param name="requestHandler">The method to be called when a request is received. See <see cref="WcfMessageHandler&lt;TSC>"/>.</param>
    public ActorInput(string name, WcfMessageHandler<TSC> requestHandler)
        : base(name)
    {
      DefaultInputHandler      = OnDefaultInput;
      m_defaultTscInputHandler = requestHandler;
    }


    // called when linking output or adding a client partner
    internal override object GetNewSenderContext()
    {
      return new TSC(); // create default SenderContext. It will be stored on the Sender partner.
    }

    
    /// <summary>
    /// Message is passed to users connect/disconnect event handler.
    /// </summary>
    /// <param name="id">WcfReqIdent containing Message and Sender.</param>
    /// <param name="msg">The message.</param>
    /// <returns>True when handled.</returns>
    protected override bool OnConnectDisconnect (WcfReqIdent id, WcfPartnerMessage msg)
    {
      TSC senderCtx = GetSenderContext(id);

      if      (msg.Usage == WcfPartnerMessage.Use.ClientConnectRequest)
      {
        if (OnInputConnected != null) OnInputConnected (id, senderCtx); // optional event
      }
      else if (msg.Usage == WcfPartnerMessage.Use.ClientDisconnectRequest)
      {
        if (OnInputDisconnected != null) OnInputDisconnected (id, senderCtx); // optional event
      }
      else
      {
        return false;
      }
      return true;
    }


    internal static TSC GetSenderContext (WcfReqIdent id)
    {
        // We are peer   : SendingP is an ActorOutput. It has only one Output and therefore only one (our) SenderContext. 
        // We are service: SendingP is the client  proxy (WcfBasicServiceUser). It has our SenderContext.
        // We NEVER are client : SendingP is ServiceIdent of WcfBasicClientAsync. It's SenderContext is the same as its ClientIdent.SenderContext. 
        TSC senderCtx = null;
        var sender = id.Sender as ActorOutput;
        if (sender != null)
        {
            senderCtx = sender.GetSenderContext() as TSC;  // base does not create a new ctx
        }

        //if (senderCtx == null && id.Sender.Uri == null) // anonymous partner
        //{
        //    senderCtx = GetAnonymousSenderContext();
        //}
        return senderCtx;
    }


    /// <summary>
    /// Message is passed to users default handler.
    /// </summary>
    /// <param name="id">WcfReqIdent containing Message and Sender.</param>
    private void OnDefaultInput (WcfReqIdent id)
    {
      TSC senderCtx = GetSenderContext(id);

      if (m_defaultTscInputHandler != null)
      {
        m_defaultTscInputHandler (id, senderCtx); // WcfMessageHandler<TSC> delegate
      } 
      else 
      {
          WcfTrc.Error( "AsyncWcfLib", "Unhandled request: " + id.Message, Logger );
      }
    }
  }// class ActorInput<TSC>

  #endregion
}// namespace