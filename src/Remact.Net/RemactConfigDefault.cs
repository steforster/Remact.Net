
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Reflection;           // Assembly
using System.Net;                  // Dns
using Remact.Net.Remote;
using System.IO;

namespace Remact.Net
{
    /// <summary>
    /// Common definitions for all interacting actors.
    /// Library users may plug in their own implementation of this class to RemactDefault.Instance.
    /// This implementation only support process internal connections.
    /// There exists other assemblies with network implementations to plug in:
    /// - JsonProtocolConfig in Remact.Net.Json.Msgpack.Alchemy.dll
    /// - BmsProtocolConfig  in Remact.Net.Bms.Tcp.dll
    /// </summary>
    public class RemactConfigDefault : IRemactConfig
    {
        //----------------------------------------------------------------------------------------------
        #region == Instance and plugin ==

        private static IRemactConfig _instance;

        /// <summary>
        /// Library users may plug in their own implementation of IRemactDefault to RemactDefault.Instance.
        /// </summary>
        public static IRemactConfig Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new RemactConfigDefault();
                }
                return _instance;
            }

            set
            {
                _instance = value;
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
            CatalogHost = "localhost";
        }

        /// <summary>
        /// Loads and executes the EntryPoint default constructor of the specified assembly file.
        /// The assemblyName also specifies the namespace of the EntryPoint class.
        /// </summary>
        /// <param name="assemblyName">Short or long form of the assembly name. E.g. "Remact.Net.Plugin.Bms.Tcp".</param>
        /// <returns>Null, when assembly not found. Otherwise the disposable EntryPoint class. Should be disposed, before the assembly unloads.</returns>
        public static IDisposable LoadPluginAssembly(string assemblyName)
        {
            var dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var fileName = Path.Combine(dllDir, assemblyName);
            if (!File.Exists(fileName))
            {
                return null;
            }
            var an = new AssemblyName(Path.GetFileNameWithoutExtension(fileName));
            an.CodeBase = fileName;
            try
            {
                var assembly = Assembly.Load(an);
                //var assembly = Assembly.LoadFrom(fileName); // for unit tests, load dependant assemblies from this folder
                return (IDisposable)assembly.CreateInstance(an.Name + ".EntryPoint");
            }
            catch (Exception ex)
            {
                RaLog.Info("RemactConfigDefault.LoadPluginAssembly", "cannot load '" + assemblyName + "': " + ex.Message);
            }
            return null;
        }

        /// <summary>
        /// Name of the Remact.Net.Plugin.Bms.Tcp.dll
        /// </summary>
        public const string DefaultProtocolPluginName = "Remact.Net.Plugin.Bms.Tcp.dll";

        /// <summary>
        /// Name of the Remact.Net.Plugin.Json.Msgpack.Alchemy.dll
        /// </summary>
        public const string JsonProtocolPluginName = "Remact.Net.Plugin.Json.Msgpack.Alchemy.dll";


        #endregion
        //----------------------------------------------------------------------------------------------
        #region == Default Service and Client configuration ==

        /// <summary>
        /// Service URIs are constructed from {scheme}/{host}:{tcpPort}/{WsNamespace}/{ServiceName}
        /// Library users may change the WsNamespace to e.g. "YourCompany.com/YourProduct".
        /// </summary>
        public const string WsNamespace = "Remact";

        /// <summary>
        /// Configures and sets up a new service for a remotly accessible RemactPortService.
        /// Feel free to overwrite this default implementation.
        /// </summary>
        /// <param name="service">The new service for an RemactPortService.</param>
        /// <param name="uri">The dynamically generated URI for this service.</param>
        /// <param name="isCatalog">true if used for Remact.Catalog service.</param>
        /// <returns>The network port manager. It must be called, when the RemactPortService is disconnected from network.</returns>
        public virtual INetworkServicePortManager DoServiceConfiguration(RemactService service, ref Uri uri, bool isCatalog)
        {
            throw new NotSupportedException("RemactConfigDefault cannot configure service for remote connection. Use LoadPluginAssembly to load plugin for remote configuration.");
        }

        /// <summary>
        /// Sets the default client configuration, when connecting without app.config.
        /// </summary>
        /// <param name="uri">The endpoint URI to connect.</param>
        /// <param name="forCatalog">true if used for Remact.Catalog service.</param>
        /// <returns>The protocol driver including serializer.</returns>
        public virtual IRemactProtocolDriverToService DoClientConfiguration(ref Uri uri, bool forCatalog)
        {
            throw new NotSupportedException("RemactConfigDefault cannot configure client for remote connection. Use LoadPluginAssembly to load a plugin for remote configuration.");
        }

        #endregion
        //----------------------------------------------------------------------------------------------
        #region == Remact.Catalog configuration ==

        /// <summary>
        /// Normally the Remact.Catalog is running on every host having services. Therefore the default hostname is 'localhost'.
        /// </summary>
        public virtual string CatalogHost { get; set; }

        /// <summary>
        /// The Remact.Catalog service listens on this port. The Remact.Catalog must be running on every host having services.
        /// </summary>
        public virtual int CatalogPort { get { return 40000; } }

        /// <summary>
        /// The Remact.Catalog service listens on this name.
        /// </summary>
        public virtual string CatalogServiceName { get { return "CatalogService"; } }

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
        /// The name of this application is used for logging and for identifying a RemactPortProxy.
        /// </summary>
        public virtual string ApplicationName { get { return m_appAssembly.GetName().Name; } }

        /// <summary>
        /// The version of this application.
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
        /// Library users may implement how to get an application instance id.
        /// </summary>
        public virtual int ApplicationInstance { get { return RaLog.ApplicationInstance; } }

        /// <summary>
        /// Applications with unique id in plant may be moved from one host to another without configuration change.
        /// By default, ApplicationInstance id's below 100 are not unique in plant. 
        /// Library users may change the logic of this property.
        /// </summary>
        public virtual bool IsAppIdUniqueInPlant(int appId) { return appId >= 100; }

        /// <summary>
        /// When ApplicationInstance is 0, the operating system process id is used for application identification.
        /// </summary>
        public virtual bool IsProcessIdUsed(int appId) { return appId == 0; }

        /// <summary>
        /// Operating system process id of this application.
        /// </summary>
        public virtual int ProcessId
        {
            get
            {
                if (m_ProcId == 0) m_ProcId = new System.Diagnostics.TraceEventCache().ProcessId;
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
        { get { return GetAppIdentification(ApplicationName, ApplicationInstance, Dns.GetHostName(), ProcessId); } }

        /// <summary>
        /// The AppIdentification is composed from AppName, HostName, AppInstance and processId to for a unique string
        /// </summary>
        public virtual string GetAppIdentification(string appName, int appInstance, string hostName, int processId)
        {
            if (IsAppIdUniqueInPlant(appInstance))
            {
                return string.Format("{0}-{1:00#}", appName, appInstance);
            }
            else if (!IsProcessIdUsed(appInstance))
            {
                return string.Format("{0}-{1:0#} ({2})", appName, appInstance, hostName);
            }
            else
            {
                return string.Format("{0} ({1}-{2})", appName, hostName, processId);
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
            RemactPort.DisconnectAll();
        }

        #endregion
    }
}

