using System.Threading.Tasks;
using Remact.Net;

namespace Test2.Contracts
{
    /// <summary>
    /// The interface definition for Test2Service.
    /// 
    /// On the serverside this interface has to be added to the RemactPortService.InputDispatcher.
    /// At runtime, when calling RemactDispatcher.AddActorInterface the methods defined here are matched to the implemented methods.
    /// The implemented methods may be private, must be unique in name and must contain an additional parameter of type RemactMessage.
    /// They may optionally contain a third parameter containing the TSC SourceContext of a RemactPortService{TSC}.
    /// 
    /// On the clientside, this interface may be implemented in a helper class that enforces strong typed sending of requests.
    /// The return types are defined as Task{RemactMessage{T}}. This allows to asynchronously convert and receive the response.
    /// </summary>
    public interface ITest2Service
    {
        Task<RemactMessage<Test2Rsp>>      GetSomeData(ReadyMessage req);

        Task<RemactMessage<ReadyMessage>>  SpeedTest1 (Test2Req req);

        Task<RemactMessage<Test2Rsp>>      SpeedTest2 (Test2Req req);
    }
}
