
// Copyright (c) 2012  AsyncWcfLib.sourceforge.net

using System;
using System.Runtime.Serialization;// DataContract
using System.Collections.Generic;  // List
using System.Reflection;           // Assembly
using System.Text;                 // StringBuilder
using SourceForge.AsyncWcfLib.Basic;
using System.Threading;
using System.Net;                  // IPAddress

namespace SourceForge.AsyncWcfLib
{
  //----------------------------------------------------------------------------------------------
  #region == class IWcfMessage ==

  /// <summary>
  /// Represents the base type for all  messages sent through AsyncWcfLib.
  /// The interface exists to find all message implementations in a project.
  /// The interface itself does not declare any constraints on your message implementation.
  /// It is your own responsibility to design the message for immutability by multiple threads when needed.
  /// </summary>
#if !MONO
  public interface IWcfMessage
  {
  }
#else
  // a bug in mono (last checked in 2.10.8.1) does not allow to use interfaces and ServiceKnownTypes as DataContracts
  [DataContract]
  [KnownType ("z_GetKnownTypeList")]
  public class IWcfMessage
  {
    // a bug in mono (last checked in 2.10.8.1) forces us to write this line in every message type
    public static IEnumerable<Type> z_GetKnownTypeList()  {return WcfMessage.z_GetKnownTypeList();}
  }
#endif


  #endregion
  //----------------------------------------------------------------------------------------------
  #region == class IExtensibleWcfMessage ==

  /// <summary>
  /// Represents the interface used for the base message class WcfMessage.
  /// It is provided for library users wishing to write their own base message implementation.
  /// </summary>
  public interface IExtensibleWcfMessage : IExtensibleDataObject
  {
      /// <summary>
      /// This SynchronizationContext is currently allowed to modify the message members. 
      /// The value is automatically set by AsyncWcfLib.
      /// </summary>
      SynchronizationContext BoundSyncContext { get; set; }

      /// <summary>
      /// True, when a message has been sent and the BoundSyncContext has to match the current SynchronizationContext in order to be allowed to modify message members.
      /// The value is automatically set by AsyncWcfLib.
      /// </summary>
      bool IsSent { get; set; }

      /// <summary>
      /// Returns true, when the message is safe for read and write by an Actor in its own SyncronizationContext.
      /// </summary>
      bool IsThreadSafe { get; }
  }

  #endregion
  //----------------------------------------------------------------------------------------------
  #region == class WcfMessage ==

  /// <summary>
  /// <para>Base class for all messages sent through AsyncWcfLib.</para>
  /// </summary>
  [DataContract (Namespace=WcfDefault.WsNamespace)]
#if !MONO
  [KnownType    ("z_GetKnownTypeList")]          // ms-help://MS.VSCC.v90/MS.MSDNQTR.v90.en/wcf_con/html/1a0baea1-27b7-470d-9136-5bbad86c4337.htm
                                                 // ms-help://MS.VSCC.v90/MS.MSDNQTR.v90.en/fxref_system.runtime.serialization/html/20c04a47-d300-0341-b725-7dffb7340bb8.htm
#endif
  public class WcfMessage: IWcfMessage, IExtensibleWcfMessage
  {
    //[OnDeserializing]
    //private void OnDeserializing(StreamingContext context) {SetDefaultsBeforeDeserializing(context);}
    //// Override this method to set default values for members in newer message versions
    //public virtual void SetDefaultsBeforeDeserializing (StreamingContext context)
    //{
    //  // don't forget base.SetDefaultsBeforeDeserializing (context);
    //}

    
    /// <summary>
    /// <para>AddBasicMessageType must be called to register a basic message directly derieved from IWcfMessage or IExtensibleWcfMessage.</para>
    /// <para>The type WcfMessage is such an example, it is registered by the library internally.</para>
    /// </summary>
    /// <param name="wcfMessageType">A basic message type used in WCF communication</param>
    public static void AddBasicMessageType( Type wcfMessageType )
    {
        CifAssembly = Assembly.GetCallingAssembly(); // store the last initialized assembly, used in ActorPort
        if( ms_ServiceContractTypeList == null ) ms_ServiceContractTypeList = new List<Type>( 10 ); // singleton initialized
        if( !ms_ServiceContractTypeList.Contains( wcfMessageType ) ) ms_ServiceContractTypeList.Add( wcfMessageType );
    }


    /// <summary>
    /// <para>AddKnownType must be called to register all known WCF message types derieved from a registered, basic message type.</para>
    /// <para>It is recommended to register all messages of an assembly during assembly initialization.</para>
    /// <para>The types of AsyncWcfLib are registered internally.</para>
    /// </summary>
    /// <param name="wcfMessageType">A message type used in WCF communication</param>
    public static void AddKnownType (Type wcfMessageType)
    {
        CifAssembly = Assembly.GetCallingAssembly(); // store the last initialized assembly, used in ActorPort
        if( ms_DataContractTypeList == null ) ms_DataContractTypeList = new List<Type>( 20 ); // singleton initialized
        if( !ms_DataContractTypeList.Contains( wcfMessageType ) ) ms_DataContractTypeList.Add( wcfMessageType );
    }
      

    /// <summary>
    /// This method is used internally only. In order to run under partial trust it probably must be public.
    /// (http://msdn.microsoft.com/en-us/library/bb412186%28v=VS.100%29.aspx)
    /// </summary>
    /// <returns>A list of known message types for the ServiceContractResolver.</returns>
    public static IEnumerable<Type> z_GetServiceKnownTypes( ICustomAttributeProvider provider ) // ServiceKnownTypeAttribute
    {
        return ms_ServiceContractTypeList;
    }
    
    /// <summary>
    /// This method is used internally only. In order to run under partial trust it probably must be public.
    /// (http://msdn.microsoft.com/en-us/library/bb412186%28v=VS.100%29.aspx)
    /// </summary>
    /// <returns> A list of known message types for the DataContractSerializer.</returns>
#if !MONO
    public static IEnumerable<Type> z_GetKnownTypeList() // KnownTypeAttribute
#else
    public static new IEnumerable<Type> z_GetKnownTypeList()
#endif
    {
        return ms_DataContractTypeList; // Break here before sending the first message
    }


    static WcfMessage() // static constructor is called before any WcfMessage object is created.
    {
      AddBasicMessageType (typeof(WcfMessage));

      AddKnownType (typeof(WcfMessage));
      AddKnownType (typeof(WcfIdleMessage));
      AddKnownType (typeof(WcfErrorMessage));
      AddKnownType (typeof(WcfNotifyResponse));
      AddKnownType (typeof(WcfPartnerMessage));
      AddKnownType (typeof(WcfPartnerListMessage));
    }

    private static List<Type>  ms_ServiceContractTypeList;
    private static List<Type>  ms_DataContractTypeList;
    
    /// <summary>
    /// The assembly containing this message definition.
    /// </summary>
    public  static Assembly    CifAssembly   { get; private set; }
    
    /// <summary>
    /// <para>Each WCF message may contain more data than the receiver has expected.</para>
    /// <para>This normally happens when the senders version is newer than the receivers version.</para>
    /// <para>Some data cannot be deserialized at the receiver. WCF provides this object to store this extension data.</para>
    /// <para>When serializing this message again (gateway functionality) the extension data is included and not lost.</para>
    /// </summary>
    public ExtensionDataObject ExtensionData { get; set; } // IExtensibleDataObject

    // not streamed
    /// <inheritdoc/>
    public SynchronizationContext BoundSyncContext { get; set; }

    // not streamed
    /// <inheritdoc/>
    public bool IsSent { get; set; }

    /// <inheritdoc/>
    public bool IsThreadSafe
    {get{
        return !IsSent || (BoundSyncContext != null && BoundSyncContext == SynchronizationContext.Current);
    }}
  }// class WcfMessage

  #endregion
  //----------------------------------------------------------------------------------------------
  #region == class WcfIdleMessage ==

  /// <summary>
  /// <para>A message without information content. Just for alive check or default response.</para>
  /// </summary>
  [DataContract (Namespace=WcfDefault.WsNamespace)]

  public class WcfIdleMessage: WcfMessage
  {
    #if MONO
    public static new IEnumerable<Type> z_GetKnownTypeList()  {return WcfMessage.z_GetKnownTypeList();}
    #endif
  }// class WcfIdleMessage has no [DataMember].

  #endregion
  //----------------------------------------------------------------------------------------------
  #region == class WcfErrorMessage ==

  /// <summary>
  /// <para>An error-message is generated when an exeption or timeout occurs on client or service side.</para>
  /// <para>The message contains a code indicating where the error occured and a text representation of the exception.</para>
  /// </summary>
  [DataContract (Namespace=WcfDefault.WsNamespace)]

  public class WcfErrorMessage : WcfMessage
  {
    /// <summary>
    /// Get the exception information.
    /// </summary>
    [DataMember] public  string    Message;

    /// <summary>
    /// Get the exception information.
    /// </summary>
    [DataMember] public  string    InnerMessage;

    /// <summary>
    /// Get the exception information.
    /// </summary>
    [DataMember] public  string    StackTrace;

    /// <summary>
    /// Get or set the Error-Code
    /// </summary>
    public Code Error 
    {
     get{if (z_error < 0 || z_error >= (int)Code.Last) return Code.Undef;
         else if (z_error >= (int)Code.LastAppCode
               && z_error <  (int)Code.NotConnected)   return Code.Undef;
                                                  else return(Code) z_error;
        }
     set{z_error = (int) value;}
    }
    
    /// <summary>
    /// Most Error-Codes uniquely definies the code position where the error occured.
    /// </summary>
    public enum Code
    {
      /// <summary>
      /// Code not set or unknown.
      /// </summary>
      Undef,
      
      /// <summary>
      /// No error.
      /// </summary>
      Ok,
      
      // -------------------- Errorcodes for free use, not used by AsyncWcfLib ------------------
      /// <summary>
      /// An exception occured while executing the request in the service-application.
      /// Set by library user.
      /// </summary>
      AppUnhandledExceptionOnService,
      
      /// <summary>
      /// The service-application does not know or does not accept this request.
      /// Set by library user.
      /// </summary>
      AppRequestNotAcceptedByService,
      
      /// <summary>
      /// The request could not be handled by the service-application as some subsystems are not ready.
      /// Set by library user.
      /// </summary>
      AppServiceNotReady,

      /// <summary>
      /// The request could not be handled by the service-application as the data is not available.
      /// Set by library user.
      /// </summary>
      AppDataNotAvailableInService,

      /// <summary>
      /// This and enum values up to NotConnected are internally mapped to 'Undef' (used to check version compatibility).
      /// </summary>
      LastAppCode,

      // -------------------- Errorcodes used by AsyncWcfLib ------------------
      /// <summary>
      /// Cannot send as the client is not (yet) connected.
      /// </summary>
      NotConnected = 1000,
      
      /// <summary>
      /// Cannot open the client (configuration error).
      /// </summary>
      CouldNotOpen,

      /// <summary>
      /// Cannot open the service connection (refused by target).
      /// </summary>
      ServiceNotRunning,

      /// <summary>
      /// Cannot open the router connection (refused by target).
      /// </summary>
      RouterNotRunning,

      /// <summary>
      /// Exception while sending (serializing) the first connect message.
      /// </summary>
      CouldNotStartConnect,

      /// <summary>
      /// No response from service, when trying to connect.
      /// </summary>
      CouldNotConnect,

      /// <summary>
      /// Wrong response from WCF router, when trying to connect.
      /// </summary>
      CouldNotConnectRouter,
      
      /// <summary>
      /// Exception while sending (serializing) a message.
      /// </summary>
      CouldNotStartSend,
      
      /// <summary>
      /// Exception received when waiting for response.
      /// </summary>
      CouldNotSend,
      
      /// <summary>
      /// Error while dispaching a message to another thread inside the application.
      /// </summary>
      CouldNotDispatch,
      
      /// <summary>
      /// The service did not respond in time. Detected by client.
      /// </summary>
      TimeoutOnClient,
      
      /// <summary>
      /// The service did not respond in time. Detected by service itself.
      /// </summary>
      TimeoutOnService,
      
      /// <summary>
      /// Exception while deserializing or serializing on service side.
      /// </summary>
      ReqOrRspNotSerializableOnService,
      
      /// <summary>
      /// null message received.
      /// </summary>
      RspNotDeserializableOnClient,
      
      /// <summary>
      /// The request-message-type is not registered as a known type on this service.
      /// </summary>
      RequestTypeUnknownOnService,
      
      /// <summary>
      /// Request with unknown client id.
      /// </summary>
      ClientIdNotFoundOnService,

      /// <summary>
      /// An exception occured while executing the request in the service-application.
      /// </summary>
      ClientDetectedUnhandledExceptionOnService,

      /// <summary>
      /// An exception occured while executing the request in the service-application.
      /// </summary>
      UnhandledExceptionOnService,

      /// <summary>
      /// This and higher enum values are internally mapped to 'Undef' (used to check version compatibility).
      /// </summary>
      Last
    }
    
    /// <summary>
    /// z_error is public but used internally only! Use 'Error' instead!
    /// Reason: http://msdn.microsoft.com/en-us/library/bb924412%28v=VS.100%29.aspx
    /// Error is stramed as int in order to make it reverse compatible to older communication partners
    /// </summary>
    [DataMember] public int z_error = 0;
    
    /// <summary>
    /// Create an empty error message
    /// </summary>
    public WcfErrorMessage (){}
    
    /// <summary>
    /// Create a error message.
    /// </summary>
    /// <param name="err">general reason.</param>
    /// <param name="text">unique information where te error occured.</param>
    public WcfErrorMessage (Code err, string text)
    {
      Error        = err;
      Message      = text;
      InnerMessage = String.Empty;
      StackTrace   = String.Empty;
    }// CTOR 1
    
    /// <summary>
    /// Create a error message.
    /// </summary>
    /// <param name="err">general reason.</param>
    /// <param name="ex">detailed information about the error.</param>
    public WcfErrorMessage (Code err, Exception ex)
    {
      Error = err;
      if (ex == null)
      {
        Message      = String.Empty;
        InnerMessage = String.Empty;
        StackTrace   = String.Empty;
      }
      else
      {
        Message      = string.Concat (ex.GetType().Name, ": ", ex.Message);
        string mainText = ex.Message;
        InnerMessage = String.Empty;
        StackTrace   = ex.StackTrace;
        ex = ex.InnerException;
        while (ex != null)
        {
        //InnerMessage += " Inner exception: ";
        //InnerMessage += ex.Message;
          if (mainText == ex.Message) {
            InnerMessage += " ...";
          }
          else {
            InnerMessage = string.Concat (InnerMessage, " ", ex.GetType().Name, ": ", ex.Message);
          }
          ex = ex.InnerException;
        }
      }
    }// CTOR 2
    
    /// <summary>
    /// Trace the errormessage
    /// </summary>
    /// <returns>string containing all information about the error</returns>
    public override string ToString ()
    {
      StringBuilder err = new StringBuilder (1000);
      err.Append("WcfError ");
      if (Error == Code.Undef) {err.Append("code="); err.Append(z_error);}
                          else {err.Append(Error.ToString());}
      err.Append (". ");
      err.Append (Message); 
      if (InnerMessage.Length > 0)
      {
        err.Append (Environment.NewLine);
        err.Append ("  ");
        err.Append (InnerMessage);
      }                  

      return err.ToString();
    }// ToString
    
    #if MONO
    public static new IEnumerable<Type> z_GetKnownTypeList()  {return WcfMessage.z_GetKnownTypeList();}
    #endif
  }// class WcfErrorMessage

  #endregion
  //----------------------------------------------------------------------------------------------
  #region == class WcfPartnerMessage ==

  /// <summary>
  /// <para>This class identifies a communication partner (client or service).</para>
  /// <para>It is used to open and close communication.</para>
  /// </summary>
  [DataContract (Namespace=WcfDefault.WsNamespace)]
  public class WcfPartnerMessage: WcfMessage
  {
    /// <summary>
    /// IsServiceName=true : A service name must be unique in the plant, independant of host or application.
    /// IsServiceName=false: A client  name must for unique identification be combined with application name, host name, instance- or process id.
    /// </summary>
    [DataMember] public bool    IsServiceName;
    
    /// <summary>
    /// Identification in Trace and name of endpoint address in App.config file.
    /// </summary>
    [DataMember] public string  Name;
    
    /// <summary>
    /// Unique name of an application or service in the users WcfContract.Namespace.
    /// </summary>
    [DataMember] public string  AppName;
    
    /// <summary>
    /// Unique instance number of the application (unique in a plant or on a host, depending on WcfDefault.IsAppIdUniqueInPlant).
    /// </summary>
    [DataMember] public int     AppInstance;
    
    /// <summary>
    /// Process id of the application, given by the operating system (unique on a host at a certain time).
    /// </summary>
    [DataMember] public int     ProcessId;
    
    /// <summary>
    /// Assembly version of the application.
    /// </summary>
    [DataMember] public Version AppVersion;
    
    /// <summary>
    /// Assembly name of an important CifComponent containig some messages
    /// </summary>
    [DataMember] public string  CifComponentName;

    /// <summary>
    /// Assembly version of the important CifComponent
    /// </summary>
    [DataMember] public Version CifVersion;
    
    /// <summary>
    /// Host running the application
    /// </summary>
    [DataMember] public string  HostName;
    
    /// <summary>
    /// <para>Universal resource identifier to reach the input of the service or client.</para>
    /// <para>E.g. RouterService: http://localhost:40000/AsyncWcfLib/RouterService</para>
    /// </summary>
    [DataMember] public  Uri    Uri;

    /// <summary>
    /// <para>To support networks without DNS server, the WcfRouter keeps a list of all IP-Adresses of a host.</para>
    /// </summary>
    [DataMember] public List<IPAddress> AddressList;

    /// <summary>
    /// After a service has no message received for TimeoutSeconds, it may render the connection to this client as disconnected.
    /// 0 means no timeout. 
    /// The client should send at least 2 messages each TimeoutSeconds-period in order to keep the correct connection state on the service.
    /// A Service is trying to notify 2 messages each TimeoutSeconds-period in order to check a dual-Http connection.
    /// </summary>
    [DataMember] public int     TimeoutSeconds;

    /// <summary>
    /// The message from the original service has RouterHopCount=0. The same message sent from the WcfRouter on the local host has RouterHopCount=1.
    /// Each router increments the hopcount on reception of a message.
    /// A router accepts new data only if the receiving hop count is smaller than the stored.
    /// </summary>
    [DataMember] public int     RouterHopCount;

    /// <summary>
    /// A service having a longer ApplicationRunTime wins the competition when two services with same name are running.
    /// </summary>
    [DataMember] public TimeSpan ApplicationRunTime;

    /// <summary>
    /// The message may be used for several purposes.
    /// </summary>
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
    /// Usage of WcfPartnerMessage triggers functionality on service oder client side while connecting/disconnecting.
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
      /// The identified service has sent a register request to WcfRouter.
      /// </summary>
      ServiceEnableRequest,
      /// <summary>
      /// The identified service has been registered in WcfRouter.
      /// </summary>
      ServiceEnableResponse,

      /// <summary>
      /// The identified service is going to be closed, it has informed WcfRouter about it.
      /// </summary>
      ServiceDisableRequest,
      /// <summary>
      /// The identified service is marked as closed in WcfRouter.
      /// </summary>
      ServiceDisableResponse,

      /// <summary>
      /// The service name is going to be looked up in WcfRouter.
      /// </summary>
      ServiceAddressRequest,
      /// <summary>
      /// The complete, matching service identification has been found in WcfRouter registry.
      /// </summary>
      ServiceAddressResponse,

      /// <summary>
      /// This and higher enum values are internally mapped to 'Undef' (used to check version compatibility).
      /// </summary>
      Last
    }

    /// <summary>
    /// m_usage is public but used internally only! Access 'Usage' instead!
    /// Reason: http://msdn.microsoft.com/en-us/library/bb924412%28v=VS.100%29.aspx
    /// 'Usage' is streamed as int in order to make it reverse compatible to older communication partners
    /// </summary>
    [DataMember] public  int    z_usage;


    /// <summary>
    /// <para>Create a message from a ActorPort.</para>
    /// </summary>
    /// <param name="p">Copy data from partner p.</param>
    /// <param name="usage">Usage enumeration of this message.</param>
    public WcfPartnerMessage (IActorPortId p, Use usage)
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
      Usage       = usage;
      
      RouterHopCount = 0;
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
    /// <para>Copy a WcfPartnerMessage.</para>
    /// </summary>
    /// <param name="p">Copy data from partner p.</param>
    public WcfPartnerMessage (WcfPartnerMessage p)
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
      Usage            = p.Usage;
      RouterHopCount     = p.RouterHopCount;
      ApplicationRunTime = p.ApplicationRunTime;
    }// CTOR2

    /// <summary>
    /// Check if two communication partner objects represent the same partner
    /// </summary>
    /// <param name="p">second partner</param>
    /// <returns>true if AppName + AppInstance + Client- or ServiceName are equal</returns>
    public bool IsEqualTo (WcfPartnerMessage p)
    {
      if (p == null || IsServiceName != p.IsServiceName) return false;

      if (IsServiceName)
      {
        return Name.Equals (p.Name); // a service may be moved to another host or another application
      }
      else if (WcfDefault.Instance.IsAppIdUniqueInPlant (AppInstance))
      { // plant unique client
        return AppInstance == p.AppInstance
            && Name.Equals (p.Name)
            && AppName.Equals (p.AppName);  // these clients may be moved to another host
      }
      else
      { // host unique client
        return AppInstance ==  p.AppInstance
            &&(!WcfDefault.Instance.IsProcessIdUsed (AppInstance) || ProcessId == p.ProcessId) // process id is valid for a running client only
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
    public bool IsEqualTo (IActorPortId p)
    {
      if (p == null || IsServiceName != p.IsServiceName) return false;

      if (IsServiceName)
      {
        return Name.Equals (p.Name); // a service may be moved to another host or another application
      }
      else if (WcfDefault.Instance.IsAppIdUniqueInPlant (AppInstance))
      { // plant unique client
        return AppInstance == p.AppInstance
            && Name.Equals (p.Name)
            && AppName.Equals (p.AppName);  // these clients may be moved to another host
      }
      else
      { // host unique client
        return AppInstance ==  p.AppInstance
            &&(!WcfDefault.Instance.IsProcessIdUsed (AppInstance) || ProcessId == p.ProcessId) // process id is valid for a running client only
            && HostName.Equals (p.HostName)
            && Name.Equals (p.Name)
            && AppName.Equals (p.AppName);  // these clients may not be moved
      }
    }


    /// <summary>
    /// Creates string representation of WcfPartnerMessage.
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
          name = WcfDefault.Instance.GetAppIdentification(AppName, AppInstance, HostName, ProcessId) + "/" + Name;
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

    #if MONO
    public static new IEnumerable<Type> z_GetKnownTypeList()  {return WcfMessage.z_GetKnownTypeList();}
    #endif
  }// WcfPartnerMessage

  #endregion

}// namespace
