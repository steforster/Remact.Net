
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using Alchemy.Classes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Remact.Net.Remote;

namespace Remact.Net.Plugin.Json.Msgpack.Alchemy
{
    /// <summary>
    /// Implements the protocol level for a WAMP server. See http://wamp.ws/spec/.
    /// Uses the Newtonsoft.Json serializer.
    /// </summary>
    public class WampClientStub : IRemactProtocolDriverToClient
    {
        private UserContext _wsChannel;
        private IRemactProtocolDriverToService _requestHandler;

        /// <summary>
        /// Constructor for a stub instance that connects from service to client.
        /// </summary>
        /// <param name="requestHandler">The interface for callbacks to the service.</param>
        /// <param name="websocketChannel">The Alchemy channel.</param>
        public WampClientStub(IRemactProtocolDriverToService requestHandler, UserContext websocketChannel)
        {
            _wsChannel = websocketChannel;
                //OnSend = OnSend,
            _wsChannel.SetOnReceive(OnReceived);
                //OnConnect = OnConnect,
                //OnConnected = OnConnected,
            _wsChannel.SetOnDisconnect(OnDisconnect);

            _requestHandler = requestHandler;
        }


        #region IRemactProtocolDriverCallbacks implementation

        /// <inheritdoc/>
        public Uri ClientUri { get { return new Uri("ws://"+_wsChannel.ClientAddress.ToString()); } }

        /// <inheritdoc/>
        public void OnServiceDisconnect()
        {
            if (_wsChannel != null)
            {
                _wsChannel.Disconnect();
                _wsChannel = null;
            }
        }

        /// <inheritdoc/>
        public void OnOpenCompleted(OpenAsyncState state)
        {
            throw new NotImplementedException();
        }


        private void RequestNotDeserializable(int id, string errorDesc)
        {
            var error = new ErrorMessage(ErrorCode.ReqestNotDeserializableOnService, errorDesc);
            OnErrorFromService(id, error);
        }

        /// <inheritdoc/>
        public void OnMessageToClient(LowerProtocolMessage lower)
        {
            switch (lower.Type)
            {
                case RemactMessageType.Response: OnResponseFromService(lower.RequestId, lower.Payload); break;
                case RemactMessageType.Error: OnErrorFromService(lower.RequestId, lower.Payload); break;
                default: OnNotificationFromService(lower.Payload); break;
            }
        }


        private void OnResponseFromService(int requestId, object payload)
        {
            JToken jToken = null;
            if (payload != null)
            {
                jToken = payload as JToken;
                if (jToken == null) 
                {
                    var serializer = JsonProtocolConfig.Instance.GetSerializer();
                    jToken = JToken.FromObject(payload, serializer);
                }
            }

            // eg. CALLRESULT message with 'null' result: [3, "CcDnuI2bl2oLGBzO", null] 

            var wamp = new JArray(WampMessageType.v1CallResult, requestId, jToken);
            _wsChannel.Send(wamp.ToString(Formatting.None));
        }


        private void OnErrorFromService(int requestId, object detail)
        {
            string type;
            if (detail != null)
            {
                type = detail.GetType().AssemblyQualifiedName;
            }
            else
            {
                type = "ErrorFromService";
            }

            // eg. CALLERROR message with generic error: [4, "gwbN3EDtFv6JvNV5", "http://autobahn.tavendo.de/error#generic", "math domain error"]

            var wamp = new JArray(WampMessageType.v1CallError, requestId, type, "");

            if (detail != null)
            {
                var jToken = detail as JToken;
                if (jToken == null) 
                {
                    var serializer = JsonProtocolConfig.Instance.GetSerializer();
                    jToken = JToken.FromObject(detail, serializer);
                }

                wamp.Add(jToken);
            }

            _wsChannel.Send(wamp.ToString(Formatting.None));
        }


        private void OnNotificationFromService(object payload)
        {
            string type;
            JToken jToken = null;
            if (payload != null)
            {
                type = payload.GetType().AssemblyQualifiedName;

                jToken = payload as JToken;
                if (jToken == null) 
                {
                    var serializer = JsonProtocolConfig.Instance.GetSerializer();
                    jToken = JToken.FromObject(payload, serializer);
                }
            }
            else
            {
                type = "NotificationFromService";
            }

            // eg. EVENT message with 'null' as payload: [8, "http://example.com/simple", null]

            var wamp = new JArray(WampMessageType.v1Event, type, jToken);
            _wsChannel.Send(wamp.ToString(Formatting.None));
        }


        #endregion
        #region Alchemy callbacks


        /// <summary>
        /// Called when a message from web socket is received.
        /// </summary>
        /// <param name="context">Alchemy connection context.</param>
        private void OnReceived(UserContext context)
        {
            int id = 0;
            bool errorReceived = false;

            try
            {
                string json = context.DataFrame.ToString();

                var serializer = JsonProtocolConfig.Instance.GetSerializer();
                JArray wamp = JArray.Parse(json);
                if(wamp.Count < 3)
                {
                    RequestNotDeserializable(id, "Wamp call with 3 or more arguments expected");
                    return;
                }

                int wampType = (int)wamp[0];
                if (wampType == (int)WampMessageType.v1Call)
                {
                    // eg. CALL message for RPC with no arguments: [2, "7DK6TdN4wLiUJgNM", "http://example.com/api#howdy"]
                    id = int.Parse((string)wamp[1]);
                    JToken jToken = null;
                    if (wamp.Count > 3)
                    {
                        jToken = wamp[3];
                    }

                    _requestHandler.MessageToService(new LowerProtocolMessage
                    {
                        DestinationMethod = (string)wamp[2],
                        Payload = jToken,
                        SerializationPayload = new NewtonsoftJsonPayload(jToken, serializer),
                        Type = RemactMessageType.Request,
                        RequestId = id,
                    });
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
                    }

                    NewtonsoftJsonPayload pld;
                    object payload;
                    if (wamp.Count > 4)
                    {
                        pld = new NewtonsoftJsonPayload(wamp[4], serializer); // JToken
                        payload = pld.TryReadAs(errorUri); // errorUri is assemblyQualifiedTypeNamewamp[4]
                    }
                    else
                    {
                        pld = null;
                        payload = new ErrorMessage(ErrorCode.Undef, errorUri + ": " + errorDesc); // Errormessage from client
                    }

                    _requestHandler.MessageToService(new LowerProtocolMessage
                    {
                        DestinationMethod = null,
                        Payload = payload,
                        SerializationPayload = pld,
                        Type = RemactMessageType.Error,
                        RequestId = id,
                    });
                }
                else if (wampType == (int)WampMessageType.v1Event)
                {
                    // eg. EVENT message with 'null' as payload: [8, "http://example.com/simple", null]
                    JToken jToken = null;
                    if (wamp.Count > 2)
                    {
                        jToken = wamp[2];
                    }

                    _requestHandler.MessageToService(new LowerProtocolMessage
                    {
                        DestinationMethod = (string)wamp[1],
                        Payload = jToken,
                        SerializationPayload = new NewtonsoftJsonPayload(jToken, serializer),
                        Type = RemactMessageType.Notification,
                        RequestId = 0,
                    });
                }
                else
                {
                    RequestNotDeserializable(id, "expected wamp message type 2 (v1Call)");
                }
            }
            catch (Exception ex)
            {
                if (!errorReceived) RequestNotDeserializable(id, ex.Message);
            }
        }

        /// <summary>
        /// Occurs, when connection failes or context is disposed.
        /// </summary>
        /// <param name="context"></param>
        private void OnDisconnect(UserContext context)
        {
            // TODO
        }

        #endregion
    }
}
