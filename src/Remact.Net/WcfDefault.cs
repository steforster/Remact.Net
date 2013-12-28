
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.ServiceModel;         // Channels
using System.ServiceModel.Channels;// Binding
using System.Reflection;           // Assembly
using System.Net;                  // Dns
using System.IO;                   // Files
using SourceForge.AsyncWcfLib.Basic;

namespace SourceForge.AsyncWcfLib
{
  /// <summary>
  /// Common definitions for all interacting actors.
  /// Library users may plug in their own implementation of this class to WcfDefault.Instance.
  /// </summary>
  public class WcfDefault : IWcfDefault
  {
    //----------------------------------------------------------------------------------------------
    #region == Instance and plugin ==

    private static IWcfDefault m_instance;

    /// <summary>
    /// Library users may plug in their own implementation of IWcfDefault to WcfDefault.Instance.
    /// </summary>
    public static IWcfDefault Instance
    {
        get{
            if( m_instance == null )
            {
                m_instance = new WcfDefault ();
            }
            return m_instance;
        }
  
        set{
            m_instance = value;
        }
    }


    /// <summary>
    /// When the Library users does not plug in its own implementation of IWcfDefault, WcfDefault will be used.
    /// </summary>
    protected WcfDefault() // constructor
    {
        m_appAssembly = Assembly.GetEntryAssembly();// exe Application
        if (m_appAssembly == null)
        {
            m_appAssembly = Assembly.GetCallingAssembly(); // UnitTests
        }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == Default WCF Service and Client configuration ==

    /// <summary>
    /// The Webservice communication namespace is used by clients and services to uniquely identify services.
    /// Library users may change this constant to e.g. "YourCompany.com/YourProduct" and rebuild AsyncWcsLib.
    /// Sorry, currently it seems there is no other possibility to set your own namespace !
    /// </summary>
    public const string  WsNamespace = "AsyncWcfLib";

    /// <summary>
    /// Returns the default binding, used by services and clients.
    /// URI may be changed e.g. from http:// to https://
    /// </summary>
    protected Binding GetDefaultBinding (ref Uri uri, bool forRouter)
    {
      // (Variant 1) uses BasicHttpBinding, it has no security but is 4 times faster than WS2007HttpBinding
      //             Problem under Windows 7: see "netsh http add uriacl". URI reservation is needed for *each* portnumber under UAC (user account control) without elevated administrator level.
      //BasicHttpBinding binding = new BasicHttpBinding ();
      //if (uri.Scheme != Uri.UriSchemeHttp)
      //{
      //  UriBuilder b = new UriBuilder (uri);
      //  b.Scheme = Uri.UriSchemeHttp;
      //  uri = b.Uri;
      //}

      // (Variant 2) uses HTTPS transport. This is secure between this client and its service.
      //WSHttpBinding binding = new WSHttpBinding ();
      //binding.Security.Mode = SecurityMode.Transport; // HTTPS
      //if (uri.Scheme != Uri.UriSchemeHttps)
      //{
      //  UriBuilder b = new UriBuilder (uri);
      //  b.Scheme = Uri.UriSchemeHttps;
      //  uri = b.Uri;
      //}

      // Add credentials:
      // Service: http://msdn.microsoft.com/en-us/library/ms733130%28v=VS.100%29.aspx
      // Endpoint + EndPointIdentity

      // Client: ServiceReference.ClientCredentials...

      // (Variant 3) uses secure messaging and binary serialization.
      // see. http://msdn.microsoft.com/en-us/library/ms729709%28v=VS.100%29.aspx
      // and  http://msdn.microsoft.com/en-us/library/ms735093%28v=VS.100%29.aspx
      //NetTcpBinding    binding = new NetTcpBinding ();
      //binding.Security.Mode = SecurityMode.Message;
      //binding.Security.Message.ClientCredentialType = MessageCredentialType.Windows;
      
      // (Variant 4) binary serialization.
      NetTcpBinding    binding = new NetTcpBinding ();
      binding.Security.Mode = SecurityMode.None;
      if (uri.Scheme != Uri.UriSchemeNetTcp)
      {
        UriBuilder b = new UriBuilder (uri);
        b.Scheme = Uri.UriSchemeNetTcp;
        uri = b.Uri;
      }
      return binding;
    }

    /// <summary>
    /// Sets the default service configuration, when no endpoint in app.config is found.
    /// </summary>
    /// <param name="serviceHost">The ServiceHost to add the endpoint with security credentials.</param>
    /// <param name="uri">The dynamically generated URI for this service.</param>
    /// <param name="isRouter">true if used for WcfRouter service.</param>
    public virtual void DoServiceConfiguration (ServiceHost serviceHost, ref Uri uri, bool isRouter)
    {
#if !MONO
      serviceHost.AddServiceEndpoint (
             "AsyncWcfLib.ServiceContract", // ConfigurationName needed for .NET 3.5 framework
              GetDefaultBinding (ref uri, isRouter), uri);
#else
      serviceHost.AddServiceEndpoint (
             "SourceForge.AsyncWcfLib.Basic.IWcfBasicContractSync", // Implementation as ConfigurationName-attribute is ignored on monoi
              GetDefaultBinding (ref uri, isRouter), uri);
#endif
    }

    /// <summary>
    /// Sets the default client configuration, when connecting without app.config.
    /// </summary>
    /// <param name="clientBase">The ClientBase object to modify the endpoint and security credentials.</param>
    /// <param name="uri">The endpoint URI to connect.</param>
    /// <param name="forRouter">true if used for WcfRouter service.</param>
    public virtual void DoClientConfiguration (ClientBase<IWcfBasicContractSync> clientBase, ref Uri uri, bool forRouter)
    {
      clientBase.Endpoint.Binding = GetDefaultBinding (ref uri, forRouter);
      clientBase.Endpoint.Address = new EndpointAddress (uri);
      //clientBase.ClientCredentials... http://msdn.microsoft.com/en-us/library/ms732391%28v=VS.100%29.aspx
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == WCF Router configuration ==

    /// <summary>
    /// The WcfRouter service listens on this port. The WcfRouter must be running on every host having services.
    /// </summary>
    public virtual int      RouterPort {get{ return 40000;}}
    
    /// <summary>
    /// The WcfRouter service listens on this name.
    /// </summary>
    public virtual string   RouterServiceName {get{ return "RouterService";}}

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == AsyncWcfLib partner identification ==

    /// <summary>
    /// The assembly that represents the application.
    /// </summary>
    protected Assembly m_appAssembly;

    /// <summary>
    /// the name of this application is used for tracing and for identifying an ActorOutput
    /// </summary>
    public virtual string  ApplicationName { get { return m_appAssembly.GetName().Name; } }

    /// <summary>
    /// The version of this application is used for information in ActorPort
    /// </summary>
    public virtual Version ApplicationVersion { get { return m_appAssembly.GetName().Version; } }

    /// <summary>
    /// Library users may change here how to get an application instance id.
    /// </summary>
    public virtual int     ApplicationInstance {get{return WcfTrc.ApplicationInstance;}}

    /// <summary>
    /// Library users may change here whether an application instance is unique in plant or on host.
    /// Applications with unique id in plant may be moved from one host to another without configuration change.
    /// </summary>
    public virtual bool    IsAppIdUniqueInPlant (int appId) {return appId >= 100;}

    /// <summary>
    /// When ApplicationInstance remains 0, the operating system process id is used as a application instance id for communication and trace.
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
    public static void ApplicationStart (string[] args, WcfTrc.ITracePlugin traceWriter, bool installExitHandler)
    {
      int appInstance = 0;
      try
      {
        if (args.Length > 0) appInstance = Convert.ToInt32 (args[0]); // by default the first commandline argument
      }
      catch{}
      WcfTrc.UsePlugin (traceWriter);
      WcfTrc.Start (appInstance);
      if (installExitHandler) WcfApplication.InstallExitHandler();
      WcfTrc.Run(); // open file and write first messages
    }

    private string m_TraceFolder = null;

    /// <summary>
    /// Get the folder name where tracefiles may be stored. 
    /// </summary>
    public virtual string TraceFolder
    {
      get{
        if (m_TraceFolder != null) return m_TraceFolder;

        string sBase = Path.GetFullPath (Path.GetDirectoryName (WcfApplication.ExecutablePath));
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
  }//class
}// namespace

