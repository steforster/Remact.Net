
// Copyright (c) 2014, github.com/steforster/Remact.Net

namespace Remact.Net
{
    /// <summary>
    /// A general handler for internal and external messages. Called on the correct thread (synchronization context).
    /// </summary>
    /// <param name="msg">the received request or response containing payload and sender.</param>
    public delegate void MessageHandler( RemactMessage msg );

    /// <summary>
    /// A handler for messages to RemactPorts. Called on the correct thread (synchronization context).
    /// </summary>
    /// <param name="msg">the received request or response containing payload and sender.</param>
    /// <param name="senderContext">the local msg.Source.SenderContext object present when the request or response is recived.</param>
    public delegate void MessageHandler<TSC>( RemactMessage msg, TSC senderContext ) where TSC : class;

    /// <summary>
    /// Extension method On implements this delegate for handling messages directly in a Send context.
    /// </summary>
    /// <param name="msg">the received response or errormessage from the connected service</param>
    public delegate RemactMessage AsyncResponseHandler (RemactMessage msg);
}
