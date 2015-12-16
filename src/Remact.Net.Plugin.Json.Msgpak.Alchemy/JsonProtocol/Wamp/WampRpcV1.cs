
// Copyright (c) https://github.com/steforster/Remact.Net

namespace Remact.Net.Plugin.Json.Msgpack.Alchemy
{
#pragma warning disable 1591
    /// <summary>
    /// Represents message types defined by the WAMP protocol version 1 and 2. See http://wamp.ws/spec/.
    /// </summary>
    public enum WampMessageType
    {
        v1Welcome = 0,
        v1Prefix = 1,
        v1Call = 2,
        v1CallResult = 3,
        v1CallError = 4,
        v1Subscribe = 5,
        v1Unsubscribe = 6,
        v1Publish = 7,
        v1Event = 8,


        v2Hello = 0,
        v2Heartbeat = 1,
        v2Goodbye = 2,

        v2Call = 16 + 0,
        v2CallCancel = 16 + 1,
        v2CallResult = 32 + 0,
        v2CallProgress = 32 + 1,
        v2CallError = 32 + 2,

        v2Subscribe = 64 + 0,
        v2Unsubscribe = 64 + 1,
        v2Publish = 64 + 2,
        v2Event = 128 + 0,
        v2Metaevent = 128 + 1,
        v2PublishAck = 128 + 2,
    }
#pragma warning restore 1591
}