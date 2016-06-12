
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Threading;   // SynchronizationContext
using System.Threading.Tasks;



namespace Remact.Net.Remote
{
    /// <summary>
    /// This is the Service Entrypoint. It dispatches requests and returns a response.
    /// </summary>
    public class MultithreadedServiceNet40 : IRemactProtocolDriverToService
    {
        private RemactService _service;
        private RemactServiceUser _svcUser;

        /// <summary>
        /// Initializes a new instance of the <see cref="MultithreadedServiceNet40"/> class.
        /// </summary>
        /// <param name="svcUser">The service user (client stub).</param>
        /// <param name="service">The service.</param>
        public MultithreadedServiceNet40(RemactServiceUser svcUser, RemactService service)
        {
            _svcUser = svcUser;
            _service = service;
        }

        /// <summary>
        /// Occurs when a client-stub calls a service.
        /// </summary>
        void IRemactProtocolDriverToService.MessageToService(LowerProtocolMessage msg)
        {
            bool connectEvent = false;
            bool disconnectEvent = false;
            // We are instantiated for each connected client, we know the _svcUser (because of the underlying WebSocket implementation).
            var message = new RemactMessage(_service.ServiceIdent, msg.DestinationMethod, msg.Payload, msg.Type,
                                            _svcUser.PortClient, _svcUser.ClientId, msg.RequestId);
            message.SerializationPayload = msg.SerializationPayload;

            object response = _service.ServiceIdent.GetResponseExceptionSafe(message, ()=>
                {
                    object rsp;
                    lock(_service)
                    {
                        rsp = _service.CheckRemactInternalResponse(message, ref _svcUser, ref connectEvent, ref disconnectEvent);
                    }
                    
                    if (rsp != null)
                    {
                        if (connectEvent || disconnectEvent) // no error
                        {
                            var reqCopy = new RemactMessage(message);
                            reqCopy.Response = reqCopy; // do not reply a ReadyMessage
                            // call event OnInputConnected or OnInputDisconnected on the correct thread.
                            //var task = DoRequestAsync(reqCopy); 
                            ((IRemotePort)message.Destination).PostInput(reqCopy);
                            if (disconnectEvent)
                            {
                                return null; // no reply to a disconnect notification
                            }
                            //var dummy = task.Result; // blocking wait
                        }
                        return rsp;// return the Remact internal response.
                    }
                    else
                    {
                        ((IRemotePort)message.Destination).PostInput(message);
                        //DoRequestAsync(message);
                        // Response and optional notifications have been returned or will be returned by the actor thread
                        return null;
                    }
                });

            // when the response is null here, it has been sent already or will asynchronously be sent from the actor thread
            if (response != null)
            {
                message.SendResponse(response);
            }
        }


        void       IRemactProtocolDriverToService.OpenAsync(OpenAsyncState state, IRemactProtocolDriverToClient callback) { }
        Uri        IRemactProtocolDriverToService.ServiceUri { get { return null; } }
        PortState  IRemactProtocolDriverToService.PortState { get { return PortState.Ok; } }
        void       IDisposable.Dispose() { }
    }
}
