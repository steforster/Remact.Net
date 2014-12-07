
// Copyright (c) 2014, github.com/steforster/Remact.Net

using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Remact.Net.CommunicationModelUnitTests
{
    public interface IClientServerReceiver
    {
        string ReceiveString(string request);
    }

    public class ClientServerService
    {
        // Expose for local connection
        public IRemactPortService Port { get; private set; }

        // Expose for remote connection
        public readonly string ServiceName = "ClientServerService";
        public readonly Uri    RemoteUri = new Uri("ws://localhost:40001/Remact/ClientServerService");

        // Constructor
        public ClientServerService(bool remote)
        {
            var port = new RemactPortService(ServiceName, DefaultRequestHandler)
            {
                IsMultithreaded = true,
            };

            port.InputDispatcher.AddActorInterface(typeof(IClientServerReceiver), this); 

            if (remote)
            {
                port.LinkInputToNetwork(ServiceName, tcpPort: 40001, publishToCatalog: false);
            }
           
            port.Open();
            Port = port;
         }

        public void DefaultRequestHandler(RemactMessage msg)
        {
            throw new NotImplementedException();
        }

        public string ReceiveString(string request, RemactMessage message)
        {
            // service side
            Assert.AreEqual("a request", request, "wrong request content");
            return "the response";
        }
    }
}
