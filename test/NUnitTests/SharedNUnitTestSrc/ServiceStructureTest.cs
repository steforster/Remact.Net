
// Copyright (c) https://github.com/steforster/Remact.Net

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Nito.Async;
using Remact.Net;
using Remact.Net.Remote;

namespace RemactNUnitTest
{
    [TestFixture]
    public class ServiceStructureTest
    {
        [TestFixtureSetUp] // run once when creating this class.
        public static void ClassInitialize()
        {
            RaLog.UsePlugin( new RaLog.PluginConsole() );

#if (BMS)
            Helper.LoadPluginDll(RemactConfigDefault.DefaultProtocolPluginName);
            var conf = Remact.Net.Plugin.Bms.Tcp.BmsProtocolConfig.Instance;
            conf.AddKnownMessageType(Request.ReadFromBms1Stream, Request.WriteToBms1Stream);
            //conf.AddKnownMessageType(RequestA1.ReadFromBms1Stream, RequestA1.WriteToBms1Stream);
            //conf.AddKnownMessageType(RequestA2.ReadFromBms1Stream, RequestA2.WriteToBms1Stream);
            conf.AddKnownMessageType(Response.ReadFromBms1Stream, Response.WriteToBms1Stream);
            //conf.AddKnownMessageType(ResponseA1.ReadFromBms1Stream, ResponseA1.WriteToBms1Stream);
            //conf.AddKnownMessageType(ResponseA2.ReadFromBms1Stream, ResponseA2.WriteToBms1Stream);
#endif

#if (JSON)
            Helper.LoadPluginDll(RemactConfigDefault.JsonProtocolPluginName);
#endif

            RemactCatalogClient.IsDisabled = true;
        }

        [SetUp] // run before each TestMethod.
        public void TestInitialize()
        {
            RaLog.ResetCount();
        }

        [TearDown] // run after each TestMethod (successful or failed).
        public void TestCleanup()
        {
            RemactPort.DisconnectAll();
            m_foreignActor.Close();
        }

        DelayActor m_foreignActor;

        [Test]
        public void When10ClientsSendTo1SyncService_ThenNoDelay()
        {
            m_foreignActor = new DelayActor();
            m_foreignActor.InputSync.LinkInputToNetwork( "DelayActorInputSync", tcpPort: 40001, publishToCatalog: false );
            m_foreignActor.Open();
            Helper.RunInWinFormsSyncContext( async () =>
            {
                Helper.AssertRunningOnClientThread();
                int clientCount = 10;
                var output = new RemactPortProxy[clientCount];
                var connnectOp = new Task<bool>[clientCount];
                var sendOp = new Task<RemactMessage<Response>>[clientCount];

                for (int i = 0; i < clientCount; i++)
                {
                    output[i] = new RemactPortProxy(string.Format("OUT{0:00}", i + 1));
                    output[i].LinkOutputToRemoteService(new Uri("ws://localhost:40001/Remact/DelayActorInputSync"));
                    output[i].TraceSend = true;
                    output[i].TraceReceive = true;
                    connnectOp[i] = output[i].ConnectAsync();
                }

                if (await Task.WhenAll(connnectOp).WhenTimeout(10000))
                {
                    Assert.Fail("Timeout while connecting");
                }

                for (int i = 0; i < clientCount; i++)
                {
                    Assert.IsTrue(output[i].IsOutputConnected, "output " + i + " not connected");
                    // we will convert and accept response payloads of type <Response> only. Other types will throw an exception.
                    sendOp[i] = output[i].SendReceiveAsync<Response>(null, new Request() { Text = ((char)('A' + i)).ToString() });
                }

                // normal delay for 10 requests is 10 x 10ms as they are handled synchronous on server side
                if (await Task.WhenAll(sendOp).WhenTimeout(700))
                {
                    Assert.Fail("Timeout, sync actor does block too long");
                }

                Assert.AreEqual(clientCount, m_foreignActor.StartedCount, "not all operations started");
                Assert.AreEqual(clientCount, m_foreignActor.FinishedCount, "not all operations finished");
                Assert.AreEqual(1, m_foreignActor.MaxParallelCount, "some operations run in parallel");
                for (int i = 0; i < clientCount; i++)
                {
                    Assert.IsInstanceOf<Response>(sendOp[i].Result.Payload, "wrong response type received");
                }
                RemactPort.DisconnectAll(); // This sends a disconnect message from all clients to the service and closes all client afterwards.
                Helper.AssertRunningOnClientThread();
                Helper.AssertTraceCount(0, 0);
            });
        }


        [Test]
        public void When10ClientsSendTo1AsyncService_ThenNoDelay()
        {
            m_foreignActor = new DelayActor();
            m_foreignActor.InputAsync.LinkInputToNetwork( "DelayActorInputAsync", tcpPort: 40001, publishToCatalog: false );
            m_foreignActor.Open();
            Helper.RunInWinFormsSyncContext(async () =>
            {
                Helper.AssertRunningOnClientThread();
                int clientCount = 10;
                var output = new RemactPortProxy[clientCount];
                var connnectOp = new Task<bool>[clientCount];
                var sendOp = new Task<RemactMessage<Response>>[clientCount];

                for (int i = 0; i < clientCount; i++)
                {
                    output[i] = new RemactPortProxy(string.Format("OUT{0:00}", i + 1));
                    output[i].LinkOutputToRemoteService(new Uri("ws://localhost:40001/Remact/DelayActorInputAsync"));
                    output[i].TraceSend = true;
                    output[i].TraceReceive = true;
                    connnectOp[i] = output[i].ConnectAsync();
                }

                if (await Task.WhenAll(connnectOp).WhenTimeout(10000))
                {
                    Assert.Fail("Timeout while connecting");
                }

                for (int i = 0; i < clientCount; i++)
                {
                    Assert.IsTrue(output[i].IsOutputConnected, "output " + i + " not connected");
                    // we will convert and accept response payloads of type <DelayActor.Response> only. Other types will throw an exception.
                    sendOp[i] = output[i].SendReceiveAsync<Response>(null, new Request() { Text = ((char)('A' + i)).ToString() });
                }

                // normal delay for 10 requests is 1 x 100ms as they are handled async on server side
                if (await Task.WhenAll(sendOp).WhenTimeout(900))
                {
                    Assert.Fail("Timeout, remote actor does not interleave successive requests");
                }

                Assert.AreEqual(clientCount, m_foreignActor.StartedCount, "not all operations started");
                Assert.AreEqual(clientCount, m_foreignActor.FinishedCount, "not all operations finished");
                Assert.IsTrue(m_foreignActor.MaxParallelCount > 1, "no operations run in parallel");
                for (int i = 0; i < clientCount; i++)
                {
                    Assert.IsInstanceOf<Response>(sendOp[i].Result.Payload, "wrong response type received");
                }
                RemactPort.DisconnectAll(); // This sends a disconnect message from all clients to the service and closes all client afterwards.
                Helper.AssertRunningOnClientThread();
                Helper.AssertTraceCount(0, 0);
            });
        }


        [Test]
        public void When20ClientsSendTo1InternalAsyncService_ThenNoDelay()
        {
            m_foreignActor = new DelayActor();
            // m_actor is not linked to network but must be opened to pick up its synchronization context
            m_foreignActor.Open();
            Helper.RunInWinFormsSyncContext(async () =>
            {
                Helper.AssertRunningOnClientThread();
                int clientCount = 20;
                var output = new RemactPortProxy[clientCount];
                var connnectOp = new Task<bool>[clientCount];
                var sendOp = new Task<RemactMessage<Response>>[clientCount];

                // trace all message flow
                m_foreignActor.InputAsync.TraceSend = true;
                m_foreignActor.InputAsync.TraceReceive = true;

                for (int i = 0; i < clientCount; i++)
                {
                    output[i] = new RemactPortProxy(string.Format("OUT{0:00}", i + 1));
                    output[i].LinkToService(m_foreignActor.InputAsync);
                    output[i].TraceSend = true;
                    output[i].TraceReceive = true;
                    connnectOp[i] = output[i].ConnectAsync();
                }

                if (await Task.WhenAll(connnectOp).WhenTimeout(100))
                {
                    Assert.Fail("Timeout while connecting");
                }

                for (int i = 0; i < clientCount; i++)
                {
                    Assert.IsTrue(output[i].IsOutputConnected, "output " + i + " not connected");
                    // we will convert and accept response payloads of type <DelayActor.Response> only. Other types will throw an exception.
                    sendOp[i] = output[i].SendReceiveAsync<Response>(null, new Request() { Text = ((char)('A' + i)).ToString() });
                }

                // normal delay for 10 requests is 1 x 100ms as they are handled async on server side
                if (await Task.WhenAll(sendOp).WhenTimeout(200))
                {
                    Assert.Fail("Timeout, internal actor does not interleave successive requests");
                }

                Assert.AreEqual(clientCount, m_foreignActor.StartedCount, "not all operations started");
                Assert.AreEqual(clientCount, m_foreignActor.FinishedCount, "not all operations finished");
                Assert.IsTrue(m_foreignActor.MaxParallelCount > 1, "no operations run in parallel");
                for (int i = 0; i < clientCount; i++)
                {
                    Assert.IsInstanceOf<Response>(sendOp[i].Result.Payload, "wrong response type received");
                }
                Helper.AssertRunningOnClientThread();
                Helper.AssertTraceCount(0, 0);
            });
        }


        [Test]
        public void When20ClientsSendTo1InternalSyncService_ThenTimeout()
        {
            m_foreignActor = new DelayActor();
            // m_actor is not linked to network but must be opened to pick up its synchronization context
            m_foreignActor.Open();
            Helper.RunInWinFormsSyncContext(async () =>
            {
                Helper.AssertRunningOnClientThread();
                int clientCount = 20;
                var output = new RemactPortProxy[clientCount];
                var connnectOp = new Task<bool>[clientCount];
                var sendOp = new Task<RemactMessage<Response>>[clientCount];

                // trace all message flow
                m_foreignActor.InputAsync.TraceSend = true;
                m_foreignActor.InputAsync.TraceReceive = true;

                for (int i = 0; i < clientCount; i++)
                {
                    output[i] = new RemactPortProxy(string.Format("OUT{0:00}", i + 1));
                    output[i].LinkToService(m_foreignActor.InputSync);
                    output[i].TraceSend = true;
                    output[i].TraceReceive = true;
                    connnectOp[i] = output[i].ConnectAsync();
                }

                if (await Task.WhenAll(connnectOp).WhenTimeout(100))
                {
                    Assert.Fail("Timeout while connecting");
                }

                for (int i = 0; i < clientCount; i++)
                {
                    Assert.IsTrue(output[i].IsOutputConnected, "output " + i + " not connected");
                    // we will convert and accept response payloads of type <DelayActor.Response> only. Other types will throw an exception.
                    sendOp[i] = output[i].SendReceiveAsync<Response>(null, new Request() { Text = ((char)('A' + i)).ToString() });
                }

                // normal delay for 20 requests is 20 x 10ms as they are handled synchronous on server side
                if (!await Task.WhenAll(sendOp).WhenTimeout(100))
                {
                    Assert.Fail("No timeout, actor does not synchronize requests");
                }
                Assert.IsTrue( m_foreignActor.StartedCount >= 5
                            && m_foreignActor.StartedCount <= 11,
                               m_foreignActor.StartedCount + " operations started");
                Assert.IsTrue( m_foreignActor.FinishedCount >= 4
                            && m_foreignActor.FinishedCount <= 10,
                               m_foreignActor.FinishedCount + " operations finished");
                Assert.AreEqual(1, m_foreignActor.MaxParallelCount, "some operations run in parallel");
                Helper.AssertRunningOnClientThread();
                Helper.AssertTraceCount(0, 0);
            });
        }
    }
}
