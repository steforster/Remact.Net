
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Text;
using System.Net;         // IPEndPoint
using System.Net.Sockets; // Socket
using System.Threading;   // SynchronizationContext
using System.Threading.Tasks;

using Alchemy;
using Alchemy.Classes;
using Remact.Net.Protocol;
using Remact.Net.Protocol.Wamp;



namespace Remact.Net.Remote
{
    /// <summary>
    /// This is the Service Entrypoint. It dispatches requests and returns a response.
    /// </summary>
    public class MultithreadedServiceNet40 : IRemactProtocolDriverService
    {
        private RemactService _service;
        private RemactServiceUser _svcUser;

        public MultithreadedServiceNet40(RemactService service, RemactServiceUser svcUser)
        {
            _service = service;
            _svcUser = svcUser;
        }

        /// <summary>
        /// Occurs when a WampClientProxy calls a service.
        /// </summary>
        void IRemactProtocolDriverService.MessageFromClient(ActorMessage message)
        {
            object response = null;
            try
            {
                // We are instantiated for each connected client, we know the _svcUser (other stacks do not).
                // Several threads may access the common RemactService. TODO: is the lock really needed ?
                lock (_service)
                {
                    response = _service.CheckBasicResponse(message, ref _svcUser);
                }

                // multithreaded access, several requests may run in parallel. They will be scheduled for execution on the right synchronization context.
                if( response != null )
                {
                    var connectMsg = response as ActorInfo;
                    if (connectMsg != null) // no error and connected
                    {
                        var reqCopy = new ActorMessage(message.Source, message.ClientId, message.RequestId, 
                                                       message.Destination, message.DestinationMethod, message.Payload, 
                                                       message.SourceLambda);
                        reqCopy.Response = reqCopy; // do not send a ReadyMessage
                        var task = DoRequestAsync( reqCopy ); // call event OnInputConnected or OnInputDisconnected on the correct thread.
                        if (connectMsg.Usage != ActorInfo.Use.ServiceDisconnectResponse)
                        {
                            var dummy = task.Result; // blocking wait!
                        }
                        // return the original response.
                    }
                }
                else
                {
                    var task = DoRequestAsync(message);
                    //message = task.Result; // blocking wait!
                    // Response and optional notifications have been returned to the client already
                    return;
                }
            }
            catch( Exception ex )
            {
                RaLog.Exception(message.SvcRcvId, ex, _service.ServiceIdent.Logger);
                response = new ErrorMessage( ErrorMessage.Code.UnhandledExceptionOnService, ex );
            }
            message.SendResponse(response);
        }



        private Task<ActorMessage> DoRequestAsync( ActorMessage msg )
        {
            var tcs = new TaskCompletionSource<ActorMessage>();

            if( msg.Destination.IsMultithreaded
                || msg.Destination.SyncContext == null
                || msg.Destination.ManagedThreadId == Thread.CurrentThread.ManagedThreadId )
            { // execute request on the calling thread or multi-threaded
            #if !BEFORE_NET45
                id.Input.DispatchMessageAsync( id )
                    .ContinueWith((t)=>
                        tcs.SetResult( id )); // when finished the first task: finish tcs and let the original request thread return the response.
            #else
                msg.Destination.DispatchMessage( msg );
                tcs.SetResult( msg );
            #endif
            }
            else
            {
                Task.Factory.StartNew(() => 
                    msg.Destination.SyncContext.Post( obj =>
                    {   // execute task on the specified thread after passing the delegate through the message queue...
                        try
                        {
                #if !BEFORE_NET45
                            id.Input.DispatchMessageAsync( id )        // execute request async on the thread bound to the Input
                                .ContinueWith((t)=>
                                    tcs.SetResult( id )); // when finished the first task: finish tcs and let the original request thread return the response.
                #else
                            msg.Destination.DispatchMessage( msg );// execute request synchronously on the thread bound to the Destination
                            tcs.SetResult( msg );
                #endif
                        }
                        catch( Exception ex )
                        {
                            RaLog.Exception( "ActorMessage to " + msg.Destination.Name + " cannot be handled by application", ex );
                            tcs.SetException( ex );
                        }
                    }, null )); // obj
            }

            return tcs.Task;
        }

        void       IRemactProtocolDriverService.OpenAsync(ActorMessage message, IRemactProtocolDriverCallbacks callback) { }
        Uri        IRemactProtocolDriverService.ServiceUri { get { return null; } }
        PortState  IRemactProtocolDriverService.PortState { get { return PortState.Ok; } }
        void       IRemactProtocolDriverService.Dispose() { }
    }
}