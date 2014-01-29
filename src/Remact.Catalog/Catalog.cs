
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Description; // ServiceHost
using System.Windows.Forms;
using System.Net;
using Remact.Net;
using Remact.Net.Remote;
using Remact.Net.Protocol;

namespace Remact.Catalog
{
  /// <summary>
  /// The SvcRegister contains some data for each service.
  /// </summary>
  class SvcDat
  {
    public int WaitSeconcsForConnect;
    public int WaitSecondsForUpdate;
  }
  
  /// <summary>
  /// The Catalog Class is used to Create and Dispose the CatalogService.
  /// It has to be called periodically to do checks on all connections and update the statusdisplay.
  /// </summary>
  class Catalog : IActorInputConfiguration, IActorOutputConfiguration
  {
    //----------------------------------------------------------------------------------------------
    #region Fields
    
    private ActorInput     m_RemactService;
    private CatalogService m_CatalogService;
    private int            m_knownServiceCount;

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Properties

    public  IActorPort             Service { get{ return m_RemactService;} }
    public  ActorInfoList    SvcRegister;
    public  bool                     SvcRegisterChanged = true;
    public  List<ActorOutput<SvcDat>> PeerCatalogs;

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Constructors/Destructor

    /// <summary>
    /// Initializes a new instance of the Catalog class.
    /// </summary>
    public Catalog()
    {
      SvcRegister = new ActorInfoList();
      PeerCatalogs = new List<ActorOutput<SvcDat>>(Properties.Settings.Default.PeerHosts.Count);
    }

    /// <summary>
    /// Deinitializes all recources.
    /// </summary>
    public void Dispose()
    {
      try
      {
        if (m_RemactService != null)
        {
            m_RemactService.Disconnect();
            m_RemactService.OnInputConnected    -= m_CatalogService.OnClientConnectedOrDisconnected;
            m_RemactService.OnInputDisconnected -= m_CatalogService.OnClientConnectedOrDisconnected;
            RaLog.Info("Remact", "Closed service " + m_RemactService.Uri);
            m_RemactService = null;
        }
      }
      catch (Exception ex)
      {
          RaLog.Exception("Remact: Error while closing the service", ex);
      }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Public Methods

    public void PeriodicCall(int seconds, TextBox tbStatus)
    {
        if (m_CatalogService == null) Open();

        int connectedPeerCatalogs = UpdatePeerCatalogs( seconds );
        int servicesFromPeerCatalogs = 0;

        foreach (ActorInfo s in SvcRegister.Item)
        {
            if (s.Usage == ActorInfo.Use.ServiceEnableRequest)
            {
                s.TimeoutSeconds -= seconds;
                if (s.TimeoutSeconds < 0)
                {
                    s.Usage = ActorInfo.Use.ServiceDisableRequest;
                    SvcRegisterChanged = true;
                    RaLog.Warning("   "+s.Name+"  ", "Timeout  "+s.ToString ());
                }
            }
            
            if( s.CatalogHopCount > 1 )
            {
                servicesFromPeerCatalogs++;
            }
        }

        if (m_RemactService.MustOpenInput) SvcRegisterChanged = true;

        if (m_RemactService.BasicService.DoPeriodicTasks ()) SvcRegisterChanged = true;

        if (SvcRegisterChanged && tbStatus != null)
        {
            SvcRegisterChanged = false;
            StringBuilder sb = new StringBuilder();

            if (!m_RemactService.MustOpenInput)
            {
                sb.Append("CatalogService listening on '" + m_RemactService.Uri + "'");
            }
            else
            {
                sb.Append(m_RemactService.InputStateFromNetwork + " CatalogService '" + m_RemactService.Uri + "'");
            }

            sb.AppendLine();
            sb.Append( SvcRegister.Item.Count+" registered Remact services (");
            sb.Append( servicesFromPeerCatalogs+" from remote catalogs). " );
            sb.Append( m_RemactService.BasicService.ConnectedClientCount+" clients. ");
            sb.Append( connectedPeerCatalogs + "/" + PeerCatalogs.Count );
            sb.Append(" configured remote catalog services.");
            sb.AppendLine();
        
            foreach (ActorInfo s in SvcRegister.Item)
            {
                int versionCount = 2;
                if (s.AppVersion.Revision != 0)
                {
                    versionCount = 4;
                }
                else if (s.AppVersion.Build != 0)
                {
                    versionCount = 3;
                }
                sb.AppendLine();
                if (s.Usage == ActorInfo.Use.ServiceEnableRequest) sb.Append ("++");
                                                                      else sb.Append ("--");
                sb.Append (s.Uri);
                sb.Append (" in ");
                sb.Append (RemactConfigDefault.Instance.GetAppIdentification (s.AppName, s.AppInstance, s.HostName, s.ProcessId));
                sb.Append (" (V");
                sb.Append (s.AppVersion.ToString (versionCount));
                sb.Append (")");
            }
            tbStatus.Text = sb.ToString ();
        }
    }// PeriodicCall


    private int UpdatePeerCatalogs( int seconds )
    {
        bool changed = SvcRegister.Item.Count != m_knownServiceCount;
        m_knownServiceCount = SvcRegister.Item.Count;

        int connectedPeerCatalogs = 0;
        foreach( ActorOutput<SvcDat> p in PeerCatalogs )
        {
            if( !p.IsOutputConnected )
            {
                if( p.MustConnectOutput )
                {
                    p.OutputContext.WaitSeconcsForConnect -= seconds;
                    if( p.OutputContext.WaitSeconcsForConnect <= 0 )
                    {
                        p.TryConnect();
                        p.OutputContext.WaitSeconcsForConnect = 30;
                        p.OutputContext.WaitSecondsForUpdate  =  0;
                    }
                }
            }
            else
            {
                connectedPeerCatalogs++;
                p.OutputContext.WaitSecondsForUpdate -= seconds;
                if( p.OutputContext.WaitSecondsForUpdate <= 0 || changed )
                {
                    p.Notify("RegisterList", SvcRegister);
                    p.OutputContext.WaitSecondsForUpdate = 30;
                }
            }
        }
        return connectedPeerCatalogs;
    }


    // Register remote service. returns true, when svc is referenced by SvcRegister.
    public bool RegisterService (ActorInfo svc, string mark)
    {
      svc.CatalogHopCount++;                                 // ==1: Direct info from service on local host
      if (svc.CatalogHopCount > 1) svc.TimeoutSeconds = 120; // > 1: Indirect info from another catalog
      
      if (svc.Usage != ActorInfo.Use.ServiceEnableRequest
       && svc.Usage != ActorInfo.Use.ServiceDisableRequest)
      {
        RaLog.Error (mark, "Got wrong status: "+svc.ToString ());
        svc.Usage = ActorInfo.Use.ServiceDisableRequest;
      }

      bool changed = false;
      int found = SvcRegister.Item.FindIndex (s => s.IsEqualTo (svc));
      if (found < 0)
      {
          RaLog.Info( mark, "Register  " + svc.Uri.ToString() );
          SvcRegister.Item.Add (svc);
          Program.Catalog.SvcRegisterChanged = true;
          return true;
      }
      else
      {
        ActorInfo registered = SvcRegister.Item[found];
        
        if (registered.Uri != svc.Uri)
        {
          // a changed or a second service tries to register
          if (registered.Usage == ActorInfo.Use.ServiceDisableRequest
                  && svc.Usage == ActorInfo.Use.ServiceEnableRequest)
          {
              RaLog.Info( mark, "Start new   " + svc.Uri.ToString() );
              changed = true;
          }
          else if (registered.Usage == ActorInfo.Use.ServiceEnableRequest
                       && svc.Usage == ActorInfo.Use.ServiceEnableRequest) 
          {
            if (registered.ApplicationRunTime < svc.ApplicationRunTime)
            {
                RaLog.Info( mark, "Switch to " + svc.Uri.ToString ());
                changed = true;
            }
            else //if (svc.CatalogHopCount > 1)
            { // will be removed from all registers after some time
                RaLog.Info( mark, "Backup    " + svc.Uri.ToString() );
            }
          }
        }
        else
        {
          if( registered.CatalogHopCount < svc.CatalogHopCount )
          {
              // circular reference: do not use this old information
          }
          else if( registered.Usage == ActorInfo.Use.ServiceDisableRequest
                       && svc.Usage == ActorInfo.Use.ServiceEnableRequest)
          {
              RaLog.Info( mark, "Restart   " + svc.Uri.ToString() );
              changed = true;
          }
          else if (registered.Usage == ActorInfo.Use.ServiceEnableRequest
                       && svc.Usage == ActorInfo.Use.ServiceDisableRequest)
          {
              RaLog.Info( mark, "Stopped   " + svc.Uri.ToString() );
              changed = true;
          }
          else if (registered.Usage == ActorInfo.Use.ServiceEnableRequest
                       && svc.Usage == ActorInfo.Use.ServiceEnableRequest)
          {
              RaLog.Info( mark, "Alive     " + svc.Uri.ToString() );
              SvcRegister.Item[found].TimeoutSeconds = svc.TimeoutSeconds;// Restart timeout
          }
        }
      }
      
      if (changed)
      {
        SvcRegister.Item[found] = svc;
        Program.Catalog.SvcRegisterChanged = true;
      }
      return changed;
    }// RegisterService


    #endregion
    //----------------------------------------------------------------------------------------------
    #region Private Methods

    private void Open()
    {
        // Create the CatalogService-Singleton
        m_CatalogService = new CatalogService();

        // Open the service
        m_RemactService = new ActorInput(RemactConfigDefault.Instance.CatalogServiceName, m_CatalogService.OnRequest);
        m_RemactService.OnInputConnected    += m_CatalogService.OnClientConnectedOrDisconnected;
        m_RemactService.OnInputDisconnected += m_CatalogService.OnClientConnectedOrDisconnected;
        m_RemactService.LinkInputToNetwork( null, RemactConfigDefault.Instance.CatalogPort, publishToCatalog: false, serviceConfig: this ); // calls our DoServiceConfiguration
        m_RemactService.TraceConnect = false;
        m_RemactService.TryConnect();
      
        string names = string.Empty;
        foreach (string host in Properties.Settings.Default.PeerHosts)
        {
            if (host != null && host.Trim().Length > 0)
            {
                var output = new ActorOutput<SvcDat>("Clt>"+host, OnResponseFromPeerCatalog);
                output.LinkOutputToRemoteService(new Uri("http://" + host + ':' + RemactConfigDefault.Instance.CatalogPort
                                 + "/" + RemactConfigDefault.WsNamespace + "/" + RemactConfigDefault.Instance.CatalogServiceName),// no catalog lookup as uri is given.
                                 this ); // calls our DoClientConfiguration
                output.OutputContext = new SvcDat();
                output.TryConnect();
                PeerCatalogs.Add (output);
                names += host+", ";
            }
        }
        RaLog.Info("Remact", "Opened clients for peer catalogs on " + PeerCatalogs.Count + " configured PeerHosts: " + names);
    }// Open


    // implement IActorInputConfiguration
    public WebSocketPortManager DoServiceConfiguration(RemactService service, ref Uri uri, bool isCatalog)
    {
        return RemactConfigDefault.Instance.DoServiceConfiguration(service, ref uri, isCatalog:true);
    }


    // implement IActorOutputConfiguration
    public void DoClientConfiguration(object clientBase, ref Uri uri, bool forCatalog)
    {
        RemactConfigDefault.Instance.DoClientConfiguration(clientBase, ref uri, forCatalog:true);
    }
    

    private void OnResponseFromPeerCatalog(ActorMessage id, SvcDat svcDat)
    {
      if (
       id.On<ActorInfo>(partner=>
      {
        RaLog.Info      (id.CltRcvId, "PeerRtr   "+partner.ToString ());
      })
      .On<ErrorMessage>(err=>
      {
        if (err.Error == ErrorMessage.Code.ServiceNotRunning) {
          RaLog.Warning (id.CltRcvId, "PeerRtr   "+err.Error.ToString ()+" at '"+id.Source.Uri+"'");
        }
        else {
          RaLog.Error   (id.CltRcvId, "PeerRtr   "+err.ToString ()+Environment.NewLine+"   partner uri = '"+id.Source.Uri+"'");
        }
      })
      .On<ActorInfoList>(list=>
      {
          RaLog.Info( id.CltRcvId, "PeerRtr responds with list containing " + list.Item.Count + " services." );
          foreach( ActorInfo s in list.Item )
        {
          RegisterService (s, id.CltRcvId);
        }
      }
      ) != null)
      {
          RaLog.Warning("Remact", "Received unexpected message from " + id.Source.Name + ": " + id.Payload.ToString());
      }
    }
    
    #endregion
  }
}
