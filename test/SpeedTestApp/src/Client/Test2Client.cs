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
  class Test2Client
  {
    Proxy _proxy;

    public event Action UpdateView;
    public StringBuilder Log;
    public IRemactPortProxy Output { get { return _proxy.Output; } }  // just return the IRemactPortClient interface
    public int LastRequestIdSent { get { return _proxy.Output.LastRequestIdSent; } }
    public int ResponseCount;
    public bool SpeedTest;


    /// <summary>
    /// Initializes a new instance of the Test2Client class.
    /// </summary>
    public Test2Client()
    {
        _proxy = new Proxy
        {
            Output = new RemactPortProxy("Client1", OnUnhandledResponse) {TraceSend = true, TraceReceive = false}
        };
        Log = new StringBuilder(11000);
    }


    public Task<bool> TryConnect()
    {
        return _proxy.Output.TryConnect();
    }


    public void SendPeriodicMessage()
    {
        if (SpeedTest)
        {
            OnSpeed1Response(null, null); // start the test
        }
        else
        {
            var task = _proxy.GetSomeData(new ReadyMessage());
            task.ContinueWith((t) => OnDataResponse(t.Result.Payload, t.Result), TaskContinuationOptions.ExecuteSynchronously);
        }
    }


    // Remact client method for unknown responses
    private void OnUnhandledResponse(RemactMessage msg)
    {
        ResponseCount++;
        Log.AppendFormat("{0} {1}, thd={2}", msg.CltRcvId, msg.Payload.ToString(), Thread.CurrentThread.ManagedThreadId.ToString());
        Log.AppendLine();

        ReadyMessage ready;
        if (msg.IsError)
        {
            ErrorMessage error;
            msg.TryConvertPayload(out error);
            RaLog.Warning(msg.CltRcvId, error.ToString());
        }
        else if (msg.TryConvertPayload(out ready))
        {
            RaLog.Info(msg.CltRcvId, msg.Payload.ToString());
        }
        else
        {
            RaLog.Warning(msg.CltRcvId, msg.Payload.ToString());
        }
    }


    // Remact client method
    private void OnSpeed1Response(ReadyMessage response, RemactMessage msg)
    {
        ResponseCount++;
        if (SpeedTest)
        {
            if (msg != null && !msg.IsResponse)
            {
                RaLog.Error(msg.CltRcvId, "received unexpected message " + msg.ToString());
            }

            // send payload to the destination method
            var task = _proxy.SpeedTest1(new Test2Req(Test2Req.ERequestCode.Normal));
            // when the response is received asynchronously (after this method is left), call this method again
            task.ContinueWith((t) => OnSpeed1Response(t.Result.Payload, t.Result), TaskContinuationOptions.ExecuteSynchronously);
        }
    }


    // Remact client method
    private void OnDataResponse(Test2Rsp response, RemactMessage msg)
    {
        ResponseCount++;
        Log.AppendFormat("{0} {1}, thd={2}", msg.CltRcvId, msg.Payload.ToString(), Thread.CurrentThread.ManagedThreadId.ToString());
        if (_proxy.Output.OutstandingResponsesCount != 0) 
        { 
            Log.Append(", ## missing replies: ");
            Log.Append(Output.OutstandingResponsesCount); 
        }
        Log.AppendLine();

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
        public RemactPortProxy Output;

        public Task<RemactMessage<Test2Rsp>> GetSomeData(ReadyMessage req)
        {
            return Output.Ask<Test2Rsp>("GetSomeData", req);
        }

        public Task<RemactMessage<ReadyMessage>> SpeedTest1(Test2Req req)
        {
            return Output.Ask<ReadyMessage>("SpeedTest1", req);
        }

        public Task<RemactMessage<Test2Rsp>> SpeedTest2(Test2Req req)
        {
            return Output.Ask<Test2Rsp>("SpeedTest2", req);
        }
    }
  }
}
