
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.ServiceModel;         // OperationContext
using System.Net;                  // Dns
using Remact.Net.Contracts;
using Newtonsoft.Json.Linq;

namespace Remact.Net.Internal
{
  /// <summary>
  /// <para>Class used on WCF service side.</para>
  /// <para>Represents a connected client.</para>
  /// <para>Has the possibility to store and forward notifications that are not expected by the client.</para>
  /// <para>This could also be handled by the more cumbersome 'DualHttpBinding'.</para>
  /// </summary>
  internal class WcfBasicServiceUser: IWcfBasicPartner, IWampRpcV1Server
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

    internal uint                      LastReceivedSendId;

    /// <summary>
    /// Set to 0 when a message has been received or sent. Incremented by milliseconds in TestNotificationChannel().
    /// </summary>
    internal int                       ChannelTestTimer;
    internal object                    ClientAccessLock   = new Object();

    private Wamp.WampClientProxy       m_wampProxy;
    private bool                       m_boDisconnectReq;
    private bool                       m_boTimeout;

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

    internal void UseDataFrom (ActorMessage remoteClient)
    {
        ClientIdent.UseDataFrom (remoteClient);
        UriBuilder uri = new UriBuilder (ClientIdent.Uri);
        uri.Scheme = ClientIdent.OutputSidePartner.Uri.Scheme; // the service's Uri scheme (e.g. http)
        ClientIdent.Uri = uri.Uri;
        //TODO m_wampProxy =
    }
    
    /// <summary>
    /// Shutdown the outgoing connection. Send a disconnect message to the partner.
    /// </summary>
    public void Disconnect ()
    {
        SetConnected( false, null );
        // communication continues with last message
    }

    /// <summary>
    /// Is the client connected ?
    /// </summary>
    public bool IsConnected
    {
      get {
          return !m_boDisconnectReq && !m_boTimeout && m_wampProxy != null;
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
    /// Was there an error that disconnected the client ?
    /// </summary>
    public bool IsFaulted {get{return m_boTimeout;}}


    /// <summary>
    /// Trace internal state of the client connection to this service
    /// </summary>
    /// <param name="mark">6 char, eg. 'Connec', 'Discon', 'Abortd'</param>
    public void TraceState(string mark)
    {
        if (m_wampProxy != null)
        {
            RaTrc.Info ("WcfSvc", "["+mark.PadRight (6)+"] "+ ClientMark
                            + ", ClientAddress=" + m_wampProxy.ClientAddress.ToString()
                            , ClientIdent.Logger );
        }
    }

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
        if (m_wampProxy != null)
        {
            try
            {
                if (ClientIdent.TimeoutSeconds > 0 
                 && ChannelTestTimer > ((ClientIdent.TimeoutSeconds+i_nMillisecondsPassed)*400)) // 2 messages per TimeoutSeconds-period
                {
                    SendNotification (new ReadyMessage());
                }
            }
            catch (Exception ex)
            {
                RaTrc.Exception( "Cannot test '" + ClientMark + "' from service", ex, ClientIdent.Logger );
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
        if (m_wampProxy == null) return;

        try
        {
            SetConnected( false, null );
            //TraceState("Abortd");
            m_wampProxy.Dispose();
            m_wampProxy = null;
        }
        catch (Exception ex)
        {
            m_wampProxy = null;
            RaTrc.Exception( "Cannot abort '" + ClientMark + "' from service", ex, ClientIdent.Logger );
        }
    }


    /// <summary>
    /// <para>Call SendNotification(...) to enqueue a notification message.</para>
    /// </summary>
    /// <param name="notification">a message not requested by the client</param>
    public void SendNotification(object notification)
    {
        ChannelTestTimer = 0;
        try
        {
            if (m_wampProxy == null)
            {
                RaTrc.Error( "WcfSvc", "Closed notification channel to " + ClientMark, ClientIdent.Logger );
            }
            else
            {
                m_wampProxy.Event(ClientIdent.Name, new JObject(notification));
            }
        }
        catch (Exception ex)
        {
            RaTrc.Exception( "Cannot notify '" + ClientMark + "' from service", ex, ClientIdent.Logger );
            m_boTimeout = true;
        }
    }// SendNotification

    /// <summary>
    /// Returns the number of notification responses, that have not been sent yet.
    /// </summary>
    public int OutstandingResponsesCount
    { get {
            //if (m_NotifyList != null) return m_NotifyList.Notifications.Count;
            return 0;
    }}

/*
    internal void StartNewRequest( Request id )
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
    public object GetNotificationsAndResponse(ref Request id)
    {
      if( id.Response == null )
      {
          id.SendResponse( new ReadyMessage() ); // no other information to return
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
*/

    #endregion
    //----------------------------------------------------------------------------------------------
    #region IWcfPartner implementation

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
    /// <param name="id">A <see cref="Request"/>the 'Sender' property references the sending partner, where the response is expected.</param>
    public void SendOut (Request id)
    {
        ClientIdent.SendOut(id); // post to service input queue
    }

    /// <summary>
    /// Send a response to the remote client belonging to this client-stub.
    /// </summary>
    /// <param name="rsp">A <see cref="Request"/>the 'Sender' property references the sending partner, where the response is expected.</param>
    public void PostInput( Request req )
    {
        m_wampProxy.CallResult(req.RequestId.ToString(), new JObject(req.Message));
    }

    /// <summary>
    /// Gets the Uri of a linked client.
    /// </summary>
    public Uri Uri {get{return ClientIdent.Uri;}}


    #endregion
    //----------------------------------------------------------------------------------------------
    #region IWampRpcV1ClientCallbacks implementation

    void IWampRpcV1Server.Call(IActorOutput client, string callId, string procUri, object[] arguments)
    {
        throw new NotImplementedException();
    }

    void IWampRpcV1Server.CallError(string callId, string errorUri, string errorDesc, object errorDetails)
    {
        throw new NotImplementedException();
    }

    #endregion
    //----------------------------------------------------------------------------------------------
  }
}
