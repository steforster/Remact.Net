
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Threading;
using System.Collections.Concurrent;
using Alchemy;
using Alchemy.Classes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Remact.Net.Protocol;
using System.IO;

namespace Remact.Net.Protocol.Wamp
{
    /// <summary>
    /// Implements the protocol level for a WAMP client. See http://wamp.ws/spec/.
    /// Uses the Newtonsoft.Json serializer.
    /// </summary>
    public class WampClient : ProtocolDriverClientBase, IRemactProtocolDriverService
    {
        /// <summary>
        /// Constructor for a client that connects to a service.
        /// </summary>
        /// <param name="websocketUri">The uri of the service.</param>
        public WampClient(Uri websocketUri)
        {
            ServiceUri = websocketUri;
            _onReceiveAction = OnReceived;
            _wsClient = new WebSocketClient(websocketUri.ToString())
            {
                //OnSend = OnSend,// Message has been dequeued and passed to the socket buffer
                //OnConnect = OnConnect,// TCP socket is connected to the server
                SubProtocols = new string[]{"wamp"} // null: take all subprotocols
                //TODO Origin = see rfc6455
            };
        }

        #region IRemactProtocolDriverService proxy implementation

        /// <inheritdoc/>
        public PortState PortState {get {return BasePortState;}}

        /// <inheritdoc/>
        public void OpenAsync(OpenAsyncState state, IRemactProtocolDriverCallbacks callback)
        {
            base.BaseOpenAsync(state, callback);
        }

        /// <inheritdoc/>
        public void MessageFromClient(RemactMessage msg)
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

        private int _lowLevelErrorCount;

        private void ResponseNotDeserializable(int id, string errorDesc)
        {
            if (++_lowLevelErrorCount > 100)
            {
                return; // do not respond endless on erronous error messages
            }

            var error = new ErrorMessage(ErrorCode.ResponseNotDeserializableOnClient, errorDesc);
            var message = new RemactMessage(null, null, error, RemactMessageType.Error, null, 0, id);
            ErrorFromClient(message);
        }

        private void ErrorFromClient(RemactMessage message)
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


        /// <summary>
        /// Called when a message from web socket is received.
        /// </summary>
        /// <param name="context">Alchemy connection context.</param>
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
                    msg.Type = RemactMessageType.Response;
                    msg.RequestId = int.Parse((string)wamp[1]);
                    msg.Payload = wamp[2]; // JToken
                    msg.SerializationPayload = new NewtonsoftJsonPayload(wamp[2]);
                    _callback.OnMessageFromService(msg);
                }
                else if (wampType == (int)WampMessageType.v1CallError)
                {
                    // eg. CALLERROR message with generic error: [4, "gwbN3EDtFv6JvNV5", "http://autobahn.tavendo.de/error#generic", "math domain error"]
                    msg.Type = RemactMessageType.Error;
                    var requestId = (string)wamp[1];
                    var errorUri  = (string)wamp[2];
                    var errorDesc = (string)wamp[3];

                    if (!string.IsNullOrEmpty(requestId))
                    {
                        msg.RequestId = int.Parse(requestId);
                    }

                    if (wamp.Count > 4)
                    {
                        var pld = new NewtonsoftJsonPayload(wamp[4]); // JToken
                        msg.Payload = pld.TryReadAs(errorUri); // errorUri is assemblyQualifiedTypeName
                        msg.SerializationPayload = pld;
                    }
                    else
                    {
                        msg.Payload = new ErrorMessage(ErrorCode.Undef, errorUri + ": " + errorDesc);
                    }

                    _callback.OnMessageFromService(msg);
                }
                else if (wampType == (int)WampMessageType.v1Event)
                {
                    // eg. EVENT message with 'null' as payload: [8, "http://example.com/simple", null]

                    msg.Type = RemactMessageType.Notification;
                    var notifyUri = (string)wamp[1];
                    var pld = new NewtonsoftJsonPayload(wamp[2]); // JToken
                    msg.Payload = pld.TryReadAs(notifyUri); // notifyUri is assemblyQualifiedTypeName
                    msg.SerializationPayload = pld; 
                    _callback.OnMessageFromService(msg);
                }
                else
                {
                    ResponseNotDeserializable(msg.RequestId, "expected wamp message type 3 (v1CallResult)");
                }
            }
            catch (Exception ex)
            {
                if (msg.Type != RemactMessageType.Error) ResponseNotDeserializable(msg.RequestId, ex.Message);
            }
        }

        #endregion
    }
}