
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Net;                  // Dns
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Remact.Net.Protocol;
using Remact.Net.Protocol.Wamp;

namespace Remact.Net.Remote
{
  /// <summary>
  /// <para>Class used on service side.</para>
  /// <para>Represents a connected client.</para>
  /// <para>Has the possibility to store and forward notifications that are not expected by the client.</para>
  /// <para>This could also be handled by the more cumbersome 'DualHttpBinding'.</para>
  /// </summary>
  internal class RemactServiceUser : IRemactProxy
  {
    //----------------------------------------------------------------------------------------------
    #region Properties

    /// <summary>
    /// <para>Detailed information about the client using this service.</para>
    /// <para>Contains the "UserContext" object that may be used freely by the service application.</para>
    /// <para>Output is linked to this service.</para>
    /// </summary>
    public   RemactPortClient               PortClient {get; private set;}
    private  RemactPortService                _serviceIdent;

    /// <summary>
    /// Set to 0 when a message has been received or sent. Incremented by milliseconds in TestNotificationChannel().
    /// </summary>
    internal int                       ChannelTestTimer;
    internal object                    ClientAccessLock   = new Object();

    private IRemactProtocolDriverCallbacks _protocolCallback;
    private bool                       m_boTimeout;

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Constructor + Methods

    /// <summary>
    /// Internally called to create a RemactServiceUser object
    /// </summary>
    /// <param name="serviceIdent">client using this service</param>
    public RemactServiceUser(RemactPortService serviceIdent)
    {
        _serviceIdent = serviceIdent;
        PortClient = new RemactPortClient()
        {
            SvcUser = this,
            IsMultithreaded = serviceIdent.IsMultithreaded,
            TraceConnect = serviceIdent.TraceConnect,
            TraceSend = serviceIdent.TraceSend,
            TraceReceive = serviceIdent.TraceReceive,
            Logger = serviceIdent.Logger,
        };

        PortClient.LinkToService(serviceIdent); // requests are posted to our service. Also creates a new TSC object if ServiceIdent is RemactPortService<TSC>
        PortClient.RedirectToProxy(this);  // the service posts notifications to svcUser, we will pass it to the remote client
    }// CTOR

    public void SetCallbackHandler(IRemactProtocolDriverCallbacks protocolCallback)
    {
        _protocolCallback = protocolCallback;
    }

    internal void UseDataFrom(ActorInfo remoteClient, int clientId)
    {
        PortClient.UseDataFrom(remoteClient);
        PortClient.OutputClientId = clientId;
        UriBuilder uri = new UriBuilder (PortClient.Uri);
        uri.Scheme = PortClient.OutputSidePartner.Uri.Scheme; // the service's Uri scheme (e.g. http)
        PortClient.Uri = uri.Uri;
    }

    public void SetConnected()
    {
        PortClient.SyncContext = _serviceIdent.SyncContext;
        PortClient.ManagedThreadId = _serviceIdent.ManagedThreadId;
        PortClient.m_isOpen = true;
        m_boTimeout = false;
        if (PortClient.Uri == null && _protocolCallback != null)
        {
            PortClient.Uri = _protocolCallback.ClientUri;
            PortClient.Name = "anonymous";
        }
    }

    /// <summary>
    /// Shutdown the outgoing connection. Send a disconnect message to the partner.
    /// </summary>
    public void Disconnect ()
    {
        PortClient.Disconnect();
    }

    /// <summary>
    /// Is the client connected ?
    /// </summary>
    public bool IsConnected
    {
      get {
          return PortClient.m_isOpen && !m_boTimeout && _protocolCallback != null;
      }
    }

    /// <summary>
    /// Was there an error that disconnected the client ?
    /// </summary>
    public bool IsFaulted {get{return m_boTimeout;}}

    /// <summary>
    /// Used for tracing messages from/to this client.
    /// </summary>
    public string ClientMark {get{return string.Format("{0}/{1}[{2}]", PortClient.HostName, PortClient.Name, PortClient.OutputClientId);}}

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
                if (PortClient.TimeoutSeconds > 0 
                 && ChannelTestTimer > ((PortClient.TimeoutSeconds+i_nMillisecondsPassed)*400)) // 2 messages per TimeoutSeconds-period
                {
                    SendNotification (new ReadyMessage());
                }
            }
            catch (Exception ex)
            {
                RaLog.Exception( "Cannot test '" + ClientMark + "' from service", ex, PortClient.Logger );
                m_boTimeout = true;
                return true; // changed
            }
        }

        if (IsConnected)
        {
            if (PortClient.TimeoutSeconds > 0 && ChannelTestTimer > PortClient.TimeoutSeconds*1000)
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
            //_protocolCallback.Dispose(); TODO
            _protocolCallback = null;
        }
        catch (Exception ex)
        {
            _protocolCallback = null;
            RaLog.Exception( "Cannot abort '" + ClientMark + "' from service", ex, PortClient.Logger );
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
                RaLog.Error( "RemactService", "Closed notification channel to " + ClientMark, PortClient.Logger );
            }
            else
            {
                var msg = new RemactMessage(null, 0, 0, null, null, notification);
                msg.MessageType = RemactMessageType.Notification;
                PostInput(msg);
            }
        }
        catch (Exception ex)
        {
            RaLog.Exception( "Cannot notify '" + ClientMark + "' from service", ex, PortClient.Logger );
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
    #region IRemoteActor implementation

    /// <summary>
    /// Dummy implementation. Client stub is always connected to the service.
    /// </summary>
    /// <returns>true</returns>
    Task<bool> IRemactProxy.TryConnect()
    {
        return RemactPort.TrueTask;
    }

    /// <summary>
    /// Send a response, error or notification to the remote client belonging to this client-stub.
    /// </summary>
    /// <param name="msg">A <see cref="RemactMessage"/>.</param>
    public void PostInput(RemactMessage msg)
    {
        if (_protocolCallback == null)
        {
            RaLog.Warning("RemactService", "Closed channel to " + ClientMark, PortClient.Logger);
        }
        else
        {
            var lower = new LowerProtocolMessage
            {
                Type = msg.MessageType,
                RequestId = msg.RequestId,
                Payload = msg.Payload
            };

            _protocolCallback.OnMessageFromService(lower);
        }
    }

    /// <summary>
    /// Gets the Uri of a linked client.
    /// </summary>
    public Uri Uri {get{return PortClient.Uri;}}


    #endregion
    //----------------------------------------------------------------------------------------------
  }
}
