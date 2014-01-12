
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.ServiceModel;         // OperationContext
using System.Net;                  // Dns
using Newtonsoft.Json.Linq;
using Remact.Net.Protocol;
using Remact.Net.Protocol.Wamp;

namespace Remact.Net.Internal
{
  /// <summary>
  /// <para>Class used on WCF service side.</para>
  /// <para>Represents a connected client.</para>
  /// <para>Has the possibility to store and forward notifications that are not expected by the client.</para>
  /// <para>This could also be handled by the more cumbersome 'DualHttpBinding'.</para>
  /// </summary>
  public class WcfBasicServiceUser : IWcfBasicPartner
  {
    //----------------------------------------------------------------------------------------------
    #region Properties

    /// <summary>
    /// <para>Detailed information about the client using this service.</para>
    /// <para>Contains the "UserContext" object that may be used freely by the service application.</para>
    /// <para>Output is linked to this service.</para>
    /// </summary>
    public   ActorOutput               ClientIdent {get; private set;}
    private  ActorInput                _serviceIdent;

    /// <summary>
    /// Set to 0 when a message has been received or sent. Incremented by milliseconds in TestNotificationChannel().
    /// </summary>
    internal int                       ChannelTestTimer;
    internal object                    ClientAccessLock   = new Object();

    private IRemactProtocolDriverCallbacks _protocolCallback;
    private bool                       m_boDisconnectReq;
    private bool                       m_boTimeout;

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Constructor + Methods

    /// <summary>
    /// Internally called to create a WcfBasicServiceUser object
    /// </summary>
    /// <param name="serviceIdent">client using this service</param>
    public WcfBasicServiceUser(ActorInput serviceIdent)
    {
        _serviceIdent = serviceIdent;
        ClientIdent = new ActorOutput()
        {
            SvcUser = this,
            IsMultithreaded = serviceIdent.IsMultithreaded,
            TraceConnect = serviceIdent.TraceConnect,
            TraceSend = serviceIdent.TraceSend,
            TraceReceive = serviceIdent.TraceReceive,
            Logger = serviceIdent.Logger,
        };

        ClientIdent.LinkOutputTo(serviceIdent); // requests are posted to our service. Also creates a new TSC object if ServiceIdent is ActorInput<TSC>
        ClientIdent.PassResponsesTo(this);  // the service posts notifications to svcUser, we will pass it to the remote client
    }// CTOR

    public void SetCallbackHandler(IRemactProtocolDriverCallbacks protocolCallback)
    {
        _protocolCallback = protocolCallback;
    }

    internal void UseDataFrom(ActorInfo remoteClient, int clientId)
    {
        ClientIdent.UseDataFrom(remoteClient);
        ClientIdent.OutputClientId = clientId;
        UriBuilder uri = new UriBuilder (ClientIdent.Uri);
        uri.Scheme = ClientIdent.OutputSidePartner.Uri.Scheme; // the service's Uri scheme (e.g. http)
        ClientIdent.Uri = uri.Uri;
    }

    public void SetConnected()
    {
        ClientIdent.SyncContext = _serviceIdent.SyncContext;
        ClientIdent.ManagedThreadId = _serviceIdent.ManagedThreadId;
        ClientIdent.m_Connected = true;
        m_boDisconnectReq = false; 
        m_boTimeout = false;
    }

    /// <summary>
    /// Shutdown the outgoing connection. Send a disconnect message to the partner.
    /// </summary>
    public void Disconnect ()
    {
        ClientIdent.Disconnect();
        m_boDisconnectReq = true;
        // communication continues with last message
    }

    /// <summary>
    /// Is the client connected ?
    /// </summary>
    public bool IsConnected
    {
      get {
          return !m_boDisconnectReq && !m_boTimeout && _protocolCallback != null;
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
        if (_protocolCallback != null)
        {
            RaTrc.Info ("WcfSvc", "["+mark.PadRight (6)+"] "+ ClientMark
                            + ", ClientAddress=" + _protocolCallback.ClientAddress
                            , ClientIdent.Logger );
        }
    }

    /// <summary>
    /// Used for tracing messages from/to this client.
    /// </summary>
    public string ClientMark {get{return string.Format("{0}/{1}[{2}]", ClientIdent.HostName, ClientIdent.Name, ClientIdent.OutputClientId);}}

    /// <summary>
    /// Internally called by DoPeriodicTasks(), notifies idle messages and disconnects the client connection in case of failure.
    /// </summary>
    /// <returns>True, when connection state has changed.</returns>
    public bool TestChannel(int i_nMillisecondsPassed)
    {
        if (_protocolCallback != null)
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
                Disconnect();
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
        if (_protocolCallback == null) return;

        try
        {
            Disconnect();
            //TraceState("Abortd");
            //_protocolCallback.Dispose(); TODO
            _protocolCallback = null;
        }
        catch (Exception ex)
        {
            _protocolCallback = null;
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
            if (_protocolCallback == null)
            {
                RaTrc.Error( "WcfSvc", "Closed notification channel to " + ClientMark, ClientIdent.Logger );
            }
            else
            {
                var message = new ActorMessage(null, 0, 0, null, null, notification);
                message.Type = ActorMessageType.Notification;
                _protocolCallback.MessageFromService(message);
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

    #endregion
    //----------------------------------------------------------------------------------------------
    #region IWcfBasicPartner implementation

    /// <summary>
    /// Dummy implementation. Client stub is always connected to the service.
    /// </summary>
    /// <returns>true</returns>
    bool IWcfBasicPartner.TryConnect()
    {
      return true;
    }

    /// <summary>
    /// Send a response to the remote client belonging to this client-stub.
    /// </summary>
    /// <param name="response">A <see cref="ActorMessage"/>.</param>
    public void PostInput(ActorMessage response)
    {
        _protocolCallback.MessageFromService(response);
    }

    /// <summary>
    /// Gets the Uri of a linked client.
    /// </summary>
    public Uri Uri {get{return ClientIdent.Uri;}}


    #endregion
    //----------------------------------------------------------------------------------------------
  }
}
