using System;
using System.Threading;
using Remact.Net;              // Copyright (c) 2014, <http://github.com/steforster/Remact.Net>
using Remact.Net.Internal;
using Test1.Messages;

namespace Test1.Service
{
  class Test1Service
  {
    //----------------------------------------------------------------------------------------------
    #region == Program startup thread ==

    static void Main (string[] args)
    {
        // Commandlinearguments:
        // 0: Application instance id. Default = 0 --> process id is used.
        // 1: TCP port number for this service (on localhost). Default = 40001.

        RemactDefault.ApplicationStart (args, new RaTrc.PluginFile(), /*ExitHandler=*/true);
        RemactApplication.ApplicationExit += ApplicationExitHandler;

        int tcpPort; // the second commandline argument
        if (args.Length < 2 || !int.TryParse(args[1], out tcpPort))
        {
            tcpPort = 40001;
        }

        ActorInput.DisableRouterClient = true; // Test1.Client does not use Remact.Catalog.exe, but we publish this service anyway.
        Console.WriteLine ("Commandline arguments:   ServiceInstance="+RemactDefault.Instance.ApplicationInstance
                        +"   ServiceTcpPort="+tcpPort+"\r\n");

        Test1Service  test = new Test1Service();
        ActorInput service = new ActorInput("Test1.Service", test.OnMessageReceived);
        service.IsMultithreaded = true; // we have no message queue in a console application

        // The clients may connect without WcfRouter. They know our TCP port. 
        service.LinkInputToNetwork(null, tcpPort);
        Console.Title = service.AppIdentification;
        if (service.TryConnect())
        {
            Console.WriteLine( "Service listening on '" + service.Uri.ToString() + "'" + Environment.NewLine );
        }
        else
        {
            Console.WriteLine(service.BasicService.LastAction);
        }
      
        while (true)
        {
            Console.WriteLine (String.Format ("Thread={0}: {1:##0} client(s) connected, {2} known",
                                               Thread.CurrentThread.ManagedThreadId,
                                               service.BasicService.ConnectedClientCount,
                                               service.BasicService.ClientCount));
            RaTrc.Run ();
            Thread.Sleep (10000);
        }
    }// Service.Main
    

    // called for all normal and exceptional close types
    static void ApplicationExitHandler (RemactApplication.CloseType closeType, ref bool goExit)
    {
        //if (closeType == WcfApplication.CloseType.CtrlC) goExit = true; // test application cancellation
        if (goExit)
        {
            ActorPort.DisconnectAll ();
            if (RemactApplication.IsRunningWithMono) Console.WriteLine(Environment.NewLine + "---application ended---"); // helpful, when started from MonoDevelop
        }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == Requesting thread ==

    // receive a message from client...
    public void OnMessageReceived (ActorMessage req)
    {
        object response;
        if (req.Payload is Test1.Messages.Test1CommandMessage)
        {
            Console.WriteLine (String.Format("Thread={0} --> received command '{1}' from client[{2}] {3}",
                               Thread.CurrentThread.ManagedThreadId,
                               (req.Payload as Test1.Messages.Test1CommandMessage).Command,
                               req.ClientId, req.Source.Uri));

            response = new ReadyMessage();
        }
        else
        {
            response = new ErrorMessage (ErrorMessage.Code.AppRequestNotAcceptedByService, "");
        }

        req.SendResponse (response);
    }

    #endregion
  }
}
