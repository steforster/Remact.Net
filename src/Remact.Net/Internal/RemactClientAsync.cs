
// Copyright (c) 2012  AsyncWcfLib.sourceforge.net

using System;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;

namespace SourceForge.AsyncWcfLib.Basic
{
    /// <summary>
    /// VS2012 extension to WcfBasicClientAsync. Supporting async / await.
    /// </summary>
    public class WcfBasicClientAsyncAwait : WcfBasicClientAsync
    {
        internal WcfBasicClientAsyncAwait(string clientName, WcfMessageHandler defaultResponseHandler)
            : base (clientName, defaultResponseHandler)
        {
        }// CTOR1


        internal WcfBasicClientAsyncAwait(ActorOutput clientIdent)
            : base(clientIdent)
        {
        }// CTOR 2

        
        /// <summary>
        /// Asynchronious connect using async / await. 
        /// </summary>
        /// <returns>True, when connect process could be started.</returns>
        public override bool TryConnect()
        {
            TryConnectLegacyAsync();
            return !m_boTimeout;
        }

        private async void TryConnectLegacyAsync()
        {
            WcfReqIdent id = await TryConnectAsync();
            if (id != null && id.Message != null)
            {
                id.Sender = ClientIdent;
                id.SendResponseFrom (ServiceIdent, id.Message, null);
            }
        }

        /// <summary>
        /// Connect or reconnect output to the previously linked partner.
        /// </summary>
        public async Task<WcfReqIdent> TryConnectAsync()
        {
            if (!(IsDisconnected || IsFaulted)) 
                return null;  // already connected or connecting
            ClientIdent.PickupSynchronizationContext();

            if (m_RouterHostToLookup != null)
            {
                // connect to router first
                return await TryConnectViaRouterAsync(null);
            }
            else
            {
                // do not connect to router
                try
                {
                    LinkToService( m_RequestedServiceUri );
                    return await OpenConnectionToServiceAsync();
                }
                catch (Exception ex)
                {
                    WcfTrc.Exception( "Cannot open Wcf connection(1a)", ex, ClientIdent.Logger );
                    m_boTimeout = true; // enter 'faulted' state when eg. configuration is incorrect
                }
                return null;
            }
        }// TryConnectAsync


        private async Task<WcfReqIdent> TryConnectViaRouterAsync(WcfMessageHandler viaResponseHandler)
        {
            try
            {
                m_TraceConnectBefore = ClientIdent.TraceConnect;
                ClientIdent.TraceConnect = ClientIdent.TraceSend;
                m_boTemporaryRouterConn = true;
                ServiceIdent.Uri = null; // neue URI wird gespeichert
                var uri = new Uri("http://" + m_RouterHostToLookup + ':' + m_WcfRouterPort + "/" + WcfDefault.WsNamespace + "/" + WcfDefault.Instance.RouterServiceName);
                WcfReqIdent id = await TryConnectViaAsync(uri, null, toRouter: true);

                var svcRsp = id.Message as WcfPartnerMessage;
                if (svcRsp == null || svcRsp.Usage != WcfPartnerMessage.Use.ServiceConnectResponse)
                {
                    return ConnectError(id);
                }
                if( ClientIdent.TraceSend ) WcfTrc.Info( id.CltRcvId, "Temporary connected router: '" + svcRsp.Name + "' on '" + svcRsp.HostName + "'", ClientIdent.Logger );

                //----- send ServiceAddressRequest to router -----
                ActorPort lookup = new ActorPort();
                lookup.HostName = m_RouterHostToLookup;
                lookup.Name = m_ServiceNameToLookup;
                lookup.IsServiceName = true;
                WcfPartnerMessage req2 = new WcfPartnerMessage(lookup, WcfPartnerMessage.Use.ServiceAddressRequest);
                // lookup the service URI (especially the TCP port)
                id = await ClientIdent.SendReceiveAsync( req2 );

                svcRsp = id.Message as WcfPartnerMessage;
                if (svcRsp == null || svcRsp.Usage != WcfPartnerMessage.Use.ServiceAddressResponse)
                {
                    return ConnectError(id);
                }

                ServiceIdent.UseDataFrom( svcRsp );
                if( ClientIdent.TraceSend )
                {
                    string s = string.Empty;
                    if( svcRsp.AddressList != null )
                    {
                        string delimiter = ", IP-adresses = ";
                        foreach( var adr in svcRsp.AddressList )
                        {
                            s = string.Concat( s, delimiter, adr.ToString() );
                            delimiter = ", ";
                        }
                    }
                    WcfTrc.Info( id.CltRcvId, "ServiceAddressResponse: " + svcRsp.Uri + s, ClientIdent.Logger );
                }
                ClientIdent.TraceConnect = m_TraceConnectBefore;
                m_boTemporaryRouterConn = false;

                return await TryConnectViaAsync(svcRsp.Uri, m_DefaultInputHandlerForApplication, toRouter: false); // next response will be sent to application
            }
            catch (Exception ex)
            {
                WcfTrc.Exception( "Cannot open Wcf connection(2a)", ex, ClientIdent.Logger );
                m_boTimeout = true; // enter 'faulted' state when eg. configuration is incorrect
                ClientIdent.TraceConnect = m_TraceConnectBefore;
                m_boTemporaryRouterConn = false;
                return null;
            }
        }// TryConnectViaRouterAsync


        // used in router client only
        internal override async void TryConnectVia( Uri endpointUri, WcfMessageHandler viaResponseHandler, bool toRouter)
        {
            if(!toRouter) throw new NotImplementedException();

            WcfReqIdent id = await TryConnectViaAsync( endpointUri, viaResponseHandler, toRouter);
            if (id != null && id.Message != null)
            {
                id.Sender = ClientIdent;
                id.SendResponseFrom(ServiceIdent, id.Message, null);
            }
        }


        /// <summary>
        /// <para>Connect this client to a router or to the requested service, without configuration from App.config file.</para>
        /// </summary>
        /// <param name="endpointUri">fully specified URI of the service</param>
        /// <param name="viaResponseHandler">The callback method when a response arrives, null for async/await semantics</param>
        /// <param name="toRouter">True, when the connection to a router is made.</param>
        internal async Task<WcfReqIdent> TryConnectViaAsync( Uri endpointUri, WcfMessageHandler viaResponseHandler, bool toRouter)
        {
            if (!IsDisconnected) Disconnect();
            try
            {
                m_RequestedServiceUri = endpointUri;
                m_ServiceReference = new WcfBasicClient( new BasicHttpBinding(), new EndpointAddress( endpointUri ), this );
                // Let now the library user change binding and security credentials.
                // By default WcfDefault.OnClientConfiguration is called.
                DoClientConfiguration( ref endpointUri, toRouter ); // TODO: changes in uri are not reflected in a new m_ServiceReference
                ClientIdent.DefaultInputHandler  = viaResponseHandler;
                return await OpenConnectionToServiceAsync();
            }
            catch (Exception ex)
            {
                m_boTimeout = true; // enter 'faulted' state when eg. configuration is incorrect
                WcfReqIdent id = new WcfReqIdent(ServiceIdent, ClientIdent.OutputClientId, ClientIdent.LastRequestIdSent, null, null);
                id.IsResponse = true;
                id.Message = new WcfErrorMessage(WcfErrorMessage.Code.CouldNotStartConnect, ex);
                return id;
            }
        }// TryConnectViaAsync


        /// <summary>
        /// Connect this Client to the prepared m_ServiceReference
        /// </summary>
        //  Running on the user thread
        private async Task<WcfReqIdent> OpenConnectionToServiceAsync()
        {
            WcfReqIdent id = null;
            try
            {
                ClientIdent.OutputClientId = 0;
                ClientIdent.LastRequestIdSent = 10;
                LastSendIdReceived = 0; // first message after connect is expected with SendId=1
                LastRequestIdReceived = 10;
                m_boFirstResponseReceived = false;
                m_boTimeout = false;
                m_boConnecting = true;
                if (ServiceIdent.Uri == null) ServiceIdent.PrepareServiceName(m_ServiceReference.Endpoint.Address.Uri);
                ServiceIdent.IsMultithreaded = ClientIdent.IsMultithreaded;
                ServiceIdent.TryConnect(); // internal from ServiceIdent to ClientIdent

                ClientIdent.PickupSynchronizationContext();
                ClientIdent.m_Connected = true;  // internal, from ActorOutput to WcfBasicClientAsync
                ClientIdent.LastSentId = 0; // ++ = 1 = connect
                id = new WcfReqIdent( ClientIdent, ClientIdent.OutputClientId, ++ClientIdent.LastRequestIdSent, null, null );

                //----- open -----
                await Task.Run(()=>m_ServiceReference.SyncOpen(id)); // synchronous WCF call
                id.DestinationLambda = null;
                id.SourceLambda = null;

                if (id.Message != null) // error ?
                {
                    m_boTimeout = true; // enter 'faulted' state when eg. configuration is incorrect
                    id.IsResponse = true;
                    id.Sender = ServiceIdent;
                    return id;
                }

                //----- send ClientConnectRequest to router -----
                string serviceAddr = GetSetServiceAddress();
                id.Message = new WcfPartnerMessage(ClientIdent, WcfPartnerMessage.Use.ClientConnectRequest);
                if (ClientIdent.TraceConnect)
                {
                    if( m_boTemporaryRouterConn ) WcfTrc.Info( id.CltSndId, string.Concat( "Temporary connecting .....: '", serviceAddr, "'" ), ClientIdent.Logger );
                                             else WcfTrc.Info( id.CltSndId, string.Concat( "Connecting svc: '", serviceAddr, "'" ), ClientIdent.Logger );
                }
                
                // send first connection request on user thread --> response will be received on this thread also
                id = await SendReceiveAsync(id);

                WcfPartnerMessage svcRsp = id.Message as WcfPartnerMessage;
                if (svcRsp == null || svcRsp.Usage != WcfPartnerMessage.Use.ServiceConnectResponse)
                {
                    ConnectError(id);
                }
            }
            catch (Exception ex)
            {
                m_boTimeout = true; // enter 'faulted' state when eg. configuration is incorrect
                id.IsResponse = true;
                id.Sender = ServiceIdent;
                id.Message = new WcfErrorMessage(WcfErrorMessage.Code.CouldNotStartConnect, ex);
            }
            return id;
        }// OpenConnectionToServiceAsync


        private WcfReqIdent ConnectError(WcfReqIdent id)
        {
            WcfErrorMessage err = id.Message as WcfErrorMessage;
            if (err != null)
            {
                //WcfTrc.Warning (rsp.CltRcvId, "Router "+rsp.ToString());
                if (err.Error == WcfErrorMessage.Code.ServiceNotRunning)
                {
                    err.Error = WcfErrorMessage.Code.RouterNotRunning;
                }
            }
            else
            {
                WcfTrc.Error( id.CltRcvId, "Receiving unexpected response from WcfRouterService: " + id.Message.ToString(), ClientIdent.Logger );
                id.Message = new WcfErrorMessage(WcfErrorMessage.Code.CouldNotConnectRouter, "Unexpected response from WcfRouterService");
            }

            m_boTimeout = true; // Fault state
            m_boTemporaryRouterConn = false;
            ClientIdent.TraceConnect = m_TraceConnectBefore;
            ClientIdent.DefaultInputHandler = m_DefaultInputHandlerForApplication;
            //ClientIdent.DefaultInputHandler(rsp); // handle the negative feedback from router
            ServiceIdent.PrepareServiceName(m_RouterHostToLookup, m_ServiceNameToLookup); // prepare for next connect try
            return id;
        }


        /// <summary>
        /// internal: Send disconnect message, wait only 30ms for a response.
        /// </summary>
        internal protected override async void SendDisconnectMessage()
        {
            var request = new WcfPartnerMessage(ClientIdent, WcfPartnerMessage.Use.ClientDisconnectRequest);
            if (ClientIdent.LastRequestIdSent == uint.MaxValue) ClientIdent.LastRequestIdSent = 10;
            WcfReqIdent id = new WcfReqIdent(ClientIdent, ClientIdent.OutputClientId, ++ClientIdent.LastRequestIdSent, request, null);
            base.PostInput(id); // do not await disconnect-response
            await Task.Delay(30);
        }


        /// <summary>
        /// Entrypoint of messages sent conventional. Now interfacing to async/await semantics.
        /// </summary>
        /// <param name="id">Message to send + responsehandler</param>
        public override async void PostInput (WcfReqIdent id)
        {
            var connectMsg = id.Message as WcfPartnerMessage;
            if (connectMsg != null && connectMsg.Usage == WcfPartnerMessage.Use.ClientDisconnectRequest )
            {
                base.PostInput( id ); // do not await disconnect-response
                return;
            }

            id = await SendReceiveAsync(id);
            if (id != null && id.Message != null) // null when cancelled during shutdown
            {
                id.SendResponseFrom(ServiceIdent, id.Message, null);
            }
        }

        
        /// <summary>
        /// Asynchronous sending and receiving a message using async/await semantics.
        /// </summary>
        /// <param name="id">The request identifier, contains the request and the response.</param>
        /// <returns>The request identifier, contains the response.</returns>
        public async Task<WcfReqIdent> SendReceiveAsync (WcfReqIdent id)
        {
            if (!IsFaulted && m_ServiceReference != null) // Send() may be used during connection buildup as well
            {
                try
                {
                    //-----------
                    // Variant a:
                    WcfBasicClient.ReceivingState x = null;
                    await Task.Run( () => x = m_ServiceReference.SyncSend( id ) ); // synchronous WCF call
                    //-----------
                    // Variant b:
                    //var x = new WcfBasicClient.ReceivingState();
                    //try
                    //{
                    //    x.idSnd = id;
                    //    x.idRcv = await m_ServiceReference.Channel.SendReceiveAsync (x.idSnd);
                    //    if (x.idRcv == null || x.idRcv.Message == null)
                    //    {
                    //        x.idRcv = x.idSnd;
                    //        x.idRcv.Message = new WcfErrorMessage(WcfErrorMessage.Code.RspNotDeserializableOnClient, "<null> message received");
                    //    }
                    //    x.idSnd.Message = null; // not used anymore
                    //}
                    //catch (Exception ex)
                    //{
                    //    x.idRcv = x.idSnd;
                    //    m_ServiceReference.HandleSendException(x, ex);
                    //}
                    //-----------

                    var rsp = x.idRcv.Message;
                    m_boTimeout = x.timeout;
                    // not streamed data, for tracing:
                    x.idRcv.IsResponse = true;
                    x.idRcv.Sender = ServiceIdent;
                    x.idRcv.Input = ClientIdent;

                    if (!m_boTimeout)
                    {
                        WcfNotifyResponse multi = rsp as WcfNotifyResponse;
                        if (multi != null)
                        {
                            WcfReqIdent idNfy = new WcfReqIdent(ServiceIdent, x.idRcv.ClientId, 0, null, x.idSnd.SourceLambda);
                            idNfy.IsResponse = true;
                            idNfy.Input = ClientIdent;
                            foreach (WcfMessage p in multi.Notifications)
                            {
                                idNfy.Message = p;
                                OnWcfNotificationFromService(idNfy, ClientIdent.DefaultInputHandler);
                            }
                            x.idRcv.Message = multi.Response;
                        }

                        rsp = CheckResponse(x.idRcv, x.idSnd, false);
                    }

                    id.Message = rsp;
                    id.ClientId = x.idRcv.ClientId;
                    id.RequestId = x.idRcv.RequestId;
                    id.SendId = x.idRcv.SendId;
                    LastRequestIdReceived = x.idRcv.RequestId;
                }
                catch (Exception ex)
                {
                    id.Message = new WcfErrorMessage(WcfErrorMessage.Code.CouldNotStartSend, ex);
                }
            }
            else
            {
                id.Message = new WcfErrorMessage(WcfErrorMessage.Code.NotConnected, "Cannot send");
            }
            id.IsResponse = true;
            id.Sender = ServiceIdent;
            if (ClientIdent.TraceReceive) WcfTrc.Info(id.CltRcvId, id.ToString(), ClientIdent.Logger);
            return id;
        }

    }
}
