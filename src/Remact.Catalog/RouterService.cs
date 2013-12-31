
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel; 
using System.Threading;
using Remact.Net;
using Remact.Net.Internal;

namespace Remact.Catalog
{
  /// <summary>
  /// This is the RouterService Entrypoint.
  /// It dispatches requests and returns a response.
  /// </summary>
  class RouterService
  {
    //----------------------------------------------------------------------------------------------
    #region Public Methods


    public void OnClientConnectedOrDisconnected (Request id)
    {
        Program.Router.SvcRegisterChanged = true;
    }

    public void OnRequest (Request id)
    {
        ActorMessage service = id.Message as ActorMessage;
        bool ok = false;
        if (service != null && service.IsServiceName)
        {
            switch (service.Usage)
            {
                case ActorMessage.Use.ServiceEnableRequest:  ok = RegisterService(service, id); break;
                case ActorMessage.Use.ServiceDisableRequest: ok = RegisterService(service, id); break;
                case ActorMessage.Use.ServiceAddressRequest: ok = GetAddress(service, id); break;
                default: break;// continue below
            }
        }

        WcfPartnerListMessage list = id.Message as WcfPartnerListMessage;
        if (list != null)
        {
            ok = RegisterList(list, id);
        }


        if (!ok)
        {
            RaTrc.Warning(id.SvcRcvId, "Unknown request or no service: " + id.Message.ToString());
            id.SendResponse(new ErrorMessage(ErrorMessage.Code.AppRequestNotAcceptedByService, "WcfRouterService"));
        }
    }


    #endregion
    //----------------------------------------------------------------------------------------------
    #region Private Methods

    // A single service entry is beeing enabled, disabled or updated
    // return the service info as response
    private bool RegisterService (ActorMessage req, Request id)
    {
      ActorMessage response = req;
      if (Program.Router.RegisterService (req, id.SvcRcvId))
      {
        // req is used in the SvcRegister now. We have to create a copy
        response = new ActorMessage(req);
      }

      // reply the registered service
      if (req.Usage == ActorMessage.Use.ServiceEnableRequest)
      {
        response.Usage = ActorMessage.Use.ServiceEnableResponse;
      }
      else 
      {
        response.Usage = ActorMessage.Use.ServiceDisableResponse;
      }

      id.SendResponse (response);
      return true;
    }


    // A list of service entries is beeing enabled, disabled or updated
    // return our list as response, to synchronize the peer router
    private bool RegisterList (WcfPartnerListMessage list, Request id)
    {
        RaTrc.Info( id.SvcRcvId, "PeerRtr sends list containing " + list.Item.Count + " services." );
        foreach( ActorMessage s in list.Item )
        {
            Program.Router.RegisterService (s, id.SvcRcvId);
        }
        id.SendResponse (Program.Router.SvcRegister);
        return true;
    }
    

    // GetAddress: Search URI (with TCP port number) of a registered service
    private bool GetAddress (ActorMessage search, Request id)
    {
      bool found = false;
      foreach (ActorMessage s in Program.Router.SvcRegister.Item)
      {
        if (s.Name == search.Name
         && s.IsServiceName == search.IsServiceName)
        {
          search = new ActorMessage (s); // create a copy in order not to change the SvcRegister
          search.Usage = ActorMessage.Use.ServiceAddressResponse;
          found = true;
          break;
        }
      }

      if (!found)
      {
        id.SendResponse (new ErrorMessage (ErrorMessage.Code.AppDataNotAvailableInService,
          "Service name = '" + search.Name + "' not registered in '" + Program.Router.Service.Uri + "'"));
      }
      else
      {
        id.SendResponse (search);
      }
      return true;
    }// GetAddress

    #endregion

  }// class RouterService
}// namespace