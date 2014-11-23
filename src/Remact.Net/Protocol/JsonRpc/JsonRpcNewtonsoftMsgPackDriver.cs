
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.IO;
using Alchemy.Classes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Remact.Net.Protocol.JsonRpc
{
    /// <summary>
    /// Implements the protocol level for a JSON-RPC client. See http://www.jsonrpc.org/specification.
    /// Uses the MsgPack-cli serializer.
    /// Is used on client as well as on service side.
    /// </summary>
    public class JsonRpcNewtonsoftMsgPackDriver : IDisposable
    {
        private IRemactProtocolDriverToClient _toClientInterface; // not null on client side
        private IRemactProtocolDriverToService _toServiceInterface;// not null on service side
        private Action<byte[], int> _sendAction;

        /// <summary>
        /// Called on client side.
        /// </summary>
        /// <param name="sendAction">Sends to the web socket.</param>
        /// <param name="toClientInterface">Callback interface.</param>
        protected void InitOnClientSide(Action<byte[], int> sendAction, IRemactProtocolDriverToClient toClientInterface)
        {
            _sendAction = sendAction;
            _toClientInterface = toClientInterface;
        }

        /// <summary>
        /// Called on service side.
        /// </summary>
        /// <param name="sendAction">Sends to the web socket.</param>
        /// <param name="toServiceInterface">Callback interface.</param>
        protected void InitOnServiceSide(Action<byte[], int> sendAction, IRemactProtocolDriverToService toServiceInterface)
        {
            _sendAction = sendAction;
            _toServiceInterface = toServiceInterface;
        }


        #region Send messages

        /// <summary>
        /// Send a message in JsonRpc and MsgPack.
        /// </summary>
        /// <param name="msg">The message.</param>
        protected void SendMessage(LowerProtocolMessage msg)
        {
            if (msg.Type == RemactMessageType.Error)
            {
                SendError(msg.RequestId, msg.Payload as ErrorMessage);
                return;
            }
            
            var rpc = new JsonRpcV2Message
            {
                jsonrpc = "2.0",
            };
            
            if (msg.RequestId > 0 && msg.Type != RemactMessageType.Notification)
            {
                rpc.id = msg.RequestId.ToString();
            } 
            
            if (msg.Type == RemactMessageType.Response)
            {
                rpc.result = msg.Payload;
            }
            else
            {   // request or notification
                rpc.method = msg.DestinationMethod;
                rpc.params1 = msg.Payload;
            }

            using (var stream = new MemoryStream())
            using (var writer = new Newtonsoft.Msgpack.MessagePackWriter(stream))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(writer, rpc);
                _sendAction(stream.GetBuffer(), (int)stream.Length); // FastDirectSendingMode must be switched on
            }
        }

        /// <summary>
        /// Called, when an incoming message cannot be deserialized.
        /// </summary>
        /// <param name="id">The request id is > 0 when it could be read. Otherwise 0.</param>
        /// <param name="errorDesc">A description of the error.</param>
        protected virtual void IncomingMessageNotDeserializable(int id, string errorDesc)
        {
            // overloded on client side to limit error responses to service
            var error = new ErrorMessage(ErrorCode.ReqestNotDeserializableOnService, errorDesc);
            SendError(id, error);
        }

        /// <summary>
        /// Send an error message in JsonRpc and MsgPack.
        /// </summary>
        /// <param name="requestId">The request id is > 0 in case the error is a response to a request. Otherwise 0.</param>
        /// <param name="payload">The error payload.</param>
        protected void SendError(int requestId, Remact.Net.ErrorMessage payload)
        {
            var rpc = new JsonRpcV2Message
            {
                jsonrpc = "2.0",
                error = new JsonRpcV2Error
                {
                    data = payload
                } 
            };
            
            if (requestId > 0)
            {
                rpc.id = requestId.ToString();
            }
            
            if (payload == null)
            {
//                rpc.error.code = (int)errorMsg.Error; TODO
                rpc.error.message = "ErrorFromService"; // message.Payload.GetType().FullName;
            }
            else
            {
                rpc.error.code = (int)payload.Error;
                rpc.error.message = payload.Message;
            }

            using (var stream = new MemoryStream())
            using (var writer = new Newtonsoft.Msgpack.MessagePackWriter(stream))
            {

                var serializer = new JsonSerializer();
                serializer.Serialize(writer, rpc);
                _sendAction(stream.GetBuffer(), (int)stream.Length);
            }
        }


        #endregion
        #region Receive messages


        /// <summary>
        /// Called when a message from web socket is received.
        /// </summary>
        /// <param name="context">Alchemy connection context.</param>
        protected void OnReceived(UserContext context)
        {
            if (_disposed)
            {
                return;
            }

            var msg = new LowerProtocolMessage();
            JsonRpcV2Message rpc = null;
            try
            {
                using (var reader = new Newtonsoft.Msgpack.MessagePackReader(context.DataFrame))
                {
                    var serializer = new JsonSerializer();
                    rpc = (JsonRpcV2Message)serializer.Deserialize(reader, typeof(JsonRpcV2Message));
                }

                int.TryParse(rpc.id, out msg.RequestId); // rpc.id == null, RequestId == 0 for notifications

                if (rpc.jsonrpc != "2.0")
                {
                    IncomingMessageNotDeserializable(msg.RequestId, "not supportet json-rpc protocol version");
                    return;
                }
                else if (rpc.method != null || rpc.params1 != null)
                {
                    if (rpc.id != null)
                    {
                        msg.Type = RemactMessageType.Request;
                    }
                    else
                    {
                        msg.Type = RemactMessageType.Notification;
                    }
                    
                    msg.DestinationMethod = rpc.method;
                    msg.Payload = rpc.params1;
                }
                else if (rpc.result != null && rpc.id != null)
                {
                    msg.Type = RemactMessageType.Response;
                    msg.Payload = rpc.result;
                }
                else if (rpc.error != null)
                {
                    msg.Type = RemactMessageType.Error;
                    msg.Payload = rpc.error;
                }
                else
                {
                    IncomingMessageNotDeserializable(msg.RequestId, "not supportet json-rpc message type");
                    return;
                }

                // in case the Payload is a primitive type, it has already been converted and SerializationPayload.AsDynamic will return null.
                msg.SerializationPayload = new NewtonsoftJsonPayload(msg.Payload as JToken);
                if (_toClientInterface != null)
                {
                    _toClientInterface.OnMessageToClient(msg); // client side
                }
                else
                {   
                    _toServiceInterface.MessageToService(msg); // service side
                }
            }
            catch (Exception ex)
            {
                if (msg.Type != RemactMessageType.Error) IncomingMessageNotDeserializable(msg.RequestId, ex.Message);
            }
        }


        #endregion
        #region IDisposable Support

        private bool _disposed;

        /// <summary>
        /// When overriding, dont forget to call the base class.
        /// </summary>
        /// <param name="disposing">True, when managed resources may be disposed of.</param>
        protected virtual void Dispose(bool disposing)
        {
            _disposed = true;
        }

        /// <summary>
        /// Stops incoming calls.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}