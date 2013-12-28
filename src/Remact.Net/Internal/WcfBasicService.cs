
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Threading;
using System.ServiceModel;         // OperationContext
using System.Collections.Generic;

namespace Remact.Net.Internal
{
  /// <summary>
  /// <para>Class used on WCF service side, base of WcfServiceAssistant.</para>
  /// <para>Handles and stores all connected clients.</para>
  /// </summary>
  public class WcfBasicService
  {
    //----------------------------------------------------------------------------------------------
    #region Properties

    /// <summary>
    /// Detailed information about this service
    /// </summary>
    internal ActorInput ServiceIdent {get; private set;}
    
    /// <summary>
    /// The count of known clients of this service (connected or disconnected)
    /// </summary>
    public   int       ClientCount { get { return ServiceIdent.InputClientList.Count - m_UnusedClientCount; } }

    /// <summary>
    /// The count of connected clients of this service
    /// </summary>
    public   int       ConnectedClientCount { get { return m_ConnectedClientCount; } }

    /// <summary>
    /// May be used for tracing of connect/reconnect/disconnect operations.
    /// </summary>L
    public   string    LastAction;
    
    /// <summary>
    /// Default = false. When set to true: Disable router client, no service in this application will be published to the WcfRouter.
    /// </summary>
    public static bool DisableRouterClient
    {
      get { return WcfRouterClient.Instance ().DisableRouterClient; }
      set { WcfRouterClient.Instance ().DisableRouterClient = value; }
    }

    /// <summary>
    /// True if any client has been connected or disconnected. Set to false by DoPeriodicTasks()
    /// </summary>
    internal bool                      HasConnectionStateChanged = true;

    /// <summary>
    /// Internally used by RouterClient
    /// </summary>
    internal  bool                     IsServiceRegistered;
    
    /// <summary>
    /// Internally used for periodic message to WcfRouterService
    /// </summary>
    internal DateTime                  NextEnableMessage;

    private  int                       m_FirstClientId;          // offset, normally = 1
    private  int                       m_UnusedClientCount = 0;  // disconnected clients having WcfDefault.IsProcessIdUsed
    private  int                       m_ConnectedClientCount = 0;
    private  int                       m_millisPeriodicTask = 0; // systemstart = 0
    private  bool                      m_boCurrentlyCalled;      // to check concurrent calls
#if BEFORE_NET40
    private  int                       m_nLastThreadId = 0;      // to check calls from different synchronization contexts
#endif


    #endregion
    //----------------------------------------------------------------------------------------------
    #region Constructors / shutdown

    /// <summary>
    /// Create a WcfBasicService object
    /// </summary>
    /// <param name="serviceIdent">a ActorInput having a unique name for the service.</param>
    /// <param name="firstClientId">client Id's start with this number, normally = 1.</param>
    /// <param name="maxClients">initial capacity of the User List.</param>
    internal WcfBasicService (ActorInput serviceIdent, int firstClientId, int maxClients)
    {
      ServiceIdent = serviceIdent;
      ServiceIdent.IsServiceName = true;
      ServiceIdent.InputClientList = new List<ActorOutput> (maxClients);
      if (firstClientId > 0) m_FirstClientId = firstClientId;
                        else m_FirstClientId = 1;
    }// CTOR1


    /// <summary>
    /// Create a WcfBasicService object, used by AsyncWcfRouter (only).
    /// </summary>
    /// <param name="serviceName">a unique service name.</param>
    /// <param name="serviceUri">the Uri for the service.</param>
    /// <param name="firstClientId">client Id's start with this number, normally = 1.</param>
    /// <param name="maxClients">initial capacity of the User List.</param>
    internal WcfBasicService(string serviceName, Uri serviceUri, int firstClientId, int maxClients)
    {
      ServiceIdent = new ActorInput (serviceName);
      ServiceIdent.Uri = serviceUri;
      ServiceIdent.IsServiceName = true;
      ServiceIdent.InputClientList = new List<ActorOutput> (maxClients);
      if (firstClientId > 0) m_FirstClientId = firstClientId;
                        else m_FirstClientId = 1;
    }// CTOR2


    /// <summary>
    /// <para>Shutdown this service and release all attached resources in subclasses</para>
    /// <para>(e.g. ServiceHost, RouterClient + WcfRouterService entry)</para>
    /// </summary>
    internal virtual void Disconnect()
    {
    }// overridden by WcfServiceAssistant


    /// <summary>
    /// <para>Abort all notification connections, do not send any messages.</para>
    /// <para>Should be called before closing the service host.</para>
    /// </summary>
    internal void AbortUserNotificationChannels()
    {
        foreach (ActorOutput clt in ServiceIdent.InputClientList)
        {
            if (clt.SvcUser != null) clt.SvcUser.AbortNotificationChannel();
        }
    }// AbortUserNotificationChannels

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Client connect / disconnect

    // Internally called to create an ActorOutput as client stub.
    internal virtual WcfBasicServiceUser AddNewSvcUser (WcfPartnerMessage receivedClientMsg, int index)
    {
      if (index < 0) // add a new element
      {
        ServiceIdent.InputClientList.Add (null);
        index = ServiceIdent.InputClientList.Count-1;
      }

      var clt = new ActorOutput ();
      WcfBasicServiceUser svcUser = new WcfBasicServiceUser (clt, index + m_FirstClientId);
      clt.SvcUser = svcUser;
      clt.LinkOutputTo (ServiceIdent); // requests are posted to our service. Also creates a new TSC object if ServiceIdent is ActorInput<TSC>
      clt.IsMultithreaded = ServiceIdent.IsMultithreaded;
      clt.TraceConnect = ServiceIdent.TraceConnect;
      clt.TraceSend = ServiceIdent.TraceSend;
      clt.TraceReceive = ServiceIdent.TraceReceive;
      clt.Logger = ServiceIdent.Logger;
      clt.PassResponsesTo( svcUser );   // this service posts notifications to svcUser, it will pass it to the remote client
      svcUser.UseDataFrom (receivedClientMsg);
      ServiceIdent.InputClientList[index] = clt;
      return svcUser;
    }


    /// <summary>
    /// Connect / Reconnect a client to this service
    /// </summary>
    /// <param name="client">Request message</param>
    /// <param name="req">the WcfReqIdent to be used for responses.</param>
    /// <param name="svcUser">Output the user object containing a "ClientIdent.UserContext" object for free application use</param>
    /// <returns>Service info as response</returns>
    private IWcfMessage ConnectPartner (WcfPartnerMessage client, WcfReqIdent req, out WcfBasicServiceUser svcUser)
    {
      svcUser = null;
      if (req.ClientId != 0)
      {// Client war schon mal verbunden
        int i = req.ClientId - m_FirstClientId;
        if (i >= 0 && i < ServiceIdent.InputClientList.Count + 100)
        {
          // Nach dem Restart eines Service können sich Clients mit der alten Nummer anmelden
          while (ServiceIdent.InputClientList.Count < i) ServiceIdent.InputClientList.Add (null);

          svcUser = ServiceIdent.InputClientList[i].SvcUser;
          if (svcUser == null)
          {
            svcUser = AddNewSvcUser (client, i);
            LastAction = "Reconnect after service restart";
          }
          else if (!client.IsEqualTo (svcUser.ClientIdent))
          {
              WcfTrc.Warning( req.SvcRcvId, svcUser.ClientIdent.ToString( "ClientId already used", 0 ), ServiceIdent.Logger );
            req.ClientId = 0; // eine neue ID vergeben, kann passieren, wenn Service, aber nicht alle Clients durchgestartet werden
            m_ConnectedClientCount -= 2; // wird sofort 2 mal inkrementiert
          }
          else if (svcUser.IsConnected)
          {
            LastAction = "Reconnect, no disconnect";
            WcfTrc.Warning( req.SvcRcvId, svcUser.ClientIdent.ToString( LastAction, 0 ), ServiceIdent.Logger );
            svcUser.UseDataFrom (client);
            m_ConnectedClientCount--; // wird sofort wieder inkrementiert
          }
          else if (svcUser.IsFaulted)
          {
            LastAction = "Reconnect after network failure";
            WcfTrc.Warning( req.SvcRcvId, svcUser.ClientIdent.ToString( LastAction, 0 ), ServiceIdent.Logger );
            svcUser.UseDataFrom (client);
            if (WcfDefault.Instance.IsProcessIdUsed (svcUser.ClientIdent.ProcessId)) m_UnusedClientCount--;
          }
          else
          {
            svcUser.UseDataFrom (client);
            LastAction = "Reconnect after client disconnect";
            if (WcfDefault.Instance.IsProcessIdUsed (svcUser.ClientIdent.ProcessId)) m_UnusedClientCount--;
          }
          m_ConnectedClientCount++;
        }
        else
        {
          WcfErrorMessage rsp = new WcfErrorMessage (WcfErrorMessage.Code.ClientIdNotFoundOnService, "Service cannot find client " + req.ClientId + " to connect");
          WcfTrc.Error( req.SvcRcvId, rsp.Message, ServiceIdent.Logger );
          LastAction = "ClientId mismatch while connecting";
          return rsp;
        }
      }

      if (req.ClientId == 0)
      {// Client wurde neu gestartet
        int found = ServiceIdent.InputClientList.FindIndex (c => client.IsEqualTo (c));
        if (found < 0)
        {
          svcUser = AddNewSvcUser (client, found);
          LastAction = "Connect first time";
          m_ConnectedClientCount++;
        }
        else
        {
          svcUser = ServiceIdent.InputClientList[found].SvcUser;
          if( svcUser.IsConnected )
          {
              LastAction = "Client is reconnecting";
          }
          else
          {
              LastAction = "Reconnect after client restart";
              m_ConnectedClientCount++;
          }
          svcUser.UseDataFrom (client);
        }
      }

      // Connection state is kept in client object
      svcUser.ChannelTestTimer = 0;
      svcUser.SetConnected( true, ServiceIdent );
      svcUser.OpenNotificationChannel();
      
      // reply ServiceIdent
      WcfPartnerMessage response = new WcfPartnerMessage (ServiceIdent, WcfPartnerMessage.Use.ServiceConnectResponse);
      req.Sender   = svcUser.ClientIdent;
      req.ClientId = svcUser.ClientId;
      req.SendId   = 1;                   // connected

      // Change RequestId after preparing the response as on first connect: client == svcUser.ClientIdent
      svcUser.LastReceivedSendId     = 1; // represents the SendId of the last received request from this client
      svcUser.ClientIdent.LastSentId = 1; // represents the SendId of the last response sent to this client

      return response;
    }// Connect


    /// <summary>
    /// Mark a client as (currently) disconnected
    /// </summary>
    /// <param name="client">Request message</param>
    /// <param name="req">the WcfReqIdent to be used for responses.</param>
    /// <param name="svcUser">Output the user object containing a "ClientIdent.UserContext" object for free application use</param>
    /// <returns>Service info as response</returns>
    private IWcfMessage DisconnectPartner (WcfPartnerMessage client, WcfReqIdent req, out WcfBasicServiceUser svcUser)
    {
      int i = req.ClientId - m_FirstClientId;
      if (i >= 0 && i < ServiceIdent.InputClientList.Count)
      {
        svcUser = ServiceIdent.InputClientList[i].SvcUser;
        svcUser.ChannelTestTimer = 0;
        //req.CurrentSvcUser = svcUser;
        req.Sender = svcUser.ClientIdent;
        HasConnectionStateChanged = true;
        if (client.IsEqualTo (svcUser.ClientIdent))
        {
          svcUser.SetConnected( false, ServiceIdent );    // disconnected
          LastAction = "Disconnect";
          if (WcfDefault.Instance.IsProcessIdUsed (svcUser.ClientIdent.ProcessId))
          {
            m_UnusedClientCount++;
            ServiceIdent.InputClientList[i] = null; // will never be used again, the client has been shutdown
          }
          m_ConnectedClientCount--;
        }
        else
        {
          LastAction = "ClientId mismatch while disconnecting";
          WcfTrc.Error( req.SvcRcvId, LastAction + ": " + client.Uri, ServiceIdent.Logger );
        }
      }
      else
      {
        svcUser = null;
        WcfTrc.Error( req.SvcRcvId, "Cannot disconnect client" + req.ClientId, ServiceIdent.Logger );
        LastAction = "Disconnect unknown client";
      }

      WcfPartnerMessage response = new WcfPartnerMessage (ServiceIdent, WcfPartnerMessage.Use.ServiceDisconnectResponse);
      return response;
    }// Disconnect


    /// <summary>
    /// Set client info into the message, call it once for each request to check the connection.
    /// </summary>
    /// <param name="req">Request message</param>
    /// <param name="svcUser">Output the user object containing a "ClientIdent.UserContext" object for free application use.</param>
    /// <returns>True, when the client has been found. False, when no client has been found and an error message must be generated.</returns>
    internal bool FindPartnerAndCheck (WcfReqIdent req, out WcfBasicServiceUser svcUser)
    {
      int i = req.ClientId - m_FirstClientId;
      if (i >= 0 && i < ServiceIdent.InputClientList.Count)
      {
        svcUser = ServiceIdent.InputClientList[i].SvcUser;
        svcUser.ChannelTestTimer = 0;
        req.Sender = svcUser.ClientIdent;

        if (!svcUser.IsConnected)
        {
          WcfTrc.Error( req.SvcRcvId, "Reconnect without ConnectRequest, SendId = " + req.SendId, ServiceIdent.Logger );
          svcUser.SetConnected( true, ServiceIdent );
          svcUser.OpenNotificationChannel();
          if (WcfDefault.Instance.IsProcessIdUsed (svcUser.ClientIdent.ProcessId)) m_UnusedClientCount--;
          m_ConnectedClientCount++;
        }
        
        if (svcUser.LastReceivedSendId == uint.MaxValue) svcUser.LastReceivedSendId = 10; // 0..10 is reserved
        if (req.SendId != ++svcUser.LastReceivedSendId)
        {
            WcfTrc.Warning( req.SvcRcvId, string.Format( "received SendId = {0}, expected = {1}", req.SendId, svcUser.LastReceivedSendId ), ServiceIdent.Logger );
            svcUser.LastReceivedSendId = req.SendId;
        }
        if (req.SendId==2) HasConnectionStateChanged = true; // Trace state after first regular request
        return true;
      }

      svcUser = null;
      return false;
    }// FindPartnerAndCheck


    /// <summary>
    /// Check if response can be generated by library or if an application message is required.
    /// </summary>
    /// <param name="req">The WcfReqIdent contains the request. It is used for the response also.</param>
    /// <param name="svcUser">output the user object containing a "ClientIdent.SenderContext" object for free application use</param>
    /// <returns><para> null when the response has to be generated by the application.</para>
    ///          <para>!null if the response already has been generated by this class.</para></returns>
    internal IWcfMessage CheckBasicResponse(WcfReqIdent req, out WcfBasicServiceUser svcUser)
    {
      if (m_boCurrentlyCalled)
      {
        WcfTrc.Error ("WcfBasicService", "called by multiple threads", ServiceIdent.Logger);
      }
      m_boCurrentlyCalled = true;
      
#if BEFORE_NET40
      var m = req.Message as IExtensibleWcfMessage;
      if (!ServiceIdent.IsMultithreaded)
      {
        int thread = Thread.CurrentThread.ManagedThreadId;
        if (thread != m_nLastThreadId && m_nLastThreadId != 0)
        {
          WcfTrc.Warning ("WcfBasicService", "calling thread changed from "+m_nLastThreadId+" to "+thread, ServiceIdent.Logger);
        } 
        m_nLastThreadId = thread;
        if( m != null ) m.BoundSyncContext = SynchronizationContext.Current;
      }
      if( m != null ) m.IsSent = true;
#endif
      
      req.Input = ServiceIdent;
      req.DestinationLambda = null;// make sure to call the DefaultHandler
      svcUser = null;
      IWcfMessage response = null;
      WcfPartnerMessage cltReq = req.Message as WcfPartnerMessage;
      if (cltReq != null)
      {
        if (ServiceIdent.Uri == null)
        {//first call after ServiceHost.Open(), WcfServiceAssistant will initialize the URI earlier.
          UriBuilder uri = new UriBuilder (OperationContext.Current.Channel.LocalAddress.Uri);
          uri.Host = ServiceIdent.HostName;
          ServiceIdent.Uri = uri.Uri;
        }

        switch (cltReq.Usage)
        {
        case WcfPartnerMessage.Use.ClientConnectRequest:    response = ConnectPartner    (cltReq, req, out svcUser); break;
        case WcfPartnerMessage.Use.ClientDisconnectRequest: response = DisconnectPartner (cltReq, req, out svcUser); break;
        default: break;// continue below
        }
      }
      
      if (response == null)
      {
        // no response generated yet (no WcfPartner-message or unknown Usage)
        if (FindPartnerAndCheck (req, out svcUser))
        {
          //req.CurrentSvcUser = svcUser;
          LastAction = "Request";// response must be generated by service-application, request.ClientIdent has been set
        }
        else
        {
          response = new WcfErrorMessage (WcfErrorMessage.Code.ClientIdNotFoundOnService, "Service cannot find client " + req.ClientId);
          WcfTrc.Error( req.SvcRcvId, (response as WcfErrorMessage).Message, ServiceIdent.Logger );
          //response.SendId bleibt 0, da wir keine ClientInfo haben 
          LastAction = "Request from unknown client";
        }
      }

      m_boCurrentlyCalled = false;
      return response;
    }// CheckBasicResponse

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Public methods
    
    /// <summary>
    /// <para>Check client connection-timeouts, should be called periodically.</para>
    /// </summary>
    /// <returns>True, when a client state has changed</returns>
    public bool DoPeriodicTasks()
    {
      bool boChange = HasConnectionStateChanged;
      HasConnectionStateChanged = false;
      
      int nConnected = 0;
      int nUnused = 0;
      int millisCurrent = Environment.TickCount;
      int deltaT = millisCurrent - m_millisPeriodicTask;
      if (deltaT < 0 || deltaT > 3600000) deltaT = 0;

      for (int i=0; i<ServiceIdent.InputClientList.Count; i++)
      {
        WcfBasicServiceUser u = ServiceIdent.InputClientList[i].SvcUser;
        if (u == null) continue;

        if (u.TestChannel (deltaT))
        { 
          boChange = true;
          if (u.IsFaulted)
          {
            WcfTrc.Warning( "Svc=" + ServiceIdent.Name, u.ClientIdent.ToString( "Timeout=" + u.ClientIdent.TimeoutSeconds + " sec. no message from clt[" + u.ClientId + "]", 0 ), ServiceIdent.Logger );
            if (WcfDefault.Instance.IsProcessIdUsed(u.ClientIdent.ProcessId))
            {
              m_UnusedClientCount++;
              ServiceIdent.InputClientList[i] = null;// will never be used again, the client has been shutdown
            }
            m_ConnectedClientCount--;
            //u.TraceState("");
          }
        }
        
        if (u.IsConnected) {
          nConnected++;
        } else if (WcfDefault.Instance.IsProcessIdUsed(u.ClientIdent.ProcessId)) {
          nUnused++;
        } 
      }
      m_millisPeriodicTask = millisCurrent;
      
      m_ConnectedClientCount = nConnected;
      m_UnusedClientCount    = nUnused;
      return boChange;
    }// DoPeriodicTasks
      
    
    #endregion
  }// class WcfBasicService
}// namespace
