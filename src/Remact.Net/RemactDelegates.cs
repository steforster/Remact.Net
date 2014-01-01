
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
#if !BEFORE_NET45
    using System.Threading.Tasks;
#endif

namespace Remact.Net
{
    /// <summary>
    /// A general handler for internal and external messages. Called on the correct thread (synchronization context).
    /// </summary>
    /// <param name="id">the received request or response containing message and sender.</param>
    public delegate void MessageHandler( ActorMessage id ); // TODO remove, legacy

    #if !BEFORE_NET45
    /// <summary>
    /// A handler for internal and external messages. Called on the correct thread (synchronization context).
    /// </summary>
    /// <param name="id">the received request or response containing message and sender.</param>
    /// <param name="dummy">unused parameter to make signature non ambiguous (temporary for VisualStudio 11).</param>
    public delegate Task WcfMessageHandlerAsync( WcfReqIdent id, bool dummy ); // bool dummy to make signature non ambiguous
    #endif

    /// <summary>
    /// A handler for internal and external messages to ActorPort objects. Called on the correct thread (synchronization context).
    /// </summary>
    /// <param name="id">the received request or response containing message and sender.</param>
    /// <param name="senderContext">the local id.Sender.SenderContext object present when the request or response is recived.</param>
    public delegate void MessageHandler<TSC>( ActorMessage id, TSC senderContext ) where TSC : class; // TODO remove, legacy

    #if !BEFORE_NET45
    /// <summary>
    /// A handler for internal and external messages to ActorPort objects. Called on the correct thread (synchronization context).
    /// </summary>
    /// <param name="id">the received request or response containing message and sender.</param>
    /// <param name="senderContext">the local id.Sender.SenderContext object present when the request or response is recived.</param>
    /// <param name="dummy">unused parameter to make signature non ambiguous (temporary for VisualStudio 11).</param>
    public delegate Task WcfMessageHandlerAsync<TSC>( WcfReqIdent id, TSC senderContext, bool dummy ) where TSC : class;
    #endif

    /// <summary>
    /// Extension method On implements this delegate for handling messages directly in a Send context.
    /// <see cref="ActorMessageExtensions.On&lt;T>(ActorMessage,Action&lt;T>)"/>, <see cref="ActorOutput.SendOut(IWcfMessage, AsyncResponseHandler)"/>
    /// </summary>
    /// <param name="id">the received response or errormessage from the connected service</param>
    public delegate ActorMessage AsyncResponseHandler (ActorMessage id);
}
