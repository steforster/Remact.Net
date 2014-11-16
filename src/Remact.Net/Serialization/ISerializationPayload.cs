
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;

namespace Remact.Net
{
    /// <summary>
    /// The interface abstracts the serializer used for the incoming payload.
    /// </summary>
    public interface ISerializationPayload
    {
        /// <summary>
        /// Gets the payload as a dynamic type native to the serializer used.
        /// </summary>
        /// <returns>Null, when there is no payload.</returns>
        object AsDynamic {get;}

        /// <summary>
        /// Tries the read the payload as a given type.
        /// </summary>
        /// <param name="payloadType">The type to convert the payload to.</param>
        /// <returns>Null, when the payload could not be converted or is null.</returns>
        object TryReadAs (Type payloadType);
    }
}

