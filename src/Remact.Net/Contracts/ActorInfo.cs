
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
    /// Identification in Trace and name of endpoint address in App.config file.
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
    public  Uri    Uri;

    /// <summary>
    /// <para>Identifies the client on the remote service.</para>
    /// <para>Id is created by remote service and transferred to client with this payload.</para>
    /// </summary>
    public int ClientId;

    /// <summary>
    /// <para>To support networks without DNS server, the Remact.Catalog keeps a list of all IP-Adresses of a host.</para>
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
    /// The message may be used for several purposes.
    /// In Json, 'Usage' is streamed as int. We do not stream this property but stream the z_usage instead.
    /// Then we protect us from usage-enums sent by a communication partner with newer version than ours.
    /// </summary>
    [JsonIgnore]
    public Use Usage
    {
      get
      {
        if (z_usage < 0 || z_usage >= (int)Use.Last) return Use.Undef;
                                                else return (Use) z_usage;
      }
      set { z_usage = (int)value; }
    }

    /// <summary>
    /// Usage of ActorInfo triggers functionality on service oder client side while connecting/disconnecting.
    /// Use is set to ServiceEnableRequest when a Service is opened or ClientConnectRequest when a client is connected. 
    /// Use is set to another state when a Service is closed or a client is disconnected or a timeout has occured. 
    /// </summary>
    public enum Use
    {
      /// <summary>
      /// A constructor has been called that does not define the usage of this class.
      /// </summary>
      Undef,
      
      /// <summary>
      /// A constructor has been called that sets the own address of a service or client.
      /// </summary>
      MyAddress,
      
      /// <summary>
      /// The identified client has sent a connect request to its service.
      /// </summary>
      ClientConnectRequest,
      /// <summary>
      /// The identified service has accepted the connect request from a client.
      /// </summary>
      ServiceConnectResponse,

      /// <summary>
      /// The identified client has sent a disconnect request to its service.
      /// </summary>
      ClientDisconnectRequest,
      /// <summary>
      /// The identified service has accepted the disconnect request from a client.
      /// </summary>
      ServiceDisconnectResponse,

      /// <summary>
      /// The identified service has sent a register request to Remact.Catalog.
      /// </summary>
      ServiceEnableRequest,
      /// <summary>
      /// The identified service has been registered in Remact.Catalog.
      /// </summary>
      ServiceEnableResponse,

      /// <summary>
      /// The identified service is going to be closed, it has informed Remact.Catalog about it.
      /// </summary>
      ServiceDisableRequest,
      /// <summary>
      /// The identified service is marked as closed in Remact.Catalog.
      /// </summary>
      ServiceDisableResponse,

      /// <summary>
      /// The service name is going to be looked up in Remact.Catalog.
      /// </summary>
      ServiceAddressRequest,
      /// <summary>
      /// The complete, matching service identification has been found in Remact.Catalog registry.
      /// </summary>
      ServiceAddressResponse,

      /// <summary>
      /// This and higher enum values are internally mapped to 'Undef' (used to check version compatibility).
      /// </summary>
      Last
    }

    /// <summary>
    /// z_usage is public but used internally only! Access 'Usage' instead!
    /// Reason: http://msdn.microsoft.com/en-us/library/bb924412%28v=VS.100%29.aspx
    /// 'Usage' is streamed as int in order to make it backwards compatible to older communication partners
    /// </summary>
    public  int    z_usage;


    /// <summary>
    /// <para>Create a message from a ActorPort.</para>
    /// </summary>
    /// <param name="p">Copy data from partner p.</param>
    /// <param name="usage">Usage enumeration of this message.</param>
    public ActorInfo (IActorPort p, Use usage)
    {
      AppName     = p.AppName;
      AppVersion  = p.AppVersion;
      AppInstance = p.AppInstance;
      ProcessId   = p.ProcessId;
      Name        = p.Name;
      IsServiceName    = p.IsServiceName;
      CifComponentName = p.CifComponentName;
      CifVersion       = p.CifVersion;
      TimeoutSeconds   = p.TimeoutSeconds;
      HostName    = p.HostName;
      AddressList = p.AddressList;
      Uri         = p.Uri;
      Usage = usage;
      
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
      IsServiceName    = p.IsServiceName;
      CifComponentName = p.CifComponentName;
      CifVersion       = p.CifVersion;
      TimeoutSeconds   = p.TimeoutSeconds;
      HostName         = p.HostName;
      AddressList      = p.AddressList;
      Uri              = p.Uri;
      ClientId         = p.ClientId;
      Usage            = p.Usage;
      CatalogHopCount     = p.CatalogHopCount;
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
    public bool IsEqualTo (IActorPort p)
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

      switch (Usage)
      {
          case Use.ServiceEnableResponse:
          case Use.ServiceDisableResponse:
          case Use.ServiceAddressResponse:
              return String.Format ("{0} for '{1}'",  Usage.ToString(), name);

          default:
              return String.Format ("{0} from '{1}'", Usage.ToString(), name);
      };
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
