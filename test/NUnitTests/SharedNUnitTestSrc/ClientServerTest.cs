
// Copyright (c) https://github.com/steforster/Remact.Net

using NUnit.Framework;
using System;
using System.Threading.Tasks;
using Remact.Net;
using System.Threading;

// Communication model tests
// Client classes:         Service classes:
// ---------------         ----------------
// ClientServer            ClientServerService
// Notification            NotificationService
// ServerClient            ServerClientService
// PublishSubscribe        PublishSubscribeService

namespace RemactNUnitTest
{
    [TestFixture]
    public class ClientServer
    {
        private RemactPortProxy _proxy;
        private ClientServerService _service;

        #region Test infrastructure

        [TestFixtureSetUp] // run once when creating this class.
        public void FixtureSetUp()
        {
            RaLog.UsePlugin( new RaLog.PluginConsole() );
#if (BMS)
            // Depending on the csproj that references the shared project, we use either the BMS or the JSON plugin for remote connections.
            Helper.LoadPluginDll(RemactConfigDefault.DefaultProtocolPluginName);
            var conf = Remact.Net.Plugin.Bms.Tcp.BmsProtocolConfig.Instance;
            conf.AddKnownMessageType(TestMessage.ReadFromBms1Stream, TestMessage.WriteToBms1Stream);
            conf.AddKnownMessageType(typeof(int),
                (reader)=>
                {
                    return reader.ReadBlock<int>((i) => reader.ReadInt32());
                },
                
                (obj, writer) =>
                {
                    writer.WriteBlock(() => writer.WriteInt32((int?)obj));
                });

            conf.AddKnownMessageType(typeof(string),
                (reader) =>
                {
                    return reader.ReadBlock<object>((s) => reader.ReadString());
                },

                (obj, writer) =>
                {
                    writer.WriteBlock(() => writer.WriteString((string)obj));
                });
#endif

#if (JSON)
            Helper.LoadPluginDll(RemactConfigDefault.JsonProtocolPluginName);
#endif
            Remact.Net.Remote.RemactCatalogClient.IsDisabled = true;
        }

        [TestFixtureTearDown]  // run once after all tests of this class have been executed.
        public void FixtureTearDown()
        {
            // This disconnects (closes) all clients and services that where created in the TestMethod.
            RemactPort.DisconnectAll();

            // This disposes all WebSocket threads, removes the test warning 'Attempted to access an unloaded AppDomain'.
            //Alchemy.WebSocketClient.Shutdown();
        }

        [SetUp] // run before each TestMethod.
        public void SetUp()
        {
        }

        [TearDown] // run after each TestMethod (successful or failed).
        public void TearDown()
        {
            SetUpTestVariant(0);
        }

        string _testName;

        // We create a matrix of tests.
        // Each test method is run in several communication variants.
        public bool SetUpTestVariant(int variant)
        {
            if (_proxy != null)
            {
                _proxy.Disconnect();
                _proxy = null;
            }

            if (_service != null)
            {
                _service.Port.Disconnect();
                _service = null;
            }

            if (variant == 1)
            {
                _testName = TestContext.CurrentContext.Test.Name;
                Console.WriteLine("start '" + _testName + "' variant 1: communicate locally in the same process");
                SetAlternateFormatter(false);
                _service = new ClientServerService(remote: false, multithreaded: true);
                _proxy = new RemactPortProxy("ClientServiceTestLocal", DefaultResponseHandler);
                _proxy.IsMultithreaded = true;
                _proxy.LinkToService(_service.Port);
            }
            else if (variant == 2)
            {
                Console.WriteLine("start '" + _testName + "' variant 2: communicate to a remote process");
                SetAlternateFormatter(false);
                _service = new ClientServerService(remote: true, multithreaded: true);
                _proxy = new RemactPortProxy("ClientServiceTestRemote", DefaultResponseHandler);
                _proxy.IsMultithreaded = true;
                _proxy.LinkOutputToRemoteService(_service.RemoteUri);
            }
            else if (variant == 3)
            {
                Console.WriteLine("start '" + _testName + "' variant 3: communicate locally in the same process, use thread synchronization");
                SetAlternateFormatter(false);
                _service = new ClientServerService(remote: false, multithreaded: false);
                _proxy = new RemactPortProxy("ClientServiceTestLocalSync", DefaultResponseHandler);
                _proxy.LinkToService(_service.Port);
            }
            else if (variant == 4)
            {
                Console.WriteLine("start '" + _testName + "' variant 4: communicate to a remote process, use thread synchronization");
                SetAlternateFormatter(false);
                _service = new ClientServerService(remote: true, multithreaded: false);
                _proxy = new RemactPortProxy("ClientServiceTestRemoteSync", DefaultResponseHandler);
                _proxy.LinkOutputToRemoteService(_service.RemoteUri);
            }
            else if (variant == 5)
            {
                if (!SetAlternateFormatter(true))
                {
                    return false; // test finished, no alternate formatter
                }
                Console.WriteLine("start '" + _testName + "' variant 5: communicate to a remote process, use MsgPack binary transport");
                _service = new ClientServerService(remote: true, multithreaded: true);
                _proxy = new RemactPortProxy("ClientServiceTestRemoteMsgPack", DefaultResponseHandler);
                _proxy.IsMultithreaded = true;
                _proxy.LinkOutputToRemoteService(_service.RemoteUri);
            }
            else if (variant == 6)
            {
                Console.WriteLine("start '" + _testName + "' variant 6: communicate to a remote process, use MsgPack binary transport and thread synchronization");
                SetAlternateFormatter(true);
                _service = new ClientServerService(remote: true, multithreaded: false);
                _proxy = new RemactPortProxy("ClientServiceTestRemoteMsgPackSync", DefaultResponseHandler);
                _proxy.LinkOutputToRemoteService(_service.RemoteUri);
            }
            else
            {
                return false;
            }

            return true;
        }

        private bool SetAlternateFormatter(bool alternateFormatter)
        {
#if (BMS)
            if (alternateFormatter) return false;
#endif
#if (JSON)
            Remact.Net.Plugin.Json.Msgpack.Alchemy.JsonProtocolConfig.UseMsgPack = alternateFormatter;
#endif
            return true;
        }


        protected Task DefaultResponseHandler(RemactMessage msg)
        {
            throw new NotImplementedException("unexpected response message type '" + msg.Payload.GetType().Name + "' received.");
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
            Console.WriteLine("successfully passed.");
        }

        private async Task SendStringReceiveStringAsync()
        {
            // client side
            var ok = await _proxy.ConnectAsync();
            Assert.IsTrue(ok, "could not connect");

            var response = await _proxy.SendReceiveAsync<string>(nameof(IClientServerReceiver.ReceiveString_ReplyString), "a request");

            Assert.AreEqual("the response", response.Payload, "wrong response content");
        }


        [Test]
        public async Task SendStringReceiveStringWithoutSyncContext()
        {
            // variants 1 and 2 do not use a synchronization context. 
            // Client and server support multithreading.
            SynchronizationContext.SetSynchronizationContext(null); // NUnit has a synchronization context - but we do not want one here.
            SetUpTestVariant(1);
            await SendStringReceiveStringAsync();

            SetUpTestVariant(2);
            await SendStringReceiveStringAsync();

            // variants 3 and 4 miss the synchronization context in this test thread. 
            // Therefore, they throw an exception.
            SetUpTestVariant(3);
            Assert.Throws<InvalidOperationException>(async () =>
                {
                    SynchronizationContext.SetSynchronizationContext(null); // NUnit has a synchronization context - but we do not want one here.
                    await SendStringReceiveStringAsync();
                });

            SetUpTestVariant(4);
            Assert.Throws<InvalidOperationException>(async () =>
                {
                    SynchronizationContext.SetSynchronizationContext(null); // NUnit has a synchronization context - but we do not want one here.
                    await SendStringReceiveStringAsync();
                });
            Console.WriteLine("successfully passed.");
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
                        var response = await _proxy.SendReceiveAsync<string>("ReceiveString_ReplyString", "BlaBlaRequest");
                        Assert.Fail("no exception thrown");
                        Assert.NotNull(response);
                    }
                    catch (RemactException ex)
                    {
                        Assert.IsInstanceOf<Exception>(ex.InnerException);
                        Assert.AreEqual(ErrorCode.UnhandledExceptionOnService, ex.ErrorCode);
                    }
                }
            });
            Console.WriteLine("successfully passed.");
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
                    var response = await _proxy.SendReceiveAsync<object>("ReceiveString_ReplyInt", "a request");
                    int result = Convert.ToInt32(response.Payload); // unbox and optionally convert from Int64 
                    Assert.AreEqual(123, result, "wrong response content");
                    Assert.AreEqual(RemactMessageType.Response, response.MessageType);
                    Assert.AreEqual("ReceiveString_ReplyInt", response.DestinationMethod);
                    Assert.IsNotNull(response.Source);
                }
            });
            Console.WriteLine("successfully passed.");
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
                        var response = await _proxy.SendReceiveAsync<ReadyMessage>("ReceiveString_ReplyInt", "a request");
                        Assert.Fail("no exception thrown");
                        Assert.NotNull(response);
                    }
                    catch (RemactException ex)
                    {
                        Assert.IsNull(ex.InnerException);
                        Assert.AreEqual(ErrorCode.UnexpectedResponsePayloadType, ex.ErrorCode);
                    }
                }
            });
            Console.WriteLine("successfully passed.");
        }


        [Test]
        public void SendTestReceiveEmpty()
        {
            Helper.RunInWinFormsSyncContext(async () =>
            {
                int variant = 1;
                while (SetUpTestVariant(variant++))
                {
                    // client side
                    var ok = await _proxy.ConnectAsync();
                    Assert.IsTrue(ok, "could not connect");

                    var request = new TestMessage{Inner = new InnerTestMessage {Id=1, Name = "Hello" }};
                    var response = await _proxy.SendReceiveAsync<ReadyMessage>("ReceiveTest_ReplyEmpty", request);
                    Assert.IsNotNull(response, "response is null");
                }
            });
            Console.WriteLine("successfully passed.");
        }


        [Test]
        public void SendEmptyReceiveTestMessage()
        {
            Helper.RunInWinFormsSyncContext(async () =>
            {
                int variant = 1;
                while (SetUpTestVariant(variant++))
                {
                    // client side
                    var ok = await _proxy.ConnectAsync();
                    Assert.IsTrue(ok, "could not connect");

                    var response = await _proxy.SendReceiveAsync<TestMessage>("ReceiveEmpty_ReplyTestMessage", new ReadyMessage());
                    Assert.IsNotNull(response.Payload, "response payload is null");
                    Assert.IsNotNull(response.Payload.Inner, "inner message is null");
                    Assert.AreEqual(2, response.Payload.Inner.Id, "wrong Id of inner message");
                    Assert.IsInstanceOf<InnerTestMessage>(response.Payload.Inner, "wrong inner message type");
                    var inner = response.Payload.Inner as InnerTestMessage;
                    Assert.AreEqual("Hi", inner.Name, "wrong Name of inner message");
                }
            });
            Console.WriteLine("successfully passed.");
        }


        [Test]
        public void SendToAsyncServiceReceiveTestMessage()
        {
            Helper.RunInWinFormsSyncContext(async () =>
            {
                int variant = 0;
                while (SetUpTestVariant(++variant))
                {
                    // client side
                    var ok = await _proxy.ConnectAsync();
                    Assert.IsTrue(ok, "could not connect");

                    var response = await _proxy.SendReceiveAsync<TestMessage>(
                        nameof(IClientServerReceiver.ReceiveTest_ReplyTestMessageAsync), 
                        new TestMessage());

                    Assert.IsNotNull(response.Payload, "response payload is null");
                    Assert.IsNotNull(response.Payload.Inner, "inner message is null");
                    Assert.AreEqual(3, response.Payload.Inner.Id, "wrong Id of inner message");
                    Assert.IsInstanceOf<InnerTestMessage>(response.Payload.Inner, "wrong inner message type");
                    var inner = response.Payload.Inner as InnerTestMessage;
                    Assert.AreEqual("Hi again", inner.Name, "wrong Name of inner message");
                }
            });
            Console.WriteLine("successfully passed.");
        }
    }
}
