
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
                RaTrc.Error("AsyncWcfLib", "No synchronization context to open " + request.Source.Name, request.Source.Logger);
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
            string procUri = request.DestinationMethod;

            // eg. CALL message for RPC with no arguments: [2, "7DK6TdN4wLiUJgNM", "http://example.com/api#howdy"]

            var wamp = new JArray(WampMessageType.v1Call, callId, procUri);
            if (request.Payload != null)
            {
                wamp.Add(request.Payload);
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
            var message = new ActorMessage(null, 0, id, error, null);
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

            if (error != null)
            {
                wamp.Add(error);
            }

            _wsClient.Send(wamp.ToString(Formatting.None));
        }


        #endregion
        #region Alchemy callbacks


        // DataFrame.State == Handlers.WebSocket.DataFrame.DataState.Complete
        private void OnReceived(UserContext context)
        {
            Console.WriteLine("Received Data From :" + context.ClientAddress);
            int id = 0;
            bool errorReceived = false;

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
                    var message = _outstandingRequests[id];
                    _outstandingRequests.Remove(id);
                    message.Payload = wamp[2];
                    message.Type = ActorMessageType.Response;

                    _callback.MessageFromService(message); // adds source and destination
                }
                else if (wampType == (int)WampMessageType.v1CallError)
                {
                    // eg. CALLERROR message with generic error: [4, "gwbN3EDtFv6JvNV5", "http://autobahn.tavendo.de/error#generic", "math domain error"]
                    errorReceived = true;
                    id = int.Parse((string)wamp[1]);
                    string errorUri = (string)wamp[2];
                    var code = (ErrorMessage.Code)Enum.Parse(typeof(ErrorMessage.Code), errorUri, false);
                    string errorDesc = (string)wamp[3];
                    var error = new ErrorMessage(code, errorDesc);
                    var message = new ActorMessage(null, 0, id, error, null);
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
                    var message = new ActorMessage(null, 0, 0, payload, null);
                    message.DestinationMethod = (string)wamp[1]; // TODO
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
                //var r = new Response { Type = ResponseType.Error, Data = new { e.Message } };
                //context.Send(JsonConvert.SerializeObject(r));
                //TODO full qualified name
                if (!errorReceived) ResponseNotDeserializable(id, ex.Message);
            }
        }

        // Connect failure or disposing context 
        private void OnDisconnect(UserContext context)
        {
        }

        #endregion
    }
}
