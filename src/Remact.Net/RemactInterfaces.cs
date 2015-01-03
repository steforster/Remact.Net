
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using Remact.Net.Remote;
using Remact.Net.Protocol;
using System.Reflection;
using Newtonsoft.Json;

namespace Remact.Net
{
    //----------------------------------------------------------------------------------------------
    #region == interface IRemactConfig ==
    /// <summary>
    /// Common definitions for all interacting actors.
    /// Library users may plug in their own implementation of this class to RemactDefaults.Instance.
    /// </summary>
    public interface IRemactConfig : IServiceConfiguration, IClientConfiguration
    {
        //----------------------------------------------------------------------------------------------
        #region == Serialization ==

        /// <summary>
        /// Returns a new serializer usable to write messages.
        /// </summary>
        JsonSerializer GetSerializer();

        #endregion
        //----------------------------------------------------------------------------------------------
        #region == Remact.Catalog configuration ==


        /// <summary>
        /// Normally the Remact.Catalog is running on every host having services. Therefore the default hostname is 'localhost'.
        /// </summary>
        string CatalogHost { get; set; }

        /// <summary>
        /// The Remact.Catalog service listens on this port. The Remact.Catalog must be running on every host having services.
        /// </summary>
        int CatalogPort { get; }

        /// <summary>
        /// The Remact.Catalog service listens on this name.
        /// </summary>
        string CatalogServiceName { get; }


        #endregion
        //----------------------------------------------------------------------------------------------
        #region == Remact partner identification ==

        /// <summary>
        /// The name of this application is used for logging and for identifying a RemactPortClient.
        /// </summary>
        string ApplicationName { get; }

        /// <summary>
        /// The version of this application.
        /// </summary>
        Version ApplicationVersion { get; }

        /// <summary>
        /// The assembly that represents the message payload version.
        /// </summary>
        Assembly CifAssembly { get; }

        /// <summary>
        /// Library users may implement how to get an application instance id.
        /// </summary>
        int ApplicationInstance { get; }

        /// <summary>
        /// Applications with unique id in plant may be moved from one host to another without configuration change.
        /// By default, ApplicationInstance id's below 100 are not unique in plant. 
        /// Library users may change the logic of this property.
        /// </summary>
        bool IsAppIdUniqueInPlant(int appId);

        /// <summary>
        /// When ApplicationInstance is 0, the operating system process id is used for application identification.
        /// </summary>
        bool IsProcessIdUsed(int appId);

        /// <summary>
        /// Common application info: Operating system process id
        /// </summary>
        int ProcessId { get; }

        /// <summary>
        /// The unique AppIdentification for this application instance
        /// </summary>
        string AppIdentification { get; }

        /// <summary>
        /// The AppIdentification is composed from AppName, HostName, AppInstance and processId to for a unique string
        /// </summary>
        string GetAppIdentification(string appName, int appInstance, string hostName, int processId);

        #endregion
        //----------------------------------------------------------------------------------------------
        #region == Remact shutdown ==

        /// <summary>
        /// Has to be called by the user, when the application is shutting down.
        /// </summary>
        void Shutdown();

        #endregion
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == interface IServiceConfiguration ==

    /// <summary>
    /// The configuration interface is implemented by RemactDefaults. It may be provided by the library user.
    /// </summary>
    public interface IServiceConfiguration
    {
        /// <summary>
        /// Sets the service configuration, when no endpoint in app.config is found.
        /// </summary>
        /// <param name="service">The server.</param>
        /// <param name="uri">The dynamically generated URI for this service.</param>
        /// <param name="isCatalog">true if used for Remact.Catalog service.</param>
        /// <returns>The network port manger (for disconnect).</returns>
        WebSocketPortManager DoServiceConfiguration(RemactService service, ref Uri uri, bool isCatalog);
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == interface IClientConfiguration ==

    /// <summary>
    /// The configuration interface is implemented by RemactDefaults. It may be provided by the library user.
    /// </summary>
    public interface IClientConfiguration
    {
        /// <summary>
        /// Sets up the client, when connecting. Must match the ServiceConfiguration of the connected service.
        /// </summary>
        /// <param name="uri">The endpoint URI to connect.</param>
        /// <param name="forCatalog">true if used for Remact.Catalog service.</param>
        /// <returns>The protocol driver including serializer.</returns>
        IRemactProtocolDriverToService DoClientConfiguration(ref Uri uri, bool forCatalog);
    }

    #endregion
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
    /// A connnection consists of a client and a service port.
    /// Messages of any type may flow in both directions.
    /// Messages types are request, response, notification and error.
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
    #region == interface IRemactPortClient ==

    /// <summary>
    /// A public client interface of an actor. It may be called from any thread.
    /// </summary>
    public interface IRemactPortProxy : IRemactPort
    {
        /// <summary>
        /// Link to application-internal partner.
        /// </summary>
        /// <param name="output">a ActorInput</param>
        void LinkToService(IRemactPortService output);

        /// <summary>
        /// Add a RemactClient and lookup the service Uri at Remact.Catalog (catalog uri is defined by RemactConfigDefault).
        /// Remact.Catalog may have synchronized its service register with peer catalogs on other hosts.
        /// </summary>
        /// <param name="serviceName">The unique service name to connect to.</param>
        /// <param name="clientConfig">Plugin your own client configuration instead of RemactDefaults.DoClientConfiguration.</param>
        void LinkOutputToRemoteService(string serviceName, IClientConfiguration clientConfig = null);

        /// <summary>
        /// Add a RemactClient. No lookup at Remact.Catalog is needed as we know the TCP portnumber.
        /// </summary>
        /// <param name="serviceUri">The uri of the remote service.</param>
        /// <param name="clientConfig">Plugin your own client configuration instead of RemactDefaults.DoClientConfiguration.</param>
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
        /// ClientIdent is the identification of the client port that is represented by the RemactPortProxy.
        /// </summary>
        IRemactPort ClientIdent { get; }

        /*/ <summary>
        /// The OutputClientId is used on the connected service to identify this client.
        /// OutputClientId is generated by the service on first connect or service restart.
        /// It remains stable on reconnect or client restart.
        /// </summary>
        int OutputClientId { get; }*/

        /// <summary>
        /// <para>Gets or sets the state of the outgoing connection.</para>
        /// <para>May be called from any thread.</para>
        /// <para>Setting OutputState to PortState.Ok or PortState.Connecting reconnects a previously disconnected link.</para>
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
    /// </summary>
    public interface IRemactPortService : IRemactPort
    {
        /// <summary>
        /// Add a service und publish Uri to Remact.Catalog.
        /// </summary>
        /// <param name="serviceName">The unique name of the service or null, when this partners name is equal to the servicename. </param>
        /// <param name="tcpPort">The TCP port for the service or 0, when automatic port allocation will be used.</param>
        /// <param name="publishToCatalog">True(=default): The servicename will be published to the Remact.Catalog on localhost.</param>
        /// <param name="serviceConfig">Plugin your own service configuration instead of RemactDefaults.DoServiceConfiguration.</param>
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