
// Copyright (c) https://github.com/steforster/Remact.Net

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Nito.Async;
using Remact.Net;

namespace DemoUnitTest
{
    /// <summary>
    /// UnitTest.ActorDemo is intended as a first introduction to Remact.Net.
    /// 
    /// For further information about Remact.Net features
    /// see [https://github.com/steforster/Remact.Net].
    /// 
    /// Remact.Net supports 
    /// - application internal messaging
    /// - user specified sender context (service or client) passed to message handlers
    /// - remote service discovery
    /// - durable services and clients
    /// - unrequested notifications from services
    /// </summary>
    [TestFixture]
    public class ActorDemo
    {
        [TestFixtureSetUp] // run once when creating this class.
        public static void ClassInitialize()
        {
            // Using the RaLog.PluginConsole, Remact.Net writes its trace to the VisualStudio output window and to the unit test output.
            RaLog.UsePlugin( new RaLog.PluginConsole() );

            // Do not use Remact.Catalog application for these unit tests.
            Remact.Net.Remote.RemactCatalogClient.IsDisabled = true;
        }

        [SetUp] // run before each TestMethod.
        public void TestInitialize()
        {
            RaLog.ResetCount();
        }

        [TearDown] // run after each TestMethod (successful or failed).
        public void TestCleanup()
        {
            // This disconnects (closes) all clients and services that where created in the TestMethod.
            RemactPort.DisconnectAll();
        }


        // We are an actor and have an output and an input.
        RemactPortProxy m_output;
        RemactPortService m_input;
        int m_pingPongRequestCount; // used to test
        
        // We will connect to this (possibly remote) actor, it is our partner.
        DelayActor m_foreignActor;



        /// <summary>
        /// The 'Ping' unit test will send a message from our actor to the foreign actor.
        /// Then it will asynchronously wait for the response.
        /// We are running on a different thread than the foreign actor.
        /// 
        /// For demonstration we will first connect to a service actor running in the same process.
        /// 
        /// Afterwards we will connect the same actor through remote connection.
        /// This way the partner actor is still running inside the same process 
        /// but could actually run on a separate application on a separate host.
        /// </summary>
        [Test]
        public void ActorDemo_Ping()
        {
            // We create the foreign actor. 
            // Its input is not yet linked to network but must be opened to pick up its own synchronization context.
            m_foreignActor = new DelayActor();
            m_foreignActor.Open();

            // Now we create our own output (named "PingOutput") and link it to the foreign actor (named "InternalAsyncInput").
            // Note: this may be executed on any thread. Linking is just a preparatory step before the connection is opened.
            // 
            // Both sides (the output and the input) must eventually call "ConnectAsync" or "Open" on their own thread SynchronizationContext
            // in order to open the connection to the linked partner.
            m_output = new RemactPortProxy("PingOutput");
            m_output.LinkToService (m_foreignActor.InputAsync);

            // we like to see the full message flow in trace output
            m_output.TraceSend = true;
            m_output.TraceReceive = true;
            m_foreignActor.InputAsync.TraceSend = true;
            m_foreignActor.InputAsync.TraceReceive = true;

            // This test method was started by VisualStudio on a threadpool thread.
            // These threads do not have a SychronizationContext.
            // Therefore we use the Helper class to start a WPF DispatcherSynchronizationContext. 
            // It will run our test and make it possible that callback messages are executed on 
            // the single thread that is responsible for this actor. 
            Helper.RunInWinFormsSyncContext(async () =>
            {
                // We execute the first test run and close the connection afterwards.
                await ActorDemo_PingAsync();
                m_foreignActor.Close();
                Trace.WriteLine("--------------------------------------------------------");

                // For the second test run, we link the foreign input to the network. 
                // It means a remotly accessible service is created and opened (on the foreign actors thread).
                // For unit tests we explicitly specify the TCP port. 
                // This would not be needed if we would run a Remact.Catalog service on the localhost.
                m_foreignActor.InputAsync.LinkInputToNetwork( "DelayActorInputAsync", tcpPort: 40001, publishToCatalog: false );
                m_foreignActor.Open();

                // Because we do not use the catalog, we have to provide the absolute Uri when linking our client to the remote service. 
                // The Uri consists of websocket scheme + localhost + port defined above + RemactConfigDefault.WsNamespace + servicename
                //
                // When using the Remact.Catalog service, we needed only specify the servicename "DelayActorInputAsync".
                // The catalog then provides information about host, port etc.
                //
                // As before, the connection is built later on, when calling ConnectAsync from the right SynchronizationContext in ActorDemo_PingAsync.
                m_output.LinkOutputToRemoteService( new Uri( "ws://localhost:40001/Remact/DelayActorInputAsync" ) );

                // Now we run the same test again, this time messages are sent through sockets.
                await ActorDemo_PingAsync();
                m_foreignActor.Close();
            });
        }

        public async Task ActorDemo_PingAsync()// the real test
        {
            Helper.AssertRunningOnClientThread();

            // When calling 'ConnectAsync', the output picks up the ClientThread's SynchronizationContext.
            // Note: We do not have to know whether our output has been linked to an application internal service or to a
            //       service in another application.
            // Linking of outputs to inputs is done in higher level management code.
            // E.g. our actor could be delivered in a library assembly. 
            // The library user would then link our output to another actors input.
            //
            // Any time the 'await' keyword is used, the program flow may be interrupted and other tasks may be interleaved.
            // After the TryConnectAsync-Task has finished, the control flow continues on the same thread
            // (thanks to its SynchronizationContext).
            bool connectOk = await m_output.ConnectAsync();
            Assert.IsTrue(connectOk, "could not connect");

            // We can check the state of an ActorOutput or -Input:
            Assert.IsTrue( m_output.IsOutputConnected, "IsOutputConnected is not set" );

            // Now we have an open connection and will try to send a message.
            // The partner actor has an interface for some request and response message types. 
            // See DelayActor.cs, #region Messages.
            var request = new DelayActor.Request() { Text = "hello world!" };

            // As response to our request, we get a RemactMessage. It contains the 'Payload' member, that is of type 'DelayActor.Response'.
            // In case, the service sends another message type (e.g. ErrorMessage), a RemactException is thrown.
            var response = await m_output.SendReceiveAsync <DelayActor.Response>(null, request);
            Assert.IsInstanceOf<DelayActor.Response>( response.Payload, "unexpected response type" );

            // When disconnecting the client, we send a last message to inform the service and close the client afterwards.
            m_output.Disconnect();

            // Just to be sure, we check whether we still safely run on our own dedicated thread.
            Helper.AssertRunningOnClientThread();
            // We also check whether some unexpected error- or warning traces have been written.
            Helper.AssertTraceCount( 0, 0 );
        }


        /// <summary>
        /// The 'PingPong' unit test will send a message to the foreign actor and asynchronously wait for the response.
        /// Before sending the response, the partner actor will send another message to us and also wait for a response.
        /// Thanks to the asynchronous behaviour, we do not create a deadlock.
        /// 
        /// As before, the test is executed without and with remote connection.
        /// </summary>
        [Test]
        public void ActorDemo_PingPong()
        {
            // We create the foreign actor with local connection. 
            m_foreignActor = new DelayActor();

            // Now we create our own output (named "PingOutput") and link it to the partner input (named "InternalAsyncInput").
            m_output = new RemactPortProxy( "PingOutput" );
            m_output.LinkToService( m_foreignActor.InputAsync );

            // Now we create our own input (named "PingPongInput") and link the partner output (named "PongOutput") to our input.
            m_input = new RemactPortService( "PingPongInput", OnPingPongInput );
            m_foreignActor.PongOutput.LinkToService( m_input );

            // we like to see the full message flow in trace output
            m_output.TraceSend = true;
            m_output.TraceReceive = true;
            m_foreignActor.InputAsync.TraceSend = true;
            m_foreignActor.InputAsync.TraceReceive = true;
            m_foreignActor.PongOutput.TraceSend = true;
            m_foreignActor.PongOutput.TraceReceive = true;
            m_input.TraceSend = true;
            m_input.TraceReceive = true;

            // we prepare the foreign actor and our synchronization context
            m_foreignActor.Open();
            Helper.RunInWinFormsSyncContext( async () =>
            {
                // The first test run is without remote connection:
                await ActorDemo_PingPongAsync();
                m_foreignActor.Close();
                Trace.WriteLine("--------------------------------------------------------");

                // Now we link the remote actors and run the same test again:
                m_foreignActor.InputAsync.LinkInputToNetwork( "DelayActorInputAsync", tcpPort: 40001, publishToCatalog: false );
                m_output.LinkOutputToRemoteService( new Uri( "ws://localhost:40001/Remact/DelayActorInputAsync" ) );

                m_input.LinkInputToNetwork( "PingPongInput", tcpPort: 40001, publishToCatalog: false );
                m_foreignActor.PongOutput.LinkOutputToRemoteService( new Uri( "ws://localhost:40001/Remact/PingPongInput" ) );
                m_foreignActor.Open();

                await ActorDemo_PingPongAsync();
                m_foreignActor.Close();
            });
        }

        public async Task ActorDemo_PingPongAsync()// the real test
        {
            Helper.AssertRunningOnClientThread();

            // Now, we connect the previously linked output and input.
            // The m_input.TryConnect() is asynchronous. m_input.Open() is exactly the same functionality.
            m_input.Open();
            await m_output.ConnectAsync();

            // We can check the state of an ActorOutput or -Input:
            Assert.IsTrue( m_output.IsOutputConnected, "IsOutputConnected is not set" );
            Assert.IsFalse( m_input.MustOpenInput,     "MustOpenInput is set" );

            // prepare the request payload
            m_pingPongRequestCount = 0;
            var request = new DelayActor.Request() { Text = "Ping" };

            // Send a message containing the request payload.
            // When we send "Ping", the DelayActor will call back to our OnPingPongInput handler.
            // Then the OnPingPongInput handler will return a response to the DelayActor.
            // This response is transferred to us. Here, we accept any response payload <object>: 
            RemactMessage msg = await m_output.SendReceiveAsync<object>(null , request );

            // Because we accepted every object as response payload, we have to analyze what payload type we received.
            ErrorMessage err;
            if (msg.IsError && msg.TryConvertPayload(out err))
            {
                RaLog.Error( "PingPongTest received ErrorMessage", err.ToString() );
            }

            DelayActor.Response rsp;
            if( !msg.TryConvertPayload(out rsp) )
            {
                Assert.Fail( "wrong response type received" );
            }

            // Test the received message. The OnPingPongInput handler must have been called in the meantime (on our thread!)
            Assert.AreEqual( 1, m_pingPongRequestCount, "wrong count of requests received" );
            Assert.AreEqual( "PingPongResponse", rsp.Text, "wrong response text" );

            // TODO, ActorPort.DisconnectAll() works only for remote clients and routed remote services.
            m_output.Disconnect();
            m_input.Disconnect();
            // Just to be sure, we check whether we still safely run on our own dedicated thread.
            Helper.AssertRunningOnClientThread();
            // We also check whether some unexpected error- or warning traces have been written.
            Helper.AssertTraceCount( 0, 0 );
        }


        // This is the messagehandler for messages coming to our m_input
        #pragma warning disable 1998 // Consider using the 'await' operator to await non-blocking API calls, or 'await Task.Run(...)' to do CPU-bound work on a background thread.
        async Task OnPingPongInput(RemactMessage msg)
        {
            // Our input is called on the dedicated ActionThread
            Helper.AssertRunningOnClientThread();
            DelayActor.Request req;
            if( !msg.TryConvertPayload(out req) )
            {
                Assert.Fail( "wrong request type received" );
            }
            Assert.AreEqual( "PingPong", req.Text, "wrong request text received" );
            m_pingPongRequestCount++;
            msg.SendResponse( new DelayActor.Response() { Text = "PingPongResponse" } );
        }
        #pragma warning restore 1998

        class MyOutputContext
        {
        }

        /// <summary>
        /// The 'PingPong' unit test will send a message to the foreign actor and asynchronously wait for the response.
        /// Before sending the response, the partner actor will send another message to us and also wait for a response.
        /// Thanks to the asynchronous behaviour, we do not create a deadlock.
        /// 
        /// As before, the test is executed without and TODO: with remote connection.
        /// </summary>
        [Test]
        public void ActorDemo_DynamicDispatch()
        {
            // We create the foreign actor with local connection. 
            m_foreignActor = new DelayActor();

            // Now we create our own output (named "DynOutput") and link it to the partner input (named "InternalSyncInput").
            var output = new RemactPortProxy<MyOutputContext>("DynOutput");
            output.LinkToService( m_foreignActor.InputSync );

            // we like to see the full message flow in trace output
            output.TraceSend = true;
            output.TraceReceive = true;
            m_foreignActor.InputSync.TraceSend = true;
            m_foreignActor.InputSync.TraceReceive = true;

            // we prepare the foreign actor and our synchronization context
            m_foreignActor.Open();
            Helper.RunInWinFormsSyncContext( async () =>
            {
                // This test is without remote connection.
                Helper.AssertRunningOnClientThread();

                // We connect the previously linked output and input.
                await output.ConnectAsync();
                Assert.IsTrue(output.IsOutputConnected, "IsOutputConnected is not set");

                m_responseCount = 0;
                m_responseA1Count = 0;
                m_defaultResponseCount = 0;

                // We send a request (without waiting) and use dynamic dispatch for the asynchronous response.
                m_pingPongRequestCount = 0;
                output.SendReceiveAsync<object>(null, new DelayActor.Request(), 
                    (pld,msg) => 
                        OnResponse(pld as dynamic));

                // While the last request is still on the way, we send the next request and await both responses
                var id2 = await output.SendReceiveAsync<object>(null, new DelayActor.RequestA1());

                // We use dynamic dispatch for the asynchronous response.
                OnResponse(id2.Payload as dynamic);
               
                Assert.AreEqual(1, m_responseCount, "wrong m_responseCount");
                Assert.AreEqual(1, m_responseA1Count, "wrong m_responseA1Count");

                // At last we send an A2 request, the A2 response has no handler here and falls back to the A1 handler (base class)
                var id3 = await output.SendReceiveAsync<object>(null, new DelayActor.RequestA2());
                OnResponse(id3.Payload as dynamic);

                Assert.AreEqual(1, m_responseCount, "wrong m_responseCount");
                Assert.AreEqual(2, m_responseA1Count, "wrong m_responseA1Count");
                Assert.IsInstanceOf<DelayActor.ResponseA2>(id3.Payload, "wrong response received");

                // disconnect and last checks
                output.Disconnect();
                m_foreignActor.Close();
                Helper.AssertRunningOnClientThread();
                Helper.AssertTraceCount(0, 0);
            });
        }

        int m_responseCount;
        int m_responseA1Count;
        int m_defaultResponseCount;

        void OnResponse(DelayActor.Response rsp)
        {
            m_responseCount++;
        }

        void OnResponse(DelayActor.ResponseA1 rsp)
        {
            m_responseA1Count++;
        }

        void OnResponse(RemactMessage rsp)
        {
            m_defaultResponseCount++;
        }

        void OnResponse(ErrorMessage rsp)
        {
            RaLog.Error("PingPongTest received ErrorMessage", rsp.ToString());
        }
    }
}
