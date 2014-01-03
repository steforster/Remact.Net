
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
        public ActorOutput _clientIdent;
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

        public string ClientAddress { get { return _wsChannel.ClientAddress.ToString(); } }


        public void Dispose()
        {
            _wsChannel = null; // TODO .Dispose();
        }

        #region IWampRpcV1ClientCallbacks proxy implementation


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
            var message = new ActorMessage(null, 0, id, error, null);
            ErrorFromService(message);
        }

        private void ResponseFromService(ActorMessage response)
        {
            string callId = response.RequestId.ToString();

            // eg. CALLRESULT message with 'null' result: [3, "CcDnuI2bl2oLGBzO", null] 

            var wamp = new JArray(WampMessageType.v1CallResult, callId, response.Payload);
            _wsChannel.Send(wamp.ToString(Formatting.None));
        }

        private void ErrorFromService(ActorMessage message)
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

            _wsChannel.Send(wamp.ToString(Formatting.None));
        }

        // TODO, this event comes without subscription !?
        private void NotificationFromService(ActorMessage notification)
        {
            // eg. EVENT message with 'null' as payload: [8, "http://example.com/simple", null]

            var wamp = new JArray(WampMessageType.v1Event, notification.DestinationMethod, notification.Payload);
            _wsChannel.Send(wamp.ToString(Formatting.None));
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
                    object payload = null;
                    if (wamp.Count > 3)
                    {
                        payload = wamp[3];
                    }
                    var message = new ActorMessage(_clientIdent, _clientIdent.OutputClientId, id, payload, null);
                    message.Destination = _serviceIdent;
                    message.DestinationMethod = (string)wamp[2];

                    _requestHandler.MessageFromClient(message);
                }
                else if (wampType == (int)WampMessageType.v1CallError)
                {
                    // eg. CALLERROR message with generic error: [4, "gwbN3EDtFv6JvNV5", "http://autobahn.tavendo.de/error#generic", "math domain error"]
                    errorReceived = true;
                    id = int.Parse((string)wamp[1]);
                    string errorUri = (string)wamp[2];
                    var code = (ErrorMessage.Code)Enum.Parse(typeof(ErrorMessage.Code), errorUri, false);
                    string errorDesc = (string)wamp[3];
                    var payload = new ErrorMessage(code, errorDesc);
                    var message = new ActorMessage(_clientIdent, _clientIdent.OutputClientId, id, payload, null);
                    message.Destination = _serviceIdent;
                    //message.DestinationMethod = TODO;
                    message.Type = ActorMessageType.Error;
                    //if (wamp.Count >= 4)
                    //{
                    //    message. (object)wamp[4]); TODO
                    //}
                    _requestHandler.MessageFromClient(message);
                }
                else if (wampType == (int)WampMessageType.v1Event)
                {
                    // eg. EVENT message with 'null' as payload: [8, "http://example.com/simple", null]

                    object payload = wamp[2];
                    var message = new ActorMessage(_clientIdent, _clientIdent.OutputClientId, 0, payload, null);
                    message.Destination = _serviceIdent;
                    message.DestinationMethod = (string)wamp[1]; // TODO
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
                //TODO full qualified name
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
