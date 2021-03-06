﻿
// Copyright (c) https://github.com/steforster/Remact.Net

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
        /// Periodically called when a RemactPortServive is open for remote access.
        /// </summary>
        /// <param name="actorInput">The <see cref="ActorInfo"/> of the opened service (RemactPortService).</param>
        Task<RemactMessage<ReadyMessage>> ServiceOpened(ActorInfo actorInput);

        /// <summary>
        /// Called before a RemactPortServive closes its remotly accessable input.
        /// </summary>
        /// <param name="actorInput">The <see cref="ActorInfo"/> of the closing service (RemactPortService).</param>
        Task<RemactMessage<ReadyMessage>> ServiceClosed(ActorInfo actorInput);

        /// <summary>
        /// Called when a client looks up a remotly accessible RemactPortService at the catalog.
        /// </summary>
        /// <param name="serviceName">The name of the RemactPortService.
        /// An actor input name must be unique in the plant, independent of host or application.
        /// When several actors have opened inputs with the same name, the lookup returns the actor with the longest running time.
        /// This allows to start 'backup' actors that will come into play, when the longest running actor is shut down 
        /// and the clients try to reconnnect the lost connection.
        /// </param>
        /// <returns>The <see cref="ActorInfo"/> of an opened RemactPortService. Null, when no such service is found.</returns>
        Task<RemactMessage<ActorInfo>> LookupService(string serviceName);

        /// <summary>
        /// Synchronization request by peer catalog. Incoming ActorInfo with larger hop count should be discarded.
        /// </summary>
        /// <param name="serviceList">The list of services known by the peer catalog.</param>
        /// <returns>The list of services registered in our catalog.</returns>
        Task<RemactMessage<ActorInfoList>> SynchronizeCatalog(ActorInfoList serviceList);
    }
}