using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Remact.Net;

namespace Test2.Contracts
{
    /// <summary>
    /// The interface definition for Test2Service
    /// </summary>
    public interface ITest2Service
    {
        Test2Rsp     GetSomeData(ReadyMessage req, ActorMessage msg);

        ReadyMessage SpeedTest1 (Test2Req req, ActorMessage msg);

        Test2Rsp     SpeedTest2 (Test2Req req, ActorMessage msg);
    }
}
