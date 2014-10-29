
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nito.Async;
using Remact.Net;

namespace UnitTest
{
    /// <summary>
    /// UnitTest.ActorDemo is intended as a first introduction to Remact.Net.
    /// 
    /// For further information about Remact.Net features
    /// see the wiki [https://github.com/steforster/Remact.Net].
    /// 
    /// Remact.Net supports 
    /// - application internal messaging
    /// - user specified sender context (service or client) passed to message handlers
    /// - remote service discovery
    /// - durable services and clients
    /// - unrequested notifications from services
    /// </summary>
    [TestClass]
    public class ActorDemo
    {
        [ClassInitialize] // run once when creating this class
        public static void ClassInitialize( TestContext testContext )
        {
            // Using the RaLog.PluginConsole, Remact.Net writes its trace to the VisualStudio output window.
            // It is captured for unit tests.
            RaLog.UsePlugin( new RaLog.PluginConsole() );

            // Register serialized message types of these unit tests.
            //WcfMessage.AddKnownType(typeof(DelayActor.Request));
            //WcfMessage.AddKnownType(typeof(DelayActor.RequestA1));
            //WcfMessage.AddKnownType(typeof(DelayActor.RequestA2));

            //WcfMessage.AddKnownType(typeof(DelayActor.Response));
            //WcfMessage.AddKnownType(typeof(DelayActor.ResponseA1));
            //WcfMessage.AddKnownType(typeof(DelayActor.ResponseA2));

            // Do not use Remact.Catalog application for these unit tests.
            Remact.Net.Remote.RemactCatalogClient.IsDisabled = true;
        }

        [TestInitialize] // run before each TestMethod
        public void TestInitialize()
        {
            RaLog.ResetCount();
        }

        [TestCleanup] // run after each TestMethod
        public void TestCleanup()
        {
            // This disconnects (closes) all clients and services that where created in the TestMethod.
            RemactPort.DisconnectAll();
        }



        // We are an actor and have an output and an input.
        RemactPortProxy m_output;
        RemactPortService m_input;
        int         m_pingPongRequestCount; // used to test
        
        // We will connect to this (possibly remote) actor, it is our partner.
        DelayActor  m_foreignActor;



        /// <summary>
        /// The 'Ping' unit test will send a message from our actor to the foreign actor.
        /// Then it will asynchronously wait for the response.
        /// We are running on a different thread than the foreign actor.
        /// 
        /// For demonstration we will first connect without WCF.
        /// 
        /// Afterwards we will connect the same actor through WCF.
        /// This way the partner actor is still running inside the same application 
        /// but could actually run on a separate application on a separate host.
        /// </summary>
        [TestMethod]
        public void ActorDemo_Ping()
        {
            // We create the foreign actor. 
            // Its input is not yet linked to network but must be opened to pick up its own synchronization context.
            m_foreignActor = new DelayActor();
            m_foreignActor.Open();

            // Now we create our own output (named "PingOutput") and link it to the foreign actor (named "InternalAsyncInput").
            // Note: this may be executed on any thread. Linking is just a preparatory step before the connection is opened.
            // 
            // Both sides (the output and the input) must eventually call "TryConnect" on their own thread SynchronizationContext
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
            Helper.RunTestInWpfSyncContext(async () =>
            {
                // We execute the first test run and close the connection afterwards.
                await ActorDemo_PingAsync();
                m_foreignActor.Close();
                Trace.WriteLine("--------------------------------------------------------");

                // For the second test run, we link the foreign input to the network. 
                // It means a WCF service is created and opened (on the foreign actors thread).
                // For unit tests we explicitly specify the TCP port. 
                // This would not be needed if we would run a WcfRouter application on the localhost.
                m_foreignActor.InputAsync.LinkInputToNetwork( "DelayActorInputAsync", tcpPort: 40001, publishToRouter: false );
                m_foreignActor.Open();

                // Now we link our output to the newly opened WCF service.
                // Here we have to specify the full URI.
                // When we would run the WcfRouter application, we needed only specify the servicename "DelayActorInputAsync".
                // The service then would be discovered on any known host.
                //
                // As before, the connection is built when calling TryConnect from the right SynchronizationContext in ActorDemo_PingAsync.
                m_output.LinkOutputToRemoteService( new Uri( "net.tcp://localhost:40001/Remact.Net/DelayActorInputAsync" ) );

                // Now we run the same test again, this time messages are sent through WCF.
                await ActorDemo_PingAsync();
                m_foreignActor.Close();
            });
        }

        public async Task ActorDemo_PingAsync()// the real test
        {
            Helper.AssertRunningOnClientThread();

            // When calling 'TryConnect', the output picks up the ClientThread's SynchronizationContext.
            // Note: We do not have to know whether our output has been linked to an application internal parner or to a
            //       partner input in another application (a WCF service).
            // Linking of outputs to inputs is done in higher level management code.
            // E.g. our actor could be delivered in a library assembly. 
            // The library user would then link our output to another actors input.
            //
            // Any time the 'await' keyword is used, the program flow may be interrupted and other tasks may be interleaved.
            // After the TryConnectAsync-Task has finished, the control flow continues on the same thread
            // (thanks to its SynchronizationContext).
            bool connectOk = await m_output.TryConnect();

            // The connectResponse is of type WcfReqIdent.
            // Actually all requests and responses are of this type.
            // The main member of this type is 'Message', of type WcfMessage.
            // This is the base class of all messages sent through AsynWcfLib.
            // When the connection has been made, we receive a WcfPartnerMessage identifying the connected ActorPort.
            // When the connection failed, we receive a 'WcfErrorMessage'. 
            // Exceptions are not thrown by Remact.Net unless you made a programming error.
            Assert.IsTrue(connectOk, "could not connect");

            // We can check the state of an ActorOutput or -Input:
            Assert.IsTrue( m_output.IsOutputConnected, "IsOutputConnected is not set" );

            // Now we have an open connection and will try to send a message.
            // The partner actor provides some request and response message types. 
            // The message types sent through WCF must be registered eg. by using WcfMessage.AddKnownType (see ClassInitialize).
            // These messages must be decorated with the [DataContract] and [DataMember] attributes in order to instruct 
            // the System.Runtime.Serialization.DataContractSerializer how to serialize this type.
            // The serializer is used only when our partner is linked over the network.
            var request = new DelayActor.Request() { Text = "hello world!" };
            var response = await m_output.Ask <DelayActor.Response>("...", request);
                
            // We either receive a message type provided by the partner actor or a WcfErrorMessage.
            Assert.IsInstanceOfType( response.Message, typeof( DelayActor.Response ), "unexpected response type" );

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
        /// As before, the test is executed without and with WCF.
        /// </summary>
        [TestMethod]
        public void ActorDemo_PingPong()
        {
            // We create the foreign actor without WCF service. 
            m_foreignActor = new DelayActor();

            // Now we create our own output (named "PingOutput") and link it to the partner input (named "InternalAsyncInput").
            m_output = new ActorOutput( "PingOutput" );
            m_output.LinkOutputTo( m_foreignActor.InputAsync );

            // Now we create our own input (named "PingPongInput") and link the partner output (named "PongOutput") to our input.
            m_input = new ActorInput( "PingPongInput", OnPingPongInput );
            m_foreignActor.PongOutput.LinkOutputTo( m_input );

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
            Helper.RunTestInWpfSyncContext( async () =>
            {
                // The first test run is without WCF:
                await ActorDemo_PingPongAsync();
                m_foreignActor.Close();
                Trace.WriteLine("--------------------------------------------------------");

                // Now we link the actors with WCF and run the same test again:
                m_foreignActor.InputAsync.LinkInputToNetwork( "DelayActorInputAsync", tcpPort: 40001, publishToRouter: false );
                m_output.LinkOutputToRemoteService( new Uri( "net.tcp://localhost:40001/Remact.Net/DelayActorInputAsync" ) );

                m_input.LinkInputToNetwork( "PingPongInput", tcpPort: 40001, publishToRouter: false );
                m_foreignActor.PongOutput.LinkOutputToRemoteService( new Uri( "net.tcp://localhost:40001/Remact.Net/PingPongInput" ) );
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
            await m_output.TryConnectAsync();

            // We can check the state of an ActorOutput or -Input:
            Assert.IsTrue( m_output.IsOutputConnected, "IsOutputConnected is not set" );
            Assert.IsFalse( m_input.MustOpenInput,     "MustOpenInput is set" );


            // Now we can send a request.
            // When we send "Ping", the actor will call back to our OnPingPongInput handler.
            m_pingPongRequestCount = 0;
            var request = new DelayActor.Request() { Text = "Ping" };
            WcfReqIdent rsp = await m_output.SendReceiveAsync( request );

            var err = rsp.Message as WcfErrorMessage;
            if( err != null )
            {
                RaLog.Error( "PingPongTest received ErrorMessage", err.ToString() );
            }
            // Test the received message. The OnPingPongInput handler must have been called in the meantime (on our thread!)
            Assert.IsInstanceOfType( rsp.Message, typeof( DelayActor.Response ), "wrong response received" );
            Assert.AreEqual( 1, m_pingPongRequestCount, "wrong count of requests received" );
            Assert.AreEqual( "PingPongResponse", (rsp.Message as DelayActor.Response).Text, "wrong response text" );

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
        async Task OnPingPongInput( WcfReqIdent req, bool dummy)
        {
            // Our input is called on the dedicated ActionThread
            Helper.AssertRunningOnClientThread();
            var request = req.Message as DelayActor.Request;
            Assert.AreEqual( "PingPong", request.Text, "wrong request received" );
            m_pingPongRequestCount++;
            req.SendResponse( new DelayActor.Response() { Text = "PingPongResponse" } );
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
        /// As before, the test is executed without and with WCF.
        /// </summary>
        [TestMethod]
        public void ActorDemo_DynamicDispatch()
        {
            // We create the foreign actor without WCF service. 
            m_foreignActor = new DelayActor();

            // Now we create our own output (named "DynOutput") and link it to the partner input (named "InternalSyncInput").
            var output = new ActorOutput<MyOutputContext>("DynOutput");
            output.LinkOutputTo( m_foreignActor.InputSync );

            // we like to see the full message flow in trace output
            output.TraceSend = true;
            output.TraceReceive = true;
            m_foreignActor.InputSync.TraceSend = true;
            m_foreignActor.InputSync.TraceReceive = true;

            // we prepare the foreign actor and our synchronization context
            m_foreignActor.Open();
            Helper.RunTestInWpfSyncContext( async () =>
            {
                // This test is without WCF
                Helper.AssertRunningOnClientThread();

                // We connect the previously linked output and input.
                await output.TryConnectAsync();
                Assert.IsTrue(output.IsOutputConnected, "IsOutputConnected is not set");

                m_responseCount = 0;
                m_responseA1Count = 0;
                m_defaultResponseCount = 0;

                // We send a request (without waiting) and use dynamic dispatch for the asynchronous response.
                m_pingPongRequestCount = 0;
                output.SendOut(new DelayActor.Request(), 
                    id1 => 
                        OnResponse(id1.Message as dynamic));

                // While the last request is still on the way, we send the next request and await both responses
                var id2 = await output.SendReceiveAsync(new DelayActor.RequestA1());

                // We use dynamic dispatch for the asynchronous response.
                OnResponse(id2.Message as dynamic);
               
                Assert.AreEqual(1, m_responseCount, "wrong m_responseCount");
                Assert.AreEqual(1, m_responseA1Count, "wrong m_responseA1Count");

                // At last we send an A2 request, the A2 response has no handler here and falls back to the A1 handler (base class)
                var id3 = await output.SendReceiveAsync(new DelayActor.RequestA2());
                OnResponse(id3.Message as dynamic);

                Assert.AreEqual(1, m_responseCount, "wrong m_responseCount");
                Assert.AreEqual(2, m_responseA1Count, "wrong m_responseA1Count");
                Assert.IsInstanceOfType(id3.Message, typeof(DelayActor.ResponseA2), "wrong response received");

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

        WcfReqIdent OnResponse(DelayActor.Response rsp)
        {
            m_responseCount++;
            return null; // the lambda expression passed to method 'SendOut' needs a return null, when the message has been handled.
        }

        void OnResponse(DelayActor.ResponseA1 rsp)
        {
            m_responseA1Count++;
        }

        void OnResponse(WcfMessage rsp)
        {
            m_defaultResponseCount++;
        }

        void OnResponse(WcfErrorMessage rsp)
        {
            RaLog.Error("PingPongTest received ErrorMessage", rsp.ToString());
        }

    }
}
