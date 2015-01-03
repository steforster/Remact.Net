
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
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
        private int _lowLevelErrorCount;
        private IRemactProtocolDriverToClient _toClientInterface; // not null on client side
        private IRemactProtocolDriverToService _toServiceInterface;// not null on service side

        /// <summary>
        /// Called on client side.
        /// </summary>
        /// <param name="toClientInterface">Callback interface.</param>
        protected void InitOnClientSide(IRemactProtocolDriverToClient toClientInterface)
        {
            _toClientInterface = toClientInterface;
        }

        /// <summary>
        /// Called on service side.
        /// </summary>
        /// <param name="toServiceInterface">Callback interface.</param>
        protected void InitOnServiceSide(IRemactProtocolDriverToService toServiceInterface)
        {
            _toServiceInterface = toServiceInterface;
        }


        #region Send messages

        /// <summary>
        /// Send a message in JsonRpc and MsgPack.
        /// </summary>
        /// <param name="msg">The message.</param>
        /// <param name="context">Alchemy connection context.</param>
        protected void SendMessage(LowerProtocolMessage msg, UserContext context)
        {
            if (msg.Type == RemactMessageType.Error)
            {
                SendError(msg.RequestId, msg.Payload as ErrorMessage, context);
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
                rpc.parameters = msg.Payload;
            }

            var stream = context.DataFrame.CreateInstance();
            stream.IsBinary = true;
            using (var writer = new Newtonsoft.Msgpack.MessagePackWriter(stream))
            {
                var serializer = RemactConfigDefault.Instance.GetSerializer();
                serializer.Serialize(writer, rpc);
                stream.Flush();
                context.Send(stream);
            }
        }

        private void IncomingMessageNotDeserializable(int id, string errorDesc, UserContext context)
        {
            if (_toClientInterface != null && _lowLevelErrorCount > 100)
            {
                return; // on client side do not respond endless on erronous error messages
            }
            _lowLevelErrorCount++;
            var error = new ErrorMessage(ErrorCode.ReqestNotDeserializableOnService, errorDesc);
            SendError(id, error, context);
        }


        /// <summary>
        /// Send an error message in JsonRpc and MsgPack.
        /// </summary>
        /// <param name="requestId">The request id is > 0 in case the error is a response to a request. Otherwise 0.</param>
        /// <param name="payload">The error payload.</param>
        /// <param name="context">Alchemy connection context.</param>
        protected void SendError(int requestId, ErrorMessage payload, UserContext context)
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
                rpc.error.code = (int)payload.ErrorCode;
                rpc.error.message = payload.Message;
            }

            var stream = context.DataFrame.CreateInstance();
            stream.IsBinary = true;
            using (var writer = new Newtonsoft.Msgpack.MessagePackWriter(stream))
            {
                var serializer = RemactConfigDefault.Instance.GetSerializer();
                serializer.Serialize(writer, rpc);
                stream.Flush();
                context.Send(stream);
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
                var serializer = RemactConfigDefault.Instance.GetSerializer();
                using (var reader = new Newtonsoft.Msgpack.MessagePackReader(context.DataFrame))
                {
                    rpc = (JsonRpcV2Message)serializer.Deserialize(reader, typeof(JsonRpcV2Message));
                }

                int.TryParse(rpc.id, out msg.RequestId); // rpc.id == null, RequestId == 0 for notifications

                if (rpc.jsonrpc != "2.0")
                {
                    IncomingMessageNotDeserializable(msg.RequestId, "not supportet json-rpc protocol version", context);
                    return;
                }
                else if (rpc.method != null || rpc.parameters != null)
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
                    msg.Payload = rpc.parameters;
                    // in case the payload is a primitive- or known type, it has already been converted and SerializationPayload.AsDynamic will return null.
                    msg.SerializationPayload = new NewtonsoftJsonPayload(msg.Payload as JToken, serializer);
                }
                else if (rpc.result != null && rpc.id != null)
                {
                    msg.Type = RemactMessageType.Response;
                    msg.Payload = rpc.result;
                    msg.SerializationPayload = new NewtonsoftJsonPayload(msg.Payload as JToken, serializer);
                }
                else if (rpc.error != null)
                {
                    msg.Type = RemactMessageType.Error;
                    if (rpc.error.data != null)
                    {
                        msg.Payload = rpc.error.data;
                    }
                    else
                    {
                        msg.Payload = rpc.error;
                    }
                    // TODO: currently error.code and error.message are unused.
                    msg.SerializationPayload = new NewtonsoftJsonPayload(rpc.error.data as JToken, serializer);
                }
                else
                {
                    IncomingMessageNotDeserializable(msg.RequestId, "not supportet json-rpc message type", context);
                    return;
                }

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
                if (msg.Type != RemactMessageType.Error) IncomingMessageNotDeserializable(msg.RequestId, ex.Message, context);
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