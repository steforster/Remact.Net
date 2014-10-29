
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Threading;
using System.Collections.Concurrent;
using Alchemy;
using Alchemy.Classes;
using MsgPack.Serialization;
using Remact.Net.Protocol;
using Remact.Net.Remote; // TODO remove (used for service side)
using System.IO;

namespace Remact.Net.Protocol.JsonRpc
{
    /// <summary>
    /// Implements the protocol level for a JSON-RPC client. See http://www.jsonrpc.org/specification.
    /// Uses the MsgPack-cli serializer.
    /// </summary>
    public class JsonRpcMsgPackClientBase : ProtocolDriverClientBase
    {
        #region Send messages


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


        protected virtual void IncomingMessageNotDeserializable(int id, string errorDesc)
        {
            // overloaded
        }
        

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
        
        // message from web socket
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
                
                // TODO same interface on client and service
                if (_callback != null)
                {
                    _callback.OnMessageFromService(msg); // client side
                }
                else
                {   // service side
                    var rm = new RemactMessage(_serviceIdent, msg.DestinationMethod, msg.Payload, msg.Type,
                                               _svcUser.PortClient, _svcUser.ClientId, msg.RequestId);
                    rm.SerializationPayload = null;
                    _requestHandler.MessageFromClient(rm);
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