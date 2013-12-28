
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.ServiceModel;
using System.Runtime.Serialization;
#if !BEFORE_NET40
    using System.Threading.Tasks;
#endif

// ContractNamespace, see: http://msdn.microsoft.com/en-us/library/ms751505(VS.85).aspx
namespace SourceForge.AsyncWcfLib.Basic
{
  /*-----------------------------------------------------
   * IWcfBasicService
   * ----------------
   * These services keep state information for each client.
   * Clients send requests asynchronous.
   * Client receive responses as callback events trough the window message queue 
   * on the same thread as the request was sent.
   * ErrorMessages are received in case of failure.
   * 
   * How to check a connection on client side ?
   * 1. Check for WcfErrorMessage's in CallbackFunc.
   * 2. After successful connect, the first response is a WcfPartner message with Usage=ServiceConnectResponse.
   * 3. Send/Receive periodic messages e.g. WcfIdleMessage. 
   *    Failure to send will trigger a (Timeout-) ErrorMessage as response.
   * 
   * What is checked by WcfBasicClientAsync and WcfBasicService ?
   * 1. Each received payload must have an incremented SendId
   * 2. A payload received by client must contain the last sent RequestId
   * 
   * What does WcfBasicClientAsync ?
   * 1. To establish a connection: Send WcfPartner payload with client info and  with Usage=ClientConnectRequest.
   * 2. Service keeps client info and returns WcfPartner payload with server info, your ClientId and Usage=ServiceConnectResponse.
   * 3. Use the ClientId for each payload
   * 4. When disconnecting: WcfPartner payload with Usage=ClientDisconnectRequest is sent.
   * 
   * Subclassing WcfPayload ?
   * ms-help://MS.VSCC.v90/MS.MSDNQTR.v90.en/wcf_con/html/7c5a26c8-89c9-4bcb-a4bc-7131e6d01f0c.htm
   * 
   * Known Types ?
   * ms-help://MS.VSCC.v90/MS.MSDNQTR.v90.en/wcf_con/html/1a0baea1-27b7-470d-9136-5bbad86c4337.htm
   * 
   * Data Contract, Extensible Data, roundtrip ?
   * ms-help://MS.VSCC.v90/MS.MSDNQTR.v90.en/fxref_system.runtime.serialization/html/20c04a47-d300-0341-b725-7dffb7340bb8.htm
   * 
   * Versioning ?
   * ms-help://MS.VSCC.v90/MS.MSDNQTR.v90.en/wcf_con/html/4a0700cb-5f5f-4137-8705-3a3ecf06461f.htm
   */


  //-----------------------------------------------------
  /// <summary>
  /// Synchronous interface, normally used on service side
  /// </summary>
  [ServiceContract (Namespace=WcfDefault.WsNamespace, Name="IWcfBasic",
                    ConfigurationName = "AsyncWcfLib.ServiceContract")]
                  //ConfigurationName = "AsyncWcfLib.ClientContract")]
  [ServiceKnownType ("z_GetServiceKnownTypes", typeof(WcfMessage))]
  public interface IWcfBasicContractSync
  {
    /// <summary>
    /// Synchronious send a request to a service.
    /// </summary>
    /// <param name="msg">The request message.</param>
    /// <param name="id">Internally used identification info for a request (received and returned).</param>
    /// <returns>The response message.</returns>
    [OperationContract]
    IWcfMessage WcfRequest (IWcfMessage msg, ref WcfReqIdent id);


//#if !BEFORE_NET45
//    /// <summary>
//    /// Send a request to a service and receive the response asynchronously (async-await pattern).
//    /// See http://blogs.msdn.com/b/endpoint/archive/2010/11/13/simplified-asynchronous-programming-model-in-wcf-with-async-await.aspx
//    /// </summary>
//    /// <param name="id">Internally used identification info for a request containing the request message.</param>
//    /// <returns>A WcfReqIdent containing the response message.</returns>
//    [OperationContract]
//    Task<WcfReqIdent> SendReceiveAsync( WcfReqIdent id );
//#endif
  }


  //-----------------------------------------------------
  /// <summary>
  /// <para>Asynchronous interface, seldom used on service side.</para>
  /// <para>                        similar to the generated client side.</para>
  /// </summary>
  [ServiceContract (Namespace=WcfDefault.WsNamespace, Name="IWcfBasic")]

  public interface IWcfBasicContractAsync
  {
    /// <summary>
    /// Handle a request on a service.
    /// </summary>
    /// <param name="msg">Request message.</param>
    /// <param name="id">Internally used identification info for a request (received and returned).</param>
    /// <param name="callback">Delegate to call on service side, when request has been handled on service.</param>
    /// <param name="asyncState">User state.</param>
    /// <returns>IAsyncResult to handle the Microsoft async pattern.</returns>
    [OperationContract (AsyncPattern=true)]
    IAsyncResult BeginWcfRequest (IWcfMessage msg, ref WcfReqIdent id, AsyncCallback callback, object asyncState);
    
    /// <summary>
    /// Wait for asynchronous completion of a thread started with BeginWcfRequest.
    /// </summary>
    /// <param name="id">Internally used identification info for a request (received and returned).</param>
    /// <param name="result">IAsyncResult to handle the Microsoft async pattern.</param>
    /// <returns>Response message.</returns>
    IWcfMessage EndWcfRequest (ref WcfReqIdent id, IAsyncResult result);
  }


  //-----------------------------------------------------
  /// <summary>
  /// <para>Synchronous interface with callback, seldom used on service side.</para>
  /// <para>Needs wsDualHttpBinding.</para>
  /// </summary>
  [ServiceContract (Namespace=WcfDefault.WsNamespace, Name="IWcfDual",
                    CallbackContract=typeof(IWcfDualCallbackContract))]
  //SessionMode      = SessionMode.Required)] // Session used by Callback.Close

  public interface IWcfDualContractSync
  {
    /// <summary>
    /// Handle a request on a service.
    /// </summary>
    /// <param name="msg">Request message.</param>
    /// <param name="id">Internally used identification info for a request (received and returned).</param>
    /// <returns>Response message.</returns>
    [OperationContract]
    IWcfMessage WcfRequest (IWcfMessage msg, ref WcfReqIdent id);
  }


  //-----------------------------------------------------
  /// <summary>
  /// <para>Asynchronous interface with callback, seldom used on service side.</para>
  /// <para>Needs wsDualHttpBinding. Similar to the generated client side.</para>
  /// </summary>
  [ServiceContract (Namespace=WcfDefault.WsNamespace, Name="IWcfDual",
                    CallbackContract = typeof(IWcfDualCallbackContract))]

  public interface IWcfDualContractAsync
  {
    /// <summary>
    /// Handle a request on a service.
    /// </summary>
    /// <param name="msg">Request message.</param>
    /// <param name="id">Internally used identification info for a request (received and returned).</param>
    /// <param name="callback">Delegate to call on service side, when request has been handled on service.</param>
    /// <param name="asyncState">User state.</param>
    /// <returns>IAsyncResult to handle the Microsoft async pattern.</returns>
    [OperationContract (AsyncPattern=true)]
    IAsyncResult BeginWcfRequest (IWcfMessage msg, ref WcfReqIdent id, AsyncCallback callback, object asyncState);
    
    /// <summary>
    /// Wait for asynchronous completion of a thread started with BeginWcfRequest.
    /// </summary>
    /// <param name="id">Internally used identification info for a request (received and returned).</param>
    /// <param name="result">IAsyncResult to handle the Microsoft async pattern.</param>
    /// <returns>Response message.</returns>
    IWcfMessage EndWcfRequest (ref WcfReqIdent id, IAsyncResult result);
  }


  //-----------------------------------------------------
  /// <summary>
  /// <para>Callback interface, used for service notifications to the client.</para>
  /// <para>Needs wsDualHttpBinding. Asynchronous implementation, otherwise service may block, when client has disappeared.</para>
  /// </summary>
  public interface IWcfDualCallbackContract
  {
    /// <summary>
    /// Handle a unrequested message from a service on the client.
    /// </summary>
    /// <param name="notification">Unrequest message.</param>
    /// <param name="id">Internally used identification info for a request (received and returned).</param>
    /// <param name="callback">Delegate to call on service side, when notification has been handled on client.</param>
    /// <param name="asyncState">User state.</param>
    /// <returns>IAsyncResult to handle the Microsoft async pattern.</returns>
    [OperationContract (IsOneWay=true, AsyncPattern=true)]
    IAsyncResult BeginOnWcfNotificationFromService (IWcfMessage notification, ref WcfReqIdent id, AsyncCallback callback, object asyncState);

    /// <summary>
    /// Wait for asynchronous completion of a thread started with BeginOnWcfNotificationFromService.
    /// <param name="id">Internally used identification info for a request (received and returned).</param>
    /// </summary>
    /// <param name="result">IAsyncResult to handle the Microsoft async pattern.</param>
    void EndOnWcfNotificationFromService (ref WcfReqIdent id, IAsyncResult result);
  }

}// namespace

