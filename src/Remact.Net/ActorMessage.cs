
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Threading;            // SynchronizationContext
using Newtonsoft.Json.Linq;
using Remact.Net.Internal;

namespace Remact.Net
{
    //----------------------------------------------------------------------------------------------
    #region == class ActorMessageExtensions ==

    /// <summary>
    /// Contains extension methods for AsyncWcfLib.
    /// To use extension methods you need to reference assembly 'System.Core'
    /// </summary>
    public static class ActorMessageExtensions
    {
        /// <summary>
        /// <para>Execute code, when message type matches the template parameter. Used to add lambda expressions, e.g.</para>
        /// <para>rsp.On&lt;ReadyMessage>(idle => {do something with idle message 'idle'})</para>
        /// <para>   .On&lt;ErrorMessage>(err => {do something with error message 'err'})</para>
        /// </summary>
        /// <typeparam name="T">The message type looked for.</typeparam>
        /// <param name="msg">Parameter is added by the compiler.</param>
        /// <param name="handle">A delegate or lambda expression that will be executed, when the type matches.</param>
        /// <returns>The same request, for chained calls.</returns>
        // inspired by http://blogs.infosupport.com/blogs/frankb/archive/2008/02/02/Using-C_2300_-3.0-Extension-methods-and-Lambda-expressions-to-avoid-nasty-Null-Checks.aspx

#if !MONO
        public static ActorMessage On<T> (this ActorMessage msg, Action<T> handle) where T : class
#else
        public static ActorMessage On<T> (this ActorMessage msg, Action<T> handle) where T : class
#endif
        {
            if (msg != null)
            {
                T typedMsg;
                if (!msg.TryConvertPayload (out typedMsg))
                {
                    return msg; // call next On extension method
                }

                handle(typedMsg);
            }
            return null; // already handled
        }
    }


    public enum ActorMessageType
    {
        Request, // default
        Response,
        Notification,
        Error
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == class ActorMessage ==

    /// <summary>
    /// <para>All data for a message sent through AsyncWcfLib.</para>
    /// <para>Contains the Payload itself as well as some request identification and a reference to the sending ActorPort.</para>
    /// <para>The class may be used to send a response to the sender and to trace unique message identification.</para>
    /// </summary>
    public class ActorMessage
    {
        /// <summary>
        /// Requests and responses carry a reference to the payload data. Only the payload is transferred over the wire.
        /// The payload itself may be sent to several internal partners. It will then be referenced by several ActorMessage objects.
        /// Be careful, use the payload as unmutable, readonly for application internal communication!
        /// </summary>
        public object Payload { get; internal set; }

        /// <summary>
        /// <para>Identifies the client sending the request on the remote service.</para>
        /// <para>0=no remote connection or message not sent yet. Id is created by remote service on first connect.</para>
        /// </summary>
        public int ClientId;
    
        /// <summary>
        /// <para>RequestId is incremented by WcfAsyncClient for remote connections only. Remote service returns the same number.</para>
        /// <para>0=Notification, 11...=remote requests</para>
        /// It is used to detect programming errors.
        /// </summary>
        public int RequestId;
    
        /// <summary>
        /// ActorMessage carry a reference to the ActorPort that has sent the message.
        /// Service side: Source is the client or client-stub   (ActorOutput) that has sent the request. 
        /// Client side : Source is the service or service-proxy (ActorInput) that has sent the response or notification.
        /// </summary>
        public ActorPort Source {get; internal set;}
    
        /// <summary>
        /// ActorMessage carry a reference to the ActorPort that is receiving the message. 
        /// Service side: Destination is the service (ActorInput) that is receiving a request. 
        /// Client side : Destination is the client (ActorOutput) that is receiving a response.
        /// </summary>
        public ActorPort Destination  {get; internal set;}

        /// <summary>
        /// The method to be called on the destination actor. Defines the data type of Payload for requests and notifications.
        /// </summary>
        public string DestinationMethod { get; internal set; }

        /// <summary>
        /// The full qualified .net data type of Payload for requests and notifications.
        /// </summary>
        public string PayloadType { get; internal set; }

        /// <summary>
        /// For local and remote requests the send operation may specify a lambda expression handling the response.
        /// </summary>
        internal AsyncResponseHandler   SourceLambda;      // delegate ActorMessage  AsyncResponseHandler (ActorMessage msg);
        internal AsyncResponseHandler   DestinationLambda; // copied from source, when returning the message

        internal ActorMessage           Response;          // to check whether a response has been sent

        /// <summary>
        /// Creates a new ActorMessage.
        /// </summary>
        /// <param name="source">The sending partner.</param>
        /// <param name="clientId">The ClientId used on the service.</param>
        /// <param name="requestId">The RequestId is incremented by the client.</param>
        /// <param name="destination">The receiving partner.</param>
        /// <param name="destinationMethod">The receiving method defines the payload type.</param>
        /// <param name="payload">The user payload to send.</param>
        /// <param name="responseHandler">null or a lamda expression to be called, when a response is aynchronously received (valid on client side requests/responses).</param>
        /// <param name="expectedResponseType"> null or the expected response payload type (valid on client side requests/responses).</param>
        internal ActorMessage(ActorPort source, int clientId, int requestId, 
            ActorPort destination, string destinationMethod, object payload, 
            AsyncResponseHandler responseHandler = null)
        {
            Source = source;
            Destination = destination;
            DestinationMethod = destinationMethod;
            ClientId = clientId;
            RequestId = requestId;
            var m = payload as IExtensibleWcfMessage;
            if (m != null)
            {
                m.BoundSyncContext = null;
                m.IsSent = true;
            }

            Payload = payload;
            if (payload != null)
            {
                PayloadType = payload.GetType().FullName;
            }
            SourceLambda = responseHandler;
        }// CTOR

        public ActorMessageType Type { get; internal set; }

        
        public bool TryConvertPayload<T>(out T result) where T : class
        {
            result = null;
            if (PayloadType != null && PayloadType != typeof(T).FullName)
            {
                return false; // different type name or method name 
            }

            if (Payload == null)
            {
                return true; 
            }

            var correctType = Payload as T; // needs 'where T : class'
            if (correctType != null)
            {
                result = correctType;
                return true;
            }

            var jToken = Payload as JToken;
            if (jToken != null)
            {
                try
                {
                    result = jToken.ToObject<T>();
                    return true;
                }
                catch { }
            }

            return false;
        }

        /// <summary>
        /// Notification is sent from service to client without a matching request.
        /// </summary>
        public bool IsNotification { get { return Type == ActorMessageType.Notification; } }

        /// <summary>
        /// ActorMessage is sent from client to service (new messages are requests by default).
        /// </summary>
        public bool IsRequest { get { return Type == ActorMessageType.Request; } }

        /// <summary>
        /// Response is sent from service to client as answer to a request.
        /// </summary>
        public bool IsResponse { get { return Type == ActorMessageType.Response; } }

        /// <summary>
        /// Error messages may be sent from server to client or from client to server. 
        /// Errors do not match the expected response type. There is never a response to a error.
        /// </summary>
        public bool IsError { get { return Type == ActorMessageType.Error; } }


        /// <summary>
        /// Respond to a request. SendResponse may be called several times on one request. The responses are added into a WcfNotificationMessage.
        /// The individual messages are received on client side.
        /// If SendResponse is not called on a request, AsyncWcfLib automatically returns a ReadyMessage to the client.
        /// </summary>
        /// <param name="payload">The message to send as response.</param>
        public void SendResponse(object payload)
        {
            SendResponseFrom(Destination, payload, null);
        }


        // Return a response to the sender.
        internal void SendResponseFrom(ActorPort service, object payload, AsyncResponseHandler responseHandler)
        {
            var m = payload as IExtensibleWcfMessage;
            if (m != null && m.BoundSyncContext != null && m.BoundSyncContext != SynchronizationContext.Current)
            {
                string name = service == null ? "null" : service.Name;
                throw new Exception("AsyncWcfLib: wrong thread synchronization context when responding from '" + name + "'");
            }

            // return same request ID for first call to SendResponse
            if (Type == ActorMessageType.Request && Response == null)
            {
                Response = new ActorMessage(service, ClientId, RequestId, 
                                            Source, null, payload, responseHandler);
                Response.Type = ActorMessageType.Response;
                Response.DestinationLambda = SourceLambda; // SourceLambda will be called later on for the first response only
                SourceLambda = null;
                if (service.TraceSend) RaTrc.Info(this.SvcSndId, Response.ToString(), service.Logger);
                Source.PostInput(Response);
            }
            else
            {
                var msg = new ActorMessage(service, ClientId, RequestId,
                                           Source, null, payload, responseHandler);
                msg.Type = ActorMessageType.Notification;
                if (service.TraceSend) RaTrc.Info(msg.SvcSndId, msg.ToString(), service.Logger);
                Source.PostInput(msg);
            }
        }


        /// <summary>
        /// Each message my be printed e.g. to trace.
        /// </summary>
        /// <returns>The message in readable text form.</returns>
        public override string ToString ()
        {
            if (!string.IsNullOrEmpty(PayloadType))
            {
                return PayloadType;
            }
            else if (Payload != null)
            {
                return Payload.GetType().FullName;
            }
            else
            {
                return string.Format ("<null> payload");
            }
        }


        /// <summary>
        /// Generates part of a standardised mark for trace output on client side.
        /// </summary>
        internal string ClientMark
        {
            get 
            {
            if (Destination == null || Destination.Name == null)
            {
                return string.Format("C[{0:0#}]", ClientId);
            }
            else
            {
                return string.Format("{0}[{1:0#}]", Destination.Name, ClientId);
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
                if (Source == null || Source.Name == null || Source.HostName == null)
                {
                    return string.Format("C[{0:0#}]", ClientId);
                }
                else
                {
                    return string.Format ("{0}/{1}[{2:0#}]", Source.AppIdentification, Source.Name, ClientId);
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
}
