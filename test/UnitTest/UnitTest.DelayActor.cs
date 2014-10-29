
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remact.Net;

namespace UnitTest
{
    public class DelayActor
    {
        public DelayActor()
        {
            m_inputSync  = new ActorInput<MyInputContext> ( "InternalSyncInput",  OnDelayResponseBy10 );
            m_inputAsync = new ActorInput ( "InternalAsyncInput", OnDelayResponseBy100Async );
            m_pongOutput = new ActorOutput( "PongOutput" );
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

        public IActorInput  InputSync  { get { return m_inputSync; } }
        public IActorInput  InputAsync { get { return m_inputAsync; } }
        public IActorOutput PongOutput { get { return m_pongOutput; } }

        public int          StartedCount;
        public int          FinishedCount;
        public int          MaxParallelCount;

        private int         m_currentParallelCount;
        private ActorInput<MyInputContext>  m_inputSync;
        private ActorInput                  m_inputAsync;
        private ActorOutput                 m_pongOutput;


        // async void is not allowed, it returns at first await, before the Task has finished.
        private void OnDelayResponseBy10( WcfReqIdent id, MyInputContext inputContext )
        {
            if (++m_currentParallelCount > MaxParallelCount) MaxParallelCount = m_currentParallelCount;
            StartedCount++;
            try
            {
                Helper.AssertRunningOnServiceThread();

                // dynamic dispatch depending on received message type
                OnRequest(id.Message as dynamic, id, inputContext);

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

        private void OnRequest(Request req, WcfReqIdent id, MyInputContext inputContext)
        {
            Assert.IsInstanceOfType(req, typeof(Request), "wrong message type received");
            Thread.Sleep(10);
            id.SendResponse(new Response() { Text = "response after blocking for 10ms" });
        }

        private void OnRequest(RequestA1 req, WcfReqIdent id, MyInputContext inputContext)
        {
            Assert.IsInstanceOfType(req, typeof(RequestA1), "wrong message type received");
            id.SendResponse(new ResponseA1());
        }

        private void OnRequest(RequestA2 req, WcfReqIdent id, MyInputContext inputContext)
        {
            Assert.IsInstanceOfType(req, typeof(RequestA2), "wrong message type received");
            id.SendResponse(new ResponseA2());
        }

        private void OnRequest(WcfMessage req, WcfReqIdent id, MyInputContext inputContext)
        {
            Assert.IsInstanceOfType(req, typeof(WcfMessage), "wrong message type received");
            id.SendResponse(new WcfIdleMessage());
        }


        // async Task matches the WcfMessageHandlerAsync delegate
        private async Task OnDelayResponseBy100Async( WcfReqIdent id, bool dummy )
        {
            if (++m_currentParallelCount > MaxParallelCount) MaxParallelCount = m_currentParallelCount;
            StartedCount++;
            try
            {
                Helper.AssertRunningOnServiceThread();
                if (!(id.Message is Request))
                {
                    RaLog.Error("", "");
                }
                Assert.IsInstanceOfType( id.Message, typeof( Request ), "wrong message type received" );

                if( (id.Message as Request).Text == "Ping" )
                {
                    if( !m_pongOutput.IsOutputConnected )
                    {
                        await m_pongOutput.TryConnectAsync();
                    }
                    // Test method ActorDemo_PingPongAsync sent us a request
                    // we will get the response from a call to another service...
                    var rsp = await m_pongOutput.SendReceiveAsync( new Request() { Text = "PingPong" } );
                    id.SendResponse( rsp.Message );
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
