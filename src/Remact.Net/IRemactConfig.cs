
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
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
  public interface IRemactConfig : IServiceConfiguration, IClientConfiguration
  {
    //----------------------------------------------------------------------------------------------
    #region == Remact.Catalog configuration ==


    /// <summary>
    /// Normally the Remact.Catalog is running on every host having services. Therefore the default hostname is 'localhost'.
    /// </summary>
    string CatalogHost { get; set; }

    /// <summary>
    /// The Remact.Catalog service listens on this port. The Remact.Catalog must be running on every host having services.
    /// </summary>
    int    CatalogPort {get;}
    
    /// <summary>
    /// The Remact.Catalog service listens on this name.
    /// </summary>
    string CatalogServiceName {get;}
    

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == Remact partner identification ==
    
    /// <summary>
    /// The name of this application is used for logging and for identifying a RemactPortClient.
    /// </summary>
    string  ApplicationName      {get;}

    /// <summary>
    /// The version of this application.
    /// </summary>
    Version ApplicationVersion   {get;}

    /// <summary>
    /// The assembly that represents the message payload version.
    /// </summary>
    Assembly CifAssembly         {get;}

    /// <summary>
    /// Library users may implement how to get an application instance id.
    /// </summary>
    int     ApplicationInstance  {get;}

    /// <summary>
    /// Applications with unique id in plant may be moved from one host to another without configuration change.
    /// By default, ApplicationInstance id's below 100 are not unique in plant. 
    /// Library users may change the logic of this property.
    /// </summary>
    bool    IsAppIdUniqueInPlant (int appId);

    /// <summary>
    /// When ApplicationInstance is 0, the operating system process id is used for application identification.
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
    #region == Logging ==

    /// <summary>
    /// Get the folder name where log files are stored. 
    /// </summary>
    string LogFolder  {get;}

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

