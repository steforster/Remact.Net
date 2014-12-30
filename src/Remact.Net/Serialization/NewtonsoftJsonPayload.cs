
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using Newtonsoft.Json.Linq;

namespace Remact.Net
{
    /// <summary>
    /// The implementation of <see cref="ISerializationPayload"/> for the text based Newtonsoft.Json serializer, see https://github.com/JamesNK/Newtonsoft.Json.
    /// </summary>
    public class NewtonsoftJsonPayload : ISerializationPayload
    {
        /// <summary>
        /// Create a serializer message instance.
        /// </summary>
        /// <param name="jToken">The incoming payload converted to JToken.</param>
        public NewtonsoftJsonPayload (JToken jToken)
        {
            _jToken = jToken;
        }

        private JToken _jToken;

        object ISerializationPayload.AsDynamic
        {
            get
            {
                return _jToken;
            }
        }

        object ISerializationPayload.TryReadAs (Type payloadType)
        {
            if (_jToken == null)
            {
                return null;
            }

            try
            {
                return _jToken.ToObject(payloadType); // deserialized
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Tries to read the payload as the given assemblyQualifiedTypeName.
        /// Currently used for the WAMP protocol.
        /// </summary>
        /// <param name="assemblyQualifiedTypeName">The type to convert the payload to.</param>
        /// <returns>JToken, when the payload could not be converted.</returns>
        public object TryReadAs (string assemblyQualifiedTypeName)
        {
            if (_jToken == null)
            {
                return null;
            }

            try
            {
                var type = System.Type.GetType(assemblyQualifiedTypeName);
                return _jToken.ToObject(type); // deserialized
            }
            catch (Exception ex)
            {
                RaLog.Exception("could not convert payload type '" + assemblyQualifiedTypeName + "'", ex);
            }

            return _jToken;
        }
    }
}
