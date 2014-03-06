
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;  // List
using System.Reflection;           // Assembly
using System.Text;                 // StringBuilder
using System.Threading;
using System.Net;                  // IPAddress
using Newtonsoft.Json;
using Remact.Net.Remote;

namespace Remact.Net
{
  /// <summary>
  /// <para>This message payload identifies a communication partner (client or service).</para>
  /// <para>It is used to open and close communication.</para>
  /// </summary>
  public class ActorInfo
  {
    /// <summary>
    /// IsServiceName=true : A service name must be unique in the plant, independant of host or application.
    /// IsServiceName=false: A client  name must be combined with application name, host name, instance- or process id for unique identification.
    /// </summary>
    public bool    IsServiceName;

    /// <summary>
    /// IsOpen=true : The service or client is currently open or connected.
    /// IsOpen=false: The service or client has closed or disconnected.
    /// </summary>
    public bool    IsOpen;

    /// <summary>
    /// Identification in logs and name of endpoint address in App.config file.
    /// </summary>
    public string  Name;
    
    /// <summary>
    /// Unique name of an application or service in the users Contract.Namespace.
    /// </summary>
    public string  AppName;
    
    /// <summary>
    /// Unique instance number of the application (unique in a plant or on a host, depending on RemactDefaults.IsAppIdUniqueInPlant).
    /// </summary>
    public int     AppInstance;
    
    /// <summary>
    /// Process id of the application, given by the operating system (unique on a host at a certain time).
    /// </summary>
    public int     ProcessId;
    
    /// <summary>
    /// Assembly version of the application.
    /// </summary>
    public Version AppVersion;
    
    /// <summary>
    /// Assembly name of an important CifComponent containig some messages
    /// </summary>
    public string  CifComponentName;

    /// <summary>
    /// Assembly version of the important CifComponent
    /// </summary>
    public Version CifVersion;
    
    /// <summary>
    /// Host running the application
    /// </summary>
    public string  HostName;
    
    /// <summary>
    /// <para>Universal resource identifier to reach the input of the service or client.</para>
    /// <para>E.g. CatalogService: http://localhost:40000/Remact/CatalogService</para>
    /// </summary>
    public  Uri  Uri;

    /// <summary>
    /// <para>Identifies the client on the remote service.</para>
    /// <para>Id is created by remote service and transferred to client with this payload.</para>
    /// </summary>
    public int ClientId;

    /// <summary>
    /// <para>To support networks without DNS server, the Remact.Catalog keeps a list of all IP-Addresses of a host.</para>
    /// </summary>
    public List<string> AddressList;

    /// <summary>
    /// After a service has no message received for TimeoutSeconds, it may render the connection to this client as disconnected.
    /// 0 means no timeout. 
    /// The client should send at least 2 messages each TimeoutSeconds-period in order to keep the correct connection state on the service.
    /// A Service is trying to notify 2 messages each TimeoutSeconds-period in order to check a dual-Http connection.
    /// </summary>
    public int TimeoutSeconds;

    /// <summary>
    /// The message from the original service has CatalogHopCount=0. The same message sent from the Remact.Catalog on the local host has CatalogHopCount=1.
    /// Each catalog service increments the hopcount on reception of a message.
    /// A catalog service accepts new data only if the receiving hop count is smaller than the stored.
    /// </summary>
    public int CatalogHopCount;

    /// <summary>
    /// A service having a longer ApplicationRunTime wins the competition when two services with same name are running.
    /// </summary>
    public TimeSpan ApplicationRunTime;

    /// <summary>
    /// The method name prefix used for Remact internal messages.
    /// </summary>
    public const string MethodNamePrefix = "Remact.ActorInfo.";

    /// <summary>
    /// <para>Create a ActorInfo message from a IRemactPort.</para>
    /// </summary>
    /// <param name="p">Copy data from partner p.</param>
    public ActorInfo (IRemactPort p)
    {
      AppName     = p.AppName;
      AppVersion  = p.AppVersion;
      AppInstance = p.AppInstance;
      ProcessId   = p.ProcessId;
      Name        = p.Name;
      IsOpen      = p.IsOpen;
      IsServiceName    = p.IsServiceName;
      CifComponentName = p.CifComponentName;
      CifVersion       = p.CifVersion;
      TimeoutSeconds   = p.TimeoutSeconds;
      HostName    = p.HostName;
      AddressList = p.AddressList;
      Uri         = p.Uri;
      
      CatalogHopCount = 0;
      ApplicationRunTime = DateTime.Now - ms_ApplicationStartTime;
      if (ApplicationRunTime < TimeSpan.FromDays(20))
      {
        // Adjusting of the clock or winter/summmertime switches should not influence the ApplicationRunTime
        // during the first 20 days (1'728'000'000 ms)
        ApplicationRunTime = TimeSpan.FromMilliseconds (Environment.TickCount - ms_ApplicationStartMillis);
      }
    }// CTOR1

    private static int      ms_ApplicationStartMillis = Environment.TickCount;
    private static DateTime ms_ApplicationStartTime   = DateTime.Now;

    
    /// <summary>
    /// <para>Copy a ActorInfo.</para>
    /// </summary>
    /// <param name="p">Copy data from partner p.</param>
    public ActorInfo (ActorInfo p)
    {
      AppName     = p.AppName;
      AppVersion  = p.AppVersion;
      AppInstance = p.AppInstance;
      ProcessId   = p.ProcessId;
      Name        = p.Name;
      IsOpen      = p.IsOpen;
      IsServiceName    = p.IsServiceName;
      CifComponentName = p.CifComponentName;
      CifVersion       = p.CifVersion;
      TimeoutSeconds   = p.TimeoutSeconds;
      HostName         = p.HostName;
      AddressList      = p.AddressList;
      Uri              = p.Uri;
      ClientId         = p.ClientId;
      CatalogHopCount  = p.CatalogHopCount;
      ApplicationRunTime = p.ApplicationRunTime;
    }// CTOR2

    private ActorInfo()
    {
    }// CTOR3 for Json deserialization

    /// <summary>
    /// Check if two communication partner objects represent the same partner
    /// </summary>
    /// <param name="p">second partner</param>
    /// <returns>true if AppName + AppInstance + Client- or ServiceName are equal</returns>
    public bool IsEqualTo (ActorInfo p)
    {
      if (p == null || IsServiceName != p.IsServiceName) return false;

      if (IsServiceName)
      {
        return Name.Equals (p.Name); // a service may be moved to another host or another application
      }
      else if (RemactConfigDefault.Instance.IsAppIdUniqueInPlant (AppInstance))
      { // plant unique client
        return AppInstance == p.AppInstance
            && Name.Equals (p.Name)
            && AppName.Equals (p.AppName);  // these clients may be moved to another host
      }
      else
      { // host unique client
        return AppInstance ==  p.AppInstance
            &&(!RemactConfigDefault.Instance.IsProcessIdUsed (AppInstance) || ProcessId == p.ProcessId) // process id is valid for a running client only
            && HostName.Equals (p.HostName)
            && Name.Equals (p.Name)
            && AppName.Equals (p.AppName);  // these clients may not be moved
      }
    }

    /// <summary>
    /// Check if two communication partner objects represent the same partner
    /// </summary>
    /// <param name="p">second partner</param>
    /// <returns>true if AppName + AppInstance + Client- or ServiceName are equal</returns>
    public bool IsEqualTo (IRemactPort p)
    {
      if (p == null || IsServiceName != p.IsServiceName) return false;

      if (IsServiceName)
      {
        return Name.Equals (p.Name); // a service may be moved to another host or another application
      }
      else if (RemactConfigDefault.Instance.IsAppIdUniqueInPlant (AppInstance))
      { // plant unique client
        return AppInstance == p.AppInstance
            && Name.Equals (p.Name)
            && AppName.Equals (p.AppName);  // these clients may be moved to another host
      }
      else
      { // host unique client
        return AppInstance ==  p.AppInstance
            &&(!RemactConfigDefault.Instance.IsProcessIdUsed (AppInstance) || ProcessId == p.ProcessId) // process id is valid for a running client only
            && HostName.Equals (p.HostName)
            && Name.Equals (p.Name)
            && AppName.Equals (p.AppName);  // these clients may not be moved
      }
    }


    /// <summary>
    /// Creates string representation of ActorInfo.
    /// </summary>
    /// <returns>String representation of this message.</returns>
    public override string ToString ()
    {
      
      string name;
      if (Uri != null)
      {
          name = Uri.ToString();
      }
      else if (IsServiceName)
      {
          name = Name;
      }
      else
      {
          name = RemactConfigDefault.Instance.GetAppIdentification(AppName, AppInstance, HostName, ProcessId) + "/" + Name;
      }

      return String.Format("ActorInfo of {0} '{1}'", IsOpen ? "open":"closed", name);
    }
  }

  //----------------------------------------------------------------------------------------------
  /// <summary>
  /// <para>This message payload contains a list of ActorInfo payloads.</para>
  /// <para>It is used by the catalogs to exchange informations.</para>
  /// </summary>
  public class ActorInfoList
  {
      /// <summary>
      /// List of services in a plant.
      /// </summary>
      public List<ActorInfo> Item;

      /// <summary>
      /// Create a ActorInfoList.
      /// </summary>
      public ActorInfoList()
      {
          Item = new List<ActorInfo>(20);
      }
  }
}
