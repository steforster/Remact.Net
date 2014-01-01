
// Copyright (c) 2014, github.com/steforster/Remact.Net

using Newtonsoft.Json.Linq;

namespace Remact.Net.Protocol
{
    /// <summary>
    /// Low level RPC protocol of a WAMP server. See http://wamp.ws/spec/.
    /// </summary>
    public interface IRemactProtocolDriverService
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
        void Call(IActorOutput client, string callId, string procUri, object[] arguments);
        
        /// <summary>
        /// Occurs when a client cannot process the servers reply. <see cref="WampMessageType.v1CallError"/>.
        /// </summary>
        /// <param name="callId">The call id.</param>
        /// <param name="errorUri">An uri to a page describing the error.</param>
        /// <param name="errorDesc">The error description.</param>
        /// <param name="errorDetails">An object representing error details.</param>
        void CallError(string callId, string errorUri, string errorDesc, object errorDetails = null);
    }

    /// <summary>
    /// Low level RPC protocol of a WAMP client. See http://wamp.ws/spec/.
    /// </summary>
    public interface IRemactProtocolDriverCallbacks
    {
        /// <summary>
        /// Occurs when the WebSocketClient could connect to the server.
        /// </summary>
        /// <param name="response">response is of type 'ActorMessage'. response.Message contains information in case of error.</param>
        void OnOpenCompleted(object response);

        /// <summary>
        /// Occurs when a call result is returned from server. <see cref="WampMessageType.v1CallResult"/>.
        /// </summary>
        /// <param name="callId">The call id.</param>
        /// <param name="result">The result of the call. 
        /// 'result.Type' corresponds to the return type of the called method. result is null for void methods.</param>
        void CallResult(string callId, JToken result);

        /// <summary>
        /// Occurs when a call error is returned from server. <see cref="WampMessageType.v1CallError"/>.
        /// </summary>
        /// <param name="callId">The call id.</param>
        /// <param name="errorUri">An uri to a page describing the error.</param>
        /// <param name="errorDesc">The error description.</param>
        /// <param name="errorDetails">An object representing error details.</param>
        void CallError(string callId, string errorUri, string errorDesc, object errorDetails = null);

        /// <summary>
        /// Occurs when a service sends a notification to the client.
        /// TODO: pub/sub
        /// </summary>
        /// <param name="topic">the subscription topic</param>
        /// <param name="notification">the notified event data</param>
        void Event(string topic, JToken data);
    }
}