
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Remact.Net;
using Remact.Net.Remote;
using Remact.Net.Protocol;
using Remact.Net.Contracts;

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
  class Catalog : IServiceConfiguration, IClientConfiguration
  {
    //----------------------------------------------------------------------------------------------
    #region Fields
    
    private RemactPortService m_RemactService;
    private CatalogService m_CatalogService;
    private int m_knownServiceCount;

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Properties

    public  IRemactPort Service { get{ return m_RemactService;} }
    public  ActorInfoList SvcRegister;
    public  bool SvcRegisterChanged = true;
    public  List<RemactPortProxy<SvcDat>> PeerCatalogs;

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Constructors/Destructor

    /// <summary>
    /// Initializes a new instance of the Catalog class.
    /// </summary>
    public Catalog()
    {
      SvcRegister = new ActorInfoList();
      PeerCatalogs = new List<RemactPortProxy<SvcDat>>(Properties.Settings.Default.PeerHosts.Count);
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
            if (s.IsOpen)
            {
                s.TimeoutSeconds -= seconds;
                if (s.TimeoutSeconds < 0)
                {
                    s.IsOpen = false;
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
                if (s.IsOpen) sb.Append ("++");
                         else sb.Append ("--");
                sb.Append (s.Uri);
                sb.Append (" in ");
                sb.Append (RemactConfigDefault.Instance.GetAppIdentification (s.AppName, s.AppInstance, s.HostName, s.ProcessId));
                sb.Append (" V ");
                sb.Append (s.AppVersion.ToString (versionCount));
            }
            tbStatus.Text = sb.ToString ();
        }
    }// PeriodicCall


    private int UpdatePeerCatalogs( int seconds )
    {
        bool changed = SvcRegister.Item.Count != m_knownServiceCount;
        m_knownServiceCount = SvcRegister.Item.Count;

        int connectedPeerCatalogs = 0;
        foreach( RemactPortProxy<SvcDat> p in PeerCatalogs )
        {
            if( !p.IsOutputConnected )
            {
                if( p.MustConnectOutput )
                {
                    p.OutputContext.WaitSeconcsForConnect -= seconds;
                    if( p.OutputContext.WaitSeconcsForConnect <= 0 )
                    {
                        p.ConnectAsync();
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
          if (!registered.IsOpen && svc.IsOpen)
          {
              RaLog.Info( mark, "Start new   " + svc.Uri.ToString() );
              changed = true;
          }
          else if (registered.IsOpen && svc.IsOpen) 
          {
            if (registered.ApplicationRunTime < svc.ApplicationRunTime)
            {
                RaLog.Info( mark, "Switch to " + svc.Uri.ToString ());
                changed = true;
            }
            else //if (svc.CatalogHopCount > 1)
            { // will be removed from all registers after some time
                RaLog.Info( mark, "Backup    " + svc.Uri.ToString() );
                throw new RemactException(null, ErrorCode.ServiceIsBackup, "Service '" + registered.Uri + "' is already active. Service '" + svc.Uri + "' may be used as backup.");
            }
          }
        }
        else
        { // same URI
          if( registered.CatalogHopCount < svc.CatalogHopCount )
          {
              // circular reference: do not use this old information
          }
          else if (!registered.IsOpen && svc.IsOpen)
          {
              RaLog.Info( mark, "Restart   " + svc.Uri.ToString() );
              changed = true;
          }
          else if (registered.IsOpen && !svc.IsOpen)
          {
              RaLog.Info( mark, "Stopped   " + svc.Uri.ToString() );
              changed = true;
          }
          else if (registered.IsOpen && svc.IsOpen)
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
        m_RemactService = new RemactPortService(RemactConfigDefault.Instance.CatalogServiceName, m_CatalogService.OnUnknownRequest);
        m_RemactService.InputDispatcher.AddActorInterface(typeof(IRemactCatalog), m_CatalogService);
        m_RemactService.OnInputConnected    += m_CatalogService.OnClientConnectedOrDisconnected;
        m_RemactService.OnInputDisconnected += m_CatalogService.OnClientConnectedOrDisconnected;
        m_RemactService.LinkInputToNetwork( null, RemactConfigDefault.Instance.CatalogPort, publishToCatalog: false, serviceConfig: this ); // calls our DoServiceConfiguration
        m_RemactService.TraceConnect = false;
        m_RemactService.ConnectAsync();
      
        string names = string.Empty;
        foreach (string host in Properties.Settings.Default.PeerHosts)
        {
            if (host != null && host.Trim().Length > 0)
            {
                var output = new RemactPortProxy<SvcDat>("Clt>"+host, OnResponseFromPeerCatalog);
                output.LinkOutputToRemoteService(new Uri("ws://" + host + ':' + RemactConfigDefault.Instance.CatalogPort
                                 + "/" + RemactConfigDefault.WsNamespace + "/" + RemactConfigDefault.Instance.CatalogServiceName),// no catalog lookup as uri is given.
                                 this ); // calls our DoClientConfiguration
                output.OutputContext = new SvcDat();
                output.ConnectAsync();
                PeerCatalogs.Add (output);
                names += host+", ";
            }
        }
        RaLog.Info("Remact", "Opened clients for peer catalogs on " + PeerCatalogs.Count + " configured PeerHosts: " + names);
    }// Open


    // implement IServiceConfiguration
    public WebSocketPortManager DoServiceConfiguration(RemactService service, ref Uri uri, bool isCatalog)
    {
        return RemactConfigDefault.Instance.DoServiceConfiguration(service, ref uri, isCatalog:true);
    }


    // implement IClientConfiguration
    public IRemactProtocolDriverToService DoClientConfiguration(ref Uri uri, bool forCatalog)
    {
        return RemactConfigDefault.Instance.DoClientConfiguration(ref uri, forCatalog:true);
    }
    

    private Task OnResponseFromPeerCatalog(RemactMessage id, SvcDat svcDat)
    {
      if (
          id.On<ActorInfo>(partner=>
          {
              RaLog.Info (id.CltRcvId, "Peer catalog   "+partner.ToString ());
          })
          .On<ErrorMessage>(err=>
          {
              RaLog.Error(id.CltRcvId, "Peer catalog   "+err.ToString ()+Environment.NewLine+"   partner uri = '"+id.Source.Uri+"'");
          })
          .On<ActorInfoList>(list=>
          {
              RaLog.Info( id.CltRcvId, "Peer catalog responds with list containing " + list.Item.Count + " services." );
              foreach( ActorInfo s in list.Item )
              {
                  RegisterService (s, id.CltRcvId);
              }
          }
      ) != null)
      {
          RaLog.Warning("Remact", "Received unexpected message from peer catalog " + id.Source.Name + ": " + id.Payload.ToString());
      }
      return null; // completed synchronously
    }
    
    #endregion
  }
}
