
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
  [DataContract (Namespace=RemactDefaults.WsNamespace)]
#if !MONO
  [KnownType    ("z_GetKnownTypeList")]          // ms-help://MS.VSCC.v90/MS.MSDNQTR.v90.en/wcf_con/html/1a0baea1-27b7-470d-9136-5bbad86c4337.htm
                                                 // ms-help://MS.VSCC.v90/MS.MSDNQTR.v90.en/fxref_system.runtime.serialization/html/20c04a47-d300-0341-b725-7dffb7340bb8.htm
#endif
  public class WcfMessage: IExtensibleWcfMessage
  {
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
      AddKnownType (typeof(ReadyMessage));
      AddKnownType (typeof(ErrorMessage));
      //AddKnownType (typeof(WcfNotifyResponse));
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
  #region == class ReadyMessage ==

  /// <summary>
  /// <para>A message without information content. Just for alive check or default response.</para>
  /// </summary>
  [DataContract (Namespace=RemactDefaults.WsNamespace)]

  public class ReadyMessage: WcfMessage
  {
  }

  #endregion
  //----------------------------------------------------------------------------------------------
}
