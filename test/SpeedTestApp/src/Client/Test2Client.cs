using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Remact.Net;
using Test2.Contracts;

namespace Test2.Client
{
  /// <summary>
  /// Implementation of the Test2 Client
  /// </summary>
  class Test2Client // : ITest2Client
  {
    ActorOutput _output;

    public event Action UpdateView;
    public StringBuilder Log;
    public IActorOutput Output { get { return _output; } }
    public int LastRequestIdSent { get { return _output.LastRequestIdSent; } }
    public int ResponseCount;
    public bool SpeedTest;


    /// <summary>
    /// Initializes a new instance of the Test2Client class.
    /// </summary>
    public Test2Client()
    {
        _output = new ActorOutput("Client1", OnUnhandledResponse);
        _output.TraceSend = true;
        _output.TraceReceive = false;
        Log = new StringBuilder(11000);
    }


    public void TryConnect()
    {
        _output.TryConnect();
    }


    public void SendPeriodicMessage()
    {
        if (SpeedTest) _output.Ask<ReadyMessage>("SpeedTest1", new Test2Req(Test2Req.ERequestCode.Normal), OnSpeed1Response);
                  else _output.Ask<Test2Rsp>    ("GetSomeData", new ReadyMessage(), OnDataResponse);
    }


    // Remact client method for unknown responses
    private void OnUnhandledResponse(ActorMessage msg)
    {
        if (msg.IsError)
        {
            ErrorMessage error;
            msg.TryConvertPayload(out error);
            RaLog.Warning(msg.CltRcvId, error.ToString());
        }
        else
        {
            RaLog.Warning(msg.CltRcvId, msg.Payload.ToString());
        }
    }


    // Remact client method
    private void OnSpeed1Response(ReadyMessage response, ActorMessage msg)
    {
        ResponseCount++;
        if (SpeedTest)
        {
            // send payload to the destination method, do not handle the response here - handle it in the default handler 'OnMessageFromService'
            _output.Ask<ReadyMessage>("SpeedTest1", new Test2Req(Test2Req.ERequestCode.Normal), OnSpeed1Response);
        }
    }


    // Remact client method
    private void OnDataResponse(Test2Rsp response, ActorMessage msg)
    {
        Log.Length = 0;
        Log.AppendFormat("{0} {1}, thd={2}", msg.CltRcvId, msg.Payload.ToString(), Thread.CurrentThread.ManagedThreadId.ToString());
        if (Output.OutstandingResponsesCount != 0) 
        { 
            Log.Append(", out="); 
            Log.Append(Output.OutstandingResponsesCount); 
        }

        string s = string.Empty;
        foreach (var item in response.Items)
        {
            s += ", " + item.ItemName;
        }

        RaLog.Info(msg.CltRcvId, "Test2Rsp contains " + response.Items.Count + " items" + s);
        UpdateView();
    }

    // implementation of the service interface for type safety
    private class Proxy : ITest2Service
    {
        public ActorOutput Output;

        public Task<ActorMessage<Test2Rsp>> GetSomeData(ReadyMessage req)
        {
            return Output.Ask<Test2Rsp>("GetSomeData", req);
        }

        public Task<ActorMessage<ReadyMessage>> SpeedTest1(Test2Req req)
        {
            return Output.Ask<ReadyMessage>("SpeedTest1", req);
        }

        public Task<ActorMessage<Test2Rsp>> SpeedTest2(Test2Req req)
        {
            return Output.Ask<Test2Rsp>("SpeedTest2", req);
        }
    }
  }
}
