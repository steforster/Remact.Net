
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
        private bool _faulted;
        private bool _disposed;

        public WampClient(Uri websocketUri)
        {
            ServiceUri = websocketUri;
            _outstandingRequests = new Dictionary<int, ActorMessage>(); 
            _wsClient = new WebSocketClient(websocketUri.ToString())
            {
                //OnSend = OnSend,// Message has been dequeued and passed to the socket buffer
                //OnConnect = OnConnect,// TCP socket is connected to the server
                SubProtocols = new string[]{"wamp"} // null: take all subprotocols
                //TODO Origin = see rfc6455
            };
        }

        public Uri ServiceUri { get; private set; }

        public PortState PortState 
        { 
            get 
            {
                if (_faulted)
                {
                    return PortState.Faulted;
                }
                else if (_wsClient.ReadyState == WebSocketClient.ReadyStates.CONNECTING)
                {
                    return PortState.Connecting;
                }
                else if (_wsClient.ReadyState == WebSocketClient.ReadyStates.OPEN)
                {
                    return PortState.Ok;
                }
                else
                {
                    return PortState.Disconnected;
                }
            } 
        }

        // Asynchronous open the connection
        public void OpenAsync(ActorMessage request, IRemactProtocolDriverCallbacks callback)
        {
            _callback = callback;
            _wsClient.OnConnected = OnConnected;
            _wsClient.OnDisconnect = OnConnectFailure;
            _wsClient.BeginConnect(request);
        }

        private void OnConnected(UserContext context)
        {
            var request = (ActorMessage)context.Data;
            request.Payload = null; // null = ok
            if (_wsClient.ReadyState != WebSocketClient.ReadyStates.OPEN)
            {
                _faulted = true;
                request.Payload = new ErrorMessage(ErrorMessage.Code.CouldNotOpen, "WebSocketClient not connected");
            }
            else
            {
                context.SetOnReceive(OnReceived);
                context.SetOnDisconnect(OnDisconnect);
            }

            ConnectCallback(request);
        }

        private void OnConnectFailure(UserContext context)
        {
            _faulted = true;
            var request = (ActorMessage)context.Data;
            request.Payload = new ErrorMessage(ErrorMessage.Code.CouldNotOpen, context.LatestException);
            ConnectCallback(request);
        }

        private void ConnectCallback(ActorMessage request)
        {
            if (_disposed)
            {
                return;
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
                    _faulted = true;
                    request.Payload = new ErrorMessage(ErrorMessage.Code.CouldNotOpen, "WebSocketClient not connected");
                }
            }
            //catch (EndpointNotFoundException ex)
            //{
            //    request.Payload = new ErrorMessage(ErrorMessage.Code.ServiceNotRunning, ex);
            //}
            catch (Exception ex)
            {
                _faulted = true;
                request.Payload = new ErrorMessage(ErrorMessage.Code.CouldNotOpen, ex);
            }

            if (_disposed)
            {
                return;
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
            try
            {
                _disposed = true;
                _wsClient.Disconnect();
                // do not dispose, this sets the global cancellation token: _wsClient.Dispose();
            }
            catch { }
        }

        #region IRemactProtocolDriverService proxy implementation


        public void MessageFromClient(ActorMessage msg)
        {
            _outstandingRequests.Add(msg.RequestId, msg);
            string callId = msg.RequestId.ToString();
            string procUri = string.Concat(/*msg.Destination.Name, '/',*/ msg.DestinationMethod, '/', msg.PayloadType);


            // eg. CALL message for RPC with no arguments: [2, "7DK6TdN4wLiUJgNM", "http://example.com/api#howdy"]

            var wamp = new JArray(WampMessageType.v1Call, callId, procUri);
            if (msg.Payload != null)
            {
                var jToken = msg.Payload as JToken;
                if (jToken == null) jToken = JToken.FromObject(msg.Payload);

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
            string errorUri;
            string errorDesc = string.Empty;
            if (message.Payload != null)
            {
                errorUri = message.Payload.GetType().FullName;
            }
            else
            {
                errorUri = "ErrorFromService";
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
            if (_disposed)
            {
                return;
            }

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
                    if (message == null)
                    {
                        RaLog.Error("Clt", "Received response to unknown request id " + id + " from " + ServiceUri);
                        return;
                    }

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
                    var errorUri  = (string)wamp[2];
                    var errorDesc = (string)wamp[3];

                    if (!string.IsNullOrEmpty(requestId))
                    {
                        id = int.Parse(requestId);
                        message = GetResponseMessage(id);
                    }

                    if (message == null)
                    {
                        message = new ActorMessage(null, 0, 0, null, null, null);
                    }

                    if (wamp.Count > 4)
                    {
                        message.Payload = wamp[4];
                        message.PayloadType = errorUri;
                    }
                    else
                    {
                        message.Payload = new ErrorMessage(ErrorMessage.Code.Undef, errorUri + ": " + errorDesc); // Errormessage from service
                        message.PayloadType = typeof(ErrorMessage).FullName;
                    }

                    message.Type = ActorMessageType.Error;
                    _callback.MessageFromService(message); // adds source and destination
                }
                else if (wampType == (int)WampMessageType.v1Event)
                {
                    // eg. EVENT message with 'null' as payload: [8, "http://example.com/simple", null]

                    object payload = wamp[2];
                    string portName, methodName, payloadType;
                    WampClientProxy.SplitProcUri((string)wamp[1], out portName, out methodName, out payloadType);
                    message = new ActorMessage(null, 0, 0, null, methodName, payload);
                    message.PayloadType = payloadType;

                    message.Type = ActorMessageType.Notification;
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
            ActorMessage message;
            if(!_outstandingRequests.TryGetValue(id, out message))
            {
                return null;
            }
            _outstandingRequests.Remove(id);
            message.Type = ActorMessageType.Response;
            message.DestinationLambda = message.SourceLambda;
            message.SourceLambda = null;
            return message;
        }

        // Connect failure or disposing context 
        private void OnDisconnect(UserContext context)
        {
            _faulted = true;
            if (_disposed)
            {
                return;
            }

            var copy = _outstandingRequests;
            _outstandingRequests = new Dictionary<int, ActorMessage>();
            foreach (var msg in copy.Values)
            {
                msg.Type = ActorMessageType.Error;
                msg.DestinationLambda = msg.SourceLambda;
                msg.SourceLambda = null;
                msg.Payload = new ErrorMessage(ErrorMessage.Code.CouldNotSend, "web socket disconnected");
                msg.PayloadType = msg.Payload.GetType().FullName;
                _callback.MessageFromService(msg); // adds source and destination
            }
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