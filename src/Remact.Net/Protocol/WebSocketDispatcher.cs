
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Alchemy;
using Alchemy.Classes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Remact.Net.Contracts;
using Remact.Net.Internal;

namespace Remact.Net.Protocol
{
    /// <summary>
    /// The WebSocketDispatcher is used on the server side. It dispatches incoming calls to the ClientProxy.
    /// It supports TCP port sharing for many ActorInputs.
    /// </summary>
    public class WebSocketDispatcher : IDisposable
    {
        private readonly static Dictionary<int, WebSocketDispatcher> ms_portMap = new Dictionary<int, WebSocketDispatcher>();
        private readonly static object ms_portLock = new object();

        public static WebSocketDispatcher GetWebSocketDispatcher(int tcpPort)
        {
            WebSocketDispatcher dispatcher;
            WebSocketServer server;
            lock (ms_portLock)
            {
                if(ms_portMap.TryGetValue(tcpPort, out dispatcher))
                {
                    return dispatcher;
                }

                dispatcher = new WebSocketDispatcher();
                ms_portMap.Add(tcpPort, dispatcher);
            }

            return dispatcher;
        }

        public WebSocketDispatcher()
        {
            _serviceMap = new Dictionary<string, RemactService>();
            _lock = new object();
        }

        private WebSocketServer _server;
        private readonly Dictionary<string, RemactService> _serviceMap;
        private readonly object _lock;

        public WebSocketServer WebSocketServer
        {
            get { return _server; }
            set
            {
                _server = value;
                _server.OnReceive = OnReceive;
            }
        }

        private void OnReceive(UserContext userContext)
        {
            RemactService service;
            if (_serviceMap.TryGetValue(userContext.RequestPath, out service))
            {
            }
        }

        public void RegisterService(string path, RemactService service)
        {
            lock (_lock)
            {
                _serviceMap.Add(path, service);
            }
        }

        public bool TryGetService(string path, out RemactService service)
        {
            lock (_lock)
            {
                return _serviceMap.TryGetValue(path, out service);
            }
        }

        public void RemoveService(string path)
        {
            lock (_lock)
            {
                _serviceMap.Remove(path);
            }
        }

        public void Dispose()
        {
            if (_server != null)
            {
                _server.Stop();
                _server.Dispose();
                _server = null;
            }
        }
    }
}
