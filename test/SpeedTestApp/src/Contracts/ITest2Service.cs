using System.Threading.Tasks;
using Remact.Net;

namespace Test2.Contracts
{
    /// <summary>
    /// The interface definition for Test2Service.
    /// 
    /// On the serverside this interface has to be added to the ActorInput.Dispatcher.
    /// At runtime, when calling Dispatcher.AddActorInterface the methods defined here are matched to the implemented methods.
    /// The implemented methods may be private, must be unique in name and must contain an additional parameter of type ActorMessage.
    /// They may optionally contain a third parameter containing the TSC SourceContext of a ActorInput{TSC}.
    /// 
    /// On the clientside, this interface may be implemented in a helper class that enforces strong typing.
    /// The return types are defined as Task{ActorMessage{T}}. This allows to asynchronously receive the response.
    /// </summary>
    public interface ITest2Service
    {
        Task<ActorMessage<Test2Rsp>>      GetSomeData(ReadyMessage req);

        Task<ActorMessage<ReadyMessage>>  SpeedTest1 (Test2Req req);

        Task<ActorMessage<Test2Rsp>>      SpeedTest2 (Test2Req req);
    }
}
