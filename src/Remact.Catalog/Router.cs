
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
using Alchemy;
using Remact.Net;
using Remact.Net.Internal;

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
  /// The Router Class is used to Create and Dispose the RouterService.
  /// It has to be called periodically to do checks on all connections and update the statusdisplay.
  /// </summary>
  class Router : IActorInputConfiguration, IActorOutputConfiguration
  {
    //----------------------------------------------------------------------------------------------
    #region Fields
    
    private ActorInput    m_WcfService;
    private RouterService m_RouterService;
    private int           m_knownServiceCount;

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Properties

    public  IActorPort             Service { get{ return m_WcfService;} }
    public  WcfPartnerListMessage    SvcRegister;
    public  bool                     SvcRegisterChanged = true;
    public  List<ActorOutput<SvcDat>> PeerRouters;

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Constructors/Destructor

    /// <summary>
    /// Initializes a new instance of the Router class.
    /// </summary>
    public Router()
    {
      SvcRegister = new WcfPartnerListMessage();
      PeerRouters = new List<ActorOutput<SvcDat>>(Properties.Settings.Default.PeerHosts.Count);
    }

    /// <summary>
    /// Deinitializes all recources.
    /// </summary>
    public void Dispose()
    {
      try
      {
        if (m_WcfService != null)
        {
            m_WcfService.Disconnect();
            m_WcfService.OnInputConnected    -= m_RouterService.OnClientConnectedOrDisconnected;
            m_WcfService.OnInputDisconnected -= m_RouterService.OnClientConnectedOrDisconnected;
            RaTrc.Info( "Wcf", "Closed service " + m_WcfService.Uri );
            m_WcfService = null;
        }
      }
      catch (Exception ex)
      {
        RaTrc.Exception ("Wcf: Error while closing the service", ex);
      }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Public Methods

    public void PeriodicCall(int seconds, TextBox tbStatus)
    {
        if (m_RouterService == null) Open();

        int connectedPeerRouters = UpdatePeerRouters( seconds );
        int servicesFromPeerRouters = 0;

        foreach (ActorInfo s in SvcRegister.Item)
        {
            if (s.Usage == ActorInfo.Use.ServiceEnableRequest)
            {
                s.TimeoutSeconds -= seconds;
                if (s.TimeoutSeconds < 0)
                {
                    s.Usage = ActorInfo.Use.ServiceDisableRequest;
                    SvcRegisterChanged = true;
                    RaTrc.Warning("   "+s.Name+"  ", "Timeout  "+s.ToString ());
                }
            }
            
            if( s.RouterHopCount > 1 )
            {
                servicesFromPeerRouters++;
            }
        }

        if (m_WcfService.MustOpenInput) SvcRegisterChanged = true;

        if (m_WcfService.BasicService.DoPeriodicTasks ()) SvcRegisterChanged = true;

        if (SvcRegisterChanged && tbStatus != null)
        {
            SvcRegisterChanged = false;
            StringBuilder sb = new StringBuilder();

            if (!m_WcfService.MustOpenInput)
            {
                sb.Append("RouterService listening on '" + m_WcfService.Uri + "'");
            }
            else
            {
                sb.Append(m_WcfService.InputStateFromNetwork + " RouterService '" + m_WcfService.Uri + "'");
            }

            sb.AppendLine();
            sb.Append( SvcRegister.Item.Count+" registered WCF services (");
            sb.Append( servicesFromPeerRouters+" from remote routers). " );
            sb.Append( m_WcfService.BasicService.ConnectedClientCount+" clients. ");
            sb.Append( connectedPeerRouters + "/" + PeerRouters.Count );
            sb.Append(" configured remote routers.");
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
                sb.Append (RemactDefaults.Instance.GetAppIdentification (s.AppName, s.AppInstance, s.HostName, s.ProcessId));
                sb.Append (" (V");
                sb.Append (s.AppVersion.ToString (versionCount));
                sb.Append (")");
            }
            tbStatus.Text = sb.ToString ();
        }
    }// PeriodicCall


    private int UpdatePeerRouters( int seconds )
    {
        bool changed = SvcRegister.Item.Count != m_knownServiceCount;
        m_knownServiceCount = SvcRegister.Item.Count;

        int connectedPeerRouters = 0;
        foreach( ActorOutput<SvcDat> p in PeerRouters )
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
                connectedPeerRouters++;
                p.OutputContext.WaitSecondsForUpdate -= seconds;
                if( p.OutputContext.WaitSecondsForUpdate <= 0 || changed )
                {
                    p.SendOut( SvcRegister );
                    p.OutputContext.WaitSecondsForUpdate = 30;
                }
            }
        }
        return connectedPeerRouters;
    }


    // Register remote service. returns true, when svc is referenced by SvcRegister.
    public bool RegisterService (ActorInfo svc, string mark)
    {
      svc.RouterHopCount++;                                 // ==1: Direct info from service on local host
      if (svc.RouterHopCount > 1) svc.TimeoutSeconds = 120; // > 1: Indirect info from another router
      
      if (svc.Usage != ActorInfo.Use.ServiceEnableRequest
       && svc.Usage != ActorInfo.Use.ServiceDisableRequest)
      {
        RaTrc.Error (mark, "Got wrong status: "+svc.ToString ());
        svc.Usage = ActorInfo.Use.ServiceDisableRequest;
      }

      bool changed = false;
      int found = SvcRegister.Item.FindIndex (s => s.IsEqualTo (svc));
      if (found < 0)
      {
          RaTrc.Info( mark, "Register  " + svc.Uri.ToString() );
          SvcRegister.Item.Add (svc);
          Program.Router.SvcRegisterChanged = true;
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
              RaTrc.Info( mark, "Start new   " + svc.Uri.ToString() );
              changed = true;
          }
          else if (registered.Usage == ActorInfo.Use.ServiceEnableRequest
                       && svc.Usage == ActorInfo.Use.ServiceEnableRequest) 
          {
            if (registered.ApplicationRunTime < svc.ApplicationRunTime)
            {
                RaTrc.Info( mark, "Switch to " + svc.Uri.ToString ());
                changed = true;
            }
            else //if (svc.RouterHopCount > 1)
            { // will be removed from all registers after some time
                RaTrc.Info( mark, "Backup    " + svc.Uri.ToString() );
            }
          }
        }
        else
        {
          if( registered.RouterHopCount < svc.RouterHopCount )
          {
              // circular reference: do not use this old information
          }
          else if( registered.Usage == ActorInfo.Use.ServiceDisableRequest
                       && svc.Usage == ActorInfo.Use.ServiceEnableRequest)
          {
              RaTrc.Info( mark, "Restart   " + svc.Uri.ToString() );
              changed = true;
          }
          else if (registered.Usage == ActorInfo.Use.ServiceEnableRequest
                       && svc.Usage == ActorInfo.Use.ServiceDisableRequest)
          {
              RaTrc.Info( mark, "Stopped   " + svc.Uri.ToString() );
              changed = true;
          }
          else if (registered.Usage == ActorInfo.Use.ServiceEnableRequest
                       && svc.Usage == ActorInfo.Use.ServiceEnableRequest)
          {
              RaTrc.Info( mark, "Alive     " + svc.Uri.ToString() );
              SvcRegister.Item[found].TimeoutSeconds = svc.TimeoutSeconds;// Restart timeout
          }
        }
      }
      
      if (changed)
      {
        SvcRegister.Item[found] = svc;
        Program.Router.SvcRegisterChanged = true;
      }
      return changed;
    }// RegisterService


    #endregion
    //----------------------------------------------------------------------------------------------
    #region Private Methods

    private void Open()
    {
        // Create the RouterService-Singleton
        m_RouterService = new RouterService();

        // Open the service
        m_WcfService = new ActorInput(RemactDefaults.Instance.RouterServiceName, m_RouterService.OnRequest);
        m_WcfService.OnInputConnected    += m_RouterService.OnClientConnectedOrDisconnected;
        m_WcfService.OnInputDisconnected += m_RouterService.OnClientConnectedOrDisconnected;
        m_WcfService.LinkInputToNetwork( null, RemactDefaults.Instance.RouterPort, publishToRouter: false, serviceConfig: this ); // calls our DoServiceConfiguration
        m_WcfService.TraceConnect = false;
        m_WcfService.TryConnect();
      
        string names = string.Empty;
        foreach (string host in Properties.Settings.Default.PeerHosts)
        {
            if (host != null && host.Trim().Length > 0)
            {
                var output = new ActorOutput<SvcDat>("Clt>"+host, OnResponseFromPeerRouter);
                output.LinkOutputToRemoteService(new Uri("http://" + host + ':' + RemactDefaults.Instance.RouterPort
                                 + "/" + RemactDefaults.WsNamespace + "/" + RemactDefaults.Instance.RouterServiceName),// no router lookup as uri is given.
                                 this ); // calls our DoClientConfiguration
                output.OutputContext = new SvcDat();
                output.TryConnect();
                PeerRouters.Add (output);
                names += host+", ";
            }
        }
        RaTrc.Info( "Wcf", "Opened clients for peer routers on " + PeerRouters.Count + " configured PeerHosts: " + names );
    }// Open


    // implement IActorInputConfiguration
    public void DoServiceConfiguration(WebSocketServer server, ref Uri uri, bool isRouter)
    {
////TODO        RemactDefaults.Instance.DoServiceConfiguration( serviceHost, ref uri, /*isRouter=*/ true );
    }


    // implement IActorOutputConfiguration
    public void DoClientConfiguration( object clientBase, ref Uri uri, bool forRouter )
    {
        RemactDefaults.Instance.DoClientConfiguration( clientBase, ref uri, /*forRouter=*/ true );
    }
    

    private void OnResponseFromPeerRouter(ActorMessage id, SvcDat svcDat)
    {
      if (
       id.On<ActorInfo>(partner=>
      {
        RaTrc.Info      (id.CltRcvId, "PeerRtr   "+partner.ToString ());
      })
      .On<ErrorMessage>(err=>
      {
        if (err.Error == ErrorMessage.Code.ServiceNotRunning) {
          RaTrc.Warning (id.CltRcvId, "PeerRtr   "+err.Error.ToString ()+" at '"+id.Source.Uri+"'");
        }
        else {
          RaTrc.Error   (id.CltRcvId, "PeerRtr   "+err.ToString ()+Environment.NewLine+"   partner uri = '"+id.Source.Uri+"'");
        }
      })
      .On<WcfPartnerListMessage>(list=>
      {
          RaTrc.Info( id.CltRcvId, "PeerRtr responds with list containing " + list.Item.Count + " services." );
          foreach( ActorInfo s in list.Item )
        {
          RegisterService (s, id.CltRcvId);
        }
      }
      ) != null)
      {
        RaTrc.Warning ("Wcf", "Received unexpected message from "+id.Source.Name+": "+id.Payload.ToString());
      }
    }
    
    #endregion
  }
}
