
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
        /// Occurs when a client calls a service.
        /// </summary>
        void Request(ActorMessage message);
        
        /// <summary>
        /// Occurs when a client cannot process the servers reply. <see cref="WampMessageType.v1CallError"/>.
        /// </summary>
        void ErrorFromClient(ActorMessage message);
    }
}