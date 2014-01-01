
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Threading;
using Alchemy;
using Alchemy.Classes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Remact.Net.Protocol;

namespace Remact.Net.Protocol.Wamp
{
    public class WampServer
    {
        private WebSocketClient _wsClient;
        private IRemactProtocolDriverCallbacks _responseHandler;

        public WampServer(Uri websocketUri)
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
        }

        public Uri Uri { get; private set; }

        public WebSocketClient.ReadyStates ReadyState { get { return _wsClient.ReadyState; } }

        
        // Asynchronous open the connection
        internal void WcfOpenAsync(IRemactProtocolDriverCallbacks responseHandler, ActorMessage request)
        {
            _responseHandler = responseHandler;
           // m_boFirstResponseReceived = false;
            ThreadPool.UnsafeQueueUserWorkItem(OpenOnThreadpool, request);
        }

        private void OpenOnThreadpool(object obj)
        {
            ActorMessage request = obj as ActorMessage;
            try
            {
                _wsClient.Connect();

                request.Message = null; // null = ok
                if (_wsClient.ReadyState != WebSocketClient.ReadyStates.OPEN)
                {
                    request.Message = new ErrorMessage(ErrorMessage.Code.CouldNotOpen, "WebSocketClient not connected");
                }
            }
            //catch (EndpointNotFoundException ex)
            //{
            //    request.Message = new ErrorMessage(ErrorMessage.Code.ServiceNotRunning, ex);
            //}
            catch (Exception ex)
            {
                request.Message = new ErrorMessage(ErrorMessage.Code.CouldNotOpen, ex);
            }

            if (request.Sender.IsMultithreaded)
            {
                _responseHandler.OnOpenCompleted(request); // Test1.ClientNoSync, RouterClient
            }
            else if (request.Sender.SyncContext == null)
            {
                RaTrc.Error("AsyncWcfLib", "No synchronization context to open " + request.Sender.Name, request.Sender.Logger);
                _responseHandler.OnOpenCompleted(request);
            }
            else
            {
                request.Sender.SyncContext.Post(_responseHandler.OnOpenCompleted, request);
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

        #region IWampRpcV1Server proxy implementation


        public void Call(IActorOutput client, string callId, string procUri, params object[] arguments)
        {
            // eg. CALL message for RPC with no arguments: [2, "7DK6TdN4wLiUJgNM", "http://example.com/api#howdy"]

            var wamp = new JArray(WampMessageType.v1Call, callId, procUri);
            foreach (var arg in arguments)
            {
                wamp.Add(arg);
            }

            _wsClient.Send(wamp.ToString(Formatting.None));
        }

        public void CallError(string callId, string errorUri, string errorDesc)
        {
            // eg. CALLERROR message with generic error: [4, "gwbN3EDtFv6JvNV5", "http://autobahn.tavendo.de/error#generic", "math domain error"]

            if (callId == null)
            {
                callId = string.Empty;
            }

            if (errorDesc == null)
            {
                errorDesc = string.Empty;
            }

            var wamp = new JArray(WampMessageType.v1CallError, callId, errorUri, errorDesc);
            _wsClient.Send(wamp.ToString(Formatting.None));
        }


        #endregion
        #region Alchemy callbacks


        // Message has been dequeued and passed to the socket buffer
        //private void OnSend(UserContext context)
        //{
        //}

        // DataFrame.State == Handlers.WebSocket.DataFrame.DataState.Complete
        private void OnReceived(UserContext context)
        {
            Console.WriteLine("Received Data From :" + context.ClientAddress);
            string id = null;

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

                JArray wampMsg = JArray.Parse(json);
                id = (string)wampMsg[1];
                int wampType = (int)wampMsg[0];
                if (wampType == (int)WampMessageType.v1CallResult)
                {
                    // eg. CALLRESULT message with 'null' result: [3, "CcDnuI2bl2oLGBzO", null]
                    _responseHandler.CallResult(id, wampMsg[2]);
                }
                else if (wampType == (int)WampMessageType.v1CallError)
                {
                    if (wampMsg.Count >= 4)
                    {
                        _responseHandler.CallError(id, (string)wampMsg[2], (string)wampMsg[3], (object)wampMsg[4]);
                    }
                    else
                    {
                        _responseHandler.CallError(id, (string)wampMsg[2], (string)wampMsg[3]);
                    }
                }
                else
                {
                    CallError(id, WampError.ClientCannotDeserializeWampMessage.ToString(""), "expected wamp message type 3 (v1CallResult)");
                }
            }
            catch (Exception ex)
            {
                //var r = new Response { Type = ResponseType.Error, Data = new { e.Message } };
                //context.Send(JsonConvert.SerializeObject(r));
                //TODO full qualified name
                CallError(id, WampError.ClientCannotDeserializeWampMessage.ToString(""), ex.Message);
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
