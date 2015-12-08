
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Threading;            // SynchronizationContext

namespace Remact.Net
{
    //----------------------------------------------------------------------------------------------
    #region == class RemactMessageExtensions ==

    /// <summary>
    /// Contains extension methods for RemactMessages.
    /// To use extension methods you need to reference assembly 'System.Core'
    /// </summary>
    public static class RemactMessageExtensions
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

        public static RemactMessage On<T> (this RemactMessage msg, Action<T> handle) where T : class
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

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == class RemactMessageType ==

    /// <summary>
    /// The communication type of a <see cref="RemactMessage"/>
    /// </summary>
    public enum RemactMessageType
    {
        /// <summary>
        /// Ba default a message is a request. For each request a response is expected at the sending port.
        /// </summary>
        Request,

        /// <summary>
        /// The response to a request.
        /// </summary>
        Response,

        /// <summary>
        /// Notifications are sent one way, without expecting a response. 
        /// </summary>
        Notification,

        /// <summary>
        /// Error messages are sent as response, when a request or a notification could not be correctly handled at the receiving side.
        /// </summary>
        Error
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == class RemactMessage ==

    /// <summary>
    /// <para>All data for a message sent through Remact.</para>
    /// <para>Contains the Payload itself as well as a request identification and a reference to the sending RemactPort.</para>
    /// <para>The object may be used to send a response to the sender and to log unique message identification.</para>
    /// </summary>
    public class RemactMessage
    {
        /// <summary>
        /// The message type (request, response, notification, error).
        /// </summary>
        public RemactMessageType MessageType { get; internal set; }

        /// <summary>
        /// Requests and responses carry a reference to the payload data. Only the payload is transferred over the wire.
        /// The payload itself may be sent to several internal partners. It will then be referenced by several RemactMessage objects.
        /// Be careful, use the payload as unmutable, readonly for application internal communication!
        /// </summary>
        public object Payload { get; internal set; }

        /// <summary>
        /// Incoming remote messages carry a reference to the serialization payload.
        /// Its concrete implementation depends on the serializer used.
        /// </summary>
        internal ISerializationPayload SerializationPayload { get; set; }

        /// <summary>
        /// <para>Identifies the client sending the request on the remote service.</para>
        /// <para>0=no remote connection or message not sent yet. Id is created by remote service on first connect.</para>
        /// </summary>
        public int ClientId;
    
        /// <summary>
        /// <para>RequestId is incremented by RemactClient for remote connections only. Remote service returns the same number.</para>
        /// <para>0=Notification, 11...=remote requests</para>
        /// </summary>
        public int RequestId;
    
        /// <summary>
        /// RemactMessages carry a reference to the RemactPort that has sent the message.
        /// Service side: Source is the client or client-stub    (RemactPortClient) that has sent the request. 
        /// Client side : Source is the service or service-proxy (RemactPortService) that has sent the response or notification.
        /// </summary>
        public RemactPort Source {get; internal set;}
    
        /// <summary>
        /// RemactMessages carry a reference to the RemactPort that is receiving the message. 
        /// Service side: Destination is the service (RemactPortService) that is receiving a request. 
        /// Client side : Destination is the client  (RemactPortClient) that is receiving a response.
        /// </summary>
        public RemactPort Destination  {get; internal set;}

        /// <summary>
        /// The method to be called on the destination actor. Defines the data type of Payload for requests and notifications.
        /// </summary>
        public string DestinationMethod { get; internal set; }

        /// <summary>
        /// For local and remote requests the send operation may specify a lambda expression handling the response.
        /// </summary>
        internal AsyncResponseHandler    SourceLambda;
        internal AsyncResponseHandler    DestinationLambda; // copied from source, when returning the message

        internal RemactMessage           Response;          // to check whether a response has been sent

        /// <summary>
        /// Creates a new RemactMessage to send from client to service.
        /// </summary>
        /// <param name="proxy">The proxy of the destination service.</param>
        /// <param name="destinationMethod">The receiving method defines the payload type.</param>
        /// <param name="payload">The user payload to send.</param>
        /// <param name="messageType">The type of the message.</param>
        /// <param name="responseHandler">null or a lamda expression to be called, when a response is aynchronously received (valid on client side requests/responses).</param>
        internal RemactMessage(RemactPortProxy proxy, string destinationMethod, object payload, RemactMessageType messageType,
                               AsyncResponseHandler responseHandler)
        {
            Destination = proxy;
            DestinationMethod = destinationMethod;
            Source = proxy.Client;
            ClientId = proxy.Client.ClientId;
            MessageType = messageType;
            if (messageType == RemactMessageType.Request)
            {
                RequestId = proxy.NextRequestId;
            }

            var m = payload as IExtensibleRemactMessage;
            if (m != null)
            {
                m.BoundSyncContext = null;
                m.IsSent = true;
            }

            Payload = payload;
            SourceLambda = responseHandler;
        }// CTOR1


        /// <summary>
        /// Creates a new RemactMessage to send from service to client.
        /// </summary>
        /// <param name="client">The destination client.</param>
        /// <param name="destinationMethod">The receiving method defines the payload type.</param>
        /// <param name="payload">The user payload to send.</param>
        /// <param name="messageType">The type of the message.</param>
        /// <param name="responseHandler">null or a lamda expression to be called, when a response is aynchronously received (valid on client side requests/responses).</param>
        internal RemactMessage(RemactPortClient client, string destinationMethod, object payload, RemactMessageType messageType,
                               AsyncResponseHandler responseHandler)
        {
            Destination = client;
            DestinationMethod = destinationMethod;
            Source = client.ServiceIdent;
            ClientId = client.ClientId;
            MessageType = messageType;
            if (messageType == RemactMessageType.Request)
            {
                RequestId = client.NextRequestId;
            }

            var m = payload as IExtensibleRemactMessage;
            if (m != null)
            {
                m.BoundSyncContext = null;
                m.IsSent = true;
            }

            Payload = payload;
            SourceLambda = responseHandler;
        }// CTOR2


        /// <summary>
        /// Creates a new RemactMessage to send from service user (client) to service.
        /// </summary>
        internal RemactMessage(RemactPort destination, string destinationMethod, object payload, RemactMessageType messageType,
                               RemactPort sender, int clientId, int requestId)
        {
            Destination = destination;
            DestinationMethod = destinationMethod;
            Source = sender;
            ClientId = clientId;
            MessageType = messageType;
            RequestId = requestId;

            var m = payload as IExtensibleRemactMessage;
            if (m != null)
            {
                m.BoundSyncContext = null;
                m.IsSent = true;
            }

            Payload = payload;
        }// CTOR3


        /// <summary>
        /// Copies a RemactMessage.
        /// </summary>
        /// <param name="msg">A RemactMessage to copy all information from.</param>
        internal RemactMessage(RemactMessage msg)
        {
            MessageType = msg.MessageType;
            Payload = msg.Payload;
            SerializationPayload = msg.SerializationPayload;
            ClientId = msg.ClientId;
            RequestId = msg.RequestId;
            Source = msg.Source;
            Destination = msg.Destination;
            DestinationMethod = msg.DestinationMethod;
            SourceLambda = msg.SourceLambda;
            DestinationLambda = msg.DestinationLambda;
            Response = msg.Response;
        }// CTOR4


        /// <summary>
        /// Tries to convert the incoming payload to an object of the given result type.
        /// </summary>
        /// <typeparam name="T">The type of the resulting payload object.</typeparam>
        /// <param name="result">The resulting payload object.</param>
        /// <returns>True, when successfully converted. False otherwise.</returns>
        public bool TryConvertPayload<T>(out T result) where T : class
        {
            result = null;
            if (Payload == null)
            {
                return true; 
            }

            if (SerializationPayload != null)
            {
                // remote serialized payload
                // Newtonsoft.jToken cannot be unboxed to value type directly, we first have to read it as object
                Type exactType = typeof(T);
                if (exactType == Payload.GetType())
                {
                    result = (T)Payload;
                    return true;
                }

                result = (T)SerializationPayload.TryReadAs(exactType);
                if (result != null)
                {
                    Payload = result; // keep converted result
                    return true;
                }
            }

            // local communication or preconverted data (Json-RPC does preconvert primitive message types e.g. int).
            var correctType = Payload as T; // needs 'where T : class'
            if (correctType != null)
            {
                result = correctType;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Source does not expect to receive a reply to this message.
        /// </summary>
        public bool IsNotification { get { return MessageType == RemactMessageType.Notification; } }

        /// <summary>
        /// Source expects to receive a reply to this message (new messages are requests by default).
        /// </summary>
        public bool IsRequest { get { return MessageType == RemactMessageType.Request; } }

        /// <summary>
        /// Response to a request.
        /// </summary>
        public bool IsResponse { get { return MessageType == RemactMessageType.Response; } }

        /// <summary>
        /// Error messages may be sent from server to client or from client to server. 
        /// Errors do not match the expected response type. There is never a response to an error.
        /// Remact specially supports 'ErrorMessage' as payload type when IsError is true.
        /// </summary>
        public bool IsError { get { return MessageType == RemactMessageType.Error; } }


        /// <summary>
        /// Respond to a request. SendResponse may be called several times on one request. The following 'responses' are sent as notifications.
        /// The individual messages are received on client side.
        /// If SendResponse is not called on a request, Remact automatically returns a ReadyMessage to the client.
        /// </summary>
        /// <param name="payload">The message to send as response.</param>
        public void SendResponse(object payload)
        {
            SendResponseFrom(Destination, payload, null);
        }


        // Return a response to the source of the message.
        internal void SendResponseFrom(RemactPort sender, object payload, AsyncResponseHandler responseHandler)
        {
            var m = payload as IExtensibleRemactMessage;
            if (m != null && m.BoundSyncContext != null && m.BoundSyncContext != SynchronizationContext.Current)
            {
                string name = sender == null ? "null" : sender.Name;
                throw new InvalidOperationException("Remact: wrong thread synchronization context when responding from '" + name + "'");
            }

            // return same request ID for first call to SendResponse
            if (MessageType == RemactMessageType.Request && Response == null)
            {
                RemactMessageType type = RemactMessageType.Response;
                if (payload is ErrorMessage)
                {
                    type = RemactMessageType.Error;
                }

                Response = new RemactMessage(Source, DestinationMethod, payload, type, sender, ClientId, RequestId);
                Response.SourceLambda = responseHandler;
                Response.DestinationLambda = SourceLambda; // SourceLambda will be called later on for the first response only
                SourceLambda = null;
                if (sender.TraceSend) RaLog.Info(Response.SvcSndId, Response.ToString(), sender.Logger);

                // Local : Destination = RemactPortClient  (when on service side) or RemactPortProxy (when on client side)
                // Remote: send via      RemactServiceUser (when on service side) or RemactClient    (when on client side)

                //         Sending on service side                  Sending on client side
                //         Source          RedirectIncoming         Source             RedirectIncoming
                //         ------------    ----------------         ------------       ----------------
                // Local : PortClient      RemactPortProxy          RemactPortProxy    RemactPortService
                // Remote: PortClient      RemactServiceUser        RemactPortProxy    RemactClient

                // PostInput does not check for the correct synchronization context. We want to send from threadpool also (exceptions and InternalResponses)
                Source.LinkedPort.PostInput(Response);
            }
            else
            {
                RemactMessageType type = RemactMessageType.Notification;
                if (payload is ErrorMessage)
                {
                    type = RemactMessageType.Error;
                }

                var msg = new RemactMessage(Source, null, payload, type, sender, ClientId, 0);
                msg.SourceLambda = responseHandler;
                if (sender.TraceSend) RaLog.Info(msg.SvcSndId, msg.ToString(), sender.Logger);
                Source.LinkedPort.PostInput(msg);
            }
        }


        /// <summary>
        /// Each message may be printed e.g. to log.
        /// </summary>
        /// <returns>The message in readable text form.</returns>
        public override string ToString ()
        {
            if (string.IsNullOrEmpty(DestinationMethod) && Payload != null)
            {
                return string.Concat(MessageType.ToString(), " type = '", Payload.GetType().Name, "'");
            }

            if (MessageType == RemactMessageType.Request || MessageType == RemactMessageType.Notification)
            {
                return string.Concat(MessageType.ToString(), " to method '", DestinationMethod, "'");
            }
            return string.Concat(MessageType.ToString(), " from method '", DestinationMethod, "'"); // Response, Error
        }

        /// <summary>
        /// Generates part of a standardised mark for log output on client side.
        /// </summary>
        internal string DestMark(bool full)
        {
            if (Destination == null || Destination.Name == null)
            {
                return string.Format("C[{0:0#}]", ClientId);
            }
            else if (full)
            {
                return string.Format ("{0}/{1}[{2:0#}]", Destination.AppIdentification, Destination.Name, ClientId);
            }
            else
            {
                return string.Format("{0}[{1:0#}]", Destination.Name, ClientId);
            }
        }

        /// <summary>
        /// Generates part of a standardised mark for log output on service side.
        /// </summary>
        internal string SourceMark(bool full)
        {
            if (Source == null || Source.Name == null || Source.HostName == null)
            {
                return string.Format("C[{0:0#}]", ClientId);
            }
            else if (full)
            {
                return string.Format ("{0}/{1}[{2:0#}]", Source.AppIdentification, Source.Name, ClientId);
            }
            else
            {
                return string.Format("{0}[{1:0#}]", Source.Name, ClientId);
            }
        }

        private string ReqMark
        {
            get
            {
                if (RequestId == 0) return ">>"; // Notification to Client
                               else return (RequestId % 100).ToString ("0#");
            }
        }

        private string RspMark
        {
            get
            {
                if (RequestId == 0) return "<<"; // Notification from Service
                               else return (RequestId % 100).ToString("0#");
            }
        }

        /// <summary>
        /// Client sending request: Standardised mark for log output.
        /// </summary>
        public string CltSndId { get { return string.Concat( SourceMark(false), ReqMark, "-->"); } }

        /// <summary>
        /// Service receiving request: Standardised mark for log output.
        /// </summary>
        public string SvcRcvId { get { return string.Concat( SourceMark(true),  ReqMark, "~~>"); } }

        /// <summary>
        /// Service sending response: Standardised mark for log output.
        /// </summary>
        public string SvcSndId { get { return string.Concat( DestMark(true),    RspMark, "<~~" ); } }

        /// <summary>
        /// Client receiving response: Standardised mark for log output.
        /// </summary>
        public string CltRcvId { get { return string.Concat( DestMark(false),   RspMark, "<--"); } }
    };

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == class RemactMessage<T> ==

    /// <summary>
    /// <para>All data for a message sent through Remact.</para>
    /// <para>Contains a Payload of type T as well as a request identification and a reference to the sending RemactPort.</para>
    /// <para>The RemactMessage may be used to send a response to the sender and to log unique message identification.</para>
    /// </summary>
    public class RemactMessage<T> : RemactMessage
    {
        /// <summary>
        /// Creates a new RemactMessage for payloads of type T.
        /// </summary>
        /// <param name="payload">The converted payload.</param>
        /// <param name="msg">A RemactMessage to copy all information from.</param>
        internal RemactMessage(T payload, RemactMessage msg)
            :base(msg)
        {
            Payload = payload;
        }

        /// <summary>
        /// Gets the strongly typed payload. <see cref="RemactMessage{T}"/> always contains a successfully converted payload.
        /// </summary>
        public new T Payload { get; internal set; }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
}
