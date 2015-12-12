
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using Remact.Net.Remote;
using Alchemy;
using Alchemy.Classes;
using Newtonsoft.Json;

namespace Remact.Net.Json.Msgpack.Alchemy
{
    /// <summary>
    /// Common definitions for all interacting actors.
    /// Library users may plug in their own implementation of this class to RemactDefault.Instance.
    /// </summary>
    public class JsonProtocolConfig : RemactConfigDefault
    {
        //----------------------------------------------------------------------------------------------
        #region == Instance and plugin ==

        /// <summary>
        /// Use experimental MessagePack integration, when set to true.
        /// </summary>
        public static bool UseMsgPack { get; set; }

        /// <summary>
        /// Library users may plug in their own implementation of IRemactDefault to RemactDefault.Instance.
        /// </summary>
        static JsonProtocolConfig()
        {
            RemactConfigDefault.Instance = new JsonProtocolConfig();
        }

        public static new JsonProtocolConfig Instance
        {
            get
            {
                return RemactConfigDefault.Instance as JsonProtocolConfig;
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
            var portManager = WebSocketPortManager.GetWebSocketPortManager(uri.Port);

            if (portManager.WebSocketServer == null)
            {
                // this TCP port has to be opened
                portManager.WebSocketServer = new WebSocketServer(false, uri.Port)
                {
                    OnConnected = (userContext) => OnClientConnected(portManager, userContext)
                };
                if (!UseMsgPack) portManager.WebSocketServer.SubProtocols = new string[] { "wamp" };
            }

            portManager.RegisterService(uri.AbsolutePath, service);
            portManager.WebSocketServer.Start(); // Listen for client connections
            return portManager; // will be called, when this RemactPortService is disconnected.
        }

        /// <summary>
        /// Do this for every new client connecting to a WebSocketPort.
        /// </summary>
        /// <param name="portManager">Our WebSocketPortManager.</param>
        /// <param name="userContext">Alchemy user context.</param>
        protected virtual void OnClientConnected(WebSocketPortManager portManager, UserContext userContext)
        {
            RemactService service;
            var absolutePath = userContext.RequestPath;
            if (!absolutePath.StartsWith("/"))
            {
                absolutePath = string.Concat('/', absolutePath); // uri.AbsolutPath contains a leading slash.
            }

            if (portManager.TryGetService(absolutePath, out service))
            {
                var svcUser = new RemactServiceUser(service.ServiceIdent);
                var handler = new MultithreadedServiceNet40(svcUser, service);
                // in future, the client stub will handle the OnReceive and OnDisconnect events for this connection
                if(UseMsgPack)
                {
                    var jsonRpcProxy = new JsonRpcMsgPackClientStub(handler, userContext);
                    svcUser.SetCallbackHandler(jsonRpcProxy);
                }
                else
                {
                    var wampProxy = new WampClientStub(handler, userContext);
                    svcUser.SetCallbackHandler(wampProxy);
                }
            }
            else
            {
                RaLog.Error("Svc:", "No service found on '" + absolutePath + "' to connect client " + userContext.ClientAddress);
            }
        }

        /// <summary>
        /// Sets the default client configuration, when connecting without app.config.
        /// </summary>
        /// <param name="uri">The endpoint URI to connect.</param>
        /// <param name="forCatalog">true if used for Remact.Catalog service.</param>
        /// <returns>The protocol driver including serializer.</returns>
        public override IRemactProtocolDriverToService DoClientConfiguration(ref Uri uri, bool forCatalog)
        {
            if (UseMsgPack)
            {
                // Protocol = JsonRpc, binary, serializer = MsgPack.
                return new JsonRpcMsgPackClient(uri);
            }
            else
            {
                // Protocol = WAMP, serializer = Newtonsoft.Json.
                return new WampClient(uri);
            }
        }

        #endregion
        //----------------------------------------------------------------------------------------------
        #region == Serialization ==

        /// <summary>
        /// Returns a new serializer usable to write polymorph messages.
        /// </summary>
        public virtual JsonSerializer GetSerializer()
        {
            return new JsonSerializer
            {
                // Auto $type metadata insertion. Simple assembly format is used to supports lax versioning.
                TypeNameHandling = TypeNameHandling.Auto,
                TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple,
            };
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
            WebSocketClient.Shutdown();
            WebSocketServer.Shutdown();
        }

        #endregion
    }
}

