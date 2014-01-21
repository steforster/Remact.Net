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
    //----------------------------------------------------------------------------------------------
    #region Fields
    
    public  IActorInput      Input {get{return m_Input;}}
    public  int              Seconds;
    public  int              Requests;
    
    private int              m_UpdateIndex = 0;
    private ActorInput       m_Input;
    
    #endregion
    //----------------------------------------------------------------------------------------------
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the Test2Service class.
    /// </summary>
    public Test2Service ()
    {
      m_Input = new ActorInput ("NEW", OnRequest);
      m_Input.OnInputConnected += OnConnectDisconnect;
      m_Input.OnInputDisconnected += OnConnectDisconnect;
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Public Methods

    public void OnConnectDisconnect(ActorMessage msg)
    {
    }
    
    public void OnRequest (ActorMessage msg)
    {
      Requests++;
      object response = null;

      if (msg.DestinationMethod == "Test2Req")
      {
          // SpeedTest
          Test2Req req;
          if (msg.TryConvertPayload(out req))
          {
              response = new ReadyMessage();
          }
          else
          {
              response = new ErrorMessage(ErrorMessage.Code.ReqOrRspNotSerializableOnService);
          }
      }
      else
      {
        RaLog.Info (msg.SvcRcvId, string.Format ("{0}, thd={1}",
                    msg.ToString(), Thread.CurrentThread.ManagedThreadId.ToString()));

        msg.SendResponse (new ReadyMessage ()); // an additional notification

        if (msg.DestinationMethod == "ReadyMessage")
        {
          var rsp = new Test2Rsp();
          rsp.Index = ++m_UpdateIndex;

          rsp.AddItem("Item A", 1, 11, 101, "text1");
          rsp.AddItem("Item B", 2, 12, 102, "text2");
          rsp.AddItem("Item C", 3, 13, 103, "text3");
          rsp.AddItem("Item D", 4, 14, 104, "text4");
          response = rsp;
        }
        else
        {
            response = new ReadyMessage();
        }
      }

      msg.SendResponse (response);

    }// OnRequest


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
      
    #endregion

  }
}
