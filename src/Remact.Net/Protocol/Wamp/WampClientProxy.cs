
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Net;
using System.Threading;
using Alchemy;
using Alchemy.Classes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Remact.Net.Contracts;

namespace Remact.Net.Protocol.Wamp
{
    /// <summary>
    /// Implements the protocol level for a WAMP server. See http://wamp.ws/spec/.
    /// </summary>
    public class WampClientProxy : IRemactProtocolDriverCallbacks
    {
        private UserContext _wsChannel;
        private IRemactProtocolDriverService _requestHandler;
        private RemactPortClient _clientIdent;
        private RemactPortService _serviceIdent;

        public WampClientProxy(RemactPortClient clientIdent, RemactPortService serviceIdent, IRemactProtocolDriverService requestHandler, UserContext websocketChannel)
        {
            _wsChannel = websocketChannel;
                //OnSend = OnSend,
            _wsChannel.SetOnReceive(OnReceived);
                //OnConnect = OnConnect,
                //OnConnected = OnConnected,
            _wsChannel.SetOnDisconnect(OnDisconnect);

            _clientIdent = clientIdent;
            _serviceIdent = serviceIdent;
            _requestHandler = requestHandler;
            Connected = true;
        }

        public bool Connected { get; private set; }

        public Uri ClientUri { get { return new Uri("ws://"+_wsChannel.ClientAddress.ToString()); } }


        public void OnServiceDisconnect()
        {
            _wsChannel = null;
        }

        #region IRemactProtocolDriverCallbacks implementation


        public void OnOpenCompleted(OpenAsyncState state)
        {
            throw new NotImplementedException();
        }


        private void RequestNotDeserializable(int id, string errorDesc)
        {
            var error = new ErrorMessage(ErrorMessage.Code.ReqestNotDeserializableOnService, errorDesc);
            OnErrorFromService(id, error);
        }


        /// <inheritdoc/>
        public void OnMessageFromService(LowerProtocolMessage lower)
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
                if (jToken == null) jToken = JToken.FromObject(payload);
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
                if (jToken == null) jToken = JToken.FromObject(detail);

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
                if (jToken == null) jToken = JToken.FromObject(payload);
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


        // message from web socket
        private void OnReceived(UserContext context)
        {
            int id = 0;
            bool errorReceived = false;

            try
            {
                string json = context.DataFrame.ToString();

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
                    JToken payload = null;
                    if (wamp.Count > 3)
                    {
                        payload = wamp[3];
                    }

                    var message = new RemactMessage(_clientIdent, _clientIdent.OutputClientId, id,
                                                   _serviceIdent, (string)wamp[2], payload);

                    _requestHandler.MessageFromClient(message);
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

                    var message = new RemactMessage(_clientIdent, _clientIdent.OutputClientId, id,
                                                   _serviceIdent, null, null);
                    if (wamp.Count > 4)
                    {
                        message.Payload = RemactMessage.Convert(wamp[4], errorUri); // errorUri is assemblyQualifiedTypeName
                    }
                    else
                    {
                        message.Payload = new ErrorMessage(ErrorMessage.Code.Undef, errorUri + ": " + errorDesc); // Errormessage from client
                    }

                    message.MessageType = RemactMessageType.Error;
                    _requestHandler.MessageFromClient(message);
                }
                else if (wampType == (int)WampMessageType.v1Event)
                {
                    // eg. EVENT message with 'null' as payload: [8, "http://example.com/simple", null]

                    var eventUri = (string)wamp[1];
                    var payload = RemactMessage.Convert(wamp[2], eventUri); // eventUri is assemblyQualifiedTypeName
                    var message = new RemactMessage(_clientIdent, _clientIdent.OutputClientId, 0,
                                                   _serviceIdent, null, payload);

                    message.MessageType = RemactMessageType.Notification;
                    _requestHandler.MessageFromClient(message);
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

        // Connect failure or disposing context 
        private void OnDisconnect(UserContext context)
        {
            Connected = false; // TODO
        }

        #endregion
    }
}
