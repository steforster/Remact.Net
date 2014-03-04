
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System.Threading.Tasks;

namespace Remact.Net.Contracts
{
    /// <summary>
    /// Remotly callable methods provided by the Remact.Catalog.exe application.
    /// <para>
    /// Note: Method calls returning void are not awaitable for a reply on the client side. These are 'one way' notifications.
    /// Use 'ReadyMessage' as return value to be able to await a reply on client side.
    /// </para>
    /// </summary>
    public interface IRemactCatalog
    {
        /// <summary>
        /// Periodically called when an actor has its input open for remote access.
        /// </summary>
        /// <param name="actorInput">The <see cref="ActorInfo"/> of the opened service (ActorInput).</param>
        Task<ActorMessage<ReadyMessage>> InputIsOpen(ActorInfo actorInput);

        /// <summary>
        /// Called before an actor closes its remotly accessable input.
        /// </summary>
        /// <param name="actorInput">The <see cref="ActorInfo"/> of the closing service (ActorInput).</param>
        Task<ActorMessage<ReadyMessage>> InputIsClosed(ActorInfo actorInput);

        /// <summary>
        /// Called when a client looks up an ActorInput (service) at the catalog.
        /// </summary>
        /// <param name="actorInputName">The name of the ActorInput.
        /// An actor input name must be unique in the plant, independent of host or application.
        /// When several actors have opened inputs with the same name, the lookup returns the actor with the longest running time.
        /// This allows to start 'backup' actors that will come into play, when the longest running actor is shut down 
        /// and the clients try to reconnnect the lost connection.
        /// </param>
        /// <returns>The <see cref="ActorInfo"/> of an opened ActorInput (service). Null, when no such ActorInput is found.</returns>
        Task<ActorMessage<ActorInfo>> LookupInput(string actorInputName);

        /// <summary>
        /// Synchronization request by peer catalog. Incoming ActorInfo with larger hop count should be discarded.
        /// </summary>
        /// <param name="serviceList">The list of services known by the peer catalog.</param>
        /// <returns>The list of services registered in our catalog.</returns>
        Task<ActorMessage<ActorInfoList>> SynchronizeCatalog(ActorInfoList serviceList);
    }
}