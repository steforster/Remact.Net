
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using Remact.Net.Remote;
using Remact.Net.Bms1Serializer;
using Remact.Net.TcpStream;

namespace Remact.Net.Bms.Tcp
{
    /// <summary>
    /// Implements the protocol level for a JSON-RPC client. See http://www.jsonrpc.org/specification.
    /// Uses the MsgPack-cli serializer.
    /// Is used on client as well as on service side.
    /// </summary>
    public class BmsProtocolDriver : IDisposable
    {
        private int _lowLevelErrorCount;
        private IRemactProtocolDriverToClient _toClientInterface; // not null on client side
        private IRemactProtocolDriverToService _toServiceInterface;// not null on service side
        private Bms1MessageSerializer _messageSerializer;

        /// <summary>
        /// Called on client side.
        /// </summary>
        /// <param name="toClientInterface">Callback interface.</param>
        protected void InitOnClientSide(IRemactProtocolDriverToClient toClientInterface)
        {
            _toClientInterface = toClientInterface;
            _messageSerializer = new Bms1MessageSerializer();
        }

        /// <summary>
        /// Called on service side.
        /// </summary>
        protected void InitOnServiceSide()
        {
            _messageSerializer = new Bms1MessageSerializer();
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
                var serializer = JsonProtocolConfig.Instance.GetSerializer();
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
                var serializer = JsonProtocolConfig.Instance.GetSerializer();
                serializer.Serialize(writer, rpc);
                stream.Flush();
                context.Send(stream);
            }
        }


        #endregion
        #region Receive messages


        /// <summary>
        /// Do this for every message from a TCP socket (client or service side).
        /// </summary>
        /// <param name="channel">The client channel.</param>
        public void OnMessageReceived(TcpStreamChannel channel)
        {
            if (_disposed)
            {
                return;
            }

            var msg = new LowerProtocolMessage();
            string protocolVersion = null;
            string servicePath = null;
            string messageType = null;
            string requestId = null;

            try
            {
                var attributes = _messageSerializer.ReadMessageStart(channel.InputStream);
                if (attributes.KeyValueAttributes != null)
                {
                    foreach (var item in attributes.KeyValueAttributes)
                    {
                        switch (item.Key)
                        {
                            case "PV":  protocolVersion = item.Value; break; // needed on first message to service
                            case "SID": servicePath = item.Value; break;     // needed on first message to service
                            case "MT":  messageType = item.Value; break;     // needed on any message
                            case "RID": requestId = item.Value; break;       // needed on requests and responses
                            case "DM":  msg.DestinationMethod = item.Value; break; // optional, when no attributes.ObjectType or BlockTypeId is sent
                            default: break;
                        };
                    }
                }

                if (messageType == "Q")
                {
                    msg.Type = RemactMessageType.Request;
                    int.TryParse(requestId, out msg.RequestId);
                }
                else if (messageType == "R")
                {
                    msg.Type = RemactMessageType.Response;
                    int.TryParse(requestId, out msg.RequestId);
                }
                else if (messageType == "N")
                {
                    msg.Type = RemactMessageType.Notification;
                }
                else // E
                {
                    msg.Type = RemactMessageType.Error;
                }

                if (_toClientInterface == null && _toServiceInterface == null)
                {
                    _toServiceInterface = FirstMessageReceivedOnServiceSide(protocolVersion, servicePath);
                }

                Func<IBms1Reader, object> deserializer;
                if (attributes.ObjectType != null)
                {
                    deserializer = BmsProtocolConfig.Instance.FindDeserializerByObjectType(attributes.ObjectType);
                }
                else if (msg.Type == RemactMessageType.Error)
                {
                    deserializer = BmsProtocolConfig.Instance.FindDeserializerByObjectType("Remact.Net.ErrorMessage");
                }
                else if (msg.DestinationMethod != null)
                {
                    deserializer = FindDeserializerByDestination(msg.DestinationMethod);
                }
                else
                {
                    throw new InvalidOperationException("Cannot deserialize message. No object type or destination method specified.");
                }

                msg.Payload = _messageSerializer.ReadMessage<object>(deserializer);

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

        protected virtual IRemactProtocolDriverToService FirstMessageReceivedOnServiceSide(string protocolVersion, string servicePath)
        {
            throw new InvalidOperationException(); // client side
        }

        protected virtual Func<IBms1Reader, object> FindDeserializerByDestination(string destinationMethod)
        {
            throw new InvalidOperationException();
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