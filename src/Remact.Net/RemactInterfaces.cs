
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using Remact.Net.Remote;
using System.Reflection;
using Newtonsoft.Json;

namespace Remact.Net
{
    //----------------------------------------------------------------------------------------------
    #region == enum PortState ==

    /// <summary>
    /// Communication state for RemactPort.
    /// </summary>
    public enum PortState
    {
        /// <summary>
        /// No link to output or input defined.
        /// </summary>
        Unlinked,

        /// <summary>
        /// Connection established, messages may be sent.
        /// </summary>
        Ok,

        /// <summary>
        /// Remact is trying to establish a connection.
        /// </summary>
        Connecting,

        /// <summary>
        /// Connection is not jet established or has been disconnected.
        /// </summary>
        Disconnected,

        /// <summary>
        /// A timeout or another error has been detected, the connection is currently unusable.
        /// </summary>
        Faulted
    };

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == interface IRemactPort ==

    /// <summary>
    /// IRemactPort is one of the public interface of an actor. It may be called from any thread.
    /// Actors may have several connections to other actors.
    /// A connection connects two IRemactPorts, one is a <see cref="IRemactPortProxy"/> the other is a <see cref="IRemactPortService"/> .
    /// Messages of any <see cref="RemactMessageType"/> may flow in both directions.
    /// RemactMessageTypes are request, response, notification and error.
    /// </summary>
    public interface IRemactPort
    {
        /// <summary>
        /// Identification in logs and name of endpoint address in App.config file.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// IsServiceName=true : 'Name' is unique in the plant, independant of host or application.
        /// IsServiceName=false: For unique identification 'Name' must be combined with HostName, AppName and AppInstance- or ProcessId.
        /// </summary>
        bool IsServiceName { get; }

        /// <summary>
        /// Host running the application
        /// </summary>
        string HostName { get; }

        /// <summary>
        /// Name of the application running the actor.
        /// </summary>
        string AppName { get; }

        /// <summary>
        /// Unique instance number of the application (unique in a plant or on a host, depending on RemactDefaults.IsAppIdUniqueInPlant).
        /// </summary>
        int AppInstance { get; }

        /// <summary>
        /// Process id of the application, given by the operating system (unique on a host at a certain time).
        /// </summary>
        int ProcessId { get; }

        /// <summary>
        /// The AppIdentification is composed from AppName, HostName, AppInstance and processId to for a unique string
        /// </summary>
        string AppIdentification { get; }

        /// <summary>
        /// Assembly version of the application.
        /// </summary>
        Version AppVersion { get; }

        /// <summary>
        /// Assembly name of an important communication interface (message library)
        /// </summary>
        string CifComponentName { get; }

        /// <summary>
        /// Assembly version of an important communication interface (message library)
        /// </summary>
        Version CifVersion { get; }

        /// <summary>
        /// <para>Universal resource identifier for the service or client.</para>
        /// <para>E.g. CatalogService: http://localhost:40000/Remact/CatalogService</para>
        /// </summary>
        Uri Uri { get; }   // EndpointAddress is not serializable, URI is set before first send operation

        /// <summary>
        /// <para>To support networks without DNS server, the Remact.Catalog sends a list of all IP-Addresses of a host.</para>
        /// <para>May be null, as long as no info from Remact.Catalog has been received.</para>
        /// </summary>
        List<string> AddressList { get; }

        /// <summary>
        /// Log or display formatted status info
        /// </summary>
        /// <param name="prefix">Start with this text</param>
        /// <param name="intendCnt">intend the following lines by some spaces</param>
        /// <returns>Formatted communication partner description</returns>
        string ToString(string prefix, int intendCnt);

        /// <summary>
        /// After a service has no message received for TimeoutSeconds, it may render the connection to this client as disconnected.
        /// 0 means no timeout. 
        /// The client should send at least 2 messages each TimeoutSeconds-period in order to keep the correct connection state on the service.
        /// A Service is trying to notify 2 messages each TimeoutSeconds-period in order to check a dual-Http connection.
        /// </summary>
        int TimeoutSeconds { get; set; }

        /// <summary>
        /// Multithreaded partners do not use a message input queue. All threads may directly call InputHandler delegates.
        /// Default = false.
        /// </summary>
        bool IsMultithreaded { get; }

        /// <summary>
        /// IsOpen=true : The input or output is currently open, connected or connecting.
        /// IsOpen=false: The input or output has closed or disconnected.
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// Shutdown the outgoing remote connection. Send a disconnect message to the partner.
        /// Close the incoming network connection.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Trace switch: Traces all sent messages (default = false).
        /// </summary>
        bool TraceSend { get; set; }

        /// <summary>
        /// Trace switch: Traces all received messages (default = false).
        /// </summary>
        bool TraceReceive { get; set; }
    };

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == interface IRemactPortProxy ==

    /// <summary>
    /// A public interface of an actors client-type port. It may be called from any thread.
    /// A IRemactPortProxy represents its linked <see cref="IRemactPortService"/> for the client side.
    /// </summary>
    public interface IRemactPortProxy : IRemactPort
    {
        /// <summary>
        /// Link to application-internal <see cref="IRemactPortService"/>.
        /// </summary>
        /// <param name="service">The application-internal service port.</param>
        void LinkToService(IRemactPortService service);

        /// <summary>
        /// Link the IRemactPortProxy to a remote service port.
        /// When the actor processes <see cref="RemactPortProxy.ConnectAsync"/>, it will lookup the service Uri at Remact.Net.CatalogApp (catalog uri is defined by RemactConfigDefault).
        /// Remact.Net.CatalogApp may have synchronized its service register with peer catalogs on other hosts.
        /// The port is referred as 'Output' because it actively creates the connection. Therefore, the first message is outgoing.
        /// </summary>
        /// <param name="serviceName">The unique service name to connect to.</param>
        /// <param name="clientConfig">Use an individual configuration instead of <see cref="RemactConfigDefault.DoClientConfiguration"/>.</param>
        void LinkOutputToRemoteService(string serviceName, IClientConfiguration clientConfig = null);

        /// <summary>
        /// Link the IRemactPortProxy to a remote service port.
        /// No lookup at Remact.Catalog is needed as we know the TCP portnumber.
        /// The port is referred as 'Output' because it actively creates the connection. Therefore, the first message is outgoing.
        /// </summary>
        /// <param name="serviceUri">The uri of the remote service.</param>
        /// <param name="clientConfig">Use an individual configuration instead of <see cref="RemactConfigDefault.DoClientConfiguration"/>.</param>
        void LinkOutputToRemoteService(Uri serviceUri, IClientConfiguration clientConfig = null);

        /// <summary>
        /// The request id given to the last message sent from this client.
        /// The request id is incremented by the client for each request.
        /// The same id is returned in the response from the service.
        /// </summary>
        int LastRequestIdSent { get; }

        /// <summary>
        /// The number of requests not yet responded by the service connected to this output.
        /// </summary>
        int OutstandingResponsesCount { get; }

        /// <summary>
        /// ClientIdent identifies the client port that is linked to the service represented by this RemactPortProxy.
        /// </summary>
        IRemactPort ClientIdent { get; }

        /// <summary>
        /// <para>Gets or sets the state of the outgoing connection.</para>
        /// <para>May be called from any thread.</para>
        /// <para>Setting OutputState to PortState.Ok or PortState.Connecting reconnects a previously disconnected connection.</para>
        /// <para>These states may be set only after an initial call to ConnectAsync from the active services internal thread.</para>
        /// <para>Setting other states will disconnect the client from network.</para>
        /// </summary>
        /// <returns>A <see cref="PortState"/></returns>
        PortState OutputState { get; set; }

        /// <summary>
        /// Trace switch: Traces connect/disconnect messages (not to the catalog service), default = true.
        /// </summary>
        bool TraceConnect { get; set; }
    };

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == interface IRemactPortService ==

    /// <summary>
    /// A public service interface of an actor. It may be called from any thread.
    /// A IRemactPortService accepts connections from many <see cref="IRemactPortProxy"/> instances.
    /// </summary>
    public interface IRemactPortService : IRemactPort
    {
        /// <summary>
        /// Link the IRemactPortService to a local network TCP port. 
        /// After the actor has successfully processed <see cref="RemactPortService.Open"/>, remote actors will be able to connect to this port.
        /// When LinkInputToNetwork is not called, only local actors can connect.
        /// The port is referred as 'Input' because it listens for incoming connections.
        /// </summary>
        /// <param name="serviceName">A service name must be unique in the plant. Null: do not change the current name.</param>
        /// <param name="tcpPort">The TCP port for the service or 0, when automatic port allocation will be used. Many services can share the same TCP port.</param>
        /// <param name="publishToCatalog">True(=default): When opening this port, the servicename will be published to Remact.Net.CatalogApp on localhost.</param>
        /// <param name="serviceConfig">Use an individual configuration instead of <see cref="RemactConfigDefault.DoServiceConfiguration"/>.</param>
        void LinkInputToNetwork(string serviceName = null, int tcpPort = 0, bool publishToCatalog = true,
                                IServiceConfiguration serviceConfig = null);

        /// <summary>
        /// Anonymous sender: Threadsafe enqueue payload at the receiving partner. No response is expected.
        /// </summary>
        /// <param name="payload">The message payload to enqueue.</param>
        void PostFromAnonymous(object payload);

        /// <summary>
        /// <para>Gets or sets the state of the incoming service connection from the network.</para>
        /// <para>May be called from any thread.</para>
        /// <para>Setting InputStateFromNetwork to PortState.Ok or PortState.Connecting reconnects a previously disconnected link.</para>
        /// <para>These states may be set only after an initial call to ConnectAsync from the active services internal thread.</para>
        /// <para>Setting other states will disconnect the service from network.</para>
        /// </summary>
        /// <returns>A <see cref="PortState"/></returns>
        PortState InputStateFromNetwork { get; set; }
    };

    #endregion
}