
// Copyright (c) 2014, github.com/steforster/Remact.Net

namespace Remact.Net.Contracts
{
    /// <summary>
    /// Remotly callable methods provided by the Remact.Catalog.exe application.
    /// </summary>
    public interface IRemactCatalog
    {
        /// <summary>
        /// Called when an actor opens its input for remote access.
        /// </summary>
        /// <param name="actorInput">The <see cref="ActorMessage"/> of the opened service (ActorInput).</param>
        void Opened(ActorMessage actorInput);

        /// <summary>
        /// Called before an actor closes its remotly accessable input.
        /// </summary>
        /// <param name="actorInput">The <see cref="ActorMessage"/> of the closing service (ActorInput).</param>
        void Closing(ActorMessage actorInput);

        /// <summary>
        /// Called when a client looks up an ActorInput (service).
        /// </summary>
        /// <param name="actorInputName">The name of the ActorInput.
        /// An actor input name must be unique in the plant, independant of host or application.
        /// When several actors have opened inputs with the same name, the lookup returns the actor with the longest running time.
        /// This allows to start 'backup' actors that will come into play, when the longest running actor is shut down 
        /// and the clients try to reconnnect the lost connection.
        /// </param>
        /// <returns>The <see cref="ActorMessage"/> of an opened ActorInput (service). Null, when no such ActorInput is found.</returns>
        ActorMessage Lookup(string actorInputName);
    }
}