
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Runtime.Serialization;// DataContract
using System.Collections.Generic;  // List
using System.Reflection;           // Assembly
using System.Text;                 // StringBuilder
using Remact.Net.Internal;
using System.Threading;
using System.Net;                  // IPAddress

namespace Remact.Net
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
      AddKnownType (typeof(ActorMessage));
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

}// namespace
