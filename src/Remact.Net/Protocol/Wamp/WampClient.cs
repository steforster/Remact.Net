
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Threading;
using System.Collections.Generic;
using Alchemy;
using Alchemy.Classes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Remact.Net.Protocol;

namespace Remact.Net.Protocol.Wamp
{
    /// <summary>
    /// Implements the protocol level for a WAMP client. See http://wamp.ws/spec/.
    /// </summary>
    public class WampClient : IRemactProtocolDriverService
    {
        private WebSocketClient _wsClient;
        private Dictionary<int, ActorMessage> _outstandingRequests;
        private IRemactProtocolDriverCallbacks _callback;
        private int _lowLevelErrorCount;

        public WampClient(Uri websocketUri)
        {
            ServiceUri = websocketUri;
            _outstandingRequests = new Dictionary<int, ActorMessage>(); 
            _wsClient = new WebSocketClient(websocketUri.ToString())
            {
                //OnSend = OnSend,// Message has been dequeued and passed to the socket buffer
                OnReceive = OnReceived,
                //OnConnect = OnConnect,// TCP socket is connected to the server
                //OnConnected = OnConnected,// WebSocket connection has been built and authenticated
                OnDisconnect = OnDisconnect,
                ConnectTimeout = new TimeSpan(0, 0, 50), // 50 sec
                SubProtocols = new string[]{"wamp"} // null: take all subprotocols
                //TODO Origin = see rfc6455
            };
        }

        public Uri ServiceUri { get; private set; }

        public ReadyState ReadyState 
        { 
            get 
            {
                switch (_wsClient.ReadyState)
                {
                    case WebSocketClient.ReadyStates.OPEN: return ReadyState.Connected;
                        //TODO Faulted
                    default: return ReadyState.Closed;
                }
            } 
        }

        public string ReadyStateAsString {get {return _wsClient.ReadyState.ToString(); }}

        
        // Asynchronous open the connection
        public void OpenAsync(ActorMessage request, IRemactProtocolDriverCallbacks callback)
        {
            _callback = callback;
            ThreadPool.UnsafeQueueUserWorkItem(OpenOnThreadpool, request);
        }

        private void OpenOnThreadpool(object obj)
        {
            ActorMessage request = obj as ActorMessage;
            try
            {
                _wsClient.Connect();

                request.Payload = null; // null = ok
                if (_wsClient.ReadyState != WebSocketClient.ReadyStates.OPEN)
                {
                    request.Payload = new ErrorMessage(ErrorMessage.Code.CouldNotOpen, "WebSocketClient not connected");
                }
            }
            //catch (EndpointNotFoundException ex)
            //{
            //    request.Payload = new ErrorMessage(ErrorMessage.Code.ServiceNotRunning, ex);
            //}
            catch (Exception ex)
            {
                request.Payload = new ErrorMessage(ErrorMessage.Code.CouldNotOpen, ex);
            }

            if (request.Source.IsMultithreaded)
            {
                _callback.OnOpenCompleted(request); // Test1.ClientNoSync, RouterClient
            }
            else if (request.Source.SyncContext == null)
            {
                RaLog.Error("Remact", "No synchronization context to open " + request.Source.Name, request.Source.Logger);
                _callback.OnOpenCompleted(request);
            }
            else
            {
                request.Source.SyncContext.Post(_callback.OnOpenCompleted, request);
            }
        }// OpenOnThreadpool


        public void Dispose()
        {
            if (_wsClient.ReadyState == WebSocketClient.ReadyStates.OPEN)
            {
                _wsClient.Disconnect();
            }
            _wsClient.Dispose();
        }

        #region IRemactProtocolDriverService proxy implementation


        public void MessageFromClient(ActorMessage request)
        {
            _outstandingRequests.Add(request.RequestId, request);
            string callId = request.RequestId.ToString();
            string procUri = request.PayloadType;

            // eg. CALL message for RPC with no arguments: [2, "7DK6TdN4wLiUJgNM", "http://example.com/api#howdy"]

            var wamp = new JArray(WampMessageType.v1Call, callId, procUri);
            if (request.Payload != null)
            {
                var jToken = request.Payload as JToken;
                if (jToken == null) jToken = JToken.FromObject(request.Payload);

                wamp.Add(jToken);
            }

            _wsClient.Send(wamp.ToString(Formatting.None));
        }

        private void ResponseNotDeserializable(int id, string errorDesc)
        {
            if (++_lowLevelErrorCount > 100)
            {
                return; // do not respond endless on erronous error messages
            }

            var error = new ErrorMessage(ErrorMessage.Code.RspNotDeserializableOnClient, errorDesc);
            var message = new ActorMessage(null, 0, id, null, null, error);
            ErrorFromClient(message);
        }

        public void ErrorFromClient(ActorMessage message)
        {
            string callId = message.RequestId.ToString();
            if (callId == null)
            {
                callId = string.Empty;
            }

            string errorUri = string.Empty;
            string errorDesc = string.Empty;
            var error = message.Payload as ErrorMessage;
            if (error != null)
            {
                errorUri = error.Error.ToString();
                errorDesc = error.Message;
            }

            // eg. CALLERROR message with generic error: [4, "gwbN3EDtFv6JvNV5", "http://autobahn.tavendo.de/error#generic", "math domain error"]


            var wamp = new JArray(WampMessageType.v1CallError, callId, errorUri, errorDesc);

            if (message.Payload != null)
            {
                var jToken = message.Payload as JToken;
                if (jToken == null) jToken = JToken.FromObject(message.Payload);

                wamp.Add(jToken);
            }

            _wsClient.Send(wamp.ToString(Formatting.None));
        }


        #endregion
        #region Alchemy callbacks


        // DataFrame.State == Handlers.WebSocket.DataFrame.DataState.Complete
        private void OnReceived(UserContext context)
        {
            //Console.WriteLine("Received Data From :" + context.ClientAddress);
            int id = 0;
            bool errorReceived = false;
            ActorMessage message = null;

            try
            {
                string json = context.DataFrame.ToString();

                // dynamics, needs Microsoft.CSharp.dll
                //dynamic obj = JsonConvert.DeserializeObject(json);
                //switch ((int)obj.Type)
                //{
                //    case (int)CommandType.Register:
                //        Register(obj.Name.Value, context);
                //        break;
                //    case (int)CommandType.Message:
                //        ChatMessage(obj.Message.Value, context);
                //        break;
                //    case (int)CommandType.NameChange:
                //        NameChange(obj.Name.Value, context);
                //        break;
                //}

                JArray wamp = JArray.Parse(json);
                int wampType = (int)wamp[0];
                if (wampType == (int)WampMessageType.v1CallResult)
                {
                    // eg. CALLRESULT message with 'null' result: [3, "CcDnuI2bl2oLGBzO", null]
                    id = int.Parse((string)wamp[1]);
                    message = GetResponseMessage(id);
                    message.Type = ActorMessageType.Response;
                    JToken payload = wamp[2];
                    message.Payload = payload;

                    // For ActorInfo-requests, we expect an ActorInfo as response.
                    if (message.PayloadType != typeof(ActorInfo).FullName)
                    {
                        if (!payload.HasValues && payload.Type == JTokenType.Object)
                        {
                            // empty responses are ReadyMessages !
                            message.Payload = new ReadyMessage();
                            message.PayloadType = typeof(ReadyMessage).FullName;
                        }
                        else
                        {
                            message.PayloadType = null; // other payloads will be converted in anonymous methods, when receiving.
                        }
                    }

                    _callback.MessageFromService(message); // adds source and destination
                }
                else if (wampType == (int)WampMessageType.v1CallError)
                {
                    // eg. CALLERROR message with generic error: [4, "gwbN3EDtFv6JvNV5", "http://autobahn.tavendo.de/error#generic", "math domain error"]
                    errorReceived = true;
                    var requestId = (string)wamp[1];
                    var errorUri = (string)wamp[2];
                    var code = (ErrorMessage.Code)Enum.Parse(typeof(ErrorMessage.Code), errorUri, false);
                    var errorDesc = (string)wamp[3];
                    var error = new ErrorMessage(code, errorDesc);

                    if (string.IsNullOrEmpty(requestId))
                    {
                        message = new ActorMessage(null, 0, id, null, null, error);
                    }
                    else 
                    {
                        id = int.Parse(requestId);
                        message = GetResponseMessage(id);
                    }

                    message.Type = ActorMessageType.Error;
                    //if (wamp.Count >= 4)
                    //{
                    //    message. (object)wamp[4]); TODO
                    //}
                    _callback.MessageFromService(message); // adds source and destination
                }
                else if (wampType == (int)WampMessageType.v1Event)
                {
                    // eg. EVENT message with 'null' as payload: [8, "http://example.com/simple", null]

                    object payload = payload = wamp[2];
                    string portName, methodName, payloadType;
                    WampClientProxy.SplitProcUri((string)wamp[1], out portName, out methodName, out payloadType);
                    message = new ActorMessage(null, 0, 0, null, methodName, payload);
                    message.Type = ActorMessageType.Notification;
                    message.PayloadType = payloadType;

                    _callback.MessageFromService(message); // adds source and destination
                }
                else
                {
                    ResponseNotDeserializable(id, "expected wamp message type 3 (v1CallResult)");
                }
            }
            catch (Exception ex)
            {
                if (!errorReceived) ResponseNotDeserializable(id, ex.Message);
            }
        }

        private ActorMessage GetResponseMessage(int id)
        {
            var message = _outstandingRequests[id];
            _outstandingRequests.Remove(id);
            message.Type = ActorMessageType.Response;
            message.DestinationLambda = message.SourceLambda;
            message.SourceLambda = null;
            return message;
        }

        // Connect failure or disposing context 
        private void OnDisconnect(UserContext context)
        {
        }

        #endregion
    }
}
/*
        internal void HandleSendException(ReceivingState data, Exception ex)
        {
            //RaLog.Exception("Could not send to Remact service", ex);
            ErrorMessage.Code Code;
            bool onSvc = false;

            if (ex is System.Reflection.TargetInvocationException)
            {
                ex = (ex as System.Reflection.TargetInvocationException).InnerException;
                onSvc = true;
            }

            if (ex is TimeoutException)
            {
                if (onSvc) Code = ErrorMessage.Code.TimeoutOnService;
                else Code = ErrorMessage.Code.TimeoutOnClient;
                data.timeout = true;
            }
            else if (ex is ProtocolException)
            {
                Code = ErrorMessage.Code.RequestTypeUnknownOnService;
            }
            else if (ex is FaultException)
            { // including FaultException<TDetail>
                Code = ErrorMessage.Code.ReqOrRspNotSerializableOnService;
                onSvc = true;
                data.timeout = true; // stop sending, enter Faulted state
            }
            else if (ex is CommunicationException)
            {
                if (!m_boFirstResponseReceived) Code = ErrorMessage.Code.CouldNotConnect;
                else Code = ErrorMessage.Code.ReqOrRspNotSerializableOnService; // ???
                data.timeout = true; // stop sending, enter Faulted state
            }
            else if (ex is ObjectDisposedException && !onSvc)
            {
                Code = ErrorMessage.Code.CouldNotSend;
                data.timeout = true;
                data.disposed = true;
            }
            else
            {
                if (onSvc) Code = ErrorMessage.Code.ClientDetectedUnhandledExceptionOnService;
                else Code = ErrorMessage.Code.CouldNotSend;
                data.timeout = true; // stop sending, enter Faulted state
            }
            m_boFirstResponseReceived = true;
            data.idSnd.Message = null; // Send and Receive normally point to the same id
            data.idRcv.Message = new ErrorMessage(Code, ex);
        }
*/