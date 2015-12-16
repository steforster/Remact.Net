
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using Remact.Net.Remote;
using Remact.Net.Bms1Serializer;
using Remact.Net.TcpStream;
using System.IO;

namespace Remact.Net.Plugin.Bms.Tcp
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
        /// Send a message in BMS format.
        /// </summary>
        /// <param name="msg">The lower protocol message.</param>
        /// <param name="channel">TCP channel to service or to client.</param>
        protected void SendMessage(LowerProtocolMessage msg, string servicePath, Stream outputStream)
        {
            string knownBaseTypeName;
            var serializer = BmsProtocolConfig.Instance.FindSerializerByObjectType(msg.Payload.GetType(), out knownBaseTypeName);
            _messageSerializer.WriteMessage(outputStream, (writer) => WriteAttributes(msg, servicePath, writer, serializer, knownBaseTypeName));
        }

        private void WriteAttributes(LowerProtocolMessage msg, string servicePath, IBms1Writer writer, Action<IBms1Writer> writeDto, string knownBaseTypeName)
        {
            if (servicePath != null) // needed on first message to service
            {
                writer.Internal.NameValueAttributes.Add("PV=1.0");
                writer.Internal.NameValueAttributes.Add(string.Concat("SID=", servicePath));
            }

            if (msg.Type == RemactMessageType.Request)
            {
                writer.Internal.NameValueAttributes.Add("MT=Q");
            }
            else if (msg.Type == RemactMessageType.Response)
            {
                writer.Internal.NameValueAttributes.Add("MT=R");
            }
            else if (msg.Type == RemactMessageType.Notification)
            {
                writer.Internal.NameValueAttributes.Add("MT=N");
            }
            else // RemactMessageType.Error
            {
                writer.Internal.NameValueAttributes.Add("MT=E");
            }

            if (msg.RequestId > 0 && msg.Type != RemactMessageType.Notification)
            {
                writer.Internal.NameValueAttributes.Add(string.Concat("RID=", msg.RequestId));
            }

            if (msg.DestinationMethod != null)
            {   // deserialize by destination method
                writer.Internal.NameValueAttributes.Add(string.Concat("DM=", msg.DestinationMethod));
            }
            else
            {   // deserialize by object type
                writer.Internal.ObjectType = knownBaseTypeName;
            }

            writeDto(writer);
        }

        private void IncomingMessageNotDeserializable(int id, string errorDesc, Stream outputStream)
        {
            if (_toClientInterface != null && _lowLevelErrorCount > 100)
            {
                return; // on client side do not respond endless on erronous error messages
            }
            _lowLevelErrorCount++;

            var msg = new LowerProtocolMessage
            {
                Type = RemactMessageType.Error,
                RequestId = id,
                Payload = new ErrorMessage(ErrorCode.ReqestNotDeserializableOnService, errorDesc),
            };

            SendMessage(msg, null, outputStream);
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
                if (msg.Type != RemactMessageType.Error) IncomingMessageNotDeserializable(msg.RequestId, ex.Message, channel.OutputStream);
            }
        }

        protected virtual IRemactProtocolDriverToService FirstMessageReceivedOnServiceSide(string protocolVersion, string servicePath)
        {
            throw new InvalidOperationException(); // client side
        }

        // must be overloaded
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