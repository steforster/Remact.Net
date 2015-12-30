
// Copyright (c) https://github.com/steforster/Remact.Net

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Remact.Net;

namespace DemoUnitTest
{
    public class DelayActor
    {
        public DelayActor()
        {
            // The actor has two service ports and one output port to another service.
            // We do not use the RemactPort.InputDispatcher. All incoming messages are handled by the defaultRequestHandler.
            m_inputSync  = new RemactPortService<MyInputContext> ( "InternalSyncInput",  OnDelayResponseBy10 );
            m_inputAsync = new RemactPortService( "InternalAsyncInput", OnDelayResponseBy100Async );
            m_pongOutput = new RemactPortProxy( "PongOutput" );
        }

        public void Open( object logger = null )
        {
            Helper.ServiceThread.DoSynchronously( ()=> 
                {
                    m_inputSync.Logger = logger;
                    m_inputAsync.Logger = logger;
                    m_pongOutput.Logger = logger;
                    m_inputSync.Open(); // open and bind the inputs to the actors ServiceThread
                    m_inputAsync.Open();
                }); 
        }

        public void Close()
        {
            m_pongOutput.Disconnect();
            m_inputSync.Disconnect();
            m_inputAsync.Disconnect();
        }

        public IRemactPortService InputSync  { get { return m_inputSync; } }
        public IRemactPortService InputAsync { get { return m_inputAsync; } }
        public IRemactPortProxy   PongOutput { get { return m_pongOutput; } }

        public int  StartedCount;
        public int  FinishedCount;
        public int  MaxParallelCount;

        private int m_currentParallelCount;
        private RemactPortService<MyInputContext>  m_inputSync;
        private RemactPortService m_inputAsync;
        private RemactPortProxy m_pongOutput;


        // This method is the defaultRequestHandler for all incoming messages of port 'InternalSyncInput'
        private Task OnDelayResponseBy10( RemactMessage msg, MyInputContext inputContext )
        {
            if (++m_currentParallelCount > MaxParallelCount) MaxParallelCount = m_currentParallelCount;
            StartedCount++;
            try
            {
                Helper.AssertRunningOnServiceThread();

                Request request;
                Assert.IsTrue(msg.TryConvertPayload(out request), "unknown request type received");
                // dynamic dispatch depending on received request type
                OnRequest(request as dynamic, msg, inputContext);

                Helper.AssertRunningOnServiceThread();
            }
            catch (Exception ex)
            {
                // failed result must be passed to the test thread
                Helper.ServiceException = ex;
            }
            FinishedCount++;
            m_currentParallelCount--;
            return null;
        }

        private void OnRequest(Request req, RemactMessage msg, MyInputContext inputContext)
        {
            Assert.IsInstanceOf<Request>(req, "wrong message type received");
            Thread.Sleep(10);
            msg.SendResponse(new Response() { Text = "response after blocking for 10ms" });
        }

        private void OnRequest(RequestA1 req, RemactMessage msg, MyInputContext inputContext)
        {
            Assert.IsInstanceOf<RequestA1>(req, "wrong message type received");
            msg.SendResponse(new ResponseA1());
        }

        private void OnRequest(RequestA2 req, RemactMessage msg, MyInputContext inputContext)
        {
            Assert.IsInstanceOf<RequestA2>(req, "wrong message type received");
            msg.SendResponse(new ResponseA2());
        }


        // This method is the defaultRequestHandler for all incoming messages of port 'InternalAsyncInput'
        private async Task OnDelayResponseBy100Async(RemactMessage msg)
        {
            if (++m_currentParallelCount > MaxParallelCount) MaxParallelCount = m_currentParallelCount;
            StartedCount++;
            try
            {
                Helper.AssertRunningOnServiceThread();
                Request request;
                if (!msg.TryConvertPayload(out request))
                {
                    RaLog.Error("", "");
                }
                Assert.IsInstanceOf<Request>(request, "wrong message type received");

                if (request.Text == "Ping")
                {
                    if( !m_pongOutput.IsOutputConnected )
                    {
                        await m_pongOutput.ConnectAsync();
                    }
                    // Test method ActorDemo_PingPongAsync sent us a request
                    // we will get the response from a call to another service...
                    var rsp = await m_pongOutput.SendReceiveAsync<object>(null, new Request() { Text = "PingPong" });
                    // we pass the response payload without conversion to the client that called us
                    msg.SendResponse(rsp.Payload);
                }
                else
                {
                    await Task.Delay( 100 );
                    msg.SendResponse( new Response() { Text = "response after 100ms" } );
                }
                Helper.AssertRunningOnServiceThread();
            }
            catch( Exception ex )
            {
                // failed result must be passed to the test thread
                Helper.ServiceException = ex;
            }
            FinishedCount++;
            m_currentParallelCount--;
        }

        public class MyInputContext
        {
        }
    }
}
