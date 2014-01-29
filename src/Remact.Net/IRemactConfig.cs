
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.ServiceModel;         // Channels
using System.ServiceModel.Channels;// Binding
using System.Reflection;           // Assembly
using System.Net;                  // Dns
using System.IO;                   // Files
using Remact.Net.Remote;

namespace Remact.Net
{
  /// <summary>
  /// Common definitions for all interacting actors.
  /// Library users may plug in their own implementation of this class to RemactDefaults.Instance.
  /// </summary>
  public interface IRemactConfig : IActorInputConfiguration, IActorOutputConfiguration
  {
    //----------------------------------------------------------------------------------------------
    #region == Remact.Catalog configuration ==

    /// <summary>
    /// The Remact.Catalog service listens on this port. The Remact.Catalog must be running on every host having services.
    /// </summary>
    int      CatalogPort {get;}
    
    /// <summary>
    /// The Remact.Catalog service listens on this name.
    /// </summary>
    string   CatalogServiceName {get;}
    

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == Remact partner identification ==
    
    /// <summary>
    /// the name of this application is used for tracing and for identifying an ActorOutput
    /// </summary>
    string  ApplicationName      {get;}

    /// <summary>
    /// The version of this application is used for information in ActorPort
    /// </summary>
    Version ApplicationVersion   {get;}

    /// <summary>
    /// The assembly that represents the message payload version.
    /// </summary>
    Assembly CifAssembly         {get;}

    /// <summary>
    /// Library users may change here how to get an application instance id.
    /// </summary>
    int     ApplicationInstance  {get;}

    /// <summary>
    /// Library users may change here whether an application instance is unique in plant or on host.
    /// Applications with unique id in plant may be moved from one host to another without configuration change.
    /// </summary>
    bool    IsAppIdUniqueInPlant (int appId);

    /// <summary>
    /// When ApplicationInstance remains 0, the operating system process id is used as a application instance id for communication and trace.
    /// </summary>
    bool    IsProcessIdUsed      (int appId);

    /// <summary>
    /// Common application info: Operating system process id
    /// </summary>
    int     ProcessId {get;}

    /// <summary>
    /// The unique AppIdentification for this application instance
    /// </summary>
    string  AppIdentification  {get;}

    /// <summary>
    /// The AppIdentification is composed from AppName, HostName, AppInstance and processId to for a unique string
    /// </summary>
    string  GetAppIdentification (string appName, int appInstance, string hostName, int processId);

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == Tracing ==

    /// <summary>
    /// Get the folder name where tracefiles may be stored. 
    /// </summary>
    string TraceFolder  {get;}

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == Remact shutdown ==

    /// <summary>
    /// Has to be called by the user, when the application is shutting down.
    /// </summary>
    void Shutdown();

    #endregion
  }
}

