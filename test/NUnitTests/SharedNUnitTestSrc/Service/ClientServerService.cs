
// Copyright (c) https://github.com/steforster/Remact.Net

using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Remact.Net;

namespace RemactNUnitTest
{
    // This is the communication contract for client-server tests.
    public interface IClientServerReceiver
    {
        string ReceiveString_ReplyString(string request);
        int ReceiveString_ReplyInt(string request);
        ReadyMessage ReceiveTest_ReplyEmpty(TestMessage request);
        TestMessage ReceiveEmpty_ReplyTest(ReadyMessage request);
    }

    // This is the service part of the client-server test.
    public class ClientServerService
    {
        // Expose for local connection
        public IRemactPortService Port { get; private set; }

        // Exposed interface for remote connection
        public readonly string ServiceName = "ClientServerService";
        public readonly Uri    RemoteUri = new Uri("ws://localhost:40001/Remact/ClientServerService");

        // Constructor
        public ClientServerService(bool remote, bool multithreaded)
        {
            var port = new RemactPortService(ServiceName, DefaultRequestHandler)
            {
                IsMultithreaded = multithreaded,
            };

            port.InputDispatcher.AddActorInterface(typeof(IClientServerReceiver), this); 

            if (remote)
            {
                port.LinkInputToNetwork(ServiceName, tcpPort: 40001, publishToCatalog: false);
            }
           
            Helper.ServiceThread.DoSynchronously( port.Open ); // pick up correct synchronization context
            Port = port;
         }

        private Task DefaultRequestHandler(RemactMessage msg)
        {
            throw new NotImplementedException();
            //return null;
        }

        private string ReceiveString_ReplyString(string request, RemactMessage message)
        {
            // service side
            Trace.WriteLine("  '" + request + "' received in 'ReceiveString_ReplyString', service: " + Port.Uri);
            Assert.AreEqual("a request", request, "wrong request content");
            return "the response";
        }

        private int ReceiveString_ReplyInt(string request, RemactMessage message)
        {
            // service side
            Trace.WriteLine("  '" + request + "' received in 'ReceiveString_ReplyInt', service: " + Port.Uri);
            Assert.AreEqual("a request", request, "wrong request content");
            return 123;
        }

        private ReadyMessage ReceiveTest_ReplyEmpty(TestMessage request, RemactMessage message)
        {
            // service side
            Trace.WriteLine("  '" + request + "' received in 'ReceiveTest_ReplyEmpty', service: " + Port.Uri);
            Assert.IsNotNull(request.Inner, "inner message is null");
            Assert.AreEqual(1, request.Inner.Id, "wrong Id of inner message");
            Assert.IsInstanceOf<InnerTestMessage>(request.Inner, "wrong inner message type");
            var inner = request.Inner as InnerTestMessage;
            Assert.AreEqual("Hello", inner.Name, "wrong Name of inner message");
            return new ReadyMessage();
        }

        private TestMessage ReceiveEmpty_ReplyTest(ReadyMessage request, RemactMessage message)
        {
            // service side
            Trace.WriteLine("  '" + request + "' received in 'ReceiveEmpty_ReplyTest', service: " + Port.Uri);
            return new TestMessage {Inner = new InnerTestMessage {Id=2, Name="Hi"}};
        }
    }
}
