
// Copyright (c) 2014, github.com/steforster/Remact.Net

namespace Remact.Net.Contracts
{
    /// <summary>
    /// Low level RPC protocol of a WAMP server. See http://wamp.ws/spec/.
    /// </summary>
    public interface IWampRpcV1Server
    {
        /// <summary>
        /// Occurs when a client calls a rpc method. <see cref="WampMessageType.v1Call"/>.
        /// </summary>
        /// <param name="client">The client who sent the message.</param>
        /// <param name="callId">An id of this call. It will be returned in the response.</param>
        /// <param name="procUri">An uri representing the method to call.
        ///                       Format: ActorName/InputName#MethodName</param>
        /// <param name="arguments">The arguments of the method to call. 
        ///                         The types must exactly correspond to the method parameters (no inheritance supported).</param>
        void Call(IActorOutput client, string callId, string procUri, params object[] arguments);
    }

    /// <summary>
    /// Low level RPC protocol of a WAMP client. See http://wamp.ws/spec/.
    /// </summary>
    public interface IWampRpcV1Client
    {
        /// <summary>
        /// Occurs when a call result is returned from server. <see cref="WampMessageType.v1CallResult"/>.
        /// </summary>
        /// <param name="callId">The call id.</param>
        /// <param name="result">The call result. 
        /// The type corresponds to the return type of the called method. Null for void methods.</param>
        void CallResult(string callId, object result);

        /// <summary>
        /// Occurs when a call error is returned from server. <see cref="WampMessageType.v1CallError"/>.
        /// </summary>
        /// <param name="callId">The call id.</param>
        /// <param name="errorUri">An uri to a page describing the error.</param>
        /// <param name="errorDesc">The error description.</param>
        void CallError(string callId, string errorUri, string errorDesc);

        /// <summary>
        /// Occurs when a call error is returned from server. <see cref="WampMessageType.v1CallError"/>.
        /// </summary>
        /// <param name="callId">The call id.</param>
        /// <param name="errorUri">An uri to a page describing the error.</param>
        /// <param name="errorDesc">The error description.</param>
        /// <param name="errorDetails">An object representing error details.</param>
        void CallError(string callId, string errorUri, string errorDesc, object errorDetails);
    }

#pragma warning disable 1591
    /// <summary>
    /// Represents message types defined by the WAMP protocol version1 and 2.
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