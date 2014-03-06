
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;// DataContract
using System.Net;                  // Dns
using System.Threading;            // SynchronizationContext
using Remact.Net.Remote;
using System.Threading.Tasks;

namespace Remact.Net
{
  //----------------------------------------------------------------------------------------------
  #region == class RemactPortService ==

  /// <summary>
  /// <para>This class represents a communication partner (service).</para>
  /// <para>It is the destination of a request message and the source of a response message.</para>
  /// </summary>
  public class RemactPortService: RemactPort, IRemactPortService, IRemoteActor
  {
    #region Constructor

    /// <summary>
    /// <para>Creates an input port for an actor running in the current application on the local host.</para>
    /// </summary>
    /// <param name="name">The unique name of this service.</param>
    /// <param name="requestHandler">The method to be called when a request is received.</param>
    public RemactPortService (string name, MessageHandler requestHandler)
         : base (name, requestHandler)
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

    
    /// <summary>
    /// <para>Creates a service proxy. Used internally by the client.</para>
    /// </summary>
    internal RemactPortService ()
        : base()
    {
        IsServiceName = true;
    }// default CTOR


    #endregion
    //----------------------------------------------------------------------------------------------
    #region Destination linking, service creation

    private RemactPortClient        m_Anonymous; // each input may have one anonymous partner carrying one TSC (sender context)
    private RemactService      m_MyInputService;

    // prepare for tracing of connect-process
    internal void PrepareServiceName(string catalogHost, string serviceName)
    {
      IsServiceName = true;
      HostName = catalogHost;
      Name = serviceName;
      Uri = new Uri("routed://" + catalogHost + "/" + RemactConfigDefault.WsNamespace + "/" + serviceName);
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
    /// Link this input to the network. Remote clients will be able to connect to this service after Open() has been called.
    /// When this method is not called, the service is accessible application internally only.
    /// </summary>
    /// <param name="serviceName">The unique name of the service or null, when this partners name is equal to the servicename. </param>
    /// <param name="tcpPort">The TCP port for the service or 0, when automatic port allocation will be used.</param>
    /// <param name="publishToCatalog">True(=default): The servicename will be published to the Remact.Catalog on localhost.</param>
    /// <param name="serviceConfig">Plugin your own service configuration instead of RemactDefaults.ServiceConfiguration.</param>
    public void LinkInputToNetwork( string serviceName = null, int tcpPort = 0, bool publishToCatalog = true,
                                    IServiceConfiguration serviceConfig = null )
    {
      if (serviceName != null) this.Name = serviceName;
      this.IsServiceName = true;
      if( publishToCatalog )
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

      m_MyInputService = new RemactService( this, tcpPort, publishToCatalog, serviceConfig ); // sets this.Uri. SenderContext is set into client stubs on connecting.
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
        if( m_isOpen ) return PortState.Ok;
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

    private RemactPortClient GetAnonymousPartner ()
    {
      if (m_Anonymous == null)
      {
        m_Anonymous = new RemactPortClient ("anonymous");
        m_Anonymous.IsMultithreaded = true;
        m_Anonymous.LinkOutputTo(this);
        m_Anonymous.TryConnect();
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
    #region IRemoteActor implementation

    /// <summary>
    /// Opens the service for incomming (network) connections (same as TryConnect).
    /// The method is accessible only by the owner of this RemactPortService object. No interface exposes the method.
    /// Open picks up the synchronization context and must be called on the receiving thread only!
    /// A ActorInfo message is received, when the connection is established.
    /// The connect-process runs asynchronous and does involve an address registration at the Remact.Catalog (when CatalogClient is not disabled).
    /// </summary>
    public void Open()
    {
        TryConnect();
    }

    /// <inheritdoc />
    public Task<bool> TryConnect()
    {
        bool ok = true;
        PickupSynchronizationContext();

        if( m_MyInputService != null && !m_MyInputService.IsOpen )
        {
            ok = m_MyInputService.OpenService();
        }
        m_isOpen = ok;
        return RemactPort.TrueTask;
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
        return null; // is overridden by RemactPortService<TSC>
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


    /// <summary>
    /// Anonymous sender: Threadsafe enqueue payload at the receiving partner. No response is expected.
    /// </summary>
    /// <param name="payload">The message to enqueue.</param>
    public void PostFromAnonymous(object payload)
    {
        var sender = GetAnonymousPartner();
        RemactMessage msg = new RemactMessage(sender, 0, sender.NextRequestId,
                                            this, payload.GetType().FullName, payload, null);
        base.PostInput(msg);
    }


    /// <summary>
    /// Message is passed to users connect/disconnect event handler, may be overloaded and call a MessageHandler;TSC>
    /// </summary>
    /// <param name="msg">RemactMessage containing Payload and Source.</param>
    /// <returns>True when handled.</returns>
    protected override bool OnConnectDisconnect (RemactMessage msg)
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

    #endregion
    //----------------------------------------------------------------------------------------------
    #region InputClientList

    // used in RemactService (TODO move it)
    internal List<RemactPortClient> InputClientList;
    private  object m_inputClientLock = new Object();

    /// <summary>
    /// Add a local or remote RemactPort.
    /// </summary>
    /// <param name="client">the local RemactPortClient</param>
    internal void AddInputClient (RemactPortClient client)
    {
      lock (m_inputClientLock)
      {
        if (InputClientList == null) InputClientList = new List<RemactPortClient>(10);
        InputClientList.Add (client);
      }
    }


    /// <summary>
    /// Remove a local or remote RemactPort while Disconnecting.
    /// </summary>
    /// <param name="client">the local RemactPortClient</param>
    internal void RemoveInputClient (RemactPortClient client)
    {
      lock (m_inputClientLock)
      {
        if (InputClientList == null) return;
        int n = InputClientList.IndexOf (client);
        if (n < 0) return; // already removed
        InputClientList.RemoveAt (n);
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
  public class RemactPortService<TSC>: RemactPortService where TSC: class, new ()
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
    /// <param name="requestHandler">The method to be called when a request is received. See <see cref="MessageHandler{TSC}"/>.</param>
    public RemactPortService(string name, MessageHandler<TSC> requestHandler)
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
    /// <param name="msg">RemactMessage containing Payload and Source.</param>
    /// <returns>True when handled.</returns>
    protected override bool OnConnectDisconnect (RemactMessage msg)
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


    internal static TSC GetSenderContext (RemactMessage msg)
    {
        // We are peer   : SendingP is a RemactPortClient. It has only one Output and therefore only one (our) SenderContext. 
        // We are service: SendingP is the client proxy (RemactServiceUser). It has our SenderContext.
        // We NEVER are client : SendingP is ServiceIdent of RemactClient. It's SenderContext is the same as its ClientIdent.SenderContext. 
        TSC senderCtx = null;
        var sender = msg.Source as RemactPortClient;
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
    /// <param name="msg">RemactMessage containing Payload and Source.</param>
    private void OnDefaultInput(RemactMessage msg)
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
  }// class RemactPortService<TSC>

  #endregion
}// namespace