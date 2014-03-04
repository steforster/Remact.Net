
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Remact.Net.Contracts;    // Timer

namespace Remact.Net.Remote
{
  
  
  /// <summary>
  /// A internally used singleton object for all RemactServices and RemactClientAsync to register/lookup a service with Remact.Catalog
  /// </summary>
  internal class RemactCatalogClient : IRemactCatalog
  {
    //----------------------------------------------------------------------------------------------
    #region Fields

    private static RemactCatalogClient  ms_Instance;
    private static object               ms_Lock = new Object();
    private static bool                 ms_DisableCatalogClient;

    private        ActorOutput          m_CatalogClient;
    private        List<RemactClient>   m_ClientList;
    private        List<RemactService>  m_ServiceList;
    private        int                  m_nCurrentSvc;
    private        Timer                m_Timer;
    private        bool                 m_Running;
    private        int                  m_nConnectTries;


    internal bool DisableCatalogClient
    {
        get { return ms_DisableCatalogClient; }
        set 
        {
            ms_DisableCatalogClient = value;
            if (!ms_DisableCatalogClient)
            {
                ms_Instance.m_Timer.Change (0, 1000);// connect in Timer thread
            }
        }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Events

    // running on threadpool timer thread
    private void OnTimerTick (object dummy)
    {
        if (m_Running) return;
        m_Running = true;
        var state = m_CatalogClient.OutputState;
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
                            m_ServiceList[0].Disconnect(); // shutdown all services and send ServiceDisable messages to Remact.CatalogService when overloaded in RemactClientAsync
                        }
              
                        while (m_ClientList.Count > 0)
                        {
                            m_ClientList[0].Disconnect (); // shutdown all clients and send ClientDisconnectRequest to its connected service
                        }
                    }

                    if (nServices > 0) Thread.Sleep (20 + (nServices*10));  // let the communication end
                    Disconnect ();                                          // shutdown the CatalogClient itself
                }
            }//-------------------------------
            else if (state == PortState.Faulted)
            {
                if (m_nConnectTries < 0) RaLog.Error("Remact", "Catalog client in fault state !", RemactApplication.Logger);
                m_CatalogClient.Disconnect();
                m_Timer.Change (15000, 1000); // 15 s warten und neu starten
            }//-------------------------------
            else if (state == PortState.Disconnected || state == PortState.Unlinked)
            {
                if (!ms_DisableCatalogClient)
                {
                    if (++m_nConnectTries >= 10) m_nConnectTries = 1;
                    var uri = new Uri(string.Format("ws://{0}:{1}/{2}/{3}", RemactConfigDefault.Instance.CatalogHost, RemactConfigDefault.Instance.CatalogPort, RemactConfigDefault.WsNamespace, RemactConfigDefault.Instance.CatalogServiceName));
                    m_CatalogClient.LinkOutputToRemoteService(uri);
                    m_CatalogClient.TryConnect();
                }

                lock (ms_Lock)
                {
                    foreach (RemactService s in m_ServiceList)
                    {
                        s.IsServiceRegistered = false; // reset in case CatalogService has been restarted 
                    }
                }
            }//-------------------------------
            else if (m_CatalogClient.IsOutputConnected)
            {
                m_nConnectTries = -1;
                if (m_ServiceList.Count <= m_nCurrentSvc)
                {
                    m_nCurrentSvc = 0;
                }
                else
                {
                    lock (ms_Lock)
                    {
                        ActorInfo req = new ActorInfo (m_ServiceList[m_nCurrentSvc].ServiceIdent, ActorInfo.Use.ServiceEnableRequest);
                        RemactService svc = m_ServiceList[m_nCurrentSvc];
                        if (!svc.IsServiceRegistered)
                        {
                            svc.NextEnableMessage = DateTime.Now.AddSeconds(20);
                            svc.IsServiceRegistered = true;
                            InputIsOpen(req);
                            RaLog.Info(_latestSentMessage.CltSndId, "Sent to Remact.Catalog: " + req.ToString(), RemactApplication.Logger);
                        }
                        else if (m_ServiceList[m_nCurrentSvc].NextEnableMessage < DateTime.Now)
                        {
                            m_ServiceList[m_nCurrentSvc].NextEnableMessage  = DateTime.Now.AddSeconds(20);
                            InputIsOpen(req);
                            //RaLog.Info (_latestSentMessage.CltSndId, "Alive    "+req.ToString ());
                        }
                        m_nCurrentSvc++; // next Svc on next timer event
                    }
                }
            }//connected

            if (ms_DisableCatalogClient && m_Timer != null) m_Timer.Change (Timeout.Infinite, 1000); // stop the timer
        }
        catch (Exception ex)
        {
            RaLog.Exception( "during CatalogClient timer", ex, RemactApplication.Logger );
        }

        m_Running = false;
    }// OnTimerTick

    
    // Response callback from Remact.CatalogService
    private void OnMessageReceived (ActorMessage rsp)
    {
        if (rsp.Payload is ErrorMessage)
        {
            if (m_nConnectTries % 20 == 1) {
                ErrorMessage err = rsp.Payload as ErrorMessage;
                if (err.Error == ErrorMessage.Code.ServiceNotRunning
                 || err.Error == ErrorMessage.Code.CatalogServiceNotRunning)
                {
                    RaLog.Warning( rsp.CltRcvId, "Remact catalog service not running at  '" + rsp.Source.Uri + "'", RemactApplication.Logger );
                }
                else
                {
                    RaLog.Warning( rsp.CltRcvId, err.ToString(), RemactApplication.Logger );
                }
            }
        }
        else if (rsp.Payload is ActorInfo && m_ServiceList != null)
        {
            m_Timer.Change (20, 1000); // 20ms warten und nächsten ActorMessage senden, bis alle erledigt sind
        }
        else
        {
            RaLog.Info( rsp.CltRcvId, rsp.ToString(), RemactApplication.Logger );
        }
    }// OnMessageReceived
    

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Constructors

    /// <summary>
    /// (static) Get or Create the Remact.CatalogClient singleton 
    /// </summary>
    /// <returns>singleton instance</returns>
    internal static RemactCatalogClient Instance
    {
        get
        {
            if (ms_Instance == null)
            {
                ms_Instance = new RemactCatalogClient();
            }
            return ms_Instance;
        }
    }


    /// <summary>
    /// Initializes a new instance of the Remact.CatalogClient class.
    /// </summary>
    internal RemactCatalogClient ()
    {
      m_ServiceList   = new List<RemactService> (20);
      m_ClientList    = new List<RemactClient> (20);
      m_CatalogClient = new ActorOutput("Remact.CatalogClient", OnMessageReceived);
      m_CatalogClient.IsMultithreaded = true; // all other clients will send LookupInput requests through this client
      m_CatalogClient.TraceConnect = false;
      m_Timer = new Timer (OnTimerTick, this, 0, 1000); // startet immediately, Periode=1s
    }// CTOR

    
    internal bool IsConnected { get { return m_CatalogClient.IsOutputConnected; } }


    /// <summary>
    /// (static) Close all incoming network connections and send a ServiceDisable messages to Remact.CatalogService.
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
    }// Remact.CatalogClient.DisconnectAll


    /// <summary>
    /// Shutdown the CatalogClient, send disconnect message to Remact.CatalogService if possible
    /// </summary>
    internal void Disconnect ()
    {
        if (m_Timer != null)
        {
            m_Timer.Dispose();
            int n = m_CatalogClient.OutstandingResponsesCount;
            if (!ms_DisableCatalogClient || n != 0 || m_CatalogClient.OutputState != PortState.Disconnected)
            {
                m_CatalogClient.Disconnect(); // send last messages, contrary to AbortCommunication();
                m_CatalogClient = null;
                RaLog.Info("Remact", "Catalog client disconnected.", RemactApplication.Logger);
            }
        
            m_Timer = null;
            m_CatalogClient = null;
            m_ServiceList.Clear();
            m_ServiceList = null;
            ms_Instance = null;
        }
    }

    
    #endregion
    //----------------------------------------------------------------------------------------------
    #region Public Methods

    /// <summary>
    /// Add a local RemactService for registration with Remact.CatalogService
    /// </summary>
    /// <param name="svc">the local RemactService</param>
    internal void AddService (RemactService svc)
    {
        lock (ms_Lock)
        {
            m_ServiceList.Add (svc);
            svc.IsServiceRegistered = false; // triggert EnableMessage
        }
    }

    
    /// <summary>
    /// Remove a local RemactService, send disable-message to Remact.CatalogService
    /// </summary>
    /// <param name="svc">the local RemactService</param>
    internal void RemoveService (RemactService svc)
    {
        lock (ms_Lock)
        {
            int n = m_ServiceList.IndexOf (svc);
            if (n < 0) return; // already removed
            if (m_CatalogClient != null && m_CatalogClient.IsOutputConnected)
            {
                ActorInfo req = new ActorInfo (m_ServiceList[n].ServiceIdent, ActorInfo.Use.ServiceDisableRequest);
                InputIsClosed(req);
                RaLog.Info(_latestSentMessage.CltSndId, "Disabled " + req.ToString(), RemactApplication.Logger);
                m_ServiceList[n].IsServiceRegistered = false;
            }
            m_ServiceList.RemoveAt(n);
        }
    }


    /// <summary>
    /// Add a local RemactClient for handling in DisconnectAll
    /// </summary>
    /// <param name="clt">the local RemactClient</param>
    internal void AddClient (RemactClient clt)
    {
        if (clt.ClientIdent == m_CatalogClient) return; // do not add the CatalogClient itself
        lock (ms_Lock)
        {
            m_ClientList.Add (clt);
        }
    }


    /// <summary>
    /// Remove a local RemactClient while Disconnecting.
    /// </summary>
    /// <param name="clt">the local RemactClient</param>
    internal void RemoveClient (RemactClient clt)
    {
        lock (ms_Lock)
        {
            int n = m_ClientList.IndexOf (clt);
            if (n < 0) return; // already removed
            m_ClientList.RemoveAt (n);
        }
    }

    #endregion
    #region IRemactCatalog implementation

    private ActorMessage _latestSentMessage;

    public Task<ActorMessage<ReadyMessage>> InputIsOpen(ActorInfo actorInput)
    {
        return m_CatalogClient.Ask<ReadyMessage>("InputIsOpen", actorInput, out _latestSentMessage, false);
    }

    public Task<ActorMessage<ReadyMessage>> InputIsClosed(ActorInfo actorInput)
    {
        return m_CatalogClient.Ask<ReadyMessage>("InputIsClosed", actorInput, out _latestSentMessage, false);
    }

    public Task<ActorMessage<ActorInfo>> LookupInput(string actorInputName)
    {
        ActorMessage sentMessage;
        return m_CatalogClient.Ask<ActorInfo>("LookupInput", actorInputName, out sentMessage, false);
    }

    public Task<ActorMessage<ActorInfoList>> SynchronizeCatalog(ActorInfoList serviceList)
    {
        throw new InvalidOperationException();
    }

    #endregion
  }//class Remact.CatalogClient
}//namespace
