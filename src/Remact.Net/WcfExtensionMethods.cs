
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
#if !BEFORE_NET45
    using System.Threading.Tasks;
#endif

namespace Remact.Net
{
  //----------------------------------------------------------------------------------------------
  #region Delegate types for message handling
  /// <summary>
  /// A general handler for internal and external messages. Called on the correct thread (synchronization context).
  /// </summary>
  /// <param name="id">the received request or response containing message and sender.</param>
  public delegate void WcfMessageHandler( WcfReqIdent id ); // TODO remove, legacy

#if !BEFORE_NET45
  /// <summary>
  /// A handler for internal and external messages. Called on the correct thread (synchronization context).
  /// </summary>
  /// <param name="id">the received request or response containing message and sender.</param>
  /// <param name="dummy">unused parameter to make signature non ambiguous (temporary for VisualStudio 11).</param>
  public delegate Task WcfMessageHandlerAsync( WcfReqIdent id, bool dummy ); // bool dummy to make signature non ambiguous
#endif

  /// <summary>
  /// A handler for internal and external messages to ActorPort objects. Called on the correct thread (synchronization context).
  /// </summary>
  /// <param name="id">the received request or response containing message and sender.</param>
  /// <param name="senderContext">the local id.Sender.SenderContext object present when the request or response is recived.</param>
  public delegate void WcfMessageHandler<TSC>( WcfReqIdent id, TSC senderContext ) where TSC : class; // TODO remove, legacy

#if !BEFORE_NET45
  /// <summary>
  /// A handler for internal and external messages to ActorPort objects. Called on the correct thread (synchronization context).
  /// </summary>
  /// <param name="id">the received request or response containing message and sender.</param>
  /// <param name="senderContext">the local id.Sender.SenderContext object present when the request or response is recived.</param>
  /// <param name="dummy">unused parameter to make signature non ambiguous (temporary for VisualStudio 11).</param>
  public delegate Task WcfMessageHandlerAsync<TSC>( WcfReqIdent id, TSC senderContext, bool dummy ) where TSC : class;
#endif

  /// <summary>
  /// Extension method On implements this delegate for handling messages directly in a Send context.
  /// <see cref="WcfExtensionMethods.On&lt;T>(WcfReqIdent,Action&lt;T>)"/>, <see cref="ActorOutput.SendOut(IWcfMessage, AsyncResponseHandler)"/>
  /// </summary>
  /// <param name="id">the received response or errormessage from the connected service</param>
  public delegate WcfReqIdent AsyncResponseHandler (WcfReqIdent id);

  #endregion


  /// <summary>
  /// Contains extension methods for AsyncWcfLib.
  /// To use extension methods you need to reference assembly 'System.Core'
  /// </summary>
  public static class WcfExtensionMethods
  {
    /// <summary>
    /// <para>Execute code, when message type matches the template parameter. Used to add lambda expressions, e.g.</para>
    /// <para>rsp.On&lt;WcfIdleMessage>(idle => {do something with idle message 'idle'})</para>
    /// <para>   .On&lt;WcfErrorMessage>(err => {do something with error message 'err'})</para>
    /// </summary>
    /// <typeparam name="T">The message type looked for.</typeparam>
    /// <param name="id">Parameter is added by the compiler.</param>
    /// <param name="handle">A delegate or lambda expression that will be executed, when the type matches.</param>
    /// <returns>The same request, for chained calls.</returns>
    // inspired by http://blogs.infosupport.com/blogs/frankb/archive/2008/02/02/Using-C_2300_-3.0-Extension-methods-and-Lambda-expressions-to-avoid-nasty-Null-Checks.aspx

#if !MONO
    public static WcfReqIdent On<T> (this WcfReqIdent id, Action<T> handle) where T: class, IWcfMessage
#else
    public static WcfReqIdent On<T> (this WcfReqIdent id, Action<T> handle) where T: IWcfMessage
#endif
    {
      if (id != null)
      {
        T   typedMsg = id.Message as T;
        if (typedMsg == null)
        {
          return id; // call next On extension method
        }
        handle (typedMsg);
      }
      return null; // already handled
    }

  }//class
}
