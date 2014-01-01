
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
    public class WampClient : IRemactProtocolDriverService
    {
        private WebSocketClient _wsClient;
        private Dictionary<int, ActorMessage> _outstandingRequests;
        private SendOrPostCallback _onOpenCompleted; // OnOpenCompleted(object actorMessage)
        private MessageHandler _onIncomingMessage;


        public WampClient(Uri websocketUri, MessageHandler onIncomingMessage)
        {
            Uri = websocketUri;
            _wsClient = new WebSocketClient(websocketUri.ToString())
            {
                //OnSend = OnSend,
                OnReceive = OnReceived,
                //OnConnect = OnConnect,
                //OnConnected = OnConnected,
                OnDisconnect = OnDisconnect,
                ConnectTimeout = new TimeSpan(0, 0, 50), // 50 sec
                SubProtocols = new string[]{"wamp"} // null: take all subprotocols
                //TODO Origin = see rfc6455
            };
            _onIncomingMessage = onIncomingMessage;
        }

        public Uri Uri { get; private set; }

        public WebSocketClient.ReadyStates ReadyState { get { return _wsClient.ReadyState; } }

        
        // Asynchronous open the connection
        internal void OpenAsync(ActorMessage request, SendOrPostCallback onOpenCompleted)
        {
            _onOpenCompleted = onOpenCompleted;
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
                _onOpenCompleted(request); // Test1.ClientNoSync, RouterClient
            }
            else if (request.Source.SyncContext == null)
            {
                RaTrc.Error("AsyncWcfLib", "No synchronization context to open " + request.Source.Name, request.Source.Logger);
                _onOpenCompleted(request);
            }
            else
            {
                request.Source.SyncContext.Post(_onOpenCompleted, request);
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


        public void Request(ActorMessage request)
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


        // Payload has been dequeued and passed to the socket buffer
        //private void OnSend(UserContext context)
        //{
        //}

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
                //    case (int)CommandType.Payload:
                //        ChatMessage(obj.Payload.Value, context);
                //        break;
                //    case (int)CommandType.NameChange:
                //        NameChange(obj.Name.Value, context);
                //        break;
                //}

                JArray wamp = JArray.Parse(json);
                int wampType = (int)wamp[0];
                id = int.Parse((string)wamp[1]);
                if (wampType == (int)WampMessageType.v1CallResult)
                {
                    // eg. CALLRESULT message with 'null' result: [3, "CcDnuI2bl2oLGBzO", null]
                    var message = _outstandingRequests[id];
                    _outstandingRequests.Remove(id);
                    message.Payload = wamp[2];
                    message.Type = ActorMessageType.Response;
                    _onIncomingMessage(message);
                }
                else if (wampType == (int)WampMessageType.v1CallError)
                {
                    // eg. CALLERROR message with generic error: [4, "gwbN3EDtFv6JvNV5", "http://autobahn.tavendo.de/error#generic", "math domain error"]
                    errorReceived = true;
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
                    _onIncomingMessage(message);
                }
                else
                {
                    ResponseNotDeserializable(id, "expected wamp message type 3 (v1CallResult)");
                }
            }
            catch (Exception ex)
            {
                //var r = new Response { Type = ResponseType.Error, Data = new { e.Payload } };
                //context.Send(JsonConvert.SerializeObject(r));
                //TODO full qualified name
                if (!errorReceived) ResponseNotDeserializable(id, ex.Message);
            }
        }

        // TCP socket is connected to the server
        //private void OnConnect(UserContext context)
        //{
        //}

        // WebSocket connection has been built and authenticated
        //private void OnConnected(UserContext context)
        //{
        //}

        // Connect failure or disposing context 
        private void OnDisconnect(UserContext context)
        {
        }

        #endregion
    }
}
