
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nito.Async;
using Remact.Net;
using Remact.Net.Remote;

namespace DemoUnitTest
{
    [TestClass]
    public class ServiceStructureTest
    {
        [ClassInitialize] // run once when creating this class
        public static void ClassInitialize( TestContext testContext )
        {
            RaLog.UsePlugin( new RaLog.PluginConsole() );
            // TODO: Init Dispatcher
            //RemactMessage.AddKnownType( typeof( DelayActor.Request ) );
            //RemactMessage.AddKnownType( typeof( DelayActor.Response ) );
            RemactCatalogClient.IsDisabled = true;
        }

        [TestInitialize] // run before each TestMethod
        public void TestInitialize()
        {
            RaLog.ResetCount();
        }

        [TestCleanup] // run after each TestMethod
        public void TestCleanup()
        {
            RemactPort.DisconnectAll();
        }

        DelayActor m_foreignActor;

        [TestMethod]
        public void When10ClientsSendTo1SyncService_ThenNoDelay()
        {
            m_foreignActor = new DelayActor();
            m_foreignActor.InputSync.LinkInputToNetwork( "DelayActorInputSync", tcpPort: 40001, publishToCatalog: false );
            m_foreignActor.Open();
            Helper.RunTestInWpfSyncContext( async () =>
            {
                Helper.AssertRunningOnClientThread();
                int clientCount = 10;
                var output = new RemactPortProxy[clientCount];
                var connnectOp = new Task<bool>[clientCount];
                var sendOp = new Task<RemactMessage<object>>[clientCount];

                for (int i = 0; i < clientCount; i++)
                {
                    output[i] = new RemactPortProxy(string.Format("OUT{0:00}", i + 1));
                    output[i].LinkOutputToRemoteService(new Uri("net.tcp://localhost:40001/Remact.Net/DelayActorInputSync"));
                    output[i].TraceSend = true;
                    output[i].TraceReceive = true;
                    connnectOp[i] = output[i].ConnectAsync();
                }

                if (await Task.WhenAll(connnectOp).WhenTimeout(10000))
                {
                    Assert.Fail("Timeout while opening");
                }

                for (int i = 0; i < clientCount; i++)
                {
                    Assert.IsTrue(output[i].IsOutputConnected, "output " + i + " not connected");
                    sendOp[i] = output[i].SendReceiveAsync<object>("???", new DelayActor.Request() { Text = ((char)('A' + i)).ToString() });
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
                    Assert.IsInstanceOfType(sendOp[i].Result.Payload, typeof(DelayActor.Response), "wrong response type received");
                }
                RemactPort.DisconnectAll(); // This sends a disconnect message from all clients to the service and closes all client afterwards.
                Helper.AssertRunningOnClientThread();
                Helper.AssertTraceCount(0, 0);
            });
            m_foreignActor.Close();
        }


        [TestMethod]
        public void When10ClientsSendTo1AsyncService_ThenNoDelay()
        {
            m_foreignActor = new DelayActor();
            m_foreignActor.InputAsync.LinkInputToNetwork( "DelayActorInputAsync", tcpPort: 40001, publishToCatalog: false );
            m_foreignActor.Open();
            Helper.RunTestInWpfSyncContext(async () =>
            {
                Helper.AssertRunningOnClientThread();
                int clientCount = 10;
                var output = new RemactPortProxy[clientCount];
                var connnectOp = new Task<bool>[clientCount];
                var sendOp = new Task<RemactMessage<object>>[clientCount];

                for (int i = 0; i < clientCount; i++)
                {
                    output[i] = new RemactPortProxy(string.Format("OUT{0:00}", i + 1));
                    output[i].LinkOutputToRemoteService(new Uri("net.tcp://localhost:40001/Remact.Net/DelayActorInputAsync"));
                    output[i].TraceSend = true;
                    output[i].TraceReceive = true;
                    connnectOp[i] = output[i].ConnectAsync();
                }

                if (await Task.WhenAll(connnectOp).WhenTimeout(10000))
                {
                    Assert.Fail("Timeout while opening");
                }

                for (int i = 0; i < clientCount; i++)
                {
                    Assert.IsTrue(output[i].IsOutputConnected, "output " + i + " not connected");
                    sendOp[i] = output[i].SendReceiveAsync<object>("???", new DelayActor.Request() { Text = ((char)('A' + i)).ToString() });
                }

                // normal delay for 10 requests is 1 x 100ms as they are handled async on server side
                if (await Task.WhenAll(sendOp).WhenTimeout(900))
                {
                    Assert.Fail("Timeout, WCF actor does not interleave successive requests");
                }

                Assert.AreEqual(clientCount, m_foreignActor.StartedCount, "not all operations started");
                Assert.AreEqual(clientCount, m_foreignActor.FinishedCount, "not all operations finished");
                Assert.IsTrue(m_foreignActor.MaxParallelCount > 1, "no operations run in parallel");
                for (int i = 0; i < clientCount; i++)
                {
                    Assert.IsInstanceOfType(sendOp[i].Result.Payload, typeof(DelayActor.Response), "wrong response type received");
                }
                RemactPort.DisconnectAll(); // This sends a disconnect message from all clients to the service and closes all client afterwards.
                Helper.AssertRunningOnClientThread();
                Helper.AssertTraceCount(0, 0);
            });
            m_foreignActor.Close();
        }


        [TestMethod]
        public void When20ClientsSendTo1InternalAsyncService_ThenNoDelay()
        {
            m_foreignActor = new DelayActor();
            // m_actor is not linked to network but must be opened to pick up its synchronization context
            m_foreignActor.Open();
            Helper.RunTestInWpfSyncContext(async () =>
            {
                Helper.AssertRunningOnClientThread();
                int clientCount = 20;
                var output = new RemactPortProxy[clientCount];
                var connnectOp = new Task<bool>[clientCount];
                var sendOp = new Task<RemactMessage<object>>[clientCount];

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

                if (await Task.WhenAll(sendOp).WhenTimeout(100))
                {
                    Assert.Fail("Timeout while opening");
                }

                for (int i = 0; i < clientCount; i++)
                {
                    Assert.IsTrue(output[i].IsOutputConnected, "output " + i + " not connected");
                    sendOp[i] = output[i].SendReceiveAsync<object>("???", new DelayActor.Request() { Text = ((char)('A' + i)).ToString() });
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
                    Assert.IsInstanceOfType(sendOp[i].Result.Payload, typeof(DelayActor.Response), "wrong response type received");
                }
                Helper.AssertRunningOnClientThread();
                Helper.AssertTraceCount(0, 0);
            });
            m_foreignActor.Close();
        }


        [TestMethod]
        public void When20ClientsSendTo1InternalSyncService_ThenTimeout()
        {
            m_foreignActor = new DelayActor();
            // m_actor is not linked to network but must be opened to pick up its synchronization context
            m_foreignActor.Open();
            Helper.RunTestInWpfSyncContext(async () =>
            {
                Helper.AssertRunningOnClientThread();
                int clientCount = 20;
                var output = new RemactPortProxy[clientCount];
                var connnectOp = new Task<bool>[clientCount];
                var sendOp = new Task<RemactMessage<object>>[clientCount];

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

                if (await Task.WhenAll(sendOp).WhenTimeout(100))
                {
                    Assert.Fail("Timeout while opening");
                }

                for (int i = 0; i < clientCount; i++)
                {
                    Assert.IsTrue(output[i].IsOutputConnected, "output " + i + " not connected");
                    sendOp[i] = output[i].SendReceiveAsync<object>("???", new DelayActor.Request() { Text = ((char)('A' + i)).ToString() });
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
            m_foreignActor.Close();
        }

    }
}
