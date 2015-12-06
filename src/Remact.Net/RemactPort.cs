
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Net;                  // Dns, IpAddress
using System.Threading;            // SynchronizationContext
using System.Threading.Tasks;
using Remact.Net.Remote;

namespace Remact.Net
{
    /// <summary>
    /// <para>RemactPort is the base class of <see cref="RemactPortProxy"/> and <see cref="RemactPortService"/>.</para>
    /// <para>It is the source or destination of message exchange.</para>
    /// </summary>
    public class RemactPort : IRemactPort, IRemotePort
    {
        #region Identification and Constructor

        /// <summary>
        /// IsServiceName=true : A service name must be unique in the plant, independant of host or application.
        /// IsServiceName=false: A client  name must be combined with application name, host name, instance- or process id for unique identification.
        /// </summary>
        public bool IsServiceName { get; internal set; }

        /// <summary>
        /// Identification in log and name of endpoint address in App.config file.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Unique name of an application or service in the users Contract.Namespace.
        /// </summary>
        public string AppName { get; private set; }

        /// <summary>
        /// Unique instance number of the application (unique in a plant or on a host, depending on RemactDefaults.IsAppIdUniqueInPlant).
        /// </summary>
        public int AppInstance { get; private set; }

        /// <summary>
        /// Process id of the application, given by the operating system (unique on a host at a certain time).
        /// </summary>
        public int ProcessId { get; private set; }


        /// <summary>
        /// The AppIdentification is composed from AppName, HostName, AppInstance and processId to for a unique string
        /// </summary>
        public string AppIdentification
        { get { return RemactConfigDefault.Instance.GetAppIdentification(AppName, AppInstance, HostName, ProcessId); } }

        /// <summary>
        /// Assembly version of the application.
        /// </summary>
        public Version AppVersion { get; private set; }

        /// <summary>
        /// Assembly name of an important CifComponent containig some messages
        /// </summary>
        public string CifComponentName { get; private set; }

        /// <summary>
        /// Assembly version of the important CifComponent
        /// </summary>
        public Version CifVersion { get; private set; }

        /// <summary>
        /// Host running the application
        /// </summary>
        public string HostName { get; internal set; }

        /// <summary>
        /// <para>Universal resource identifier to reach the input of the service or client.</para>
        /// <para>E.g. CatalogService: http://localhost:40000/Remact/CatalogService</para>
        /// </summary>
        public Uri Uri { get; internal set; }

        /// <summary>
        /// <para>To support networks without DNS server, the Remact.Catalog sends a list of all IP-Addresses of a host.</para>
        /// <para>May be null, when no info from Remact.Catalog has been received yet.</para>
        /// </summary>
        public List<string> AddressList { get; internal set; }

        /// <summary>
        /// After a service has no message received for TimeoutSeconds, it may render the connection to this client as disconnected.
        /// 0 means no timeout. 
        /// The client should send at least 2 messages each TimeoutSeconds-period in order to keep the correct connection state on the service.
        /// A Service is trying to notify 2 messages each TimeoutSeconds-period in order to check a dual-Http connection.
        /// </summary>
        public int TimeoutSeconds { get; set; }

        /// <summary>
        /// <para>Creates an address for a client or service running in the current application on the local host.</para>
        /// </summary>
        /// <param name="name">The application internal name of this service or client.</param>
        /// <param name="defaultMessageHandler">The method to be called when a request or response is received and no other handler is applicatable.</param>
        public RemactPort(string name, MessageHandler defaultMessageHandler = null)
        {
            AppName = RemactConfigDefault.Instance.ApplicationName;
            AppVersion = RemactConfigDefault.Instance.ApplicationVersion;
            AppInstance = RemactConfigDefault.Instance.ApplicationInstance;
            ProcessId = RemactConfigDefault.Instance.ProcessId;
            Name = name;
            IsServiceName = false; // must be set to true by user, when a unique servicename is given.
            CifComponentName = RemactConfigDefault.Instance.CifAssembly.GetName().Name;
            CifVersion = RemactConfigDefault.Instance.CifAssembly.GetName().Version;
            TimeoutSeconds = 60;
            TraceConnect = true;
            HostName = Dns.GetHostName(); // concrete name of localhost
            DefaultInputHandler = defaultMessageHandler;

            // Prepare uri for application internal partner. May be overwritten, when linking to external partner.
            if (!RemactConfigDefault.Instance.IsProcessIdUsed(AppInstance))
            {
                Uri = new Uri(string.Format("cli://{0}/{1}-{2:0#}/{3}", HostName, AppName, AppInstance, Name));
            }
            else
            {
                Uri = new Uri(string.Format("cli://{0}/{1}({2})/{3}", HostName, AppName, ProcessId, Name));
            }
        }// CTOR1


        /// <summary>
        /// <para>Creates a dummy address for a client or service not running yet.</para>
        /// </summary>
        public RemactPort()
        {
            AppName = string.Empty;
            AppVersion = new Version();
            AppInstance = 0;
            ProcessId = 0;
            Name = "<unlinked>";
            IsServiceName = false;
            CifComponentName = string.Empty;
            CifVersion = new Version();
            TimeoutSeconds = 60;
            TraceConnect = true;
            HostName = string.Empty;
            Uri = null;
        }// default CTOR


        /// <summary>
        /// <para>(internal) Copy data from a ActorInfo, but keep SenderContext.</para>
        /// </summary>
        /// <param name="p">Copy data from partner p</param>
        internal void UseDataFrom(ActorInfo p)
        {
            // get RemactPort members from a remote Actor:
            AppName = p.AppName;
            AppVersion = p.AppVersion;
            AppInstance = p.AppInstance;
            ProcessId = p.ProcessId;
            Name = p.Name;
            IsServiceName = p.IsServiceName;
            CifComponentName = p.CifComponentName;
            CifVersion = p.CifVersion;
            TimeoutSeconds = p.TimeoutSeconds;
            HostName = p.HostName;
            Uri = p.Uri;
            AddressList = p.AddressList;
        }


        /// <summary>
        /// Log or display status info
        /// </summary>
        /// <returns>Readable communication partner description</returns>
        public override string ToString()
        {
            return ToString(string.Empty, 0);
        }


        /// <summary>
        /// Log or display formatted status info
        /// </summary>
        /// <param name="prefix">Start with this text</param>
        /// <param name="intendCnt">intend the following lines by some spaces</param>
        /// <returns>Formatted communication partner description</returns>
        public string ToString(string prefix, int intendCnt)
        {
            string intend;
            string uri;
            if (prefix.Length == 0)
            {
                if (IsServiceName) prefix = "Remact Service";
                else prefix = "Remact Client ";
            }

            if (intendCnt == 0) intend = " ";
            else intend = Environment.NewLine.PadRight(intendCnt);

            if (Uri == null) uri = HostName;
            else uri = Uri.ToString();

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
                return String.Format("{0}: '{1}' uri = '{2}'",
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
                    return String.Format("{0}: '{1}'{2}in application {3} V {4}",
                           prefix, uri, intend, AppIdentification, AppVersion.ToString(versionCount));
                }
                else
                {
                    return String.Format("{0}: '{1}' in application {2} V {3}",
                           prefix, Name, AppIdentification, AppVersion.ToString(versionCount));
                }
            }
        }// ToString

        #endregion
        //----------------------------------------------------------------------------------------------
        #region IRemotePort implementation

        /// <inheritdoc />
        public virtual Task<bool> ConnectAsync()
        {
            throw new NotImplementedException(); // implemented in subclass
        }

        /// <summary>
        /// Shutdown the outgoing remote connection. Send a disconnect message to the partner.
        /// Close the incoming network connection.
        /// </summary>
        public virtual void Disconnect()
        {
            m_isOpen = false;
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
        /// Used by the library to post a request or response message to the input of this port. May be called on any thread.
        /// Usage:
        /// Internal:    Post a message into this ports input queue.
        /// Serviceside: Source.PostInput() sends a response from client-stub to the remote client.
        /// Clientside:  Post a response into this clients input queue.
        /// </summary>
        /// <param name="msg">A <see cref="RemactMessage"/> the 'Source' property references the sending partner.</param>
        void IRemotePort.PostInput(RemactMessage msg)
        {
            if (!m_isOpen)
            {
                DispatchingError(msg, new ErrorMessage(ErrorCode.NotConnected, "Input port not open, cannot post message"));
            }
            else if (IsMultithreaded)
            {
                MessageHandlerBase(msg); // response to unsynchronized context (Test1.ClientNoSync)
            }
            else if (this.SyncContext == null)
            {
                throw new InvalidOperationException("Remact: Destination of '" + Name + "' has not picked up a thread synchronization context.");
            }
            else
            {
                try
                {
                    this.SyncContext.Post(MessageHandlerBase, msg);// Message is posted into the message queue
                }
                catch (Exception ex)
                {
                    DispatchingError(msg, new ErrorMessage(ErrorCode.CouldNotDispatch, ex));
                }
            }
        }

        /// <summary>
        /// Returns 0 unless otherwise implemented in subclass.
        /// </summary>
        public virtual int OutstandingResponsesCount
        {
            get
            {
                return 0;
            }
        }

        #endregion
        //----------------------------------------------------------------------------------------------
        #region Sending notification messages to the connected actor port (service or client)


        // Send requests, notifications and responses to application-internal proxy (used by library only)
        internal IRemotePort RedirectIncoming; // RemactPortProxy / RemactPortService (app. internal) or RemactClient / RemactServiceUser (remote)

        /// <summary>
        /// Send a notification message to the partner RemactPort.
        /// Invoke the specified remote method and pass the payload as parameter.
        /// Do not wait for a response.
        /// </summary>
        /// <param name="method">The name of the method to be called.</param>
        /// <param name="payload">The message payload to send. It must be of a type acceptable for the called method.</param>
        public void Notify(string method, object payload)
        {
            RemactMessage msg = NewMessage(method, payload, RemactMessageType.Notification, null);
            SendOut(msg);
        }

        /// <summary>
        /// Send a request or notification message to the partner on the outgoing connection.
        /// Usage:
        /// Clientside:  Send a request to the connected remote service.
        /// Internal:    Send a message to the connected partner running on another thread synchronization context.
        /// Serviceside: Source.SendOut() sends a request from client-proxy to the internal service.
        /// </summary>
        /// <param name="msg">A <see cref="RemactMessage"/>the 'Source' property references the sending partner, where the response is expected.</param>
        public void SendOut(RemactMessage msg)
        {
            if (RedirectIncoming == null) throw new InvalidOperationException("Remact: Output of '" + Name + "' has not been linked. Cannot send message.");

            if (!m_isOpen) throw new InvalidOperationException("Remact: ActorPort '" + Name + "' is not connected. Cannot send message.");

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
                    throw new InvalidOperationException("Remact: wrong thread synchronization context when sending from '" + Name + "'");
                }
            }

            RedirectIncoming.PostInput(msg);
        }

        #endregion
        //----------------------------------------------------------------------------------------------
        #region Sending request messages to the connected actor port (service or client) and awaiting a response payload.

        // must be overridden to be able to send messages from the concrete port type.
        internal virtual RemactMessage NewMessage(string method, object payload, RemactMessageType messageType,
                                                   AsyncResponseHandler responseHandler)
        {
            throw new NotSupportedException("not allowed to send a message from RemactPortService");
        }

        /// <summary>
        /// SendReceiveAsync: send a request message to the partner RemactPort.
        /// Invokes the specified remote method and pass the payload as parameter.
        /// The remote method has to return a payload of type TRsp.
        /// The asynchronous responseHandler expects a payload of type TRsp.
        /// Unexpected response types do not throw an exception. Such (error) messages are sent to the default message handler.
        /// </summary>
        /// <param name="method">The name of the method to be called.</param>
        /// <param name="payload">The message payload to send. It must be of a type acceptable for the called method.</param>
        /// <param name="responseHandler">A method or lambda expression handling the asynchronous response.</param>
        /// <typeparam name="TRsp">The expected type of the response payload. When receiving other types, the message will be passed to the default message handler.</typeparam>
        public void SendReceiveAsync<TRsp>(string method, object payload, Action<TRsp, RemactMessage> responseHandler) where TRsp : class
        {
            RemactMessage msg = NewMessage(method, payload, RemactMessageType.Request,

                                                delegate (RemactMessage rsp)
                                                {
                                                    TRsp response;
                                                    if (responseHandler != null
                                                     && rsp.MessageType == RemactMessageType.Response
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
        /// SendReceiveAsync: sends a request message to the partner RemactPort.
        /// Invokes the specified remote method and passes the payload as parameter.
        /// The remote method has to return a payload of type TRsp.
        /// The returned asynchronous Task.Result is of type TRsp.
        /// </summary>
        /// <param name="method">The name of the method to be called.</param>
        /// <param name="payload">The message payload to send. It must be of a type acceptable for the called method.</param>
        /// <typeparam name="TRsp">The expected type of the response payload. 
        ///    When receiving a payload of another type, a RemactException will be thrown.</typeparam>
        /// <returns>A Task to track the asynchronous completion of the request.</returns>
        public Task<RemactMessage<TRsp>> SendReceiveAsync<TRsp>(string method, object payload) where TRsp : class
        {
            RemactMessage sentMessage;
            return SendReceiveAsync<TRsp>(method, payload, out sentMessage, true);
        }

        /// <summary>
        /// SendReceiveAsync: sends a request message to the partner RemactPort.
        /// Invokes the specified remote method and passes the payload as parameter.
        /// The remote method has to return a payload of type TRsp.
        /// The returned asynchronous Task.Result is of type TRsp.
        /// </summary>
        /// <param name="method">The name of the method to be called.</param>
        /// <param name="payload">The message payload to send. It must be of a type acceptable for the called method.</param>
        /// <param name="sentMessage">The message that has been sent. Useful for tracing.</param>
        /// <param name="throwException">When set to true, a response with unexpected type (e.g. an ErrorMessage) will be thrown as a RemactException.
        ///                              When set to false, a message with unexpected response type will be sent to the default message handler.</param>
        /// <typeparam name="TRsp">The expected type of the response payload.</typeparam>
        /// <returns>A Task to track the asynchronous completion of the request.</returns>
        public Task<RemactMessage<TRsp>> SendReceiveAsync<TRsp>(string method, object payload, out RemactMessage sentMessage, bool throwException = true) where TRsp : class
        {
            var tcs = new TaskCompletionSource<RemactMessage<TRsp>>();

            sentMessage = NewMessage(method, payload, RemactMessageType.Request,

                            delegate (RemactMessage rsp) // the response handler
                            {
                                TRsp typedPayload;
                                if (rsp.MessageType == RemactMessageType.Response && rsp.TryConvertPayload(out typedPayload))
                                {
                                    var typedRsp = new RemactMessage<TRsp>(typedPayload, rsp);
                                    tcs.SetResult(typedRsp);
                                    return null;
                                }
                                else if (throwException)
                                {
                                    //var dynamicRsp = new RemactMessage<dynamic>((dynamic)rsp.Payload, rsp);
                                    //var ex = new RemactException<dynamic>(dynamicRsp, "unexpected response type '" + rsp.Payload.GetType().FullName + "' from method '" + method + "'");
                                    Exception ex;
                                    ErrorMessage error;
                                    if (rsp.MessageType == RemactMessageType.Error && rsp.TryConvertPayload(out error))
                                    {
                                        Exception inner = null;
                                        if (!string.IsNullOrEmpty(error.InnerMessage))
                                        {
                                            inner = new Exception(error.InnerMessage);
                                        }
                                        ex = new RemactException(rsp, error.ErrorCode, error.ToString(), inner, error.StackTrace);
                                    }
                                    else
                                    {
                                        ex = new RemactException(rsp, ErrorCode.UnexpectedResponsePayloadType, "got unexpected response payload type '" + rsp.Payload.GetType().FullName + "' from method '" + method + "'");
                                    }

                                    tcs.SetException(ex);
                                    return null;
                                }
                                return rsp; // response will be handled by default message handler
                        });

            SendOut(sentMessage);
            return tcs.Task;
        }

        /*    private Exception ReconstructedException (ErrorMessage err)
            {
                Exception inner = null;
                if (!string.IsNullOrEmpty (err.InnerMessage))
                {
                    inner = new Exception(err.InnerMessage);
                }

                switch (err.Error)
                {
                    case Code.NotImplementedOnService:
                        return new NotImplementedException(err.Message, inner);

                    case Code.ArgumentExceptionOnService:
                        return new ArgumentException(err.Message, inner);

                    default:
                        return new Exception(err.Error.ToString() + " " + err.Message, inner);
                }
            }*/

        #endregion
        //----------------------------------------------------------------------------------------------
        #region Message dispatching

        /// <summary>
        /// Get the dispatcher for incoming messages. The user must call RemactDispatcher.AddActorInterface() to make the dispatcher ready for incoming messages.
        /// </summary>
        public RemactDispatcher InputDispatcher
        {
            get
            {
                if (m_Dispatcher == null) m_Dispatcher = new RemactDispatcher();
                return m_Dispatcher;
            }
        }

        /// <summary>
        /// Trace switch: Traces all sent messages. Default = false;
        /// </summary>
        public bool TraceSend { get; set; }

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
        /// It is passed to the logging methods of RaLog.ILogPlugin.
        /// You will use it when writing your own adapter class based on RaLog.ILogPlugin.
        /// The adapter class is needed to redirect log output to your own logging/tracing framework.
        /// </summary>
        public object Logger { get; set; }

        /// <summary>
        /// The request id given to the last message sent from this client.
        /// The request id is incremented by the client for each request.
        /// The same id is returned in the response from the service.
        /// </summary>
        public int LastRequestIdSent { get; internal set; }

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
        /// The actor providing a multithreaded port is responsible to protect its internal data from race conditions.
        /// When IsMultithreaded is false, all requests and responses of the port are synchronized to the single thread that has opened the port.
        /// Default = false.
        /// </summary>
        public bool IsMultithreaded { get; set; }

        /// <summary>
        /// IsOpen=true : The input or output is currently open, connected or connecting.
        /// IsOpen=false: The input or output has closed or disconnected.
        /// </summary>
        public bool IsOpen { get { return m_isOpen; } }


        /// <summary>
        /// Gets a completed task having the Result true. 
        /// </summary>
        internal static Task<bool> TrueTask
        {
            get
            {
                var tcs = new TaskCompletionSource<bool>();
                tcs.SetResult(true);
                return tcs.Task;
            }
        }


        /// <summary>
        /// False when not connected or disconnected. Prevents message passing during shutdown.
        /// </summary>
        internal protected bool m_isOpen;
        private RemactDispatcher m_Dispatcher;
        internal SynchronizationContext SyncContext;
        internal int ManagedThreadId;
        internal MessageHandler DefaultInputHandler;


        private void DispatchingError(RemactMessage msg, ErrorMessage err)
        {
            try
            {
                var err1 = msg.Payload as ErrorMessage;
                if (err1 != null)
                {
                    err.InnerMessage = err1.Message;
                    err.StackTrace = err1.StackTrace;
                }

                if (msg.IsRequest)
                {
                    if (err.ErrorCode == ErrorCode.CouldNotDispatch) err.ErrorCode = ErrorCode.UnhandledExceptionOnService;
                    msg.SendResponseFrom(this, err, null);
                }
                else
                {
                    RaLog.Error(msg.SvcSndId, err.ToString(), Logger);
                }
            }
            catch (Exception ex)
            {
                RaLog.Exception("Cannot return dispatching error message", ex, Logger);
            }
        }

        internal void PickupSynchronizationContext()
        {
            if (IsMultithreaded)
            {
                SyncContext = null;
                ManagedThreadId = 0;
            }
            else
            {
                SynchronizationContext currentThreadSyncContext = SynchronizationContext.Current;
                if (currentThreadSyncContext == null)
                {
                    throw new InvalidOperationException("The thread that opens RemactPort '" + Name + "' has no message queue. Set RemactPort.IsMultithreaded=true, when your message handlers are threadsafe!");
                }
                else
                {
                    int threadId = Thread.CurrentThread.ManagedThreadId;
                    if (SyncContext != null && ManagedThreadId != threadId && m_isOpen)
                    {
                        RaLog.Error("Remact", "Thread connecting RemactPort '" + Name + "' has changed. Only one synchronization context is supported!", Logger);
                    }
                    ManagedThreadId = threadId;
                    SyncContext = currentThreadSyncContext;
                }
            }
        }


        // Message comes out of message queue in user thread
        private void MessageHandlerBase(object userState)
        {
            try
            {
                DispatchMessage(userState as RemactMessage);
            }
            catch (Exception ex)
            {
                DispatchingError(userState as RemactMessage, new ErrorMessage(ErrorCode.CouldNotDispatch, ex));
            }
        }


        // Message is passed to the lambda functions of the sending context or to the default response handler
        internal void DispatchMessage(RemactMessage msg)
        {
            if (!m_isOpen)
            {
                RaLog.Warning("Remact", "RemactPort '" + Name + "' is not connected. Cannot dispatch message!", Logger);
                return;
            }

            if (!IsMultithreaded)
            {
                var m = msg.Payload as IExtensibleRemactMessage;
                if (m != null) m.BoundSyncContext = SyncContext;
            }

            //msg.Destination = this;

            if (msg.DestinationLambda == null && msg.DestinationMethod != null && IsServiceName && msg.DestinationMethod.StartsWith(ActorInfo.MethodNamePrefix))
            {
                if (TraceConnect)
                {
                    RaLog.Info(msg.SvcRcvId, String.Format("{0} to service './{1}'", msg.DestinationMethod, Name), Logger);
                }

                OnConnectDisconnect(msg); // may be overloaded
                return;
            }// -------

            if (TraceReceive)
            {
                if (IsServiceName) RaLog.Info(msg.SvcRcvId, msg.ToString(), Logger);
                else RaLog.Info(msg.CltRcvId, msg.ToString(), Logger);
            }
            bool needsResponse = msg.IsRequest;

            if (msg.DestinationLambda != null)
            {
                msg = msg.DestinationLambda(msg); // a response to a lambda function, one of the On<T> extension methods may handle the message type
            }

            if (msg != null && m_Dispatcher != null)
            {
                msg = m_Dispatcher.CallMethod(msg, null); // TODO context
            }

            if (msg != null) // not handled yet
            {
                if (DefaultInputHandler != null)
                {
                    DefaultInputHandler(msg); // MessageHandlerlegate
                }
                else
                {
                    //No logging for anonymous RemactPortProxy
                    //RaLog.Error( "Remact", "Unhandled response: " + id.Payload, Logger );
                }
            }

            if (msg != null && needsResponse && msg.Response == null)
            {
                msg.SendResponse(new ReadyMessage());
            }
        }// DispatchMessage


        /// <summary>
        /// Message is passed to users connect/disconnect event handler, may be overloaded and call a MessageHandler{TSC}
        /// </summary>
        /// <param name="msg">RemactMessage containing Payload and Source.</param>
        /// <returns>True when handled.</returns>
        protected virtual bool OnConnectDisconnect(RemactMessage msg)
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
        public void StartClockSeconds(int seconds)
        {
            m_Clock = seconds * 1000; // Milliseconds, max = 25 days
            m_TickCountStart = Environment.TickCount;
        }

        /// <summary>
        /// Check if time has passed.
        /// </summary>
        /// <returns>True if time has passed.</returns>
        public bool ClockSecondsPassed()
        {
            int delta = Environment.TickCount - m_TickCountStart;
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