
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Threading;
using System.Threading.Tasks;
using Remact.Net;
using Remact.Net.Remote;
using Test1.Contracts;
using Remact.TestUtilities;

namespace Test1.Service
{
    //----------------------------------------------------------------------------------------------
    #region == Program startup and exit ==
    static class Program
    {
        static void Main(string[] args)
        {
            // Commandlinearguments:
            // 0: Application instance id. Default = 0 --> process id is used.
            // 1: TCP port number for this service (on localhost). Default = 40001.
            // 2: Transport protocol plugin: 'BMS' or 'JSON'

            RemactDesktopApp.ApplicationStart(args, new RaLog.PluginFile());
            RemactDesktopApp.ApplicationExit += ApplicationExitHandler;

            int tcpPort; // the second commandline argument
            if (args.Length < 2 || !int.TryParse(args[1], out tcpPort))
            {
                tcpPort = 40001;
            }

            string transportPlugin = "BMS"; // default third commandline argument
            if (args.Length >= 3)
            {
                transportPlugin = args[2];
            }

            Console.WriteLine("Commandline arguments:   ServiceInstance=" + RemactConfigDefault.Instance.ApplicationInstance
                            + "   ServiceTcpPort=" + tcpPort
                            + "   Transport=" + transportPlugin + "\r\n");

            PluginSelector.LoadRemactConfigDefault(transportPlugin);
            RemactCatalogClient.IsDisabled = false; // Test1.Client does not use Remact.Net.CatalogApp.exe, but we publish this service to the catalog anyway.

            // We create the service actor. Main threads of console applications have no a SynchronizationContext. 
            // And we do not use a Nito.Async.ActionThread. Therefore, the service executes successive requests on different threadpool threads.
            // We set the IsMultithreaded flag to true. This disables synchronization to a single actor thread.
            var test = new Test1Service("Test1.Service");
            test.ServicePort.IsMultithreaded = true; // we have no message queue in a console application

            // The clients may connect without Remact.Net.CatalogApp. They know our TCP port. 
            test.ServicePort.LinkInputToNetwork(null, tcpPort);

            Console.Title = test.ServicePort.AppIdentification;
            if (test.ServicePort.ConnectAsync().Result)
            {
                Console.WriteLine("Service is listening on '" + test.ServicePort.Uri.ToString() + "'" + Environment.NewLine);
            }
            else
            {
                Console.WriteLine(test.ServicePort.BasicService.LastAction);
            }

            while (true)
            {
                Console.WriteLine(String.Format("Thread={0}: {1:##0} client(s) connected, {2} known",
                                 Thread.CurrentThread.ManagedThreadId,
                                 test.ServicePort.BasicService.ConnectedClientCount,
                                 test.ServicePort.BasicService.ClientCount));
                RaLog.Run();
                Thread.Sleep(10000);
            }
        }


        // called for all normal and exceptional close types
        static void ApplicationExitHandler(RemactDesktopApp.CloseType closeType, ref bool goExit)
        {
            //if (closeType == RemactApplication.CloseType.CtrlC) goExit = true; // test application cancellation
            if (goExit)
            {
                RemactConfigDefault.Instance.Shutdown();
                if (RemactApplication.IsRunningWithMono) Console.WriteLine(Environment.NewLine + "---application ended---"); // helpful, when started from MonoDevelop
            }
        }
    }


    #endregion
    //----------------------------------------------------------------------------------------------
    #region == Service class ==


    class Test1Service
    {
        RemactPortService m_servicePort;

        public Test1Service(string uniqueName)
        {
            m_servicePort = new RemactPortService(uniqueName, OnMessageReceived);
        }

        public RemactPortService ServicePort {get {return m_servicePort;} }

        // receive a message from a client...
        private Task OnMessageReceived(RemactMessage req)
        {
            object response;
            Test1CommandMessage testMessage;
            ErrorMessage error;

            if (req.TryConvertPayload(out testMessage))
            {
                Console.WriteLine(String.Format("Thread={0} --> received command '{1}' from client[{2}] {3}",
                                   Thread.CurrentThread.ManagedThreadId,
                                   testMessage.Command, req.ClientId, req.Source.Uri));

                response = new ReadyMessage(); // signals a successful operation
            }
            else if (req.TryConvertPayload(out error))
            {
                Console.WriteLine(String.Format("Thread={0} --> received '{1}' from client[{2}] {3}",
                                  Thread.CurrentThread.ManagedThreadId,
                                  error.ToString(), req.ClientId, req.Source.Uri));

                response = new ErrorMessage(ErrorCode.ActorReceivedUnexpectedPayloadType, "Test1.Service got: " + error.ToString());
            }
            else
            {
                response = new ErrorMessage(ErrorCode.ActorReceivedUnexpectedPayloadType, "Test1.Service got: " + req.ToString());
            }

            // return the response message to the client
            req.SendResponse(response);
            return null;
        }
    }
    #endregion
}
