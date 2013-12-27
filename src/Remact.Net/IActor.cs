
// Copyright (c) 2012  AsyncWcfLib.sourceforge.net

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;// DataContract
using System.ServiceModel;         // ServiceHost
using System.Net;                  // Dns
using System.Threading;            // SynchronizationContext
using SourceForge.AsyncWcfLib.Basic;
#if !BEFORE_NET45
    using System.Threading.Tasks;
#endif

namespace SourceForge.AsyncWcfLib
{
  //----------------------------------------------------------------------------------------------
  #region == enum WcfState ==

  /// <summary>
  /// Communication state for WcfPartners
  /// </summary>
  public enum WcfState
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
    /// AsyncWcfLib is trying to establish a connection.
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
  #region == interface IActorPortId ==

  /// <summary>
  /// The public interface of ActorPort for inputs and outputs.
  /// Actors may have several outgoing connection to other actors. An incoming connection may receive messages from several actors.
  /// </summary>
  public interface IActorPortId
  {
    /// <summary>
    /// Identification in Trace and name of endpoint address in App.config file.
    /// </summary>
    string  Name             {get;}
    
    /// <summary>
    /// IsServiceName=true : 'Name' is unique in the plant, independant of host or application.
    /// IsServiceName=false: For unique identification 'Name' must be combined with HostName, AppName and AppInstance- or ProcessId.
    /// </summary>
    bool    IsServiceName    {get;}

    /// <summary>
    /// Host running the application
    /// </summary>
    string  HostName         {get;}

    /// <summary>
    /// Name of the application running the actor.
    /// </summary>
    string  AppName          {get;}
    
    /// <summary>
    /// Unique instance number of the application (unique in a plant or on a host, depending on WcfDefault.IsAppIdUniqueInPlant).
    /// </summary>
    int     AppInstance      {get;}
    
    /// <summary>
    /// Process id of the application, given by the operating system (unique on a host at a certain time).
    /// </summary>
    int     ProcessId        {get;}
    
    /// <summary>
    /// The AppIdentification is composed from AppName, HostName, AppInstance and processId to for a unique string
    /// </summary>
    string  AppIdentification {get;}

    /// <summary>
    /// Assembly version of the application.
    /// </summary>
    Version AppVersion {get;}
    
    /// <summary>
    /// Assembly name of an important communication interface (message library)
    /// </summary>
    string  CifComponentName {get;}

    /// <summary>
    /// Assembly version of an important communication interface (message library)
    /// </summary>
    Version CifVersion {get;}
    
    /// <summary>
    /// <para>Universal resource identifier for the service or client.</para>
    /// <para>E.g. RouterService: http://localhost:40000/AsyncWcfLib/RouterService</para>
    /// </summary>
    Uri     Uri {get;}   // EndpointAddress is not serializable, URI is set before first send operation

    /// <summary>
    /// <para>To support networks without DNS server, the WcfRouter sends a list of all IP-Adresses of a host.</para>
    /// <para>May be null, as long as no info from WcfRouter has been received.</para>
    /// </summary>
    List<IPAddress> AddressList { get; }

    /// <summary>
    /// Trace or display formatted status info
    /// </summary>
    /// <param name="prefix">Start with this text</param>
    /// <param name="intendCnt">intend the following lines by some spaces</param>
    /// <returns>Formatted communication partner description</returns>
    string  ToString(string prefix, int intendCnt);

    /// <summary>
    /// After a service has no message received for TimeoutSeconds, it may render the connection to this client as disconnected.
    /// 0 means no timeout. 
    /// The client should send at least 2 messages each TimeoutSeconds-period in order to keep the correct connection state on the service.
    /// A Service is trying to notify 2 messages each TimeoutSeconds-period in order to check a dual-Http connection.
    /// </summary>
    int     TimeoutSeconds {get; set;}

    /// <summary>
    /// Multithreaded partners do not use a message input queue. All threads may directly call InputHandler delegates.
    /// Default = false.
    /// </summary>
    bool    IsMultithreaded { get; }

    /// <summary>
    /// Shutdown the outgoing remote connection. Send a disconnect message to the partner.
    /// Close the incoming network connection.
    /// </summary>
    void    Disconnect ();

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
  #region == interface IWcfServiceConfiguration ==

  /// <summary>
  /// The configuration interface is implemented by WcfDefault. It may be provided by the library user.
  /// </summary>
  public interface IWcfServiceConfiguration
  {
      /// <summary>
      /// Sets the service configuration, when no endpoint in app.config is found.
      /// </summary>
      /// <param name="serviceHost">The ServiceHost to add the endpoint with security credentials.</param>
      /// <param name="uri">The dynamically generated URI for this service.</param>
      /// <param name="isRouter">true if used for WcfRouter service.</param>
      void DoServiceConfiguration( ServiceHost serviceHost, ref Uri uri, bool isRouter );
  }

  #endregion
  //----------------------------------------------------------------------------------------------
  #region == interface IWcfClientConfiguration ==

  /// <summary>
  /// The configuration interface is implemented by WcfDefault. It may be provided by the library user.
  /// </summary>
  public interface IWcfClientConfiguration
  {
      /// <summary>
      /// Sets the default client configuration, when connecting without app.config. Must match to ServiceConfiguration of the connected service.
      /// </summary>
      /// <param name="clientBase">The ClientBase object to modify the endpoint and security credentials.</param>
      /// <param name="uri">The endpoint URI to connect.</param>
      /// <param name="forRouter">true if used for WcfRouter service.</param>
      void DoClientConfiguration( ClientBase<IWcfBasicContractSync> clientBase, ref Uri uri, bool forRouter );
  }

  #endregion
  //----------------------------------------------------------------------------------------------
  #region == interface IActorInput ==

  /// <summary>
  /// The public input interface of an actor. It may be called from any thread.
  /// The members of IActorPortId represent the actor receiving the incoming messages.
  /// </summary>
  public interface IActorInput: IActorPortId
  {
    /// <summary>
    /// Add a WCF service und publish Uri to WcfRouter.
    /// </summary>
    /// <param name="serviceName">The unique name of the service or null, when this partners name is equal to the servicename. </param>
    /// <param name="tcpPort">The TCP port for the service or 0, when automatic port allocation will be used.</param>
    /// <param name="publishToRouter">True(=default): The servicename will be published to the WcfRouter on localhost.</param>
    /// <param name="serviceConfig">Plugin your own service configuration instead of WcfDefault.DoServiceConfiguration.</param>
    void LinkInputToNetwork( string serviceName = null, int tcpPort = 0, bool publishToRouter = true, 
                             IWcfServiceConfiguration serviceConfig = null );

    /// <summary>
    /// Threadsafe enqueue message at the receiving partner. No response is expected.
    /// </summary>
    /// <param name="msg">The IWcfMessage to enqueue.</param>
    void PostInput (IWcfMessage msg);

    /// <summary>
    /// Threadsafe enqueue message at the receiving partner.
    /// </summary>
    /// <param name="sender">The source partner sending the message <see cref="ActorPort"/>. Its default message handler will receive the response.</param>
    /// <param name="msg">The IWcfMessage to enqueue.</param>
    void PostInputFrom (ActorOutput sender, IWcfMessage msg);

    /// <summary>
    /// Threadsafe enqueue message at the receiving partner.
    /// </summary>
    /// <param name="sender">The source partner sending the message <see cref="ActorPort"/></param>
    /// <param name="msg">The IWcfMessage to enqueue.</param>
    /// <param name="responseHandler">The lambda expression executed at the source partner, when a response arrives.</param>
    void PostInputFrom(ActorOutput sender, IWcfMessage msg, AsyncResponseHandler responseHandler);

    /// <summary>
    /// <para>Gets or sets the state of the incoming service connection from the network.</para>
    /// <para>May be called from any thread.</para>
    /// <para>Setting InputStateFromNetwork to WcfState.Ok or WcfState.Connecting reconnects a previously disconnected link.</para>
    /// <para>These states may be set only after an initial call to TryConnect from the active services internal thread.</para>
    /// <para>Setting other states will disconnect the WCF service from network.</para>
    /// </summary>
    /// <returns>A <see cref="WcfState"/></returns>
    WcfState InputStateFromNetwork {get; set;}
  };

  #endregion
  //----------------------------------------------------------------------------------------------
  #region == interface IActorOutput ==

  /// <summary>
  /// The public output interface of an actor may be called from any thread.
  /// The members of IActorPortId represent the actor sending the outgoing messages.
  /// </summary>
  public interface IActorOutput: IActorPortId
  {
    /// <summary>
    /// Link to application-internal partner.
    /// </summary>
    /// <param name="output">a ActorInput</param>
    void LinkOutputTo (IActorInput output);

    /// <summary>
    /// Add a WcfClientAsync and lookup the service Uri at WcfRouter.
    /// WcfRouter may have synchronized its service register with peer routers on other hosts.
    /// </summary>
    /// <param name="serviceName">The unique service name to connect to.</param>
    /// <param name="clientConfig">Plugin your own client configuration instead of WcfDefault.DoClientConfiguration.</param>
    void LinkOutputToRemoteService( string serviceName, IWcfClientConfiguration clientConfig = null );

    /// <summary>
    /// Add a WcfClientAsync and lookup the service Uri at WcfRouter.
    /// </summary>
    /// <param name="routerHost">The hostname, where the WcfRouter runs.</param>
    /// <param name="serviceName">The unique service name to connect to.</param>
    /// <param name="clientConfig">Plugin your own client configuration instead of WcfDefault.DoClientConfiguration.</param>
    void LinkOutputToRemoteService( string routerHost, string serviceName, IWcfClientConfiguration clientConfig = null );

    /// <summary>
    /// Add a WcfBasicClientAsync. No lookup at WcfRouter is needed as we know the TCP portnumber.
    /// </summary>
    /// <param name="serviceUri">The uri of the remote service.</param>
    /// <param name="clientConfig">Plugin your own client configuration instead of WcfDefault.DoClientConfiguration.</param>
    void LinkOutputToRemoteService( Uri serviceUri, IWcfClientConfiguration clientConfig = null );

    /// <summary>
    /// The send id given to the last message sent from this sender.
    /// It is used to detect missing messages on the receiving side.
    /// </summary>
    uint LastSentId {get;}

    /// <summary>
    /// The request id given to the last message sent from this client.
    /// The request id is incremented by the client for each request.
    /// The same id is returned in the response from the service.
    /// It is used to detect programming erors leading to request/response mismatch.
    /// </summary>
    uint LastRequestIdSent {get;}

    /// <summary>
    /// The number of requests not yet responded by the service connected to this output.
    /// </summary>
    int OutstandingResponsesCount { get; }

    /// <summary>
    /// OutputSidePartner is the identification of the partner that is linked to our output.
    /// It returns null, as long as we are not linked (OutputState==WcfState.Unlinked).
    /// </summary>
    IActorPortId OutputSidePartner { get; }

    /// <summary>
    /// The OutputClientId is used on the connected service to identify this client.
    /// OutputClientId is generated by the service on first connect or service restart.
    /// It remains stable on reconnect or client restart.
    /// </summary>
    int OutputClientId { get; }

    /// <summary>
    /// <para>Gets or sets the state of the outgoing connection.</para>
    /// <para>May be called from any thread.</para>
    /// <para>Setting OutputState to WcfState.Ok or WcfState.Connecting reconnects a previously disconnected link.</para>
    /// <para>These states may be set only after an initial call to TryConnect from the active services internal thread.</para>
    /// <para>Setting other states will disconnect the WCF client from network.</para>
    /// </summary>
    /// <returns>A <see cref="WcfState"/></returns>
    WcfState OutputState {get; set;}

    /// <summary>
    /// Trace switch: Traces connect/disconnect messages (not to the router), default = true.
    /// </summary>
    bool TraceConnect { get; set; }
  };

  #endregion
}// namespace