
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Reflection;           // Assembly
using System.Net;                  // Dns
using System.IO;                   // Files
using Remact.Net.Remote;
using Remact.Net.Protocol;
using Remact.Net.Protocol.Wamp;
using Alchemy;
using Alchemy.Classes;

namespace Remact.Net
{
  /// <summary>
  /// Common definitions for all interacting actors.
  /// Library users may plug in their own implementation of this class to RemactDefault.Instance.
  /// </summary>
  public class RemactConfigDefault : IRemactConfig
  {
    //----------------------------------------------------------------------------------------------
    #region == Instance and plugin ==

    private static IRemactConfig m_instance;

    /// <summary>
    /// Library users may plug in their own implementation of IRemactDefault to RemactDefault.Instance.
    /// </summary>
    public static IRemactConfig Instance
    {
        get{
            if( m_instance == null )
            {
                m_instance = new RemactConfigDefault ();
            }
            return m_instance;
        }
  
        set{
            m_instance = value;
        }
    }


    /// <summary>
    /// When the Library users does not plug in its own implementation of IRemactDefaults, RemactDefaults will be used.
    /// </summary>
    protected RemactConfigDefault() // constructor
    {
        m_appAssembly = Assembly.GetEntryAssembly();// exe Application
        if (m_appAssembly == null)
        {
            m_appAssembly = Assembly.GetCallingAssembly(); // UnitTests
        }

        // static configuration
        Alchemy.Handlers.Handler.FastDirectSendingMode = true;
        CatalogHost = "localhost";
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == Default Service and Client configuration ==

    /// <summary>
    /// The Webservice communication namespace is used by clients and services to uniquely identify services.
    /// Library users may change this constant to e.g. "YourCompany.com/YourProduct".
    /// For use in attrbutes (Microsoft WCF ServiceContract) this is defined as a constant string.
    /// </summary>
    public const string  WsNamespace = "Remact";

    /// <summary>
    /// Configures and sets up a new service for a remotly accessible ActorInput.
    /// Feel free to overwrite this default implementation.
    /// Here we set up a WAMP WebSocket with TCP portsharing.
    /// The 'path' part of the uri addresses the ActorInput.
    /// </summary>
    /// <param name="service">The new service for an ActorInput.</param>
    /// <param name="uri">The dynamically generated URI for this service.</param>
    /// <param name="isCatalog">true if used for Remact.Catalog service.</param>
    /// <returns>The network port manager. It must be called, when the ActorInput is disconnected from network.</returns>
    public virtual WebSocketPortManager DoServiceConfiguration(RemactService service, ref Uri uri, bool isCatalog)
    {
        var portManager = WebSocketPortManager.GetWebSocketPortManager(uri.Port);

        if (portManager.WebSocketServer == null)
        {
            // this TCP port has to be opened
            portManager.WebSocketServer = new WebSocketServer()
                {
                    Port = uri.Port,
                    FlashAccessPolicyEnabled = false,
                    SubProtocols = new string[] { "wamp" },

                    OnConnected = (userContext) => OnClientConnected(portManager, userContext)
                };
        }

        portManager.RegisterService(uri.AbsolutePath, service);
        portManager.WebSocketServer.Start(); // Listen for client connections
        return portManager; // will be called, when this ActorInput is disconnected.
    }

    // Do this for every new client connecting to a WebSocketPort:
    protected virtual void OnClientConnected(WebSocketPortManager portManager, UserContext userContext)
    {
        RemactService service;
        var absolutePath = userContext.RequestPath;
        if (!absolutePath.StartsWith("/"))
        {
            absolutePath = string.Concat('/', absolutePath); // uri.AbsolutPath contains a leading slash.
        }

        if (portManager.TryGetService(absolutePath, out service))
        {
            var svcUser = new RemactServiceUser(service.ServiceIdent);
            var handler = new MultithreadedServiceNet40(service, svcUser);
            // in future, the new WampClientProxy will handle the OnReceive and OnDisconnect events for this connection
            var wampProxy = new WampClientProxy(svcUser.ClientIdent, service.ServiceIdent, handler, userContext);
            svcUser.SetCallbackHandler(wampProxy);
        }
        else
        {
            RaLog.Error("Svc:", "No service found on '" + absolutePath + "' to connect client " + userContext.ClientAddress);
        }
    }

    /// <summary>
    /// Sets the default client configuration, when connecting without app.config.
    /// </summary>
    /// <param name="clientBase">The ClientBase object to modify the endpoint and security credentials.</param>
    /// <param name="uri">The endpoint URI to connect.</param>
    /// <param name="forCatalog">true if used for Remact.Catalog service.</param>
    public virtual void DoClientConfiguration(object clientBase, ref Uri uri, bool forCatalog)
    {
        // TODO !
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == Remact.Catalog configuration ==

    /// <summary>
    /// Normally the Remact.Catalog is running on every host having services. Therefore the default hostname is 'localhost'.
    /// </summary>
    public virtual string   CatalogHost { get; set; }

    /// <summary>
    /// The Remact.Catalog service listens on this port. The Remact.Catalog must be running on every host having services.
    /// </summary>
    public virtual int      CatalogPort {get{ return 40000;}}
    
    /// <summary>
    /// The Remact.Catalog service listens on this name.
    /// </summary>
    public virtual string   CatalogServiceName {get{ return "CatalogService";}}

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == Application identification ==

    /// <summary>
    /// The assembly that represents the application.
    /// </summary>
    protected Assembly m_appAssembly;

    /// <summary>
    /// The assembly that represents the message payload version.
    /// </summary>
    protected Assembly m_cifAssembly;

    /// <summary>
    /// the name of this application is used for tracing and for identifying an ActorOutput
    /// </summary>
    public virtual string  ApplicationName { get { return m_appAssembly.GetName().Name; } }

    /// <summary>
    /// The version of this application is used for information in ActorPort
    /// </summary>
    public virtual Version ApplicationVersion { get { return m_appAssembly.GetName().Version; } }

    /// <summary>
    /// The assembly that represents the message payload version.
    /// </summary>
    public virtual Assembly CifAssembly
    {
        get
        {
            if (m_cifAssembly == null) return m_appAssembly;
            return m_cifAssembly;
        }

        set 
        { 
            m_cifAssembly = value; 
        }
    }

    /// <summary>
    /// Library users may change here how to get an application instance id.
    /// </summary>
    public virtual int     ApplicationInstance {get{return RaLog.ApplicationInstance;}}

    /// <summary>
    /// Library users may change here whether an application instance is unique in plant or on host.
    /// Applications with unique id in plant may be moved from one host to another without configuration change.
    /// </summary>
    public virtual bool    IsAppIdUniqueInPlant (int appId) {return appId >= 100;}

    /// <summary>
    /// When ApplicationInstance is 0, the operating system process id is used for application identification.
    /// </summary>
    public virtual bool    IsProcessIdUsed      (int appId) {return appId == 0;}

    /// <summary>
    /// Operating system process id of this application.
    /// </summary>
    public virtual int     ProcessId
    {
      get
      {
        if (m_ProcId==0) m_ProcId = new System.Diagnostics.TraceEventCache().ProcessId;
        return m_ProcId;
      }
    }
    
    /// <summary>
    /// Operating system process id of this application.
    /// </summary>
    protected int m_ProcId;

    /// <summary>
    /// The unique AppIdentification for this application instance
    /// </summary>
    public virtual string AppIdentification
          {get{return GetAppIdentification(ApplicationName, ApplicationInstance, Dns.GetHostName(), ProcessId);}}

    /// <summary>
    /// The AppIdentification is composed from AppName, HostName, AppInstance and processId to for a unique string
    /// </summary>
    public virtual string GetAppIdentification (string appName, int appInstance, string hostName, int processId)
    {
        if (IsAppIdUniqueInPlant (appInstance))
        {
            return string.Format ("{0}-{1:00#}", appName, appInstance);
        }
        else if (!IsProcessIdUsed (appInstance))
        {
            return string.Format ("{0}-{1:0#} ({2})", appName, appInstance, hostName);
        }
        else
        {
            return string.Format ("{0} ({1}-{2})", appName, hostName, processId);
        }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == Default application startup and commandline arguments ==

    /// <summary>
    /// Library users may change here how to extract the application instance id from commandline arguments.
    /// </summary>
    /// <param name="args">the commandline arguments passed to Main()</param>
    /// <param name="logWriter">null or the plugin to write trace</param>
    public static void ApplicationStart (string[] args, RaLog.ILogPlugin logWriter)
    {
        int appInstance; // by default the first commandline argument
        if (args.Length == 0 || !int.TryParse(args[0], out appInstance))
        {
            appInstance = 0; // use ProcessId
        }

        RaLog.UsePlugin (logWriter);
        RaLog.Start (appInstance);
        RemactApplication.InstallExitHandler();
        RaLog.Run(); // open file and write first messages
    }

    protected string m_LogFolder = null;

    /// <summary>
    /// Get the folder name where log files may be stored. 
    /// </summary>
    public virtual string LogFolder
    {
      get{
        if (m_LogFolder != null) return m_LogFolder;

        string sBase = Path.GetFullPath (Path.GetDirectoryName (RemactApplication.ExecutablePath));
        m_LogFolder = sBase + "/../logs";
        if (Directory.Exists(m_LogFolder)) return m_LogFolder;

        m_LogFolder = Path.GetFullPath(sBase + "/../../logs");
        if (Directory.Exists(m_LogFolder)) return m_LogFolder;

        m_LogFolder = Path.GetFullPath(sBase + "/../../../logs");
        if (Directory.Exists(m_LogFolder)) return m_LogFolder;

        m_LogFolder = Path.GetFullPath(sBase + "/../../../../logs");
        if (Directory.Exists(m_LogFolder)) return m_LogFolder;

        m_LogFolder = Path.GetFullPath(sBase + "/../../../../../logs");
        if (Directory.Exists(m_LogFolder)) return m_LogFolder;

        // store logs beside .exe file, if no other logs path exists
        m_LogFolder = sBase;
        return m_LogFolder;
      }

      set{
        m_LogFolder = value;
      }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == Default application shutdown ==

    /// <summary>
    /// Has to be called by the user, when the application is shutting down.
    /// </summary>
    public virtual void Shutdown()
    {
        ActorPort.DisconnectAll();
        Alchemy.WebSocketClient.Shutdown();
        Alchemy.WebSocketServer.Shutdown();
    }

    #endregion
  }
}

