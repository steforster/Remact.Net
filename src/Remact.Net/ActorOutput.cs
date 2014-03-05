
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
  #region == class ActorOutput ==

  /// <summary>
  /// <para>This class represents a communication partner (client).</para>
  /// <para>It is the source of a request message and the destination of the response.</para>
  /// </summary>
  public class ActorOutput: ActorPort, IActorOutput, IRemoteActor
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

    private   object             m_SenderCtx;      // TSC created by the connected ActorInput<TSC>
    private   IActorInput        m_Output;         // ActorInput, BasicClientAsync.ServiceIdent or BasicServiceUser.ServiceIdent
    private   RemactClient       m_MyOutputProxy;
    internal  RemactServiceUser  SvcUser;          // used by RemactService

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
      m_BasicOutput = partner as IRemoteActor;
      m_MyOutputProxy = null;

      var input = partner as ActorInput;
      m_SenderCtx = null;
      if (input != null)
      {
          m_SenderCtx = input.GetNewSenderContext();
      }
    }

    /// <summary>
    /// Link output to remote service. Look for the service Uri at Remact.Catalog (catalog uri is defined by RemactConfigDefault).
    /// Remact.Catalog may have synchronized its service register with peer catalogs on other hosts.
    /// </summary>
    /// <param name="serviceName">The unique service name.</param>
    /// <param name="clientConfig">Plugin your own client configuration instead of RemactDefaults.Instance.DoClientConfiguration.</param>
    public void LinkOutputToRemoteService (string serviceName, IActorOutputConfiguration clientConfig = null)
    {
      Disconnect();
      if (!string.IsNullOrEmpty(serviceName))
      {
        var clt = new RemactClient(this);
        clt.LinkToService(serviceName, clientConfig);
        m_Output        = clt.ServiceIdent;
        m_BasicOutput   = clt;
        m_MyOutputProxy = clt;
      }
    }

    /// <summary>
    /// Link output to remote service. No lookup at Remact.Catalog is needed as we know the romote host and the service TCP portnumber.
    /// </summary>
    /// <param name="serviceUri">The uri of the remote service.</param>
    /// <param name="clientConfig">Plugin your own client configuration instead of RemactDefaults.DoClientConfiguration.</param>
    public void LinkOutputToRemoteService (Uri serviceUri, IActorOutputConfiguration clientConfig = null)
    {
      Disconnect();
      if (serviceUri != null)
      {
        m_MyOutputProxy = new RemactClient(this);
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
    /// <para>Gets or sets the state of the outgoing connection.</para>
    /// <para>May be called from any thread.</para>
    /// <para>Setting OutputState to PortState.Ok or PortState.Connecting reconnects a previously disconnected link.</para>
    /// <para>These states may be set only after an initial call to TryConnect from the active services internal thread.</para>
    /// <para>Setting other states will disconnect the Remact client from network.</para>
    /// </summary>
    /// <returns>A <see cref="PortState"/></returns>
    public PortState OutputState
    {
      get
      {
        if (m_MyOutputProxy != null) return m_MyOutputProxy.OutputState; // proxy for remote actor
        if( m_BasicOutput != null )
        {   // internal actor
            if( m_isOpen ) return PortState.Ok;
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
    #region IRemoteActor implementation

    /// <summary>
    /// 'TryConnect' opens the outgoing connection to the previously linked partner.
    /// The method is accessible by the owner of this ActorOutput object only. No interface exposes the method.
    /// TryConnect picks up the synchronization context and must be called on the sending thread only!
    /// The connect-process runs asynchronous and may involve an address lookup at the Remact.Catalog.
    /// An ActorInfo message is received, after the connection has been established.
    /// An ErrorMessage is received, when the partner is not reachable.
    /// </summary>
    /// <returns>A task. When this task is run to completion, the task.Result corresponds to IsOpen.</returns>
    public Task<bool> TryConnect()
    {
        if( m_MyOutputProxy != null ) return m_MyOutputProxy.TryConnect(); // calls PickupSynchronizationContext and sets m_Connected
        if( m_BasicOutput == null )   throw new InvalidOperationException("ActorOutput is not linked");
        PickupSynchronizationContext();
        m_isOpen = true;
        return ActorPort.TrueTask;
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
    /// The number of requests not yet responded by the service connected to this output.
    /// </summary>
    public int OutstandingResponsesCount
    { get {
            if (m_BasicOutput == null) return 0;
            return m_BasicOutput.OutstandingResponsesCount;
    }}

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
      /// Send a request payload to the partner on the outgoing connection.
      /// The responseHandler expects a response payload of a given type TRsp.
      /// </summary>
      /// <param name="payload">The message payload to send.</param>
      /// <param name="responseHandler">A method or lambda expression handling the asynchronous response.</param>
      /// <typeparam name="TRsp">The expected type of the response payload. Other types and errors are sent to the default message handler.</typeparam>
      public void SendOut<TRsp>(object payload, Action<TRsp, ActorMessage, TOC> responseHandler) where TRsp : class
      {
          ActorMessage msg = new ActorMessage(this, OutputClientId, NextRequestId,
                                              this, null, payload,
                                              (rsp) =>
                                              {
                                                  TRsp response;
                                                  if (rsp.Type == ActorMessageType.Response
                                                   && rsp.TryConvertPayload(out response))
                                                  {
                                                      responseHandler(response, rsp, m_outputCtx);
                                                      return null;
                                                  }
                                                  return rsp;
                                              });
          SendOut(msg);
      }

      
      /// <summary>
      /// Message is passed to users default handler.
      /// </summary>
      /// <param name="msg">ActorMessage containing Payload and Source.</param>
      private void OnDefaultInput (ActorMessage msg)
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

  }// class ActorOutput<TOC>
  #endregion
}