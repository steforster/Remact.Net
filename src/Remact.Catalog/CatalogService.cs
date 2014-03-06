
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
    public void OnClientConnectedOrDisconnected (RemactMessage id)
    {
        Program.Catalog.SvcRegisterChanged = true;
    }

    public void OnUnknownRequest (RemactMessage msg)
    {
        RaLog.Warning(msg.SvcRcvId, "Unknown request or no service: " + msg.Payload.ToString());
        msg.SendResponse(new ErrorMessage(ErrorMessage.Code.AppRequestNotAcceptedByService, "Remact.CatalogService"));
    }

    // service request method implements IRemactCatalog
    private ReadyMessage InputIsOpen(ActorInfo service, RemactMessage msg)
    {
        Program.Catalog.RegisterService(service, msg.SvcRcvId);
        return new ReadyMessage();
    }

    // service request method implements IRemactCatalog
    ReadyMessage InputIsClosed(ActorInfo service, RemactMessage msg)
    {
        Program.Catalog.RegisterService(service, msg.SvcRcvId);
        return new ReadyMessage();
    }

    // service request method implements IRemactCatalog
    ActorInfo LookupInput(string serviceName, RemactMessage msg)
    {
        ActorInfo found = null;
        foreach (ActorInfo s in Program.Catalog.SvcRegister.Item)
        {
            if (s.Name == serviceName && s.IsServiceName)
            {
                found = new ActorInfo(s); // create a copy in order not to change the SvcRegister
                break;
            }
        }

        if (found == null)
        {
            msg.SendResponse(new ErrorMessage(ErrorMessage.Code.AppDataNotAvailableInService,
              "Service name = '" + serviceName + "' not registered in '" + Program.Catalog.Service.Uri + "'"));
        }

        return found;
    }

    // service request method implements IRemactCatalog
    ActorInfoList SynchronizeCatalog(ActorInfoList serviceList, RemactMessage msg)
    {
        RaLog.Info(msg.SvcRcvId, "Peer catalog sends list containing " + serviceList.Item.Count + " services.");
        foreach (ActorInfo s in serviceList.Item)
        {
            Program.Catalog.RegisterService(s, msg.SvcRcvId);
        }

        return Program.Catalog.SvcRegister;
    }
  }
}