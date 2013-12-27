
// Copyright (c) 2012  AsyncWcfLib.sourceforge.net

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Description; // ServiceHost
using System.Windows.Forms;
using SourceForge.AsyncWcfLib.Basic;
using System.Net;

namespace SourceForge.AsyncWcfLib
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
  class Router : IWcfServiceConfiguration, IWcfClientConfiguration
  {
    //----------------------------------------------------------------------------------------------
    #region Fields
    
    private ActorInput    m_WcfService;
    private RouterService m_RouterService;
    private int           m_knownServiceCount;

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Properties

    public  IActorPortId             Service { get{ return m_WcfService;} }
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
            WcfTrc.Info( "Wcf", "Closed service " + m_WcfService.Uri );
            m_WcfService = null;
        }
      }
      catch (Exception ex)
      {
        WcfTrc.Exception ("Wcf: Error while closing the service", ex);
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

        foreach (WcfPartnerMessage s in SvcRegister.Item)
        {
            if (s.Usage == WcfPartnerMessage.Use.ServiceEnableRequest)
            {
                s.TimeoutSeconds -= seconds;
                if (s.TimeoutSeconds < 0)
                {
                    s.Usage = WcfPartnerMessage.Use.ServiceDisableRequest;
                    SvcRegisterChanged = true;
                    WcfTrc.Warning("   "+s.Name+"  ", "Timeout  "+s.ToString ());
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
        
            foreach (WcfPartnerMessage s in SvcRegister.Item)
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
                if (s.Usage == WcfPartnerMessage.Use.ServiceEnableRequest) sb.Append ("++");
                                                                      else sb.Append ("--");
                sb.Append (s.Uri);
                sb.Append (" in ");
                sb.Append (WcfDefault.Instance.GetAppIdentification (s.AppName, s.AppInstance, s.HostName, s.ProcessId));
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
    public bool RegisterService (WcfPartnerMessage svc, string mark)
    {
      svc.RouterHopCount++;                                 // ==1: Direct info from service on local host
      if (svc.RouterHopCount > 1) svc.TimeoutSeconds = 120; // > 1: Indirect info from another router
      
      if (svc.Usage != WcfPartnerMessage.Use.ServiceEnableRequest
       && svc.Usage != WcfPartnerMessage.Use.ServiceDisableRequest)
      {
        WcfTrc.Error (mark, "Got wrong status: "+svc.ToString ());
        svc.Usage = WcfPartnerMessage.Use.ServiceDisableRequest;
      }

      bool changed = false;
      int found = SvcRegister.Item.FindIndex (s => s.IsEqualTo (svc));
      if (found < 0)
      {
          WcfTrc.Info( mark, "Register  " + svc.Uri.ToString() );
          SvcRegister.Item.Add (svc);
          Program.Router.SvcRegisterChanged = true;
          return true;
      }
      else
      {
        WcfPartnerMessage registered = SvcRegister.Item[found];
        
        if (registered.Uri != svc.Uri)
        {
          // a changed or a second service tries to register
          if (registered.Usage == WcfPartnerMessage.Use.ServiceDisableRequest
                  && svc.Usage == WcfPartnerMessage.Use.ServiceEnableRequest)
          {
              WcfTrc.Info( mark, "Start new   " + svc.Uri.ToString() );
              changed = true;
          }
          else if (registered.Usage == WcfPartnerMessage.Use.ServiceEnableRequest
                       && svc.Usage == WcfPartnerMessage.Use.ServiceEnableRequest) 
          {
            if (registered.ApplicationRunTime < svc.ApplicationRunTime)
            {
                WcfTrc.Info( mark, "Switch to " + svc.Uri.ToString ());
                changed = true;
            }
            else //if (svc.RouterHopCount > 1)
            { // will be removed from all registers after some time
                WcfTrc.Info( mark, "Backup    " + svc.Uri.ToString() );
            }
          }
        }
        else
        {
          if( registered.RouterHopCount < svc.RouterHopCount )
          {
              // circular reference: do not use this old information
          }
          else if( registered.Usage == WcfPartnerMessage.Use.ServiceDisableRequest
                       && svc.Usage == WcfPartnerMessage.Use.ServiceEnableRequest)
          {
              WcfTrc.Info( mark, "Restart   " + svc.Uri.ToString() );
              changed = true;
          }
          else if (registered.Usage == WcfPartnerMessage.Use.ServiceEnableRequest
                       && svc.Usage == WcfPartnerMessage.Use.ServiceDisableRequest)
          {
              WcfTrc.Info( mark, "Stopped   " + svc.Uri.ToString() );
              changed = true;
          }
          else if (registered.Usage == WcfPartnerMessage.Use.ServiceEnableRequest
                       && svc.Usage == WcfPartnerMessage.Use.ServiceEnableRequest)
          {
              WcfTrc.Info( mark, "Alive     " + svc.Uri.ToString() );
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
        m_WcfService = new ActorInput(WcfDefault.Instance.RouterServiceName, m_RouterService.OnRequest);
        m_WcfService.OnInputConnected    += m_RouterService.OnClientConnectedOrDisconnected;
        m_WcfService.OnInputDisconnected += m_RouterService.OnClientConnectedOrDisconnected;
        m_WcfService.LinkInputToNetwork( null, WcfDefault.Instance.RouterPort, publishToRouter: false, serviceConfig: this ); // calls our DoServiceConfiguration
        m_WcfService.TraceConnect = false;
        m_WcfService.TryConnect();
      
        string names = string.Empty;
        foreach (string host in Properties.Settings.Default.PeerHosts)
        {
            if (host != null && host.Trim().Length > 0)
            {
                var output = new ActorOutput<SvcDat>("Clt>"+host, OnResponseFromPeerRouter);
                output.LinkOutputToRemoteService(new Uri("http://" + host + ':' + WcfDefault.Instance.RouterPort
                                 + "/" + WcfDefault.WsNamespace + "/" + WcfDefault.Instance.RouterServiceName),// no router lookup as uri is given.
                                 this ); // calls our DoClientConfiguration
                output.OutputContext = new SvcDat();
                output.TryConnect();
                PeerRouters.Add (output);
                names += host+", ";
            }
        }
        WcfTrc.Info( "Wcf", "Opened clients for peer routers on " + PeerRouters.Count + " configured PeerHosts: " + names );
    }// Open


    // implement IWcfServiceConfiguration
    public void DoServiceConfiguration(ServiceHost serviceHost, ref Uri uri, bool isRouter)
    {
        WcfDefault.Instance.DoServiceConfiguration( serviceHost, ref uri, /*isRouter=*/ true );
    }


    // implement IWcfClientConfiguration
    public void DoClientConfiguration( ClientBase<IWcfBasicContractSync> clientBase, ref Uri uri, bool forRouter )
    {
        WcfDefault.Instance.DoClientConfiguration( clientBase, ref uri, /*forRouter=*/ true );
    }
    

    private void OnResponseFromPeerRouter(WcfReqIdent id, SvcDat svcDat)
    {
      if (
       id.On<WcfPartnerMessage>(partner=>
      {
        WcfTrc.Info      (id.CltRcvId, "PeerRtr   "+partner.ToString ());
      })
      .On<WcfErrorMessage>(err=>
      {
        if (err.Error == WcfErrorMessage.Code.ServiceNotRunning) {
          WcfTrc.Warning (id.CltRcvId, "PeerRtr   "+err.Error.ToString ()+" at '"+id.Sender.Uri+"'");
        }
        else {
          WcfTrc.Error   (id.CltRcvId, "PeerRtr   "+err.ToString ()+Environment.NewLine+"   partner uri = '"+id.Sender.Uri+"'");
        }
      })
      .On<WcfPartnerListMessage>(list=>
      {
          WcfTrc.Info( id.CltRcvId, "PeerRtr responds with list containing " + list.Item.Count + " services." );
          foreach( WcfPartnerMessage s in list.Item )
        {
          RegisterService (s, id.CltRcvId);
        }
      }
      ) != null)
      {
        WcfTrc.Warning ("Wcf", "Received unexpected message from "+id.Sender.Name+": "+id.Message.ToString());
      }
    }
    
    #endregion
  }
}
