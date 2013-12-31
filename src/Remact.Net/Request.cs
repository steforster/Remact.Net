
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Runtime.Serialization;// DataContract
using System.Threading;            // SynchronizationContext
using Remact.Net.Internal;

namespace Remact.Net
{
    //----------------------------------------------------------------------------------------------
    #region == class RequestExtensions ==

    /// <summary>
    /// Contains extension methods for AsyncWcfLib.
    /// To use extension methods you need to reference assembly 'System.Core'
    /// </summary>
    public static class RequestExtensions
    {
        /// <summary>
        /// <para>Execute code, when message type matches the template parameter. Used to add lambda expressions, e.g.</para>
        /// <para>rsp.On&lt;ReadyMessage>(idle => {do something with idle message 'idle'})</para>
        /// <para>   .On&lt;ErrorMessage>(err => {do something with error message 'err'})</para>
        /// </summary>
        /// <typeparam name="T">The message type looked for.</typeparam>
        /// <param name="id">Parameter is added by the compiler.</param>
        /// <param name="handle">A delegate or lambda expression that will be executed, when the type matches.</param>
        /// <returns>The same request, for chained calls.</returns>
        // inspired by http://blogs.infosupport.com/blogs/frankb/archive/2008/02/02/Using-C_2300_-3.0-Extension-methods-and-Lambda-expressions-to-avoid-nasty-Null-Checks.aspx

#if !MONO
        public static WcfReqIdent On<T> (this Request id, Action<T> handle) where T: class
#else
        public static Request On<T>(this Request id, Action<T> handle) where T : class
#endif
        {
            if (id != null)
            {
                T typedMsg = id.Message as T;
                if (typedMsg == null)
                {
                    return id; // call next On extension method
                }

                handle(typedMsg);
            }
            return null; // already handled
        }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == class Request ==

    /// <summary>
    /// <para>All data for a message sent through AsyncWcfLib.</para>
    /// <para>Contains the Message itself as well as some request identification and a reference to the sending ActorPort.</para>
    /// <para>The class may be used to send a response to the sender and to trace unique message identification.</para>
    /// </summary>
    [DataContract (Namespace=RemactDefaults.WsNamespace)]
    public class Request
    {
        /// <summary>
        /// Requests and responses carry a reference to the message. Only the message is transferred over the wire.
        /// The Message itself may be sent to several internal partners. It will then be referenced by several Requests.
        /// Be careful, use the message as unmutable, readonly for application internal communication!
        /// </summary>
        public object Message { get; internal set; }

        /// <summary>
        /// <para>Identifies the client sending the request on the remote service.</para>
        /// <para>0=no remote connection or message not sent yet. Id is created by remote service on first connect.</para>
        /// </summary>
        [DataMember] public int         ClientId;
    
        /// <summary>
        /// <para>RequestId is incremented by WcfAsyncClient for remote connections only. Remote service returns the same number.</para>
        /// <para>0=Notification, 11...=remote requests</para>
        /// It is used to detect programming errors.
        /// </summary>
        [DataMember] public uint        RequestId;
    
        /// <summary>
        /// SendId is incremented by WcfAsyncClient for remote connections and remote service on each send operation.
        /// It is used to detect missing messages.
        /// </summary>
      //  [DataMember] public uint        SendId;
    
        /// <summary>
        /// Request carry a reference to the ActorPort that has sent the message.
        /// Service side: Sender is the client or client-stub   (ActorOutput) that has sent the request. 
        /// Client side : Sender is the service or service-proxy (ActorInput) that has sent the response.
        /// </summary>
        public   ActorPort              Sender {get; internal set;}
    
        /// <summary>
        /// Request carry a reference to the ActorPort that is receiving the message. 
        /// Service side: Input is the service (ActorInput) that is receiving a request. 
        /// Client side : Input is the client (ActorOutput) that is receiving a response.
        /// </summary>
        public   ActorPort              Input  {get; internal set;}
    
        /// <summary>
        /// For local and remote requests the send operation may specify a lambda expression handling the response.
        /// </summary>
        internal AsyncResponseHandler   SourceLambda;      // delegate IWcfMessage  AsyncResponseHandler (IWcfMessage msg);
        internal AsyncResponseHandler   DestinationLambda; // copied from source, when returning the message

        internal Request                Response;     // for WcfBasicServiceUser
        internal object                 MessageSaved; // for WcfBasicServiceUser
      //  internal WcfNotifyResponse      NotifyList;   // for WcfBasicServiceUser

        /// <summary>
        /// Create a new Request.
        /// </summary>
        /// <param name="sender">The sending partner.</param>
        /// <param name="clientId">The ClientId used on the service.</param>
        /// <param name="requestId">The RequestId is incremented by the client.</param>
        /// <param name="message">The user payload message to send.</param>
        /// <param name="responseHandler">null or a lamda expression to be called, when a response is aynchronously received.</param>
        internal Request( ActorPort sender, int clientId, uint requestId, object message, AsyncResponseHandler responseHandler )
        {
            Sender   = sender;
            Input    = sender;   // default for tracing on client side
            ClientId = clientId;
            RequestId = requestId;
            //SendId = ++sender.LastSentId;
            var m = message as IExtensibleWcfMessage;
            if (m != null)
            {
                m.BoundSyncContext = null;
                m.IsSent = true;
            }
            Message = message;
            SourceLambda = responseHandler;
        }// CTOR


        /// <summary>
        /// After creation a Request is a request.
        /// After reception on client side a Request is either a notification or a response.
        /// </summary>
        private bool m_boResponse;

        /// <summary>
        /// Notification is sent from service to client without matching request.
        /// </summary>
        public bool IsNotification { get { return m_boResponse && RequestId == 0; } }

        /// <summary>
        /// Request is sent from client to service (new messages are requests by default).
        /// </summary>
        public bool IsRequest { get { return !m_boResponse; } }

        /// <summary>
        /// Response is sent from service to client as answer to a request.
        /// </summary>
        public bool HasResponse { get { return m_boResponse && RequestId != 0; } 
                                    internal set{m_boResponse = value;}}


        /// <summary>
        /// Respond to a request. SendResponse may be called several times on one request. The responses are added into a WcfNotificationMessage.
        /// The individual messages are received on client side.
        /// If SendResponse is not called on a request, AsyncWcfLib automatically returns a ReadyMessage to the client.
        /// </summary>
        /// <param name="msg">The message to send as response.</param>
        public void SendResponse (object msg)
        {
            SendResponseFrom (Input, msg, null);
        }


        // Return a response to the sender.
        internal void SendResponseFrom(ActorPort service, object msg, AsyncResponseHandler responseHandler)
        {
            var m = msg as IExtensibleWcfMessage;
            if (m != null && m.BoundSyncContext != null && m.BoundSyncContext != SynchronizationContext.Current)
            {
                string name = service == null ? "null" : service.Name;
                throw new Exception("AsyncWcfLib: wrong thread synchronization context when responding from '" + name + "'");
            }

            // return same request ID
            if (Response == null || Response.MessageSaved == null) // first response message or not gathering notifications
            {
                Response = new Request(service, ClientId, RequestId, msg, responseHandler);
                Response.m_boResponse = true;
                Response.DestinationLambda = SourceLambda; // SourceLambda will be called later on for the first response only
                SourceLambda = null;
                //Response.NotifyList = NotifyList; // notifications gathered before this request
            }
            else
            {
                Response.Message = msg; // msg will be added to NotifyList later on
                ++service.LastSentId; // count messages on the service, Response.SendId will be set by BasicServiceUser later on
            }
            if (service.TraceSend) RaTrc.Info(this.SvcSndId, Response.ToString(), service.Logger);
            Sender.PostInput(Response); // several responses may be added in BasicServiceUser
        }


        /// <summary>
        /// Each message my be printed e.g. to trace.
        /// </summary>
        /// <returns>The message in readable text form.</returns>
        public override string ToString ()
        {
            if(Message != null)
            {
                return Message.ToString();
            }
            else
            {
                return string.Format ("<null> message, ClientId={0}, RequestId={1}", ClientId, RequestId);
            }
        }


        /// <summary>
        /// Generates part of a standardised mark for trace output on client side.
        /// </summary>
        internal string ClientMark
        {
            get 
            {
            if (Input == null || Input.Name == null)
            {
                return string.Format("C[{0:0#}]", ClientId);
            }
            else
            {
                return string.Format("{0}[{1:0#}]", Input.Name, ClientId);
            }
            }
        }// ClientMark


        /// <summary>
        /// Generates part of a standardised mark for trace output on service side.
        /// </summary>
        internal string SenderMark
        {
            get
            {
                if (Sender == null || Sender.Name == null || Sender.HostName == null)
                {
                    return string.Format("C[{0:0#}]", ClientId);
                }
                else
                {
                    return string.Format ("{0}/{1}[{2:0#}]", Sender.AppIdentification, Sender.Name, ClientId);
                }
            }
        }// SenderMark


        /// <summary>
        /// Generates part of a standardised mark for trace output.
        /// </summary>
        private string ReqMarkRcv
        {
            get
            {
                if (RequestId == 0) return "<<"; // Notification from Service
                                else return (RequestId % 100).ToString("0#");
            }
        }// ReqMarkRcv

        /// <summary>
        /// Generates part of a standardised mark for trace output.
        /// </summary>
        private string ReqMarkSnd
        {
            get
            {
                if (RequestId == 0) return ">>"; // Notification to Client
                                else return (RequestId % 100).ToString ("0#");
            }
        }// ReqMarkSnd

        /// <summary>
        /// Client sending request: Standardised mark for trace output.
        /// </summary>
        public string CltSndId { get { return string.Concat( ClientMark, ReqMarkSnd, "-->"); } }

        /// <summary>
        /// Client receiving response: Standardised mark for trace output.
        /// </summary>
        public string CltRcvId { get { return string.Concat( ClientMark, ReqMarkRcv, "<--"); } }

        /// <summary>
        /// Service receiving request: Standardised mark for trace output.
        /// </summary>
        public string SvcRcvId { get { return string.Concat( SenderMark, ReqMarkRcv, "~~>"); } }

        /// <summary>
        /// Service sending response: Standardised mark for trace output.
        /// </summary>
        public string SvcSndId { get { return string.Concat( SenderMark, ReqMarkSnd, "<~~" ); } }

    };

    #endregion
    //----------------------------------------------------------------------------------------------
}// namespace
