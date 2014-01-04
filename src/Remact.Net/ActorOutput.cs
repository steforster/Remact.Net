
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
  #region == class ActorOutput ==

  /// <summary>
  /// <para>This class represents a communication partner (client).</para>
  /// <para>It is the source of a request message and the destination of the response.</para>
  /// </summary>
  public class ActorOutput: ActorPort, IActorOutput, IWcfBasicPartner
  {
    #region Constructor

    /// <summary>
    /// <para>Creates an output port for an actor.</para>
    /// </summary>
    /// <param name="name">The application internal name of this output port.</param>
    /// <param name="defaultResponseHandler">The method to be called when a response is received and no other handler is applicatable.</param>
    public ActorOutput (string name, MessageHandler defaultResponseHandler=null)
        : base(name, defaultResponseHandler)
    {
    }// CTOR1

    
#if !BEFORE_NET45
    /// <summary>
    /// <para>Creates an output port for an actor using async-await.</para>
    /// </summary>
    /// <param name="name">The application internal name of this output port.</param>
    /// <param name="defaultMessageHandlerAsync">The awaitable method to be called when a response is received and no other handler is applicatable.</param>
    public ActorOutput( string name, WcfMessageHandlerAsync defaultMessageHandlerAsync )
         : base( name, defaultMessageHandlerAsync )
    {
    }// CTOR2
#endif


    /// <summary>
    /// <para>Creates a client stub, used internally by a service.</para>
    /// </summary>
    internal ActorOutput()
        : base()
    {
    }// default CTOR

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Output-linking, proxy creation

    private   object                 m_SenderCtx;      // TSC created by the connected ActorInput<TSC>
    private   IActorInput            m_Output;         // ActorInput, BasicClientAsync.ServiceIdent or BasicServiceUser.ServiceIdent
    private   IWcfBasicPartner       m_BasicOutput;    // ActorInput, BasicClientAsync or BasicServiceUser
    private   WcfBasicClientAsync    m_MyOutputProxy;
    internal  WcfBasicServiceUser    SvcUser;          // used by WcfBasicService

    internal object GetSenderContext()
    {
        return m_SenderCtx;
    }

    /// <summary>
    /// Link output to application-internal service.
    /// </summary>
    /// <param name="partner">a ActorInput</param>
    public void LinkOutputTo (IActorInput partner)
    {
      Disconnect();
      m_Output      = partner;
      m_BasicOutput = partner as IWcfBasicPartner;
      m_MyOutputProxy = null;

      var input = partner as ActorInput;
      m_SenderCtx = null;
      if (input != null)
      {
          m_SenderCtx = input.GetNewSenderContext();
      }
    }

    /// <summary>
    /// Link output to remote service. Look for the service Uri at WcfRouter on local host.
    /// WcfRouter may have synchronized its service register with peer routers on other hosts.
    /// </summary>
    /// <param name="serviceName">The unique service name to connect to.</param>
    /// <param name="clientConfig">Plugin your own client configuration instead of RemactDefaults.Instance.DoClientConfiguration.</param>
    public void LinkOutputToRemoteService( string serviceName, IActorOutputConfiguration clientConfig = null )
    {
        LinkOutputToRemoteService( "localhost", serviceName, clientConfig );
    }

    /// <summary>
    /// Link output to remote service. Look for the service Uri at WcfRouter on a remote host.
    /// WcfRouter may have synchronized its service register with peer routers on other hosts.
    /// </summary>
    /// <param name="routerHost">The hostname, where the WcfRouter runs.</param>
    /// <param name="serviceName">The unique service name.</param>
    /// <param name="clientConfig">Plugin your own client configuration instead of RemactDefaults.Instance.DoClientConfiguration.</param>
    public void LinkOutputToRemoteService( string routerHost, string serviceName, IActorOutputConfiguration clientConfig = null )
    {
      Disconnect();
      if (routerHost != null && serviceName != null && serviceName.Length > 0)
      {
#if !BEFORE_NET45
        var clt = new WcfBasicClientAsyncAwait(this);
#else
        var clt = new WcfBasicClientAsync(this);
#endif
        clt.LinkToService( routerHost, serviceName, clientConfig );
        m_Output        = clt.ServiceIdent;
        m_BasicOutput   = clt;
        m_MyOutputProxy = clt;
      }
    }

    /// <summary>
    /// Link output to remote service. No lookup at WcfRouter is needed as we know the romote host and the services TCP portnumber.
    /// </summary>
    /// <param name="serviceUri">The uri of the remote service.</param>
    /// <param name="clientConfig">Plugin your own client configuration instead of RemactDefaults.DoClientConfiguration.</param>
    public void LinkOutputToRemoteService( Uri serviceUri, IActorOutputConfiguration clientConfig = null )
    {
      Disconnect();
      if (serviceUri != null)
      {
#if !BEFORE_NET45
        m_MyOutputProxy = new WcfBasicClientAsyncAwait(this);
#else
        m_MyOutputProxy = new WcfBasicClientAsync(this);
#endif
        m_MyOutputProxy.LinkToService( serviceUri, clientConfig );
        m_BasicOutput = m_MyOutputProxy;
        m_Output      = m_MyOutputProxy.ServiceIdent;
      }
    }

    /// <summary>
    /// When true: Sending of requests is possible
    /// </summary>
    public bool IsOutputConnected {get{return PortState.Ok == OutputState;}}

    /// <summary>
    /// When true: TryConnect() must be called (first connect or reconnect)
    /// </summary>
    public bool MustConnectOutput {get {PortState s=OutputState; return s==PortState.Disconnected || s==PortState.Faulted; } }

    /// <summary>
    /// OutputSidePartner is an IActorPort interface to the service (or its proxy) that is linked to this output.
    /// It returns null, as long as we are not linked (OutputState==PortState.Unlinked).
    /// It is used to return identification data like Uri, AppVersion... (see IActorPort).
    /// </summary>
    public IActorPort OutputSidePartner
    {
      get
      {
        if (m_Output != null) return m_Output;
        else return null;// GetAnonymousPartner();
      }
    }

    /// <summary>
    /// The OutputClientId used on the connected service to identify this client.
    /// OutputClientId is generated by the service on first connect or service restart.
    /// It remains stable on reconnect or client restart.
    /// </summary>
    public int OutputClientId { get; internal set; }

    /// <summary>
    /// <para>Gets or sets the state of the outgoing connection.</para>
    /// <para>May be called from any thread.</para>
    /// <para>Setting OutputState to PortState.Ok or PortState.Connecting reconnects a previously disconnected link.</para>
    /// <para>These states may be set only after an initial call to TryConnect from the active services internal thread.</para>
    /// <para>Setting other states will disconnect the WCF client from network.</para>
    /// </summary>
    /// <returns>A <see cref="PortState"/></returns>
    public PortState OutputState
    {
      get
      {
        if (m_MyOutputProxy != null) return m_MyOutputProxy.OutputState; // proxy for remote actor
        if( m_BasicOutput != null )
        {   // internal actor
            if( m_Connected ) return PortState.Ok;
            return PortState.Disconnected; 
        }
        return PortState.Unlinked;
      }
      set
      {
        if (m_MyOutputProxy != null) m_MyOutputProxy.OutputState = value;
      }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region IWcfBasicPartner implementation

    /// <summary>
    /// 'TryConnect' opens the outgoing connection to the previously linked partner.
    /// The method is accessible by the owner of this ActorOutput object only. No interface exposes the method.
    /// TryConnect picks up the synchronization context and must be called on the sending thread only!
    /// The connect-process runs asynchronous and may involve an address lookup at the WcfRouter.
    /// A WcfPartnerMessage is received, after the connection has been established.
    /// A ErrorMessage is received, when the partner is not reachable.
    /// </summary>
    /// <returns>false, when the connect-process is not startable.</returns>
    public bool TryConnect()
    {
        if( m_MyOutputProxy != null ) return m_MyOutputProxy.TryConnect(); // calls PickupSynchronizationContext and sets m_Connected
        if( m_BasicOutput == null )   return false; // not linked
        PickupSynchronizationContext();
        m_Connected = true;
        return true;
    }

    /// <summary>
    /// Shutdown the outgoing connection. Send a disconnect message to the partner.
    /// </summary>
    public override void Disconnect ()
    {
      if( m_MyOutputProxy != null ) m_MyOutputProxy.Disconnect ();
      base.Disconnect();
    }

    /// <summary>
    /// Send a request message to the partner on the outgoing connection.
    /// At least a ReadyMessage will asynchronously be received through 'PostInput', when the partner has processed the request.
    /// Usage:
    /// Clientside:  Send a request to the connected remote service.
    /// Internal:    Send a message to the connected partner running on another thread synchronization context.
    /// Serviceside: Source.SendOut() sends a request from client-proxy to the internal service.
    /// </summary>
    /// <param name="msg">A <see cref="ActorMessage"/>the 'Source' property references the sending partner, where the response is expected.</param>
    public void SendOut (ActorMessage msg)
    {
      if (m_BasicOutput == null) throw new Exception ("AsyncWcfLib: Output of '"+Name+"' has not been linked");

      if( !m_Connected )
      {
          RaTrc.Warning( "AsyncWcfLib", "ActorPort '" + Name + "' is not connected. Cannot send message!", Logger );
          return;
      }

      if( !IsMultithreaded )
      {
          int threadId = Thread.CurrentThread.ManagedThreadId;
          if (SyncContext == null)
          {
              ManagedThreadId = threadId;
              SyncContext = SynchronizationContext.Current;    // set on first send operation
          }
          else if (ManagedThreadId != threadId)
          {
              throw new Exception("AsyncWcfLib: wrong thread synchronization context when sending from '" + Name + "'");
          }
      }
      m_BasicOutput.PostInput(msg);
    }

    /// <summary>
    /// The number of requests not yet responded by the service connected to this output.
    /// </summary>
    public int OutstandingResponsesCount
    { get {
            if (m_BasicOutput == null) return 0;
            return m_BasicOutput.OutstandingResponsesCount;
    }}

#if !BEFORE_NET45
    /// <summary>
    /// VS2012 Version:
    /// Asynchroniously connect the previously linked output.
    /// </summary>
    public async Task<WcfReqIdent> TryConnectAsync()
    {
        if( m_MyOutputProxy != null )
        {
            var clt = m_MyOutputProxy as WcfBasicClientAsyncAwait;
            return await clt.TryConnectAsync(); // calls PickupSynchronizationContext and sets m_Connected
        }
        
        if( m_BasicOutput == null )
        {
            throw new Exception( "cannot connect unlinked output" );
        }

        // TODO, do we have to simulate a ConnectRequest ?
        var id = new WcfReqIdent( m_Output as ActorInput, /*clientId=*/-1, /*requestId=*/1,
                                  new WcfPartnerMessage( m_Output, WcfPartnerMessage.Use.ServiceConnectResponse ),
                                  null );
        PickupSynchronizationContext();
        var m = id.Message as IExtensibleWcfMessage;
        if( m != null )
        {
            m.BoundSyncContext = SyncContext;
            m.IsSent = true;
        }
        id.IsResponse = true;
        m_Connected = true;
        return id;
    }

    /// <summary>
    /// VS2012 Version:
    /// Send a request message to the partner on the outgoing connection.
    /// At least a WcfIdleMessage will asynchronously be received and passed to the Task-return value.
    /// </summary>
    /// <param name="request">The message to send.</param>
    public async Task<WcfReqIdent> SendReceiveAsync (IWcfMessage request)
    {
        if( LastRequestIdSent == uint.MaxValue ) LastRequestIdSent = 10;
        var id = new WcfReqIdent( this, OutputClientId, ++LastRequestIdSent, request, null );
        if( TraceSend ) WcfTrc.Info( id.CltSndId, id.ToString(), Logger );

        if( !m_Connected )
        {
            id.Message = new WcfErrorMessage(WcfErrorMessage.Code.NotConnected, "ActorOutput is disconnected");
            if( TraceReceive ) WcfTrc.Info( id.CltRcvId, id.ToString(), Logger );
            return id;
        }

        if( m_MyOutputProxy != null )
        {
            var clt = m_MyOutputProxy as WcfBasicClientAsyncAwait;
            WcfState state = clt.OutputState;
            if( state == WcfState.Ok || (state == WcfState.Connecting && request is WcfPartnerMessage ))
            {
                id = await clt.SendReceiveAsync( id );
            }
            else
            {
                id.Message = new WcfErrorMessage( WcfErrorMessage.Code.NotConnected, "ActorOutput is not connected to remote input." );
                if( TraceReceive ) WcfTrc.Info( id.CltRcvId, id.ToString(), Logger );
            }
            return id;
        }

        if( m_BasicOutput == null )
        {
            throw new Exception( "cannot send from unlinked output" );
        }

        var svc = m_BasicOutput as ActorInput;
        id = await svc.SendReceiveAsync (id);

        id.Sender = svc;
        id.Input = this;
        var m = id.Message as IExtensibleWcfMessage;
        if( m != null )
        {
            m.BoundSyncContext = SyncContext;
            m.IsSent = true;
        }
        id.IsResponse = true;
        return id;
    }
#endif

    /// <summary>
    /// Send a request payload to the partner on the outgoing connection.
    /// At least a ReadyMessage will asynchronously be received through 'PostInput', after the partner has processed the request.
    /// </summary>
    /// <param name="payload">The message payload to send.</param>
    public void SendOut(object payload)
    {
      if (LastRequestIdSent == int.MaxValue) LastRequestIdSent = 10;
      ActorMessage msg = new ActorMessage(this, OutputClientId, ++LastRequestIdSent, payload, null);
      SendOut(msg);
    }

    /// <summary>
    /// Send a request payload to the partner on the outgoing connection.
    /// At least a ReadyMessage will asynchronously be received in responseHandler.
    /// </summary>
    /// <param name="payload">The message payload to send.</param>
    /// <param name="responseHandler">A method or lambda expression handling the asynchronous response.</param>
    public void SendOut(object payload, AsyncResponseHandler responseHandler)
    {
      if (LastRequestIdSent == int.MaxValue) LastRequestIdSent = 10;
      ActorMessage msg = new ActorMessage(this, OutputClientId, ++LastRequestIdSent, payload, responseHandler);
      SendOut (msg);
    }

    #endregion
  }// class ActorPort



  #endregion
  //----------------------------------------------------------------------------------------------
  #region == class ActorOutput<TSC> ==

  /// <summary>
  /// <para>This class represents an outgoing (client) connection to an actor (service).</para>
  /// <para>It is the destination of responses and contains additional data representing the session and the remote service.</para>
  /// </summary>
  /// <typeparam name="TOC">Additional data (output context) representing the communication session and the remote service.</typeparam>
  public class ActorOutput<TOC> : ActorOutput where TOC : class
  {
      /// <summary>
      /// <para>OutputContext is an object of type TOC defined by the application.</para>
      /// <para>The OutputContext is not sent over the network.</para>
      /// <para>The OutputContext remains untouched by the library. The application may initialize and use it.</para>
      /// </summary>
      public TOC OutputContext
      {
          get { 
            //if (m_outputCtx == null) m_outputCtx = new TOC();
            return m_outputCtx; 
          }

          set { 
            m_outputCtx = value; 
          }
      }

      private TOC m_outputCtx;


      /// <summary>
      /// <para>Creates an output port for an actor.</para>
      /// </summary>
      /// <param name="name">The application internal name of this output port.</param>
      /// <param name="defaultTocResponseHandler">The method to be called when a response is received and no other handler is applicatable. May be null.</param>
      public ActorOutput (string name, MessageHandler<TOC> defaultTocResponseHandler = null)
          : base(name)
      {
          DefaultInputHandler = OnDefaultInput;
          m_defaultTocResponseHandler = defaultTocResponseHandler;
      }

      private MessageHandler<TOC> m_defaultTocResponseHandler;

      /// <summary>
      /// Message is passed to users default handler.
      /// </summary>
      /// <param name="msg">ActorMessage containing Payload and Source.</param>
      private void OnDefaultInput (ActorMessage msg)
      {
          if (m_defaultTocResponseHandler != null)
          {
              m_defaultTocResponseHandler(msg, OutputContext); // MessageHandlerC> delegate
          }
          else
          {
              RaTrc.Error("AsyncWcfLib", "Unhandled response: " + msg.Payload, Logger);
          }
      }

  }// class ActorOutput<TSC>
  #endregion
}