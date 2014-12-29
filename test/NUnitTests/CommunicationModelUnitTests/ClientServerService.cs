
// Copyright (c) 2014, github.com/steforster/Remact.Net

using NUnit.Framework;
using System;
using System.Diagnostics;

namespace Remact.Net.UnitTests.CommunicationModel
{
    // This is the communication contract for clinet-server tests.
    public interface IClientServerReceiver
    {
        string ReceiveStringReplyString(string request);
        int ReceiveStringReplyInt(string request);
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
           
            port.Open();
            Port = port;
         }

        private void DefaultRequestHandler(RemactMessage msg)
        {
            throw new NotImplementedException();
        }

        private string ReceiveStringReplyString(string request, RemactMessage message)
        {
            // service side
            Trace.WriteLine("  '" + request + "' received in 'ReceiveStringReplyString', service: " + Port.Uri);
            Assert.AreEqual("a request", request, "wrong request content");
            return "the response";
        }

        private int ReceiveStringReplyInt(string request, RemactMessage message)
        {
            // service side
            Trace.WriteLine("  '" + request + "' received in 'ReceiveStringReplyInt', service: " + Port.Uri);
            Assert.AreEqual("a request", request, "wrong request content");
            return 123;
        }
    }
}
