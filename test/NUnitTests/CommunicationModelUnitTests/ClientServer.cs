
// Copyright (c) 2014, github.com/steforster/Remact.Net

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// CommunicationModels:    Partners:
// ClientServer            ClientServerService
// Notification            NotificationService
// ServerClient            ServerClientService
// PublishSubscribe        PublishSubscribeService

namespace Remact.Net.CommunicationModelUnitTests
{
    [TestFixture]
    public class ClientServer
    {
        private RemactPortProxy _proxy;

        #region Test infrastructure

        [TestFixtureSetUp] // run once when creating this class.
        public void FixtureSetUp()
        {
        }

        [TestFixtureTearDown]  // run once after all tests of this class have been executed.
        public void FixtureTearDown()
        {
            // This disconnects (closes) all clients and services that where created in the TestMethod.
            RemactPort.DisconnectAll();
            // This disposes all WebSocket threads
            Alchemy.WebSocketClient.Shutdown();
        }

        [SetUp] // run before each TestMethod.
        public void SetUp()
        {
        }

        [TearDown] // run after each TestMethod (successful or failed).
        public void TearDown()
        {
        }

        #endregion

        public bool SetUpTestVariant(string testName, int variant)
        {
            if (_proxy != null)
            {
                _proxy.Disconnect();
                _proxy = null;
            }

            if (variant == 1)
            {
                Trace.WriteLine("Start '" + testName + "' variant 1: communicate locally in the same process");
                var service = new ClientServerService(false);
                _proxy = new RemactPortProxy("ClientServiceTestLocal", DefaultResponseHandler);
                _proxy.IsMultithreaded = true;
                _proxy.LinkToService(service.Port);
            }
            else if (variant == 2)
            {
                Trace.WriteLine("Start '" + testName + "' variant 2: communicate to a remote process");
                var service = new ClientServerService(true);
                _proxy = new RemactPortProxy("ClientServiceTestRemote", DefaultResponseHandler);
                _proxy.IsMultithreaded = true;
                _proxy.LinkOutputToRemoteService(service.RemoteUri);
            }
            else
            {
                return false;
            }

            return true;
        }

        protected void DefaultResponseHandler(RemactMessage msg)
        {
            throw new NotImplementedException();
        }

        [Test]
        public void SendReceiveString()
        {
            int variant = 0;
            while (SetUpTestVariant("SendReceiveString", ++variant))
            {
                // client side
                var t1 = _proxy.TryConnect();
                Assert.IsTrue(t1.Result, "could not connect");

                var t2 = _proxy.Ask<string>("ReceiveString", "a request");
                Assert.AreEqual("the response", t2.Result.Payload, "wrong response content");
            }
        }
    }
}
