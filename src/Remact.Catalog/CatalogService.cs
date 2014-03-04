
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
    public void OnClientConnectedOrDisconnected (ActorMessage id)
    {
        Program.Catalog.SvcRegisterChanged = true;
    }

    public void OnUnknownRequest (ActorMessage msg)
    {
        RaLog.Warning(msg.SvcRcvId, "Unknown request or no service: " + msg.Payload.ToString());
        msg.SendResponse(new ErrorMessage(ErrorMessage.Code.AppRequestNotAcceptedByService, "Remact.CatalogService"));
    }

    // service request method implements IRemactCatalog
    private ReadyMessage InputIsOpen(ActorInfo actorInput, ActorMessage msg)
    {
        Program.Catalog.RegisterService(actorInput, msg.SvcRcvId);
        return new ReadyMessage();
    }

    // service request method implements IRemactCatalog
    ReadyMessage InputIsClosed(ActorInfo actorInput, ActorMessage msg)
    {
        Program.Catalog.RegisterService(actorInput, msg.SvcRcvId);
        return new ReadyMessage();
    }

    // service request method implements IRemactCatalog
    ActorInfo LookupInput(string actorInputName, ActorMessage msg)
    {
        ActorInfo found = null;
        foreach (ActorInfo s in Program.Catalog.SvcRegister.Item)
        {
            if (s.Name == actorInputName && s.IsServiceName)
            {
                found = new ActorInfo(s); // create a copy in order not to change the SvcRegister
                found.Usage = ActorInfo.Use.ServiceAddressResponse;
                break;
            }
        }

        if (found == null)
        {
            msg.SendResponse(new ErrorMessage(ErrorMessage.Code.AppDataNotAvailableInService,
              "Service name = '" + actorInputName + "' not registered in '" + Program.Catalog.Service.Uri + "'"));
        }

        return found;
    }

    // service request method implements IRemactCatalog
    ActorInfoList SynchronizeCatalog(ActorInfoList serviceList, ActorMessage msg)
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