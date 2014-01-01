
// Copyright (c) 2014, github.com/steforster/Remact.Net

namespace Remact.Net.Contracts
{
    /// <summary>
    /// Remotly callable methods provided by the Remact.Catalog.exe application.
    /// <para>
    /// Note: Method calls returning void are not awaitable for a reply on the client side. These are 'one way' requests.
    /// Use 'ReadyMessage' as return value to be able to await a reply on client side.
    /// </para>
    /// </summary>
    public interface IRemactCatalog
    {
        /// <summary>
        /// Called when an actor opens its input for remote access.
        /// </summary>
        /// <param name="actorInput">The <see cref="ActorInfo"/> of the opened service (ActorInput).</param>
        ReadyMessage OpenedInput(ActorInfo actorInput);

        /// <summary>
        /// Called before an actor closes its remotly accessable input.
        /// </summary>
        /// <param name="actorInput">The <see cref="ActorInfo"/> of the closing service (ActorInput).</param>
        ReadyMessage ClosingInput(ActorInfo actorInput);

        /// <summary>
        /// Called when a client looks up an ActorInput (service).
        /// </summary>
        /// <param name="actorInputName">The name of the ActorInput.
        /// An actor input name must be unique in the plant, independant of host or application.
        /// When several actors have opened inputs with the same name, the lookup returns the actor with the longest running time.
        /// This allows to start 'backup' actors that will come into play, when the longest running actor is shut down 
        /// and the clients try to reconnnect the lost connection.
        /// </param>
        /// <returns>The <see cref="ActorInfo"/> of an opened ActorInput (service). Null, when no such ActorInput is found.</returns>
        ActorInfo LookupInput(string actorInputName);
    }
}