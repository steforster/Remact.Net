
// Copyright (c) 2014, github.com/steforster/Remact.Net

using NUnit.Framework;
using System;
using System.Threading.Tasks;

// Communication model tests
// Client classes:         Service classes:
// ---------------         ----------------
// ClientServer            ClientServerService
// Notification            NotificationService
// ServerClient            ServerClientService
// PublishSubscribe        PublishSubscribeService

namespace Remact.Net.UnitTests.CommunicationModel
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

            // This disposes all WebSocket threads, removes the test warning 'Attempted to access an unloaded AppDomain'.
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

        // We create a matrix of tests.
        // Each test method is run in several communication variants.
        public bool SetUpTestVariant(int variant)
        {
            if (_proxy != null)
            {
                _proxy.Disconnect();
                _proxy = null;
            }

            if (variant == 1)
            {
                Console.WriteLine("start '" + TestContext.CurrentContext.Test.Name + "' variant 1: communicate locally in the same process");
                var service = new ClientServerService(remote: false, multithreaded: true);
                _proxy = new RemactPortProxy("ClientServiceTestLocal", DefaultResponseHandler);
                _proxy.IsMultithreaded = true;
                _proxy.LinkToService(service.Port);
            }
            else if (variant == 2)
            {
                Console.WriteLine("start '" + TestContext.CurrentContext.Test.Name + "' variant 2: communicate to a remote process");
                var service = new ClientServerService(remote: true, multithreaded: true);
                _proxy = new RemactPortProxy("ClientServiceTestRemote", DefaultResponseHandler);
                _proxy.IsMultithreaded = true;
                _proxy.LinkOutputToRemoteService(service.RemoteUri);
            }
            else if (variant == 3)
            {
                Console.WriteLine("start '" + TestContext.CurrentContext.Test.Name + "' variant 3: communicate locally in the same process, use thread synchronization");
                var service = new ClientServerService(remote: false, multithreaded: false);
                _proxy = new RemactPortProxy("ClientServiceTestLocalSync", DefaultResponseHandler);
                _proxy.LinkToService(service.Port);
            }
            else if (variant == 4)
            {
                Console.WriteLine("start '" + TestContext.CurrentContext.Test.Name + "' variant 4: communicate to a remote process, use thread synchronization");
                var service = new ClientServerService(remote: true, multithreaded: false);
                _proxy = new RemactPortProxy("ClientServiceTestRemoteSync", DefaultResponseHandler);
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

        #endregion

        [Test]
        public void SendStringReceiveString()
        {
            Helper.RunInWinFormsSyncContext(async () =>
            {
                // service side: see ClientServerService.ReceiveStringReplyString.
                //               It is scheduled on the same thread. Therefore, we have to use await in order to avoid deadlocks.
                int variant = 1;
                while (SetUpTestVariant(variant++))
                {
                    await SendStringReceiveStringAsync();
                }
            });
        }

        private async Task SendStringReceiveStringAsync()
        {
            // client side
            var ok = await _proxy.ConnectAsync();
            Assert.IsTrue(ok, "could not connect");

            var response = await _proxy.SendReceiveAsync<string>("ReceiveStringReplyString", "a request");
            Assert.AreEqual("the response", response.Payload, "wrong response content");
        }

        [Test]
        public async Task SendStringReceiveStringWithoutSyncContext()
        {
            // variants 1 and 2 do not use a synchronization context. 
            // Client and server support multithreading.
            SetUpTestVariant(1);
            await SendStringReceiveStringAsync();

            SetUpTestVariant(2);
            await SendStringReceiveStringAsync();

            // variants 3 and 4 miss the synchronization context in this test thread. 
            // Therefore, they throw an exception.
            Assert.Throws<InvalidOperationException>(() =>
            {
                SetUpTestVariant(3);
            });

            Assert.Throws<InvalidOperationException>(() =>
            {
                SetUpTestVariant(4);
            });
        }


        [Test]
        public void SendStringReceiveError()
        {
            Helper.RunInWinFormsSyncContext(async () =>
            {
                // When executing this test, an exception is thrown on service side: see ClientServerService.ReceiveStringReplyString.
                // This exception is propagated as a 'RemactException' to this thread.
                int variant = 1;
                while (SetUpTestVariant(variant++))
                {
                    // client side
                    var ok = await _proxy.ConnectAsync();
                    Assert.IsTrue(ok, "could not connect");

                    // we cannot use Assert.Throws<AggregateException> because this is not async and will deadlock.
                    try
                    {
                        var response = await _proxy.SendReceiveAsync<string>("ReceiveStringReplyString", "BlaBlaRequest");
                        Assert.Fail("no exception thrown");
                    }
                    catch (RemactException ex)
                    {
                        Assert.IsInstanceOf<Exception>(ex.InnerException);
                        Assert.AreEqual(ErrorCode.UnhandledExceptionOnService, ex.ErrorCode);
                    }
                }
            });
        }


        [Test]
        public void SendStringReceiveInt()
        {
            Helper.RunInWinFormsSyncContext(async () =>
            {
                // service side: see ClientServerService.ReceiveStringReplyInt.
                //               It is scheduled on the same thread. Therefore, we have to use await in order to avoid deadlocks.
                int variant = 1;
                while (SetUpTestVariant(variant++))
                {
                    // client side
                    var ok = await _proxy.ConnectAsync();
                    Assert.IsTrue(ok, "could not connect");

                    // value types are returned as object (boxed)
                    var response = await _proxy.SendReceiveAsync<object>("ReceiveStringReplyInt", "a request");
                    Assert.AreEqual(123, response.Payload, "wrong response content");
                    Assert.AreEqual(RemactMessageType.Response, response.MessageType);
                    Assert.AreEqual("ReceiveStringReplyInt", response.DestinationMethod);
                    Assert.IsNotNull(response.Source);
                }
            });
        }


        [Test]
        public void SendStringReceiveUnexpectedType()
        {
            Helper.RunInWinFormsSyncContext(async () =>
            {
                // When executing this test, an exception is thrown on client side caused by not expected reply type.
                int variant = 1;
                while (SetUpTestVariant(variant++))
                {
                    // client side
                    var ok = await _proxy.ConnectAsync();
                    Assert.IsTrue(ok, "could not connect");

                    // we cannot use Assert.Throws<AggregateException> because this is not async and will deadlock.
                    try
                    {
                        var response = await _proxy.SendReceiveAsync<string>("ReceiveStringReplyInt", "a request");
                        Assert.Fail("no exception thrown");
                    }
                    catch (RemactException ex)
                    {
                        Assert.IsNull(ex.InnerException);
                        Assert.AreEqual(ErrorCode.UnexpectedResponsePayloadType, ex.ErrorCode);
                    }
                }
            });
        }
    }
}
