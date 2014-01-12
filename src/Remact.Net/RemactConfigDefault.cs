
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Reflection;           // Assembly
using System.Net;                  // Dns
using System.IO;                   // Files
using Remact.Net.Internal;
using Remact.Net.Protocol;
using Remact.Net.Protocol.Wamp;
using Alchemy;

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
    /// Sets the default service configuration, when no endpoint in app.config is found.
    /// </summary>
    /// <param name="service">The server.</param>
    /// <param name="uri">The dynamically generated URI for this service.</param>
    /// <param name="isRouter">true if used for Remact.Catalog service.</param>
    /// <returns>The protocol driver (for dispose).</returns>
    public IDisposable DoServiceConfiguration(RemactService service, ref Uri uri, bool isRouter)
    {
        var wsServer = new MyWebSocketServer()
        {
            Port = uri.Port,
            FlashAccessPolicyEnabled = false,
            SubProtocols = new string[] { "wamp" },

            // Do this for every new client connection:
            OnConnected = (userContext) =>
                {
                    var svcUser = new RemactServiceUser (service.ServiceIdent);
                    var handler = new InternalMultithreadedServiceNet40(service, svcUser);
                    var wampProxy = new WampClientProxy(svcUser.ClientIdent, service.ServiceIdent, handler, userContext);
                    svcUser.SetCallbackHandler(wampProxy);
                    svcUser.SetConnected();
                }
        };

        // Listen for client connections:
        wsServer.Start();
        return wsServer; // IDisposable
    }

    private class MyWebSocketServer : WebSocketServer, IDisposable
    {
        public new void Dispose()
        {
            Stop();
            base.Dispose();
        }
    }

    /// <summary>
    /// Sets the default client configuration, when connecting without app.config.
    /// </summary>
    /// <param name="clientBase">The ClientBase object to modify the endpoint and security credentials.</param>
    /// <param name="uri">The endpoint URI to connect.</param>
    /// <param name="forRouter">true if used for Remact.Catalog service.</param>
    public virtual void DoClientConfiguration (object clientBase, ref Uri uri, bool forRouter)
    {
        // TODO !
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == Remact.Catalog configuration ==

    /// <summary>
    /// The Remact.Catalog service listens on this port. The Remact.Catalog must be running on every host having services.
    /// </summary>
    public virtual int      RouterPort {get{ return 40000;}}
    
    /// <summary>
    /// The Remact.Catalog service listens on this name.
    /// </summary>
    public virtual string   RouterServiceName {get{ return "RouterService";}}

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
    public virtual int     ApplicationInstance {get{return RaTrc.ApplicationInstance;}}

    /// <summary>
    /// Library users may change here whether an application instance is unique in plant or on host.
    /// Applications with unique id in plant may be moved from one host to another without configuration change.
    /// </summary>
    public virtual bool    IsAppIdUniqueInPlant (int appId) {return appId >= 100;}

    /// <summary>
    /// When ApplicationInstance remains 0, the operating system process id is used as a application instance payload for communication and trace.
    /// </summary>
    public bool            IsProcessIdUsed      (int appId) {return appId == 0;}

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
            return string.Format ("{0}.{1}-{2:0#}", appName, hostName, appInstance);
        }
        else
        {
            return string.Format ("{0}.{1}({2})", appName, hostName, processId);
        }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == Default application startup and commandline arguments ==

    /// <summary>
    /// Library users may change here how to extract the application instance id from commandline arguments.
    /// </summary>
    /// <param name="args">the commandline arguments passed to Main()</param>
    /// <param name="traceWriter">null or the plugin to write trace</param>
    /// <param name="installExitHandler">when true: install handlers for normal and exceptional application exit</param>
    public static void ApplicationStart (string[] args, RaTrc.ITracePlugin traceWriter, bool installExitHandler)
    {
        int appInstance; // by default the first commandline argument
        if (args.Length == 0 || !int.TryParse(args[0], out appInstance))
        {
            appInstance = 0; // use ProcessId
        }

        RaTrc.UsePlugin (traceWriter);
        RaTrc.Start (appInstance);
        if (installExitHandler) RemactApplication.InstallExitHandler();
        RaTrc.Run(); // open file and write first messages
    }

    private string m_TraceFolder = null;

    /// <summary>
    /// Get the folder name where tracefiles may be stored. 
    /// </summary>
    public virtual string TraceFolder
    {
      get{
        if (m_TraceFolder != null) return m_TraceFolder;

        string sBase = Path.GetFullPath (Path.GetDirectoryName (RemactApplication.ExecutablePath));
        m_TraceFolder = sBase+"/../trace";
        if (Directory.Exists(m_TraceFolder)) return m_TraceFolder;

        m_TraceFolder = sBase+"/../../trace";
        if (Directory.Exists(m_TraceFolder)) return m_TraceFolder;

        m_TraceFolder = sBase+"/../../../trace";
        if (Directory.Exists(m_TraceFolder)) return m_TraceFolder;

        // store trace beside .exe file, if no other tracepath exists
        m_TraceFolder = sBase;
        return m_TraceFolder;
      }

      set{
        m_TraceFolder = value;
      }
    }

    #endregion
  }
}

