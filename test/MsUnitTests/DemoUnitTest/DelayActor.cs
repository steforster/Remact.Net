
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remact.Net;

namespace DemoUnitTest
{
    public class DelayActor
    {
        public DelayActor()
        {
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
        public IRemactPortService  InputAsync { get { return m_inputAsync; } }
        public IRemactPortProxy PongOutput { get { return m_pongOutput; } }

        public int          StartedCount;
        public int          FinishedCount;
        public int          MaxParallelCount;

        private int         m_currentParallelCount;
        private RemactPortService<MyInputContext>  m_inputSync;
        private RemactPortService m_inputAsync;
        private RemactPortProxy m_pongOutput;


        // async void is not allowed, it returns at first await, before the Task has finished.
        private void OnDelayResponseBy10( RemactMessage id, MyInputContext inputContext )
        {
            if (++m_currentParallelCount > MaxParallelCount) MaxParallelCount = m_currentParallelCount;
            StartedCount++;
            try
            {
                Helper.AssertRunningOnServiceThread();

                // dynamic dispatch depending on received message type
                OnRequest(id.Payload as dynamic, id, inputContext);

                Helper.AssertRunningOnServiceThread();
            }
            catch (Exception ex)
            {
                // failed result must be passed to the test thread
                Helper.ServiceException = ex;
            }
            FinishedCount++;
            m_currentParallelCount--;
        }

        private void OnRequest(Request req, RemactMessage id, MyInputContext inputContext)
        {
            Assert.IsInstanceOfType(req, typeof(Request), "wrong message type received");
            Thread.Sleep(10);
            id.SendResponse(new Response() { Text = "response after blocking for 10ms" });
        }

        private void OnRequest(RequestA1 req, RemactMessage id, MyInputContext inputContext)
        {
            Assert.IsInstanceOfType(req, typeof(RequestA1), "wrong message type received");
            id.SendResponse(new ResponseA1());
        }

        private void OnRequest(RequestA2 req, RemactMessage id, MyInputContext inputContext)
        {
            Assert.IsInstanceOfType(req, typeof(RequestA2), "wrong message type received");
            id.SendResponse(new ResponseA2());
        }

        private void OnRequest(RemactMessage req, RemactMessage id, MyInputContext inputContext)
        {
            Assert.IsInstanceOfType(req, typeof(RemactMessage), "wrong message type received");
            id.SendResponse(new ReadyMessage());
        }


        // async Task matches the WcfMessageHandlerAsync delegate
        private async Task OnDelayResponseBy100Async(RemactMessage id)
        {
            if (++m_currentParallelCount > MaxParallelCount) MaxParallelCount = m_currentParallelCount;
            StartedCount++;
            try
            {
                Helper.AssertRunningOnServiceThread();
                if (!(id.Payload is Request))
                {
                    RaLog.Error("", "");
                }
                Assert.IsInstanceOfType( id.Payload, typeof( Request ), "wrong message type received" );

                if( (id.Payload as Request).Text == "Ping" )
                {
                    if( !m_pongOutput.IsOutputConnected )
                    {
                        await m_pongOutput.ConnectAsync();
                    }
                    // Test method ActorDemo_PingPongAsync sent us a request
                    // we will get the response from a call to another service...
                    var rsp = await m_pongOutput.SendReceiveAsync<object>("???", new Request() { Text = "PingPong" } );
                    id.SendResponse( rsp.Payload );
                }
                else
                {
                    await Task.Delay( 100 );
                    id.SendResponse( new Response() { Text = "response after 100ms" } );
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


        
        #region Messages

        public class Request
        {
            public string Text;
            
            public override string ToString()
            {
                return GetType().Name + ": " + Text;
            }
        }

        public class RequestA1 : Request
        {
        }

        public class RequestA2 : RequestA1
        {
        }


        public class Response
        {
            public string Text;

            public override string ToString()
            {
                return GetType().Name + ": " + Text;
            }
        }

        public class ResponseA1 : Response
        {
        }

        public class ResponseA2 : ResponseA1
        {
        }

        #endregion
    }
}
