
// Copyright (c) https://github.com/steforster/Remact.Net

using System.Threading.Tasks;
using Remact.Net;

namespace Remact.Catalog
{
    /// <summary>
    /// This is the CatalogService Entrypoint.
    /// It dispatches requests and returns a response.
    /// </summary>
    class CatalogService
  {
    public Task OnClientConnectedOrDisconnected (RemactMessage id)
    {
        Program.Catalog.SvcRegisterChanged = true;
        return null; // completed synchronously
    }

    public Task OnUnknownRequest (RemactMessage msg)
    {
        RaLog.Warning(msg.SvcRcvId, "Unknown request or no service: " + msg.Payload.ToString());
        msg.SendResponse(new ErrorMessage(ErrorCode.ActorReceivedMessageForUnknownDestinationMethod, "Remact.CatalogService got unknown request: "+msg.ToString()));
        return null; // completed synchronously
    }

    // service request method implements IRemactCatalog
    private ReadyMessage ServiceOpened(ActorInfo service, RemactMessage msg)
    {
        Program.Catalog.RegisterService(service, msg.SvcRcvId);
        return new ReadyMessage();
    }

    // service request method implements IRemactCatalog
    ReadyMessage ServiceClosed(ActorInfo service, RemactMessage msg)
    {
        Program.Catalog.RegisterService(service, msg.SvcRcvId);
        return new ReadyMessage();
    }

    // service request method implements IRemactCatalog
    ActorInfo LookupService(string serviceName, RemactMessage msg)
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
            msg.SendResponse(new ErrorMessage(ErrorCode.ServiceNameNotRegisteredInCatalog,
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