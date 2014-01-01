
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Net;
using System.Threading;
using Alchemy;
using Alchemy.Classes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Remact.Net.Contracts;

namespace Remact.Net.Internal.Wamp
{
    internal class WampClientProxy: IWampRpcV1ClientCallbacks
    {
        private UserContext _wsChannel;
        private IWampRpcV1Server _requestHandler;

        public WampClientProxy(IWampRpcV1Server requestHandler, UserContext websocketChannel)
        {
            _wsChannel = websocketChannel;
                //OnSend = OnSend,
            _wsChannel.SetOnReceive(OnReceived);
                //OnConnect = OnConnect,
                //OnConnected = OnConnected,
            _wsChannel.SetOnDisconnect(OnDisconnect);
            _requestHandler = requestHandler;
            Connected = true;
        }

        public bool Connected { get; private set; }

        public EndPoint ClientAddress { get { return _wsChannel.ClientAddress; } }


        public void Dispose()
        {
            _wsChannel = null; // TODO .Dispose();
        }

        #region IWampRpcV1ClientCallbacks proxy implementation


        public void OnOpenCompleted(object response)
        {
            throw new NotImplementedException();
        }

        public void CallResult(string callId, JToken result)
        {
            // eg. CALLRESULT message with 'null' result: [3, "CcDnuI2bl2oLGBzO", null] 

            var wamp = new JArray(WampMessageType.v1CallResult, callId, result);
            _wsChannel.Send(wamp.ToString(Formatting.None));
        }

        public void CallError(string callId, string errorUri, string errorDesc, object errorDetails = null)
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

            if (errorDetails != null)
            {
                wamp.Add(errorDetails);
            }

            _wsChannel.Send(wamp.ToString(Formatting.None));
        }

        // TODO, this event comes without subscription !?
        public void Event(string topic, JToken notification)
        {
            // eg. EVENT message with 'null' as payload: [8, "http://example.com/simple", null]

            var wamp = new JArray(WampMessageType.v1Event, topic, notification);
            _wsChannel.Send(wamp.ToString(Formatting.None));
        }


        #endregion
        #region Alchemy callbacks


        // DataFrame.State == Handlers.WebSocket.DataFrame.DataState.Complete
        private void OnReceived(UserContext context)
        {
            Console.WriteLine("Received Data From :" + context.ClientAddress);
            string id = null;

            try
            {
                string json = context.DataFrame.ToString();

                JArray wamp = JArray.Parse(json);
                id = (string)wamp[1];
                if(wamp.Count < 3)
                {
                    CallError(id, WampError.ServiceCannotDeserializeWampMessage.ToString(""), "Wamp call with 3 or more arguments expected");
                    return;
                }

                int wampType = (int)wamp[0];
                if (wampType == (int)WampMessageType.v1Call)
                {
                    // eg. CALL message for RPC with no arguments: [2, "7DK6TdN4wLiUJgNM", "http://example.com/api#howdy"]
                    var arguments = new object[wamp.Count-3];
                    for (int i=3; i<wamp.Count; i++)
                    {
                        arguments[i-3] = wamp[i];
                    }

                    _requestHandler.Call(null, id, (string)wamp[2], arguments);
                }
                else if (wampType == (int)WampMessageType.v1CallError)
                {
                    if (wamp.Count >= 4)
                    {
                        _requestHandler.CallError(id, (string)wamp[2], (string)wamp[3], (object)wamp[4]);
                    }
                    else
                    {
                        _requestHandler.CallError(id, (string)wamp[2], (string)wamp[3]);
                    }
                }
                else
                {
                    CallError(id, WampError.ServiceCannotDeserializeWampMessage.ToString(""), "expected wamp message type 2 (v1Call)");
                }
            }
            catch (Exception ex)
            {
                //var r = new Response { Type = ResponseType.Error, Data = new { e.Message } };
                //context.Send(JsonConvert.SerializeObject(r));
                //TODO full qualified name
                CallError(id, WampError.ServiceCannotDeserializeWampMessage.ToString(""), ex.Message);
            }
        }

        // Connect failure or disposing context 
        private void OnDisconnect(UserContext context)
        {
            Connected = false;
        }

        #endregion
    }
}
