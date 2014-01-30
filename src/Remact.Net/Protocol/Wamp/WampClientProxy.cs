
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
        private ActorOutput _clientIdent;
        private ActorInput _serviceIdent;

        public WampClientProxy(ActorOutput clientIdent, ActorInput serviceIdent, IRemactProtocolDriverService requestHandler, UserContext websocketChannel)
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


        public void Dispose()
        {
            _wsChannel = null; // TODO .Dispose();
        }

        #region IRemactProtocolDriverCallbacks implementation


        public void OnOpenCompleted(object response)
        {
            throw new NotImplementedException();
        }

        public void MessageFromService(ActorMessage message)
        {
            switch (message.Type)
            {
                case ActorMessageType.Response: ResponseFromService(message); break;
                case ActorMessageType.Error:    ErrorFromService(message); break;
                default:                        NotificationFromService(message); break;
            }
        }

        private void RequestNotDeserializable(int id, string errorDesc)
        {
            var error = new ErrorMessage(ErrorMessage.Code.ReqOrRspNotSerializableOnService, errorDesc);
            var message = new ActorMessage(null, 0, id, null, null, error);
            ErrorFromService(message);
        }

        private void ResponseFromService(ActorMessage response)
        {
            string callId = response.RequestId.ToString();
            JToken payload = null;
            if (response.Payload != null)
            {
                payload = response.Payload as JToken;
                if (payload == null) payload = JToken.FromObject(response.Payload);
            }

            // eg. CALLRESULT message with 'null' result: [3, "CcDnuI2bl2oLGBzO", null] 

            var wamp = new JArray(WampMessageType.v1CallResult, callId, payload);
            _wsChannel.Send(wamp.ToString(Formatting.None));
        }

        private void ErrorFromService(ActorMessage message)
        {
            string callId = message.RequestId.ToString();
            string errorUri = string.Empty;
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

            _wsChannel.Send(wamp.ToString(Formatting.None));
        }

        // TODO, this event comes without subscription !?
        private void NotificationFromService(ActorMessage notification)
        {
            // eg. EVENT message with 'null' as payload: [8, "http://example.com/simple", null]

            JToken payload = null;
            if (notification.Payload != null)
            {
                payload = notification.Payload as JToken;
                if (payload == null) payload = JToken.FromObject(notification.Payload);
            }

            var wamp = new JArray(WampMessageType.v1Event, notification.DestinationMethod, payload);
            _wsChannel.Send(wamp.ToString(Formatting.None));
        }


        #endregion
        #region Alchemy callbacks


        // DataFrame.State == Handlers.WebSocket.DataFrame.DataState.Complete
        private void OnReceived(UserContext context)
        {
            //Console.WriteLine("Received Data From :" + context.ClientAddress);
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

                    //string portName, methodName, payloadType;
                    //SplitProcUri((string)wamp[2], out portName, out methodName, out payloadType);
                    var message = new ActorMessage(_clientIdent, _clientIdent.OutputClientId, id,
                                                   _serviceIdent, (string)wamp[2], payload);
                    message.PayloadType = null; // has to be converted

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

                    var message = new ActorMessage(_clientIdent, _clientIdent.OutputClientId, id,
                                                   _serviceIdent, null, null);
                    if (wamp.Count > 4)
                    {
                        message.Payload = wamp[4];
                        message.PayloadType = errorUri; // TODO ???
                    }
                    else
                    {
                        message.Payload = new ErrorMessage(ErrorMessage.Code.Undef, errorUri + ": " + errorDesc); // Errormessage from client
                        message.PayloadType = typeof(ErrorMessage).FullName;
                    }

                    message.Type = ActorMessageType.Error;
                    _requestHandler.MessageFromClient(message);
                }
                else if (wampType == (int)WampMessageType.v1Event)
                {
                    // eg. EVENT message with 'null' as payload: [8, "http://example.com/simple", null]

                    JToken payload = wamp[2];
                    //string portName, methodName, payloadType;
                    //SplitProcUri((string)wamp[1], out portName, out methodName, out payloadType);
                    var message = new ActorMessage(_clientIdent, _clientIdent.OutputClientId, 0,
                                                   _serviceIdent, (string)wamp[2], payload);
                    message.PayloadType = null; // has to be converted

                    message.Type = ActorMessageType.Notification;
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

       /* internal static void SplitProcUri(string uri, out string portName, out string methodName, out string payloadType)
        {
            // <ActorPortName> / <MethodName> / <FullQualifiedPayloadType>
            portName = null;
            methodName = null;
            payloadType = null;
            if(string.IsNullOrEmpty(uri)) return;
 
            int i = 0;
            while (uri[i] == '/')
            {
                i++; // skip leading slashes
                if (i >= uri.Length) return;
            }

            int j = uri.IndexOf('/', i);
            if (j < 0)
            {
                payloadType = uri.Substring(i);
                return;
            }

            string first = uri.Substring(i, j - i);
            i = j + 1;
            j = uri.IndexOf('/', i);
            if (j < 0)
            {
                methodName = first;
                payloadType = uri.Substring(i);
                return;
            }

            portName = first;
            methodName = uri.Substring(i, j - i);
            i = j + 1;
            j = uri.IndexOf('/', i);
            if (j < 0)
            {
                payloadType = uri.Substring(i);
                return;
            }

            payloadType = uri.Substring(i, j - i);
        }*/
    }
}
