
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Remact.Net;
using Remact.Net.Remote;

namespace Remact.Catalog
{
  /// <summary>
  /// This is the CatalogService Entrypoint.
  /// It dispatches requests and returns a response.
  /// </summary>
  class CatalogService
  {
    //----------------------------------------------------------------------------------------------
    #region Public Methods


    public void OnClientConnectedOrDisconnected (ActorMessage id)
    {
        Program.Catalog.SvcRegisterChanged = true;
    }

    public void OnRequest (ActorMessage id)
    {
        ActorInfo service = id.Payload as ActorInfo;
        bool ok = false;
        if (service != null && service.IsServiceName)
        {
            switch (service.Usage)
            {
                case ActorInfo.Use.ServiceEnableRequest:  ok = RegisterService(service, id); break;
                case ActorInfo.Use.ServiceDisableRequest: ok = RegisterService(service, id); break;
                case ActorInfo.Use.ServiceAddressRequest: ok = GetAddress(service, id); break;
                default: break;// continue below
            }
        }

        ActorInfoList list = id.Payload as ActorInfoList;
        if (list != null)
        {
            ok = RegisterList(list, id);
        }


        if (!ok)
        {
            RaLog.Warning(id.SvcRcvId, "Unknown request or no service: " + id.Payload.ToString());
            id.SendResponse(new ErrorMessage(ErrorMessage.Code.AppRequestNotAcceptedByService, "Remact.CatalogService"));
        }
    }


    #endregion
    //----------------------------------------------------------------------------------------------
    #region Private Methods

    // A single service entry is beeing enabled, disabled or updated
    // return the service info as response
    private bool RegisterService (ActorInfo req, ActorMessage id)
    {
      ActorInfo response = req;
      if (Program.Catalog.RegisterService (req, id.SvcRcvId))
      {
        // req is used in the SvcRegister now. We have to create a copy
        response = new ActorInfo(req);
      }

      // reply the registered service
      if (req.Usage == ActorInfo.Use.ServiceEnableRequest)
      {
        response.Usage = ActorInfo.Use.ServiceEnableResponse;
      }
      else 
      {
        response.Usage = ActorInfo.Use.ServiceDisableResponse;
      }

      id.SendResponse (response);
      return true;
    }


    // A list of service entries is beeing enabled, disabled or updated
    // return our list as response, to synchronize the peer catalog
    private bool RegisterList (ActorInfoList list, ActorMessage id)
    {
        RaLog.Info( id.SvcRcvId, "PeerRtr sends list containing " + list.Item.Count + " services." );
        foreach( ActorInfo s in list.Item )
        {
            Program.Catalog.RegisterService (s, id.SvcRcvId);
        }
        id.SendResponse (Program.Catalog.SvcRegister);
        return true;
    }
    

    // GetAddress: Search URI (with TCP port number) of a registered service
    private bool GetAddress (ActorInfo search, ActorMessage id)
    {
      bool found = false;
      foreach (ActorInfo s in Program.Catalog.SvcRegister.Item)
      {
        if (s.Name == search.Name
         && s.IsServiceName == search.IsServiceName)
        {
          search = new ActorInfo (s); // create a copy in order not to change the SvcRegister
          search.Usage = ActorInfo.Use.ServiceAddressResponse;
          found = true;
          break;
        }
      }

      if (!found)
      {
        id.SendResponse (new ErrorMessage (ErrorMessage.Code.AppDataNotAvailableInService,
          "Service name = '" + search.Name + "' not registered in '" + Program.Catalog.Service.Uri + "'"));
      }
      else
      {
        id.SendResponse (search);
      }
      return true;
    }// GetAddress

    #endregion

  }// class CatalogService
}// namespace