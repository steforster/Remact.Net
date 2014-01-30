
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Net;                  // Dns, IpAddress
using System.Threading;            // SynchronizationContext
using System.Threading.Tasks;
using Remact.Net.Remote;

namespace Remact.Net
{
  //----------------------------------------------------------------------------------------------
  /// <summary>
  /// <para>The base class of ActorInput and ActorOutput.</para>
  /// <para>It is the source or destination of message exchange.</para>
  /// </summary>
  public class ActorPort: IActorPort
  {
    #region Identification and Constructor
    
    /// <summary>
    /// IsServiceName=true : A service name must be unique in the plant, independant of host or application.
    /// IsServiceName=false: A client  name must be combined with application name, host name, instance- or process id for unique identification.
    /// </summary>
    public bool    IsServiceName    {get; internal set;}
    
    /// <summary>
    /// Identification in Trace and name of endpoint address in App.config file.
    /// </summary>
    public string  Name             {get; internal set;}
    
    /// <summary>
    /// Unique name of an application or service in the users Contract.Namespace.
    /// </summary>
    public string  AppName          {get; private set;}
    
    /// <summary>
    /// Unique instance number of the application (unique in a plant or on a host, depending on RemactDefaults.IsAppIdUniqueInPlant).
    /// </summary>
    public int     AppInstance      {get; private set;}
    
    /// <summary>
    /// Process id of the application, given by the operating system (unique on a host at a certain time).
    /// </summary>
    public int     ProcessId        {get; private set;}
    

    /// <summary>
    /// The AppIdentification is composed from AppName, HostName, AppInstance and processId to for a unique string
    /// </summary>
    public string  AppIdentification
      {get {return RemactConfigDefault.Instance.GetAppIdentification (AppName, AppInstance, HostName, ProcessId);}}

    /// <summary>
    /// Assembly version of the application.
    /// </summary>
    public Version AppVersion       {get; private set;}
    
    /// <summary>
    /// Assembly name of an important CifComponent containig some messages
    /// </summary>
    public string  CifComponentName {get; private set;}

    /// <summary>
    /// Assembly version of the important CifComponent
    /// </summary>
    public Version CifVersion       {get; private set;}
    
    /// <summary>
    /// Host running the application
    /// </summary>
    public string  HostName         {get; internal set;}
    
    /// <summary>
    /// <para>Universal resource identifier to reach the input of the service or client.</para>
    /// <para>E.g. CatalogService: http://localhost:40000/Remact/CatalogService</para>
    /// </summary>
    public  Uri Uri  {get; internal set;}

    /// <summary>
    /// <para>To support networks without DNS server, the Remact.Catalog sends a list of all IP-Adresses of a host.</para>
    /// <para>May be null, when no info from Remact.Catalog has been received yet.</para>
    /// </summary>
    public List<string> AddressList  {get; internal set;}

    /// <summary>
    /// After a service has no message received for TimeoutSeconds, it may render the connection to this client as disconnected.
    /// 0 means no timeout. 
    /// The client should send at least 2 messages each TimeoutSeconds-period in order to keep the correct connection state on the service.
    /// A Service is trying to notify 2 messages each TimeoutSeconds-period in order to check a dual-Http connection.
    /// </summary>
    public int TimeoutSeconds   {get; set;}

    /// <summary>
    /// <para>Creates an address for a client or service running in the current application on the local host.</para>
    /// </summary>
    /// <param name="name">The application internal name of this service or client.</param>
    /// <param name="defaultMessageHandler">The method to be called when a request or response is received and no other handler is applicatable.</param>
    public ActorPort ( string name, MessageHandler defaultMessageHandler=null )
    {
      AppName          = RemactConfigDefault.Instance.ApplicationName;
      AppVersion       = RemactConfigDefault.Instance.ApplicationVersion;
      AppInstance      = RemactConfigDefault.Instance.ApplicationInstance;
      ProcessId        = RemactConfigDefault.Instance.ProcessId;
      Name             = name;
      IsServiceName    = false; // must be set to true by user, when a unique servicename is given.
      CifComponentName = RemactConfigDefault.Instance.CifAssembly.GetName().Name;
      CifVersion       = RemactConfigDefault.Instance.CifAssembly.GetName().Version;
      TimeoutSeconds   = 60;
      TraceConnect     = true;
      HostName         = Dns.GetHostName(); // concrete name of localhost
      DefaultInputHandler = defaultMessageHandler;

      // Prepare uri for application internal partner. May be overwritten, when linking to external partner.
      if (!RemactConfigDefault.Instance.IsProcessIdUsed (AppInstance))
      {
          Uri = new Uri(string.Format ("cli://{0}/{1}-{2:0#}/{3}", HostName, AppName, AppInstance, Name));
      }
      else
      {
          Uri = new Uri(string.Format ("cli://{0}/{1}({2})/{3}", HostName, AppName, ProcessId, Name));
      }
    }// CTOR1

#if !BEFORE_NET45
    /// <summary>
    /// <para>Creates an I/O port for an awaitable service or client running in the current application on the local host.</para>
    /// </summary>
    /// <param name="name">The unique name of a service or the application internal name of a client.</param>
    /// <param name="defaultMessageHandlerAsync">The awaitable method to be called when a request or response is received and no other handler is applicatable.</param>
    public ActorPort( string name, MessageHandlerAsync defaultMessageHandlerAsync )
    : this( name )
    {
        DefaultInputHandlerAsync = defaultMessageHandlerAsync;
    }// CTOR2
#endif

    /// <summary>
    /// <para>Creates a dummy address for a client or service not running yet.</para>
    /// </summary>
    public ActorPort ()
    {
      AppName          = string.Empty;
      AppVersion       = new Version ();
      AppInstance      = 0;
      ProcessId        = 0;
      Name             = "<unlinked>";
      IsServiceName    = false;
      CifComponentName = string.Empty;
      CifVersion       = new Version ();
      TimeoutSeconds   = 60;
      TraceConnect     = true;
      HostName         = string.Empty;
      Uri              = null;
    }// default CTOR


    /// <summary>
    /// <para>(internal) Copy data from a ActorInfo, but keep SenderContext.</para>
    /// </summary>
    /// <param name="p">Copy data from partner p</param>
    internal void UseDataFrom (ActorInfo p)
    {
      // copy ActorPort members from a remote Actor:
      AppName     = p.AppName;
      AppVersion  = p.AppVersion;
      AppInstance = p.AppInstance;
      ProcessId   = p.ProcessId;
      Name        = p.Name;
      IsServiceName    = p.IsServiceName;
      CifComponentName = p.CifComponentName;
      CifVersion       = p.CifVersion;
      TimeoutSeconds   = p.TimeoutSeconds;
      HostName    = p.HostName;
      Uri         = p.Uri;
      AddressList = p.AddressList;
    }


    /// <summary>
    /// Trace or display status info
    /// </summary>
    /// <returns>Readable communication partner description</returns>
    public override string ToString ()
    {
      return ToString (string.Empty, 0);
    }


    /// <summary>
    /// Trace or display formatted status info
    /// </summary>
    /// <param name="prefix">Start with this text</param>
    /// <param name="intendCnt">intend the following lines by some spaces</param>
    /// <returns>Formatted communication partner description</returns>
    public string ToString (string prefix, int intendCnt)
    {
      string intend;
      string uri;
      if (prefix.Length == 0)
      {
        if (IsServiceName)  prefix = "Remact Service";//+Usage.ToString();
                      else  prefix = "Remact Client ";//+Usage.ToString();
      }

      if (intendCnt==0) intend = " ";
                   else intend = Environment.NewLine.PadRight(intendCnt);

      if (Uri == null)  uri    = HostName;
                   else uri    = Uri.ToString();
      
      int versionCount = 2;
      if (AppVersion.Revision != 0 || CifVersion.Revision != 0)
      {
        versionCount = 4;
      }
      else if (AppVersion.Build != 0 || CifVersion.Build != 0) 
      {
        versionCount = 3;
      }
      
      if (AppName.Length == 0)
      {
        return String.Format ("{0}: '{1}' uri = '{2}'",
                              prefix, Name, uri);
      }
      else
      {
//        return String.Format ("{0}: '{1}' in application '{2}' (V{3}){4}using {5} (V{6}){7}uri = '{8}'",
//                              prefix, Name, AppIdentification, AppVersion.ToString (versionCount), // ACHTUNG ToString(3) kann Exception geben, falls nur 2 Felder spezifiziert sind !
//                       /*4*/intend, CifComponentName, CifVersion.ToString (versionCount),
//                       /*7*/intend, uri);
          if (IsServiceName)
          {       
              return String.Format ("{0}: '{1}'{2}in application {3} V {4}",
                     prefix, uri, intend, AppIdentification, AppVersion.ToString (versionCount));
          }
          else
          {
              return String.Format ("{0}: '{1}' in application {2} V {3}",
                     prefix, Name, AppIdentification, AppVersion.ToString (versionCount));
          }
      }
    }// ToString

    #endregion
    //----------------------------------------------------------------------------------------------
    #region IRemoteActor implementation

    /// <summary>
    /// Shutdown the outgoing remote connection. Send a disconnect message to the partner.
    /// Close the incoming network connection.
    /// </summary>
    public virtual void Disconnect ()
    {
        m_Connected = false;
        ManagedThreadId = 0;
        SyncContext = null;
    }
    
      
    /// <summary>
    /// (static) Close all incoming network connections and send a ServiceDisable messages to Remact.CatalogService.
    ///          Disconnects all outgoing network connections and send ClientDisconnectRequest to connected services.
    /// </summary>
    public static void DisconnectAll()
    {
        try
        {
            RemactCatalogClient.DisconnectAll();
        }
        catch (Exception ex)
        {
            RaLog.Exception("Svc: Error while closing all services and disconnecting all clients", ex, RemactApplication.Logger);
        }
    }


    /// <summary>
    /// Used by the library to post a request or response message to the input of this partner. May be called on any thread.
    /// Usage:
    /// Internal:    Post a message into this partners input queue.
    /// Serviceside: Source.PostInput() sends a response from client-stub to the remote client.
    /// Clientside:  Post a response into this clients input queue.
    /// </summary>
    /// <param name="msg">A <see cref="ActorMessage"/> the 'Source' property references the sending partner.</param>
    public void PostInput(ActorMessage msg)
    {
        if( !m_Connected )
        {
            var disconnectMsg = msg.Payload as ActorInfo;
            if (disconnectMsg == null || disconnectMsg.Usage != ActorInfo.Use.ServiceDisconnectResponse)
            {
                DispatchingError(msg, new ErrorMessage(ErrorMessage.Code.NotConnected, "Cannot post message"));
            }
        }
        else if (m_RedirectIncoming != null)
        {
            m_RedirectIncoming.PostInput(msg); // to BasicServiceUser (post notification) or to ClientIdent of BasicClientAsync
        }
        else if (IsMultithreaded)
        {
            MessageHandlerBase(msg); // response to unsynchronized context (Test1.ClientNoSync)
        }
        else if (this.SyncContext == null)
        {
            throw new Exception ("Remact: Destination of '"+Name+"' has not picked up a thread synchronization context.");
        }
        else
        {
            try
            {
            #if !BEFORE_NET45
                this.SyncContext.Post( MessageHandlerBaseAsync, msg );// Message is posted into the message queue
            #else
                this.SyncContext.Post(MessageHandlerBase, msg);// Message is posted into the message queue
            #endif
            }
            catch( Exception ex )
            {
                DispatchingError(msg, new ErrorMessage(ErrorMessage.Code.CouldNotDispatch, ex));
            }
        }
    }


    #endregion
    //----------------------------------------------------------------------------------------------
    #region Sending notification messages to the connected actor port (service or client)

    protected IRemoteActor m_BasicOutput;    // ActorInput, BasicClientAsync or BasicServiceUser

    /// <summary>
    /// The OutputClientId used on the connected service to identify this client.
    /// OutputClientId is generated by the service on first connect or service restart.
    /// It remains stable on reconnect or client restart.
    /// </summary>
    public int OutputClientId { get; internal set; }

    /// <summary>
    /// Send a notification message to the partner ActorPort.
    /// Invoke the specified remote method and pass the payload as parameter.
    /// Do not wait for a response.
    /// </summary>
    /// <param name="method">The name of the method to be called.</param>
    /// <param name="payload">The message payload to send. It must be of a type acceptable for the called method.</param>
    public void Notify(string method, object payload)
    {
        ActorMessage msg = new ActorMessage(this, OutputClientId, 0,
                                            this, method, payload);
        SendOut(msg);
    }

    /// <summary>
    /// Send a request or notification message to the partner on the outgoing connection.
    /// At least a ReadyMessage will asynchronously be received through 'PostInput', when the partner has processed the request.
    /// Usage:
    /// Clientside:  Send a request to the connected remote service.
    /// Internal:    Send a message to the connected partner running on another thread synchronization context.
    /// Serviceside: Source.SendOut() sends a request from client-proxy to the internal service.
    /// </summary>
    /// <param name="msg">A <see cref="ActorMessage"/>the 'Source' property references the sending partner, where the response is expected.</param>
    public void SendOut(ActorMessage msg)
    {
        if (m_BasicOutput == null) throw new Exception("Remact: Output of '" + Name + "' has not been linked");

        if (!m_Connected)
        {
            RaLog.Warning("Remact", "ActorPort '" + Name + "' is not connected. Cannot send message!", Logger);
            return;
        }

        if (!IsMultithreaded)
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            if (SyncContext == null)
            {
                ManagedThreadId = threadId;
                SyncContext = SynchronizationContext.Current;    // set on first send operation
            }
            else if (ManagedThreadId != threadId)
            {
                throw new Exception("Remact: wrong thread synchronization context when sending from '" + Name + "'");
            }
        }
        m_BasicOutput.PostInput(msg);
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Sending request messages to the connected actor port (service or client) and awaiting a response payload.

    /// <summary>
    /// Ask: send a request message to the partner ActorPort.
    /// Invokes the specified remote method and pass the payload as parameter.
    /// The remote method has to return a payload of type TRsp.
    /// The asynchronous responseHandler expects a payload of type TRsp.
    /// </summary>
    /// <param name="method">The name of the method to be called.</param>
    /// <param name="payload">The message payload to send. It must be of a type acceptable for the called method.</param>
    /// <param name="responseHandler">A method or lambda expression handling the asynchronous response.</param>
    /// <typeparam name="TRsp">The expected type of the response payload. When receiving other types, the message will be passed to the default message handler.</typeparam>
    public void Ask<TRsp>(string method, object payload, Action<TRsp, ActorMessage> responseHandler) where TRsp : class
    {
        ActorMessage msg = new ActorMessage(this, OutputClientId, NextRequestId,
                                            this, method, payload,

                                            delegate(ActorMessage rsp)
                                            {
                                                TRsp response;
                                                if (responseHandler != null
                                                 && rsp.Type == ActorMessageType.Response
                                                 && rsp.TryConvertPayload(out response))
                                                {
                                                    responseHandler(response, rsp);
                                                    return null;
                                                }
                                                return rsp;
                                            });
        SendOut(msg);
    }


    /// <summary>
    /// Ask: sends a request message to the partner ActorPort.
    /// Invokes the specified remote method and passes the payload as parameter.
    /// The remote method has to return a payload of type TRsp.
    /// The returned asynchronous Task expects a payload of type TRsp.
    /// </summary>
    /// <param name="method">The name of the method to be called.</param>
    /// <param name="payload">The message payload to send. It must be of a type acceptable for the called method.</param>
    /// <typeparam name="TRsp">The expected type of the response payload. 
    ///    When receiving a payload of OtherType, an ActorException{OtherType} will be thrown.</typeparam>
    /// <returns>A Task to track the asynchronous completion of the request.</returns>
    public Task<ActorMessage<TRsp>> Ask<TRsp>(string method, object payload) where TRsp : class
    {
        var tcs = new TaskCompletionSource<ActorMessage<TRsp>>();

        ActorMessage msg = new ActorMessage(this, OutputClientId, NextRequestId,
                                            this, method, payload,

                                            delegate (ActorMessage rsp)
                                            {
                                                TRsp typedPayload;
                                                if (rsp.Type == ActorMessageType.Response
                                                 && rsp.TryConvertPayload(out typedPayload))
                                                {
                                                    var typedRsp = new ActorMessage<TRsp>(typedPayload, rsp);
                                                    tcs.SetResult(typedRsp);
                                                }
                                                else
                                                {
                                                    var dynamicRsp = new ActorMessage<dynamic>((dynamic)rsp.Payload, rsp);
                                                    var ex = new ActorException<dynamic>(dynamicRsp, "unexpected response type '" + rsp.Payload.GetType().FullName + "' from method '" + method + "'");
                                                    tcs.SetException(ex);
                                                }
                                                return null;
                                            });

        SendOut(msg);
        return tcs.Task;
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Message dispatching

    /// <summary>
    /// The dispatcher for incoming messages must be added by the user.
    /// </summary>
    public Dispatcher Dispatcher
    {
        get
        {
            if (m_Dispatcher == null) m_Dispatcher = new Dispatcher();
            return m_Dispatcher;
        }
    }

    /// <summary>
    /// Trace switch: Traces all sent messages. Default = false;
    /// </summary>
    public bool TraceSend    {get; set;}

    /// <summary>
    /// Trace switch: Traces all received messages. Default = false;
    /// </summary>
    public bool TraceReceive { get; set; }

    /// <summary>
    /// Trace switch: Traces connect/disconnect messages (not to the catalog service). Default = true;
    /// </summary>
    public bool TraceConnect { get; set; }

    /// <summary>
    /// Set your logging object here (null by default).
    /// It is passed to the logging methods of RaLog.ITracePlugin.
    /// You will use it when writing your own adapter class based on RaLog.ITracePlugin.
    /// The adapter class is needed to redirect trace output to your own logging/tracing framework.
    /// </summary>
    public object Logger     { get; set; }

    /// <summary>
    /// The request id given to the last message sent from this client.
    /// The request id is incremented by the client for each request.
    /// The same id is returned in the response from the service.
    /// </summary>
    public int LastRequestIdSent  {get; internal set;}

    /// <summary>
    /// Increment the LastRequestIdSent.
    /// </summary>
    internal int NextRequestId
    {
        get
        {
            if (LastRequestIdSent >= 999999) LastRequestIdSent = 9;
            return ++LastRequestIdSent;
        }
    }

    /// <summary>
    /// Multithreaded partners do not use a message input queue. All threads may directly call InputHandler delegates.
    /// Default = false.
    /// </summary>
    public bool IsMultithreaded    {get; set;}

    /// <summary>
    /// Incoming messages are directly redirected to this partner (used library intern)
    /// </summary>
    internal protected IRemoteActor  m_RedirectIncoming;

    /// <summary>
    /// False when not connected or disconnected. Prevents message passing during shutdown.
    /// </summary>
    internal protected bool          m_Connected;
    private  Dispatcher              m_Dispatcher;
    private  ActorMessage            m_CurrentReq;
    internal SynchronizationContext  SyncContext;
    internal int                     ManagedThreadId;
    internal MessageHandler          DefaultInputHandler;


    private void DispatchingError( ActorMessage msg, ErrorMessage err )
    {
        try
        {
            var err1 = msg.Payload as ErrorMessage;
            if( err1 != null )
            {
                err.InnerMessage = err1.Message;
                err.StackTrace = err1.StackTrace;
            }

            if (msg.IsRequest)
            {
                if (err.Error == ErrorMessage.Code.CouldNotDispatch) err.Error = ErrorMessage.Code.UnhandledExceptionOnService;
                msg.SendResponseFrom(this, err, null);
            }
            else
            {
                RaLog.Error(msg.SvcSndId, err.ToString(), Logger);
            }
        }
        catch( Exception ex )
        {
            RaLog.Exception( "Cannot return dispatching error message", ex, Logger );
        }
    }

    // Link to application-internal partner (used by library only)
    internal void PassResponsesTo(IRemoteActor input)
    {
        m_RedirectIncoming = input;
    }

    internal void PickupSynchronizationContext()
    {
        if( IsMultithreaded )
        {
            SyncContext = null;
            ManagedThreadId = 0;
        }
        else
        {
            SynchronizationContext currentThreadSyncContext = SynchronizationContext.Current;
            if( currentThreadSyncContext == null )
            {
                throw new Exception( "Remact: Thread connecting ActorPort '" + Name + "' has no message queue. Set ActorPort.IsMultithreaded=true, when your message handlers are threadsafe!" );
            }
            else
            {
                int threadId = Thread.CurrentThread.ManagedThreadId;
                if (SyncContext != null && ManagedThreadId != threadId && m_Connected)
                {
                    RaLog.Error( "Remact", "Thread connecting ActorPort '" + Name + "' has changed. Only one synchronization context is supported!", Logger );
                }
                ManagedThreadId = threadId;
                SyncContext = currentThreadSyncContext;
            }
        }
    }


    // Message comes out of message queue in user thread
    private void MessageHandlerBase( object userState )
    {
        try
        {
            DispatchMessage( userState as ActorMessage );
        }
        catch( Exception ex )
        {
            DispatchingError( userState as ActorMessage, new ErrorMessage( ErrorMessage.Code.CouldNotDispatch, ex ) );
        }
    }


    // Message is passed to the lambda functions of the sending context or to the default response handler
    internal void DispatchMessage (ActorMessage id)
    {
        if( !m_Connected )
        {
            RaLog.Warning( "Remact", "ActorPort '" + Name + "' is not connected. Cannot dispatch message!", Logger );
            return;
        }

        if( m_CurrentReq != null )
        {
            RaLog.Error( "Remact", "Multithreading not allowed when dispatching a message in " + Name, Logger );
            Thread.Sleep(0); // let the other thread finish - may be it helps..
        }

        if (!IsMultithreaded)
        {
            var m = id.Payload as IExtensibleActorMessage;
            if( m != null ) m.BoundSyncContext = SyncContext;
        }

        try
        {
            m_CurrentReq   = id;
            id.Destination = this;
            var connectMsg = id.Payload as ActorInfo;
            if (connectMsg != null)
            {
                if (connectMsg.Usage == ActorInfo.Use.ClientConnectRequest
                 || connectMsg.Usage == ActorInfo.Use.ClientDisconnectRequest)
                {
                    if (TraceConnect && IsServiceName)
                    {
                        RaLog.Info(id.SvcRcvId, String.Format("{0} to Remact service './{1}'", connectMsg.Usage.ToString(), Name), Logger);
                    }
                    OnConnectDisconnect(id, connectMsg); // may be overloaded
                    return;
                }// -------
            }

            if (TraceReceive)
            {
                if( IsServiceName ) RaLog.Info( id.SvcRcvId, id.ToString(), Logger );
                               else RaLog.Info( id.CltRcvId, id.ToString(), Logger );
            }
            bool needsResponse = id.IsRequest;

            if (id.DestinationLambda != null)
            {
                id = id.DestinationLambda(id); // a response to a lambda function, one of the On<T> extension methods may handle the message type
            }

            if (id != null && m_Dispatcher != null)
            {
                id = m_Dispatcher.CallMethod(id, null); // TODO context
            }

            if (id != null) // not handled yet
            { 
                if (DefaultInputHandler != null)
                {
                    DefaultInputHandler (id); // MessageHandlerlegate
                }
                else
                {
                    //No trace for anonymous ActorOutput
                    //RaLog.Error( "Remact", "Unhandled response: " + id.Payload, Logger );
                }
            }

            if (id != null && needsResponse && id.Response == null)
            {
                id.SendResponse(new ReadyMessage());
            }
        }
        finally
        {
            m_CurrentReq = null;
        }
    }// DispatchMessage


    /// <summary>
    /// Message is passed to users connect/disconnect event handler, may be overloaded and call a MessageHandler;TSC>
    /// </summary>
    /// <param name="msg">ActorMessage containing Payload and Source.</param>
    /// <param name="info">The message payload.</param>
    /// <returns>True when handled.</returns>
    protected virtual bool OnConnectDisconnect(ActorMessage msg, ActorInfo info)
    {
         return false;
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Clock

    private int m_Clock;
    private int m_TickCountStart;

    /// <summary>
    /// Start the timer. Use ClockSecondsPassed() to check whether time has passed.
    /// </summary>
    /// <param name="seconds">Time to pass.</param>
    public void StartClockSeconds (int seconds)
    {
      m_Clock = seconds*1000; // Milliseconds, max = 25 days
      m_TickCountStart = Environment.TickCount;
    }

    /// <summary>
    /// Check if time has passed.
    /// </summary>
    /// <returns>True if time has passed.</returns>
    public bool ClockSecondsPassed ()
    {
      int delta  = Environment.TickCount - m_TickCountStart;
      if (delta >= m_Clock || delta < 0)
      {
        m_Clock = 0; // finished
        return true;
      }
      return false;
    }

    #endregion
  }
}