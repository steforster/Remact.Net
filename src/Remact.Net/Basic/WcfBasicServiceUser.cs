
// Copyright (c) 2012  AsyncWcfLib.sourceforge.net

using System;
using System.ServiceModel;         // OperationContext
using System.Net;                  // Dns

namespace SourceForge.AsyncWcfLib.Basic
{
  /// <summary>
  /// <para>Class used on WCF service side.</para>
  /// <para>Represents a connected client.</para>
  /// <para>Has the possibility to store and forward notifications that are not expected by the client.</para>
  /// <para>This could also be handled by the more cumbersome 'DualHttpBinding'.</para>
  /// </summary>
  internal class WcfBasicServiceUser: IWcfBasicPartner
  {
    //----------------------------------------------------------------------------------------------
    #region Properties

    /// <summary>
    /// <para>Detailed information about the client using this service.</para>
    /// <para>Contains the "UserContext" object that may be used freely by the service application.</para>
    /// <para>Output is linked to this service.</para>
    /// </summary>
    public   ActorOutput               ClientIdent {get; private set;}

    /// <summary>
    /// The ClientId used on this service.
    /// </summary>
    public   int                       ClientId    {get; private set;}

    internal uint                      LastReceivedSendId = 0;

    /// <summary>
    /// Set to 0 when a message has been received or sent. Incremented by milliseconds in TestNotificationChannel().
    /// </summary>
    internal int                       ChannelTestTimer   = 0;
    internal object                    ClientAccessLock   = new Object();
    
    private OperationContext           m_OperationContext = null;
    private IWcfDualCallbackContract   m_NotifyCallback   = null; // wsDualHttbBinding
    private WcfNotifyResponse          m_NotifyList       = null; // non dual-Bindings
    private bool                       m_boOutstandingNotification = false;
    private bool                       m_boDisconnectReq  = false;
    private bool                       m_boTimeout        = false;

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Constructor + Methods

    /// <summary>
    /// Internally called to create a WcfBasicServiceUser object
    /// </summary>
    /// <param name="clientIdent">client using this service</param>
    /// <param name="clientId">client id used for this client on this service.</param>
    internal WcfBasicServiceUser (ActorOutput clientIdent, int clientId)
    {
      ClientId    = clientId;
      ClientIdent = clientIdent;
    }// CTOR

    internal void UseDataFrom (WcfPartnerMessage remoteClient)
    {
      ClientIdent.UseDataFrom (remoteClient);
      UriBuilder uri = new UriBuilder (ClientIdent.Uri);
      uri.Scheme = ClientIdent.OutputSidePartner.Uri.Scheme; // the service's Uri scheme (e.g. http)
      ClientIdent.Uri = uri.Uri; 
    }
    
    /// <summary>
    /// Shutdown the outgoing connection. Send a disconnect message to the partner.
    /// </summary>
    public void Disconnect ()
    {
      SetConnected( false, null );
      if (m_OperationContext == null) return;
      AbortNotificationChannel ();
    }

    /// <summary>
    /// Internally called on client connect or reconnect
    /// </summary>
    internal void OpenNotificationChannel()
    {
      try
      {
        m_NotifyList     = null;
        m_NotifyCallback = OperationContext.Current.GetCallbackChannel<IWcfDualCallbackContract>();
      }
      catch (Exception)
      {
        m_NotifyCallback = null;
        m_NotifyList = new WcfNotifyResponse();
      }
      m_OperationContext = OperationContext.Current;
      m_boOutstandingNotification = false;
      m_boTimeout                 = false;
    }// OpenNotificationChannel


    /// <summary>
    /// Is the client connected ?
    /// </summary>
    public bool IsConnected 
    {
      get {
        return !m_boDisconnectReq && !m_boTimeout;
      }
    }

    
    internal void SetConnected( bool connected, ActorInput serviceIdent )
    {
        if( connected ) 
        {
            ClientIdent.SyncContext = serviceIdent.SyncContext;
            ClientIdent.ManagedThreadId = serviceIdent.ManagedThreadId;
            ClientIdent.m_Connected = true;
            m_boDisconnectReq = false; m_boTimeout = false; 
        }
        else
        {
            ClientIdent.Disconnect();
            m_boDisconnectReq = true;
        }
    }


    /// <summary>
    /// Is the client connected and is no error on the notification channel ?
    /// </summary>
    public bool IsNotificationChannelOk {get{return m_OperationContext != null && m_OperationContext.Channel != null 
                                         && m_OperationContext.Channel.State != CommunicationState.Closed
                                         && m_OperationContext.Channel.State != CommunicationState.Faulted
                                         && IsConnected;}}
    /// <summary>
    /// Was there an error that disconnected the client ?
    /// </summary>
    public bool IsFaulted               {get{return m_boTimeout;}}

    /// <summary>
    /// Trace internal state of the client connection to this service
    /// </summary>
    /// <param name="mark">6 char, eg. 'Connec', 'Discon', 'Abortd'</param>
    public void TraceState(string mark)
    {
      if (m_OperationContext != null)
      {
        WcfTrc.Info ("WcfSvc", "["+mark.PadRight (6)+"] "+ ClientMark
                      +", SessionId="+m_OperationContext.SessionId
                      +", Channel="+m_OperationContext.Channel.State
                      +", InstanceContext=" +m_OperationContext.InstanceContext.State
                      +", IncomingChannels="+m_OperationContext.InstanceContext.IncomingChannels.Count
                      +", OutgoingChannels="+m_OperationContext.InstanceContext.OutgoingChannels.Count
                      , ClientIdent.Logger );
      }
    }// TraceState

    /// <summary>
    /// Used for tracing messages from/to this client.
    /// </summary>
    public string ClientMark {get{return string.Format("{0}/{1}[{2}]", ClientIdent.HostName, ClientIdent.Name, ClientId);}}

    /// <summary>
    /// Internally called by DoPeriodicTasks(), notifies idle messages and disconnects the client connection in case of failure.
    /// </summary>
    /// <returns>True, when connection state has changed.</returns>
    internal bool TestChannel (int i_nMillisecondsPassed)
    {
      if (m_NotifyCallback != null)
      { // Notification channel has been opened
        try
        {
          if (!IsNotificationChannelOk)
          {
            if (IsConnected) TraceState("Discon");
                        else AbortNotificationChannel();
            m_NotifyCallback   = null;
            m_OperationContext = null;
          }
          else if (!m_boOutstandingNotification 
                 && ClientIdent.TimeoutSeconds > 0 
                 && ChannelTestTimer > ((ClientIdent.TimeoutSeconds+i_nMillisecondsPassed)*400)) // 2 messages per TimeoutSeconds-period
          {
            SendNotification (new WcfIdleMessage());
          }
        }
        catch (Exception ex)
        {
            WcfTrc.Exception( "Cannot test '" + ClientMark + "' from service", ex, ClientIdent.Logger );
          m_boTimeout = true;
          return true; // changed
        }
      }

      if (IsConnected)
      {
        if (ClientIdent.TimeoutSeconds > 0 && ChannelTestTimer > ClientIdent.TimeoutSeconds*1000)
        {
          m_boTimeout = true; // Client has not sent a request for longer than the specified timeout
          SetConnected( false, null );
          return true; // changed
        }
        ChannelTestTimer += i_nMillisecondsPassed; // increment at end, to give a chance to recover after longer debugging breakpoints
      }
      return false;
    }// TestChannel


    /// <summary>
    /// Internally called on service shutdown or timeout on notification channel
    /// </summary>
    internal void AbortNotificationChannel ()
    {
      if (m_OperationContext == null) return;

      try
      {
        SetConnected( false, null );
        IContextChannel channel = m_OperationContext.Channel;
        
        if (channel.State != CommunicationState.Closed
         && channel.State != CommunicationState.Faulted)
        {
          //TraceState("Abortd");
          m_OperationContext = null; // mark channel as closed, Abort may take a while ???
          channel.Abort(); // Abort all pending notifications, ???? maybe reduce "deadlock" time when several clients are shutdown without disconnect
        }
      }
      catch (Exception ex)
      {
          WcfTrc.Exception( "Cannot abort '" + ClientMark + "' from service", ex, ClientIdent.Logger );
      }
      m_OperationContext = null;
      m_NotifyCallback   = null;
      m_boOutstandingNotification = false;
    }// AbortNotificationChannel


    /// <summary>
    /// <para>Call SendNotification(...) to enqueue a notification message.</para>
    /// <para>All queued messages are added to the next response by NotificationsAndResponse().</para>
    /// </summary>
    /// <param name="notification">a message not requested by the client</param>
    public void SendNotification (IWcfMessage notification)
    {
      ChannelTestTimer = 0;
      try
      {
        if (m_NotifyList == null && !IsNotificationChannelOk)
        {
            WcfTrc.Error( "WcfSvc", "Closed notification channel to " + ClientMark, ClientIdent.Logger );
        }
        else
        {
          m_boOutstandingNotification = true;
          if (m_NotifyList != null)
          { // Multimessage is sent with next response
            m_NotifyList.Notifications.Add (notification);
            ++ClientIdent.LastSentId;
          }
          else
          { // Send over wsDualHttpBinding
            // RequestId = 0 = Notification
            WcfReqIdent id = new WcfReqIdent (ClientIdent, ClientId, 0, notification, null);
            m_NotifyCallback.BeginOnWcfNotificationFromService (notification, ref id, OnAsyncNotificationCallback, notification);
          }
        }
      }
      catch (Exception ex)
      {
          WcfTrc.Exception( "Cannot notify '" + ClientMark + "' from service", ex, ClientIdent.Logger );
          m_boTimeout = true;
      }
    }// SendNotification


    /// <summary>
    /// Returns the number of notification responses, that have not been sent yet.
    /// </summary>
    public int OutstandingResponsesCount
    { get {
            if (m_NotifyList != null) return m_NotifyList.Notifications.Count;
            return 0;
    }}


    internal void StartNewRequest( WcfReqIdent id )
    {
      if( m_NotifyList.Notifications.Count > 0 )
      {
        id.NotifyList = m_NotifyList; // take notifications not sent to a particular request
        m_NotifyList  = new WcfNotifyResponse(); // prepare for next notifications
      }
    }


    /// <summary>
    /// <para>GetNotificationsAndResponse() returns a message of type 'WcfNotifyResponse' when  notification messages are queued.</para>
    /// <para>It must be called to return the response by the service.</para>
    /// </summary>
    /// <returns>The responses plus notifications.</returns>
    public IWcfMessage GetNotificationsAndResponse (ref WcfReqIdent id)
    {
      if( id.Response == null )
      {
          id.SendResponse( new WcfIdleMessage() ); // no other information to return
      }

      id = id.Response; // return the new id also
      if (id.NotifyList != null)
      {
          m_boOutstandingNotification = false;
          id.NotifyList.Response = id.MessageSaved;
          id.Message = id.NotifyList;
      }
      else
      {
          id.Message = id.MessageSaved;
      }
      return id.Message;
    }


    // End of a wsDualHttpBinding notify/response process
    private void OnAsyncNotificationCallback(IAsyncResult ar)
    {
      m_boOutstandingNotification = false;

      if (m_NotifyCallback == null || m_boTimeout) return;
      
      try
      {
        IWcfMessage notification = ar.AsyncState as IWcfMessage;
        if (ar.IsCompleted)
        {
          WcfReqIdent id = null;
          m_NotifyCallback.EndOnWcfNotificationFromService (ref id, ar);
          WcfTrc.Info (/*notification.SvcSndId*/"WcfSvc", notification.ToString(), ClientIdent.Logger);
        }
        else
        {
            WcfTrc.Error(/*notification.SvcSndId*/"WcfSvc", "notification not completed", ClientIdent.Logger );
          m_boTimeout = true;
        }
      }
      catch (Exception ex)
      {
          WcfTrc.Exception( "lost notification to " + ClientMark, ex, ClientIdent.Logger );
        m_boTimeout = true;
      }
    }// OnAsyncNotificationCallback


    #endregion
    //----------------------------------------------------------------------------------------------
    #region IWcfPartner implementation

    /*/ <summary>
    /// Get the state of the outgoing connection. May be called on any thread.
    /// </summary>
    /// <returns>A <see cref="WcfState"/></returns>
    public WcfState GetConnectionState ()
    {
      if (m_NotifyList == null)
      { // Dual channel
        if (IsNotificationChannelOk) return WcfState.Ok;
      }
      else
      { // Polling channel
        if (IsConnected)  return WcfState.Ok;
      }
      if (IsFaulted)      return WcfState.Faulted;
      return WcfState.Disconnected;
      //return WcfState.Connecting;
    }*/

    /// <summary>
    /// Dummy implementation. Client stub is always connected to the service.
    /// </summary>
    /// <returns>true</returns>
    public bool TryConnect ()
    {
      return true;
    }

    /// <summary>
    /// Send a request to the service internally connected to this client-stub.
    /// </summary>
    /// <param name="id">A <see cref="WcfReqIdent"/>the 'Sender' property references the sending partner, where the response is expected.</param>
    public void SendOut (WcfReqIdent id)
    {
      ClientIdent.SendOut(id); // post to service input queue
    }

    /// <summary>
    /// Send a response to the remote client belonging to this client-stub.
    /// </summary>
    /// <param name="rsp">A <see cref="WcfReqIdent"/>the 'Sender' property references the sending partner, where the response is expected.</param>
    public void PostInput( WcfReqIdent rsp )
    {
        if (rsp.MessageSaved == null)
        {
            rsp.MessageSaved = rsp.Message; // keep the first message as main response
        }
        else
        {
            if( rsp.NotifyList == null )
            {
                rsp.NotifyList = new WcfNotifyResponse();
            }
            rsp.NotifyList.Notifications.Add( rsp.Message );
        }
        rsp.SendId = ++ClientIdent.LastSentId;
    }

    /// <summary>
    /// Gets the Uri of a linked client.
    /// </summary>
    public Uri Uri {get{return ClientIdent.Uri;}}

    #endregion

  }// class WcfBasicServiceUser
}// namespace
