
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using Alchemy.Classes;
using MsgPack.Serialization;
using Remact.Net.Remote; // TODO remove (used for service side)
using System.IO;

namespace Remact.Net.Protocol.JsonRpc
{
    /// <summary>
    /// Implements the protocol level for a JSON-RPC client. See http://www.jsonrpc.org/specification.
    /// Uses the MsgPack-cli serializer.
    /// Is used on client as well as on service side.
    /// </summary>
    public class JsonRpcMsgPackDriver : ProtocolDriverClientBase
    {
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
            
            if (msg.RequestId >= 0 && msg.Type != RemactMessageType.Notification)
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
            
            var serializer = MessagePackSerializer.Get<JsonRpcV2Message>();
            var stream = new MemoryStream();
            serializer.Pack( stream, rpc );

            _wsClient.Send(stream.GetBuffer(), (int)stream.Length);
        }

        /// <summary>
        /// Called, when an incoming message cannot be deserialized.
        /// </summary>
        /// <param name="id">The request id is > 0 when it could be read. Otherwise 0.</param>
        /// <param name="errorDesc">A description of the error.</param>
        protected virtual void IncomingMessageNotDeserializable(int id, string errorDesc)
        {
            // overloaded
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
            
            if (requestId >= 0)
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
            
            var serializer = MessagePackSerializer.Get<JsonRpcV2Message>();
            var stream = new MemoryStream();
            serializer.Pack( stream, rpc );

            _wsClient.Send(stream.GetBuffer(), (int)stream.Length);
        }


        #endregion
        #region Receive messages


        // members for service side
        protected IRemactProtocolDriverService _requestHandler;
        protected RemactServiceUser _svcUser;
        protected RemactPortService _serviceIdent;

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
            try
            {
                var serializer = MessagePackSerializer.Get<JsonRpcV2Message>();
                var rpc = serializer.Unpack( context.DataFrame );
                if (!int.TryParse(rpc.id, out msg.RequestId))
                {
                    msg.RequestId = -1;
                }
                
                if (rpc.jsonrpc != "2.0")
                {
                    IncomingMessageNotDeserializable(msg.RequestId, "not supportet json-rpc protocol version");
                    return;
                }
                else if (rpc.method != null || rpc.params1 != null)
                {
                    if (msg.RequestId >= 0)
                    {
                        msg.Type = RemactMessageType.Request;
                    }
                    else
                    {
                        msg.Type = RemactMessageType.Notification;
                    }
                    
                    msg.DestinationMethod = rpc.method;
                    msg.Payload = rpc.params1;
                    // TODO msg.SerializationPayload = new MsgPackPayload(rpc.params1); 
                }
                else if (rpc.result != null && msg.RequestId >= 0)
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
                
                if (_callback != null)
                {
                    _callback.OnMessageFromService(msg); // client side
                }
                else
                {   
                    // TODO msg.SerializationPayload;
                    _requestHandler.MessageFromClient(msg); // service side
                }
            }
            catch (Exception ex)
            {
                if (msg.Type != RemactMessageType.Error) IncomingMessageNotDeserializable(msg.RequestId, ex.Message);
            }
        }

        #endregion
    }
}