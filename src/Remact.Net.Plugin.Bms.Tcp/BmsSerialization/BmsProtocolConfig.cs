
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using Remact.Net.Remote;
using Remact.Net.TcpStream;
using Remact.Net.Bms1Serializer;
using System.Collections.Generic;

namespace Remact.Net.Plugin.Bms.Tcp
{
    /// <summary>
    /// Common definitions for all interacting actors.
    /// This plugin builds remote connection over TCP, using the Bms1 binary message stream serializer.
    /// </summary>
    public class BmsProtocolConfig : RemactConfigDefault
    {
        //----------------------------------------------------------------------------------------------
        #region == Instance and plugin ==

        /// <summary>
        /// Library users may plug in their own implementation of IRemactDefault to RemactDefault.Instance.
        /// </summary>
        public static new BmsProtocolConfig Instance
        {
            get
            {
                return RemactConfigDefault.Instance as BmsProtocolConfig;
            }
            set
            {
                RemactConfigDefault.Instance = value;
            }
        }

        #endregion
        //----------------------------------------------------------------------------------------------
        #region == Default Service and Client configuration ==

        /// <summary>
        /// Configures and sets up a new service for a remotly accessible RemactPortService.
        /// Feel free to overwrite this default implementation.
        /// Here we set up a WAMP WebSocket with TCP portsharing.
        /// The 'path' part of the uri addresses the RemactPortService.
        /// </summary>
        /// <param name="service">The new service for an RemactPortService.</param>
        /// <param name="uri">The dynamically generated URI for this service.</param>
        /// <param name="isCatalog">true if used for Remact.Catalog service.</param>
        /// <returns>The network port manager. It must be called, when the RemactPortService is disconnected from network.</returns>
        public override INetworkServicePortManager DoServiceConfiguration(RemactService service, ref Uri uri, bool isCatalog)
        {
            var portManager = TcpPortManager.GetTcpPortManager(uri.Port);

            if (portManager.TcpListener == null)
            {
                // this TCP port has to be opened
                portManager.TcpListener = new TcpStreamService(uri.Port, (channel)=>OnClientAccepted(portManager, channel));
            }

            portManager.RegisterService(uri.AbsolutePath, service);
            return portManager; // will be called, when this RemactPortService is disconnected.
        }

        /// <summary>
        /// Do this for every new client connecting to a TCP port.
        /// </summary>
        /// <param name="portManager">TcpPortManager for this shared TCP port.</param>
        /// <param name="channel">The newly accepted client channel.</param>
        protected virtual void OnClientAccepted(TcpPortManager portManager, TcpStreamChannel channel)
        {
            var protocolDriver = new BmsProtocolClientStub(portManager, channel);
            channel.UserContext = protocolDriver;
            channel.Start(protocolDriver.OnMessageReceived, protocolDriver.OnDisconnect);
        }

        /// <summary>
        /// Sets the default client configuration, when connecting without app.config.
        /// </summary>
        /// <param name="uri">The endpoint URI to connect.</param>
        /// <param name="forCatalog">true if used for Remact.Catalog service.</param>
        /// <returns>The protocol driver including serializer.</returns>
        public override IRemactProtocolDriverToService DoClientConfiguration(ref Uri uri, bool forCatalog)
        {
            return new BmsProtocolClient(uri);
        }

        #endregion
        //----------------------------------------------------------------------------------------------
        #region == Serialization ==


        private Dictionary<string, Func<IBms1Reader, object>> _deserializerMap = new Dictionary<string, Func<IBms1Reader, object>>();
        
        private Dictionary<Type, Action<IBms1Writer>> _serializerMap = new Dictionary<Type, Action<IBms1Writer>>();

        /// <summary>
        /// Returns a deserializer method for a base object type.
        /// </summary>
        public Func<IBms1Reader, object> FindDeserializerByObjectType(string objectTypeName)
        {
            Func<IBms1Reader, object> deserializer;
            if (!_deserializerMap.TryGetValue(objectTypeName, out deserializer))
            {
                throw new InvalidOperationException("Cannot deserialize message with unknown type: " + objectTypeName);
            }
            return deserializer;
        }

        /// <summary>
        /// Returns a serializer method for a base object type.
        /// </summary>
        public Action<IBms1Writer> FindSerializerByObjectType(Type objectType, out string objectTypeName)
        {
            Action<IBms1Writer> serializer;
            if (_serializerMap.TryGetValue(objectType, out serializer))
            {
                objectTypeName = objectType.FullName;
            }
            else
            {
                foreach (var item in _serializerMap)
                {
                    if (item.Key.IsAssignableFrom(objectType))
                    {
                        objectTypeName = item.Key.FullName;
                        return item.Value;
                    }
                }
                objectTypeName = null;
                throw new InvalidOperationException("Cannot serialize message with unknown (base) type: " + objectType.FullName);
            }
            return serializer;
        }

        /// <summary>
        /// Adds a (static) deserializer method for a base object type.
        /// </summary>
        public void AddKnownMessageType<T>(Func<IBms1Reader, T> deserializer, Action<IBms1Writer> serializer) where T : class
        {
            lock (_deserializerMap)
            {
                _deserializerMap.Add(typeof(T).FullName, deserializer);
                _serializerMap.Add(typeof(T), serializer);
            }
        }

        /// <summary>
        /// Adds a (static) deserializer method for a base object type.
        /// </summary>
        public void AddKnownMessageType(Type messageType, Func<IBms1Reader, object> deserializer, Action<IBms1Writer> serializer)
        {
            lock (_deserializerMap)
            {
                _deserializerMap.Add(messageType.FullName, deserializer);
                _serializerMap.Add(messageType, serializer);
            }
        }

        #endregion
        //----------------------------------------------------------------------------------------------
        #region == Default application shutdown ==

        /// <summary>
        /// Has to be called by the user, when the application is shutting down.
        /// </summary>
        public override void Shutdown()
        {
            base.Shutdown();
            // TODO: shutdown all TcpPortManagers and clients
        }

        #endregion
    }
}

