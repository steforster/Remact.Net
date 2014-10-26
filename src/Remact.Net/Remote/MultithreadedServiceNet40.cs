
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

        public MultithreadedServiceNet40(RemactServiceUser svcUser, RemactService service)
        {
            _svcUser = svcUser;
            _service = service;
        }

        /// <summary>
        /// Occurs when a WampClientProxy calls a service.
        /// </summary>
        void IRemactProtocolDriverService.MessageFromClient(RemactMessage message)
        {
            object response = null;
            bool connectEvent = false;
            bool disconnectEvent = false;
            try
            {
                // We are instantiated for each connected client, we know the _svcUser (because of the underlying WebSocket implementation).
                // Several threads may access the common RemactService. TODO: is the lock really needed ?
                lock (_service)
                {
                    response = _service.CheckRemactInternalResponse(message, ref _svcUser, ref connectEvent, ref disconnectEvent);
                }

                // multithreaded access, several requests may run in parallel. They will be scheduled for execution on the right synchronization context.
                if (response != null)
                {
                    if (connectEvent || disconnectEvent) // no error
                    {
                        var reqCopy = new RemactMessage(message);
                        reqCopy.Response = reqCopy; // do not reply a ReadyMessage
                        var task = DoRequestAsync(reqCopy); // call event OnInputConnected or OnInputDisconnected on the correct thread.
                        if (disconnectEvent)
                        {
                            return; // no reply to disconnect notification
                        }
                        var dummy = task.Result; // blocking wait
                    }
                    // return the Remact internal response.
                }
                else
                {
                    DoRequestAsync(message);
                    // Response and optional notifications have been returned to the client already
                    return;
                }
            }
            catch (RemactException ex)
            {
                RaLog.Exception(message.SvcRcvId, ex, _service.ServiceIdent.Logger);
                response = new ErrorMessage(ex);
            }
            catch (NotImplementedException ex)
            {
                RaLog.Exception(message.SvcRcvId, ex, _service.ServiceIdent.Logger);
                response = new ErrorMessage(ErrorCode.NotImplementedOnService, ex);
            }
            catch (NotSupportedException ex)
            {
                RaLog.Exception(message.SvcRcvId, ex, _service.ServiceIdent.Logger);
                response = new ErrorMessage(ErrorCode.NotImplementedOnService, ex);
            }
            catch (ArgumentException ex)
            {
                RaLog.Exception(message.SvcRcvId, ex, _service.ServiceIdent.Logger);
                response = new ErrorMessage(ErrorCode.ArgumentExceptionOnService, ex);
            }
            catch (Exception ex)
            {
                RaLog.Exception(message.SvcRcvId, ex, _service.ServiceIdent.Logger);
                response = new ErrorMessage(ErrorCode.UnhandledExceptionOnService, ex);
            }

            message.SendResponse(response);
        }



        private Task<RemactMessage> DoRequestAsync( RemactMessage msg )
        {
            var tcs = new TaskCompletionSource<RemactMessage>();

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
                            RaLog.Exception("RemactMessage to '" + msg.Destination.Name + "' cannot be handled by application", ex);
                            tcs.SetException( ex );
                        }
                    }, null )); // obj
            }

            return tcs.Task;
        }

        void       IRemactProtocolDriverService.OpenAsync(OpenAsyncState state, IRemactProtocolDriverCallbacks callback) { }
        Uri        IRemactProtocolDriverService.ServiceUri { get { return null; } }
        PortState  IRemactProtocolDriverService.PortState { get { return PortState.Ok; } }
        void       IRemactProtocolDriverService.Dispose() { }
    }
}
