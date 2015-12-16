
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using Alchemy;
using Remact.Net.Remote;

namespace Remact.Net.Plugin.Json.Msgpack.Alchemy
{
    /// <summary>
    /// The WebSocketPortManager is used on server side. 
    /// For each WebSocketServer TCP port a WebSocketPortManager is created.
    /// Several RemactPortService may be registered for the single TCP port (portsharing)
    /// When a client connects, the PortManager is used for finding the addressed RemactPortService.
    /// The path part of an uri is used to address the RemactPortService: ws://{host}:{tcpPort}/{path}.
    /// </summary>
    public class WebSocketPortManager : INetworkServicePortManager, IDisposable
    {
        private readonly static Dictionary<int, WebSocketPortManager> ms_portMap = new Dictionary<int, WebSocketPortManager>();
        private readonly static object ms_portLock = new object();

        /// <summary>
        /// Gets or creates a new instance of WebSocketPortManager for the specified TCP port.
        /// </summary>
        /// <param name="tcpPort">The TCP port number.</param>
        /// <returns>The WebSocketPortManager.</returns>
        public static WebSocketPortManager GetWebSocketPortManager(int tcpPort)
        {
            lock (ms_portLock)
            {
                WebSocketPortManager portManager;
                if (ms_portMap.TryGetValue(tcpPort, out portManager))
                {
                    return portManager;
                }

                portManager = new WebSocketPortManager();
                ms_portMap.Add(tcpPort, portManager);
                return portManager;
            }
        }

        private WebSocketPortManager()
        {
            _serviceMap = new Dictionary<string, RemactService>();
            _lock = new object();
        }

        private WebSocketServer _wsServer;
        private readonly Dictionary<string, RemactService> _serviceMap;
        private readonly object _lock;

        /// <summary>
        /// The WebsocketServer is added in <see cref="RemactConfigDefault.DoServiceConfiguration"/>.
        /// </summary>
        public WebSocketServer WebSocketServer
        {
            get { return _wsServer; }
            set
            {
                _wsServer = value;
            }
        }

        /// <summary>
        /// Registers a service for a TCP port and a uri path.
        /// </summary>
        /// <param name="path">The Uri.AbsolutePath.</param>
        /// <param name="service">The service to be registered under this path.</param>
        public void RegisterService(string path, RemactService service)
        {
            lock (_lock)
            {
                _serviceMap.Add(path, service);
            }
        }

        /// <summary>
        /// Gets a service identified by a TCP port and a uri path.
        /// </summary>
        /// <param name="path">The Uri.AbsolutePath.</param>
        /// <param name="service">The service. Null when not found.</param>
        /// <returns>true, when found.</returns>
        public bool TryGetService(string path, out RemactService service)
        {
            lock (_lock)
            {
                return _serviceMap.TryGetValue(path, out service);
            }
        }

        /// <summary>
        /// Removes a service from the register. Stops the WebSocketServer, when no more services are listening for clients.
        /// </summary>
        /// <param name="path">The Uri.AbsolutePath.</param>
        public void RemoveService(string path)
        {
            lock (_lock)
            {
                if (!_serviceMap.Remove(path))
                {
                    RaLog.Error("Svc:", "No service found on '" + path + "' to remove from WebSocketPortManager");
                }

                if (_serviceMap.Count == 0 && _wsServer != null)
                {
                    _wsServer.Stop();
                }
            }
        }

        /// <summary>
        /// Stops the WebSocketServer and closes the TCP port.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Stops the WebSocketServer and closes the TCP port.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && _wsServer != null)
            {
                _wsServer.Stop();
                _wsServer = null;
            }
        }
    }
}
