
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Threading;
using System.Threading.Tasks;
using Remact.Net;
using Remact.SpeedTest.Contracts;

namespace Remact.SpeedTest.Service
{
    /// <summary>
    /// Implementation of the Test2 Service
    /// </summary>
    class Test2Service
    {
        public IRemactPortService Input { get { return m_Input; } } // just the RemactPortService interface is public
        public volatile int Requests;

        private int m_UpdateIndex = 0;
        private RemactPortService m_Input;


        /// <summary>
        /// Initializes a new instance of the Test2Service class.
        /// </summary>
        public Test2Service()
        {
            m_Input = new RemactPortService("NEW", OnUnhandledRequest);
            m_Input.OnInputConnected += OnConnectDisconnect;
            m_Input.OnInputDisconnected += OnConnectDisconnect;
            m_Input.InputDispatcher.AddActorInterface(typeof(ITest2Service), this);
            //m_Input.TraceSend = true;
        }


        // returns true, when a client state has changed.
        public bool DoPeriodicTasks()
        {
            if (m_Input.MustOpenInput)
            {
                m_Input.Open(); // opens the service host
                return false;
            }
            else
            {
                return m_Input.DoPeriodicTasks();
            }
        }

        // Remact service method for connect and disconnect requests
        public Task OnConnectDisconnect(RemactMessage msg)
        {
            // nothing to do, connect-logging is switched on
            return null;
        }


        // Remact service method for unknown messages
        Task OnUnhandledRequest(RemactMessage msg)
        {
            // will send ErrorMessage.Code.NotImplementedOnService
            throw new NotImplementedException("unknown message on service " + msg.ToString());
            //return null;
        }


        // Remact service method
        Test2Rsp GetSomeData(ReadyMessage req, RemactMessage msg)
        {
            Requests++;
            RaLog.Info(msg.SvcRcvId, string.Format("{0}, thd={1}",
                        msg.ToString(), Thread.CurrentThread.ManagedThreadId.ToString()));

            var rsp = new Test2Rsp();
            rsp.Index = ++m_UpdateIndex;

            rsp.AddItem("Item A", 1, 11, 101, "text1");
            rsp.AddItem("Item B", 2, 12, 102, "text2");
            rsp.AddItem("Item C", 3, 13, 103, "text3");
            rsp.AddItem("Item D", 4, 14, 104, "text4");

            msg.Source.Notify("AdditionalData", new ReadyMessage()); // an additional notification in case of "GetSomeData"

            return rsp;
        }

        // Remact service method
        ReadyMessage SpeedTest1(Test2Req req, RemactMessage msg)
        {
            Requests++;
            return new ReadyMessage();
        }

        // Remact service method
        Test2Rsp SpeedTest2(Test2Req req, RemactMessage msg)
        {
            Requests++;
            var rsp = new Test2Rsp();
            rsp.Index = ++m_UpdateIndex;

            rsp.AddItem("Item Q", 1, 11, 101, "text1");
            rsp.AddItem("Item R", 2, 12, 102, "text2");
            rsp.AddItem("Item S", 3, 13, 103, "text3");
            rsp.AddItem("Item T", 4, 14, 104, "text4");

            return rsp;
        }
    }
}
