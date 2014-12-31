
// Copyright (c) https://github.com/steforster/Remact.Net

using Remact.Net.Remote;

namespace Remact.Net
{
    #region == class RemactPortClient ==

    /// <summary>
    /// <para>This class represents a communication partner (client).</para>
    /// <para>It can be asked to send a request to the actor owning this client port.</para>
    /// </summary>
    internal class RemactPortClient : RemactPort
    {
        /// <summary>
        /// <para>Creates a client identification for an actor port.</para>
        /// </summary>
        /// <param name="name">The name of this client port.</param>
        public RemactPortClient(string name)
            : base(name)
        {
            IsMultithreaded = true;
        }


        /// <summary>
        /// <para>Creates a client stub, used internally by a service.</para>
        /// </summary>
        internal RemactPortClient()
            : base()
        {
            IsMultithreaded = true;
        }


        internal object SenderCtx;           // TSC created by the connected RemactPortService<TSC>
        internal RemactPort ServiceIdent;    // RemactPortProxy, RemactPortService
        internal RemactServiceUser SvcUser;  // used by RemactService

        /// <summary>
        /// The ClientId used on the connected service to identify this client.
        /// ClientId is generated by the service on first connect or service restart.
        /// It remains stable on reconnect or client restart.
        /// </summary>
        public int ClientId { get; internal set; }

        // must be overridden to be able to send messages from the concrete port type.
        internal override RemactMessage NewMessage(string method, object payload, RemactMessageType messageType, AsyncResponseHandler responseHandler)
        {
            return new RemactMessage(this, method, payload, messageType, responseHandler);
        }

    }// class RemactPort

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == class RemactPortClient<TOC> ==

    /// <summary>
    /// <para>This class represents an outgoing (client) connection to an actor (service).</para>
    /// <para>It is the destination of responses and contains additional data representing the session and the remote service.</para>
    /// </summary>
    /// <typeparam name="TOC">Additional data (output context) representing the communication session and the remote service.</typeparam>
    internal class RemactPortClient<TOC> : RemactPortClient where TOC : class
    {
        /// <summary>
        /// <para>OutputContext is an object of type TOC defined by the application.</para>
        /// <para>The OutputContext is not sent over the network.</para>
        /// <para>The OutputContext remains untouched by the library. The application may initialize and use it.</para>
        /// </summary>
        public TOC OutputContext
        {
            get
            {
                //if (m_outputCtx == null) m_outputCtx = new TOC();
                return m_outputCtx;
            }

            set
            {
                m_outputCtx = value;
            }
        }

        private TOC m_outputCtx;
        private MessageHandler<TOC> m_defaultTocResponseHandler;


        /// <summary>
        /// <para>Creates an output port for an actor.</para>
        /// </summary>
        /// <param name="name">The application internal name of this output port.</param>
        /// <param name="defaultTocResponseHandler">The method to be called when a response is received and no other handler is applicatable. May be null.</param>
        public RemactPortClient(string name, MessageHandler<TOC> defaultTocResponseHandler = null)
            : base(name)
        {
            DefaultInputHandler = OnDefaultInput;
            m_defaultTocResponseHandler = defaultTocResponseHandler;
        }


        /// <summary>
        /// Message is passed to users default handler.
        /// </summary>
        /// <param name="msg">RemactMessage containing Payload and Source.</param>
        private void OnDefaultInput(RemactMessage msg)
        {
            if (m_defaultTocResponseHandler != null)
            {
                m_defaultTocResponseHandler(msg, OutputContext); // MessageHandler<TOC> delegate
            }
            else
            {
                RaLog.Error("Remact", "Unhandled response: " + msg.Payload, Logger);
            }
        }

    }// class RemactPortClient<TOC>
    #endregion
}