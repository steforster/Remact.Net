
// Copyright (c) 2014, github.com/steforster/Remact.Net

namespace Remact.Net.Contracts
{
    /// <summary>
    /// Common, remotly callable methods provided by all Remact services (ActorInput).
    /// </summary>
    public interface IRemactService
    {
        /// <summary>
        /// Called when a client connects to a service.
        /// </summary>
        /// <param name="client">The <see cref="ActorMessage"/> of the client (ActorOutput).</param>
        /// <returns>The <see cref="ActorMessage"/> of the service (ActorInput).</returns>
        ActorMessage Connect(ActorMessage client);

        /// <summary>
        /// Called when a client gracefully disconnects from a service.
        /// </summary>
        /// <param name="client">The <see cref="ActorMessage"/> of the client (ActorOutput).</param>
        /// <returns>The <see cref="ActorMessage"/> of the service (ActorInput).</returns>
        ActorMessage Disconnect(ActorMessage client);
    }
}