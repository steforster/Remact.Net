
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.ServiceModel; // EndpointAddress
using System.Collections.Generic;
using System.Threading;    // Timer

namespace SourceForge.AsyncWcfLib.Basic
{
  
  
  /// <summary>
  /// A internally used singleton object for all WcfServiceAssistants and WcfClientAsync to register/lookup a service with WcfRouter
  /// </summary>
  internal class WcfRouterClient
  {
    //----------------------------------------------------------------------------------------------
    #region Fields

    private static WcfRouterClient           ms_Instance;
    private static object                    ms_Lock = new Object();
    private static bool                      ms_DisableRouterClient;

#if !BEFORE_NET45
    private WcfBasicClientAsyncAwait         m_RouterClient;
#else
    private WcfBasicClientAsync              m_RouterClient;
#endif

    private        List<WcfBasicClientAsync> m_ClientList;
    private        List<WcfBasicService>     m_ServiceList;
    private        int                       m_nCurrentSvc;
    private        Timer                     m_Timer;
    private        bool                      m_Running;
    private        int                       m_nConnectTries;


    internal bool DisableRouterClient
    {
      get { return ms_DisableRouterClient; }
      set {
        ms_DisableRouterClient = value;
        if (!ms_DisableRouterClient)
        {
          ms_Instance.m_Timer.Change (0, 1000);// connect in Timer thread
        }
      }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Events

    // running on threadpool timer thread
    private void OnTimerTick (object state)
    {
      if (m_Running) return;
      m_Running = true;
      try
      { //-------------------------------
        if (m_nCurrentSvc == -100) // Disconnect all services and clients
        {
          if (ms_Instance != null && m_ServiceList != null)
          {
            int nServices = m_ServiceList.Count;
            lock (ms_Lock)
            {
              while (m_ServiceList.Count > 0)
              {
                m_ServiceList[0].Disconnect(); // shutdown all services and send ServiceDisable messages to WcfRouterService when overloaded in WcfClientAsync
              }
              
              while (m_ClientList.Count > 0)
              {
                m_ClientList[0].Disconnect (); // shutdown all clients and send ClientDisconnectRequest to its connected service
              }
            }
            if (nServices > 0) Thread.Sleep (20 + (nServices*10));  // let the communication end
            Disconnect ();                                          // shutdown the RouterClient itself
          }
        }//-------------------------------
        else if (m_RouterClient.IsFaulted)
        {
          if (m_nConnectTries < 0) WcfTrc.Error ("Wcf", "Router client in Fault state !", WcfApplication.Logger );
          m_RouterClient.AbortCommunication ();
          m_Timer.Change (15000, 1000); // 15 s warten und neu starten
        }//-------------------------------
        else if (m_RouterClient.IsDisconnected)
        {
          if (!ms_DisableRouterClient)
          {
            //m_nSendingThreadId = Thread.CurrentThread.ManagedThreadId;
            if (++m_nConnectTries >= 10) m_nConnectTries = 1;
            var uri = new Uri("http://localhost:" + WcfDefault.Instance.RouterPort + "/" + WcfDefault.WsNamespace + "/" + WcfDefault.Instance.RouterServiceName);
            m_RouterClient.TryConnectVia( uri, OnWcfMessageReceived, toRouter:true );
          }
          lock (ms_Lock)
          { // ev. wurde der RouterService neu gestartet
            foreach (WcfBasicService s in m_ServiceList)
            {
              s.IsServiceRegistered = false; // Status zurücksetzen, so dass er wieder gemeldet wird
            }
          }
        }//-------------------------------
        else if (m_RouterClient.IsConnected)
        {
          m_nConnectTries = -1;
          if (m_ServiceList.Count <= m_nCurrentSvc)
          {
            m_nCurrentSvc = 0; // keine Svc oder Maximum erreicht
          }
          else
          {
            lock (ms_Lock)
            {
              WcfPartnerMessage req = new WcfPartnerMessage (m_ServiceList[m_nCurrentSvc].ServiceIdent,
                                                             WcfPartnerMessage.Use.ServiceEnableRequest);
              WcfBasicService svc = m_ServiceList[m_nCurrentSvc];
              if (!svc.IsServiceRegistered)
              {
                svc.NextEnableMessage = DateTime.Now.AddSeconds(20);
                svc.IsServiceRegistered = true;
                WcfReqIdent id = m_RouterClient.SendOut (req);
                WcfTrc.Info( id.CltSndId, "send to WcfRouter: " + req.ToString(), WcfApplication.Logger );// id.CltSndId is updated in Send()

              }
              else if (m_ServiceList[m_nCurrentSvc].NextEnableMessage < DateTime.Now)
              {
                m_ServiceList[m_nCurrentSvc].NextEnableMessage  = DateTime.Now.AddSeconds(20);
                m_RouterClient.SendOut (req);
                //WcfTrc.Info (req.CltSndId, "Alive    "+req.ToString ()); // req.CltSndId is updated in Send()
              }
              m_nCurrentSvc++; // next Svc on next timer event
            }
          }
        }//connected

        if (ms_DisableRouterClient && m_Timer != null) m_Timer.Change (Timeout.Infinite, 1000); // stop the timer
      }
      catch (Exception ex)
      {
          WcfTrc.Exception( "during RouterClient timer", ex, WcfApplication.Logger );
      }
      m_Running = false;
    }// OnTimerTick

    
    // Response callback from WcfRouterService
    private void OnWcfMessageReceived (WcfReqIdent rsp)
    {
      if (rsp.Message is WcfErrorMessage)
      {
        if (m_nConnectTries % 20 == 1) {
          WcfErrorMessage err = rsp.Message as WcfErrorMessage;
          if (err.Error == WcfErrorMessage.Code.ServiceNotRunning
           || err.Error == WcfErrorMessage.Code.RouterNotRunning)
          {
              WcfTrc.Warning( rsp.CltRcvId, "WCF router service not running at  '" + rsp.Sender.Uri + "'", WcfApplication.Logger );
          }
          else
          {
              WcfTrc.Warning( rsp.CltRcvId, err.ToString(), WcfApplication.Logger );
          }
        }
      }
      else if (rsp.Message is WcfPartnerMessage && m_ServiceList != null)
      {
          m_Timer.Change (20, 1000); // 20ms warten und nächsten Request senden, bis alle erledigt sind
      }
      else
      {
          WcfTrc.Info( rsp.CltRcvId, rsp.ToString(), WcfApplication.Logger );
      }
    }// OnWcfMessageReceived
    

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Constructors

    /// <summary>
    /// (static) Get or Create the WcfRouterClient singleton 
    /// </summary>
    /// <returns>singleton instance</returns>
    internal static WcfRouterClient Instance ()
    {
      if (ms_Instance == null)
      {
        ms_Instance = new WcfRouterClient ();
      }
      return ms_Instance;
    }


    /// <summary>
    /// Initializes a new instance of the WcfRouterClient class.
    /// </summary>
    internal WcfRouterClient ()
    {
      m_ServiceList  = new List<WcfBasicService> (20);
      m_ClientList   = new List<WcfBasicClientAsync> (20);
#if !BEFORE_NET45
      m_RouterClient = new WcfBasicClientAsyncAwait("WcfRouterClt", OnWcfMessageReceived);
#else
      m_RouterClient = new WcfBasicClientAsync("WcfRouterClt", OnWcfMessageReceived);
#endif
      m_RouterClient.ClientIdent.IsMultithreaded = true;
      m_RouterClient.ClientIdent.TraceConnect = false;
      m_Timer        = new Timer (OnTimerTick, this, 1000, 1000); // startet in 1s, Periode=1s
    }// CTOR


    /// <summary>
    /// (static) Close all incoming network connections and send a ServiceDisable messages to WcfRouterService.
    ///          Disconnects all outgoing network connections and send ClientDisconnectRequest to connected services.
    /// </summary>
    internal static void DisconnectAll ()
    {
      if (ms_Instance != null && ms_Instance.m_ServiceList != null)
      {
        // Send disconnect messages on the ThreadPool timer thread.
        // Responses are routed to the normal synchronization context of services or clients.
        if (ms_Instance.m_Running) Thread.Sleep (20);
        ms_Instance.m_nCurrentSvc = -100;    // Markierung für Dispose
        ms_Instance.m_Timer.Change (0, 1000);// Im Timer Thread ausführen, damit Responses dort verarbeitet werden
        int n = 0;
        while (ms_Instance != null && n < 500) {Thread.Sleep (20); n += 20;}
      }
    }// WcfRouterClient.DisconnectAll


    /// <summary>
    /// Shutdown the RouterClient, send disconnect message to WcfRouterService if possible
    /// </summary>
    internal void Disconnect ()
    {
      if (m_Timer != null)
      {
        m_Timer.Dispose();
        int n = m_RouterClient.OutstandingResponsesCount;
        if (!ms_DisableRouterClient || n != 0 || !m_RouterClient.IsDisconnected)
        {
          if (m_RouterClient.IsConnected)
          {
            try
            {
              m_RouterClient.Disconnect(); // send last messages, contrary to AbortCommunication();
              m_RouterClient = null;
              WcfTrc.Info( "Wcf", "Router client disconnected.", WcfApplication.Logger );
            }
            catch
            {
            }
          }

          if( m_RouterClient != null && !m_RouterClient.IsDisconnected )
          {
            m_RouterClient.AbortCommunication();
            WcfTrc.Error( "Wcf", "Router client aborted, outstanding responses = " + n, WcfApplication.Logger );
          }
        }
        
        m_Timer = null;
        m_RouterClient = null;
        m_ServiceList.Clear();
        m_ServiceList = null;
        ms_Instance = null;
      }
    }// Disconnect

    
    #endregion
    //----------------------------------------------------------------------------------------------
    #region Public Methods

    /// <summary>
    /// Add a local WcfServiceAssistant for registration with WcfRouterService
    /// </summary>
    /// <param name="svc">the local WcfServiceAssistant</param>
    internal void AddService (WcfBasicService svc)
    {
      lock (ms_Lock)
      {
        m_ServiceList.Add (svc);
        svc.IsServiceRegistered = false; // triggert EnableMessage
      }
    }// AddService

    
    /// <summary>
    /// Remove a local WcfServiceAssistant, send disable-message to WcfRouterService
    /// </summary>
    /// <param name="svc">the local WcfServiceAssistant</param>
    internal void RemoveService (WcfBasicService svc)
    {
      lock (ms_Lock)
      {
        int n = m_ServiceList.IndexOf (svc);
        if (n < 0) return; // already removed
        if (m_RouterClient != null && m_RouterClient.IsConnected)
        {
          WcfPartnerMessage req = new WcfPartnerMessage (m_ServiceList[n].ServiceIdent, 
                                                         WcfPartnerMessage.Use.ServiceDisableRequest);
          WcfReqIdent id = m_RouterClient.SendOut (req);
          WcfTrc.Info( id.CltSndId, "Disable  " + req.ToString(), WcfApplication.Logger );
          m_ServiceList[n].IsServiceRegistered = false;
        }
        m_ServiceList.RemoveAt(n);
      }
    }// RemoveService


    /// <summary>
    /// Add a local WcfBasicClientAsync for handling in DisconnectAll
    /// </summary>
    /// <param name="clt">the local WcfBasicClientAsync</param>
    internal void AddClient (WcfBasicClientAsync clt)
    {
      if (clt == m_RouterClient) return; // do not add the RouterClient itself
      lock (ms_Lock)
      {
        m_ClientList.Add (clt);
      }
    }// AddClient


    /// <summary>
    /// Remove a local WcfBasicClientAsync while Disconnecting.
    /// </summary>
    /// <param name="clt">the local WcfBasicClientAsync</param>
    internal void RemoveClient (WcfBasicClientAsync clt)
    {
      lock (ms_Lock)
      {
        int n = m_ClientList.IndexOf (clt);
        if (n < 0) return; // already removed
        m_ClientList.RemoveAt (n);
      }
    }// RemoveClient

    #endregion

  }//class WcfRouterClient
}//namespace
