using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Remact.Net;
using Test2.Contracts;

namespace Test2.Service
{
  /// <summary>
  /// Implementation of the Test2 Service
  /// </summary>
  class Test2Service
  {
    public  IActorInput  Input      { get { return m_Input; } } // just the ActorInput interface is public
    public  int          Seconds;
    public  volatile int Requests;
    
    private int          m_UpdateIndex = 0;
    private ActorInput   m_Input;
    

    /// <summary>
    /// Initializes a new instance of the Test2Service class.
    /// </summary>
    public Test2Service ()
    {
        m_Input = new ActorInput ("NEW", OnUnhandledRequest);
        m_Input.OnInputConnected += OnConnectDisconnect;
        m_Input.OnInputDisconnected += OnConnectDisconnect;
        m_Input.Dispatcher.AddActorInterface(typeof(ITest2Service), this);
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
            Seconds++;
            return m_Input.DoPeriodicTasks();
        }
    }

    // Remact service method for connect and disconnect requests
    public void OnConnectDisconnect(ActorMessage msg)
    {
        // nothing to do, connect-logging is switched on
    }


    // Remact service method for unknown messages
    void OnUnhandledRequest(ActorMessage msg)
    {
        msg.SendResponse(new ErrorMessage(ErrorMessage.Code.ReqOrRspNotSerializableOnService));
    }

      
    // Remact service method
    Test2Rsp GetSomeData(ReadyMessage req, ActorMessage msg)
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
    ReadyMessage SpeedTest1(Test2Req req, ActorMessage msg)
    {
        Requests++;
        return new ReadyMessage();
    }

    // Remact service method
    Test2Rsp SpeedTest2(Test2Req req, ActorMessage msg)
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