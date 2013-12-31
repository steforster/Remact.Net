
// Copyright (c) 2014, github.com/steforster/Remact.Net

namespace Remact.Net.Contracts
{
    /// <summary>
    /// Common, remotly callable methods provided by all Remact ActorInputs (services).
    /// <para>
    /// Note: Method calls returning void are not awaitable for a reply on the client side. These are 'one way' requests.
    /// Use 'ReadyMessage' as return value to be able to await a reply on client side.
    /// </para>
    /// </summary>
    public interface IRemactService
    {
        /// <summary>
        /// Called when a client connects to a service.
        /// </summary>
        /// <param name="actorOutput">The <see cref="ActorMessage"/> of the client.</param>
        /// <returns>The <see cref="ActorMessage"/> of the service (ActorInput).</returns>
        ActorMessage ConnectOutput(ActorMessage actorOutput);

        /// <summary>
        /// Called when a client gracefully disconnects from a service.
        /// </summary>
        /// <param name="actorOutput">The <see cref="ActorMessage"/> of the client.</param>
        /// <returns>The <see cref="ActorMessage"/> of the service (ActorInput).</returns>
        ActorMessage DisconnectOutput(ActorMessage actorOutput);
    }
}