
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using Remact.Net.Remote;
using Remact.Net.TcpStream;

namespace Remact.Net.Plugin.Bms.Tcp
{
    /// <summary>
    /// The TcpPortManager is used on server side. 
    /// For each TCP port a TcpPortManager is created.
    /// Several RemactPortService may be registered for the single TCP port (portsharing)
    /// When a client connects, the PortManager is used for finding the addressed RemactPortService.
    /// The path part of an uri is used to address the RemactPortService: net-tcp://{host}:{tcpPort}/{path}.
    /// </summary>
    public class TcpPortManager : INetworkServicePortManager, IDisposable
    {
        private readonly static Dictionary<int, TcpPortManager> ms_portMap = new Dictionary<int, TcpPortManager>();
        private readonly static object ms_portLock = new object();

        /// <summary>
        /// Gets or creates a new instance of TcpPortManager for the specified TCP port.
        /// </summary>
        /// <param name="tcpPort">The TCP port number.</param>
        /// <returns>The TcpPortManager.</returns>
        public static TcpPortManager GetTcpPortManager(int tcpPort)
        {
            lock (ms_portLock)
            {
                TcpPortManager portManager;
                if (ms_portMap.TryGetValue(tcpPort, out portManager))
                {
                    return portManager;
                }

                portManager = new TcpPortManager();
                ms_portMap.Add(tcpPort, portManager);
                return portManager;
            }
        }

        private TcpPortManager()
        {
            _serviceMap = new Dictionary<string, RemactService>();
            _lock = new object();
        }

        private TcpStreamService _tcpListener;
        private readonly Dictionary<string, RemactService> _serviceMap;
        private readonly object _lock;

        /// <summary>
        /// The TcpStreamService is added in <see cref="RemactConfigDefault.DoServiceConfiguration"/>.
        /// </summary>
        public TcpStreamService TcpListener
        {
            get { return _tcpListener; }
            set
            {
                _tcpListener = value;
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
        /// Removes a service from the register. Stops the TCP listener, when no more services are listening for clients.
        /// </summary>
        /// <param name="path">The Uri.AbsolutePath.</param>
        public void RemoveService(string path)
        {
            lock (_lock)
            {
                if (!_serviceMap.Remove(path))
                {
                    RaLog.Error("Svc:", "No service found on '" + path + "' to remove from TcpPortManager");
                }

                if (_serviceMap.Count == 0 && _tcpListener != null)
                {
                    _tcpListener.Dispose();
                }
            }
        }

        /// <summary>
        /// Stops the  TCP listener and closes the TCP port.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Stops the  TCP listener and closes the TCP port.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && _tcpListener != null)
            {
                _tcpListener.Dispose();
                _tcpListener = null;
            }
        }
    }
}
