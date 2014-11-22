
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using MsgPack;
using MsgPack.Serialization;

namespace Remact.Net
{
    /// <summary>
    /// The implementation of <see cref="ISerializationPayload"/> for the binary MsgPack serializer, see https://github.com/msgpack/msgpack-cli/wiki.
    /// </summary>
    public class MsgPackPayload : ISerializationPayload
    {
        /// <summary>
        /// Create a serializer message instance.
        /// </summary>
        /// <param name="msgPackObj">The incoming payload converted to JToken.</param>
        public MsgPackPayload (MessagePackObject[] msgPackObj)
        {
            _msgPackObj = msgPackObj;
        }

        private MessagePackObject[] _msgPackObj;

        /// <iheritdoc/>
        public object AsDynamic
        {
            get
            {
                return _msgPackObj;
            }
        }

        object ISerializationPayload.TryReadAs (Type payloadType)
        {
            try
            {
                var serializer = MessagePackSerializer.Get(payloadType);
                //serializer.
                return null; // TODO _msgPackObj[0]..ToObject(payloadType); // deserialized
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Tries to read the payload as the given assemblyQualifiedTypeName.
        /// </summary>
        /// <param name="assemblyQualifiedTypeName">The type to convert the payload to.</param>
        /// <returns>JToken, when the payload could not be converted.</returns>
        public object TryReadAs (string assemblyQualifiedTypeName)
        {
            try
            {
                var type = Type.GetType(assemblyQualifiedTypeName);
                return null; // TODO _msgPackObj.ToObject(type); // deserialized
            }
            catch (Exception ex)
            {
                RaLog.Exception("could not convert payload type '" + assemblyQualifiedTypeName + "'", ex);
            }

            return _msgPackObj;
        }
    }
}

