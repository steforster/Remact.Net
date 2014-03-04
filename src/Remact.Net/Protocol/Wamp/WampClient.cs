
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Threading;
using System.Collections.Concurrent;
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
        private IRemactProtocolDriverCallbacks _callback;
        private int _lowLevelErrorCount;
        private bool _faulted;
        private bool _disposed;

        public WampClient(Uri websocketUri)
        {
            ServiceUri = websocketUri;
            _wsClient = new WebSocketClient(websocketUri.ToString())
            {
                //OnSend = OnSend,// Message has been dequeued and passed to the socket buffer
                //OnConnect = OnConnect,// TCP socket is connected to the server
                SubProtocols = new string[]{"wamp"} // null: take all subprotocols
                //TODO Origin = see rfc6455
            };
        }

        #region IRemactProtocolDriverService proxy implementation


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

            _callback.OnOpenCompleted(request);
        }

        private void OnConnectFailure(UserContext context)
        {
            _faulted = true;
            var request = (ActorMessage)context.Data;
            request.Payload = new ErrorMessage(ErrorMessage.Code.CouldNotOpen, context.LatestException);
            _callback.OnOpenCompleted(request);
        }

        public void Dispose()
        {
            try
            {
                _disposed = true;
                _wsClient.Disconnect();
            }
            catch { }
        }


        public void MessageFromClient(ActorMessage msg)
        {
            string callId = msg.RequestId.ToString();

            // eg. CALL message for RPC with no arguments: [2, "7DK6TdN4wLiUJgNM", "http://example.com/api#howdy"]
            var wamp = new JArray(WampMessageType.v1Call, callId, msg.DestinationMethod);

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

            var error = new ErrorMessage(ErrorMessage.Code.ResponseNotDeserializableOnClient, errorDesc);
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


        // message from web socket
        private void OnReceived(UserContext context)
        {
            if (_disposed)
            {
                return;
            }

            var msg = new LowerProtocolMessage();
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
                    msg.Type = ActorMessageType.Response;
                    msg.RequestId = int.Parse((string)wamp[1]);
                    msg.Payload = wamp[2]; // JToken
                    _callback.OnMessageFromService(msg);
                }
                else if (wampType == (int)WampMessageType.v1CallError)
                {
                    // eg. CALLERROR message with generic error: [4, "gwbN3EDtFv6JvNV5", "http://autobahn.tavendo.de/error#generic", "math domain error"]
                    msg.Type = ActorMessageType.Error;
                    var requestId = (string)wamp[1];
                    var errorUri  = (string)wamp[2];
                    var errorDesc = (string)wamp[3];

                    if (!string.IsNullOrEmpty(requestId))
                    {
                        msg.RequestId = int.Parse(requestId);
                    }

                    if (wamp.Count > 4)
                    {
                        msg.Payload = ActorMessage.Convert(wamp[4], errorUri); // errorUri is assemblyQualifiedTypeName
                    }
                    else
                    {
                        msg.Payload = new ErrorMessage(ErrorMessage.Code.Undef, errorUri + ": " + errorDesc);
                    }

                    _callback.OnMessageFromService(msg);
                }
                else if (wampType == (int)WampMessageType.v1Event)
                {
                    // eg. EVENT message with 'null' as payload: [8, "http://example.com/simple", null]

                    msg.Type = ActorMessageType.Notification;
                    var notifyUri = (string)wamp[1];
                    msg.Payload = ActorMessage.Convert(wamp[2], notifyUri); // eventUri is assemblyQualifiedTypeName
                    _callback.OnMessageFromService(msg);
                }
                else
                {
                    ResponseNotDeserializable(msg.RequestId, "expected wamp message type 3 (v1CallResult)");
                }
            }
            catch (Exception ex)
            {
                if (msg.Type != ActorMessageType.Error) ResponseNotDeserializable(msg.RequestId, ex.Message);
            }
        }


        // Connect failure or disposing context 
        private void OnDisconnect(UserContext context)
        {
            _faulted = true;
            if (_disposed)
            {
                return;
            }

            _callback.OnServiceDisconnect();
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