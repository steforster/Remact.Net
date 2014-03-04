
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System.Threading.Tasks;
namespace Remact.Net.Contracts
{
    /// <summary>
    /// Common, remotly callable methods provided by all Remact ActorInputs (services).
    /// </summary>
    internal interface IRemactService
    {
        /// <summary>
        /// Called when a client connects to a service.
        /// The sent destination method name actually is "Remact.ActorInfo.ClientConnectRequest".
        /// </summary>
        /// <param name="actorOutput">The <see cref="ActorInfo"/> of the client. Usage = ClientConnectRequest.</param>
        /// <returns>The <see cref="ActorInfo"/> of the service (ActorInput). Usage = ServiceConnectResponse.</returns>
        Task<ActorMessage<ActorInfo>> Remact_ActorInfo_ClientConnectRequest(ActorInfo actorOutput);

        /// <summary>
        /// Called when a client gracefully disconnects from a service. No reply is expected.
        /// The sent destination method name actually is "Remact.ActorInfo.ClientDisconnectNotification".
        /// </summary>
        /// <param name="actorOutput">The <see cref="ActorInfo"/> of the client. Usage = ClientDisconnectNotification.</param>
        void Remact_ActorInfo_ClientDisconnectNotification(ActorInfo actorOutput);
    }
}