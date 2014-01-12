using System;
using System.Threading;
using Nito.Async;              // Copyright (c) 2009, Nito Programs, <http://nitoasync.codeplex.com>
using Remact.Net;              // Copyright (c) 2014, <http://github.com/steforster/Remact.Net>

using Test1.Messages;


namespace Test1.Client
{
  //----------------------------------------------------------------------------------------------
  #region == Program startup thread ==

  class Test1Client
  {
    public static ActorInput  TestInput;
    public static ActorOutput Test;

    static void Main (string[] args)
    {
        // Commandlinearguments:
        // 0: Application instance id. Default = 0 --> process id is used.
        // 1: Hostname and TCP port for the service to connect to. Default: "localhost:40001" is used.

        RemactDefault.ApplicationStart(args, new RaTrc.PluginFile(), /*ExitHandler=*/true);
        RemactApplication.ApplicationExit += ApplicationExitHandler;

        string host = "localhost:40001";
        if (args.Length > 1 && args[1].Length > 0) host = args[1];
        string serviceUri = "ws://"+host+"/Remact/Test1.Service";

        TestInput = new ActorInput ("NitoIn", OnMessageReceived);
        Test      = new ActorOutput("Nito",   OnMessageReceived);
        Test.LinkOutputToRemoteService (new Uri(serviceUri));
        Test.TraceSend = true;
        ActionThread actionThread = new ActionThread();

        Console.Title = Test.AppIdentification;
        Console.WriteLine ("Commandline arguments:   ClientInstance="+RemactDefault.Instance.ApplicationInstance
                        +"   ServiceHostname:Port='"+host+"'\r\n");
        Console.WriteLine ("Starting client '"+Test.Name+"' for service '"+Test.OutputSidePartner.Uri+"'\r\n");
        Console.WriteLine ("Press 'q' to quit.");
        Console.WriteLine ("The client is using Nito.Async.ActionThread to queue and synchronize responses on the same thread as the request was sent.\r\n");

        actionThread.Start();
        actionThread.Do (OnStartup);

        Console.Write ("\n\r Thread="+Thread.CurrentThread.ManagedThreadId+", is reading commands from console...");
        while (true)
        {
            RaTrc.Run();
            string command = Console.ReadLine();
            if (command == null)
            {
                Thread.Sleep (3000); // Mono debugging returns null and does not wait
                command = "xyz";
            }

            if (command.ToLower() == "q") break; // from while
            TestInput.PostInput(new Test1CommandMessage(command));
        }
      
        RemactApplication.Exit (0);
    }// Main


    // called for all normal and exceptional close types
    static void ApplicationExitHandler(RemactApplication.CloseType closeType, ref bool goExit)
    {
        //if (closeType == WcfApplication.CloseType.CtrlC) goExit = true; // test application cancellation

        RaTrc.Info("ApplicationExitHandler", "handling " + closeType + ", terminating=" + goExit);
        if (goExit)
        {
            Test.Disconnect();
            if (RemactApplication.IsRunningWithMono) Console.WriteLine("\n\r---application ended---"); // helpful, when started from MonoDevelop
        }
    }

  #endregion
  //----------------------------------------------------------------------------------------------
  #region == Nito.Async.ActionThread ==

    // picks up this thread synchronization context and builds connection to service
    static void OnStartup ()
    {
        Console.Write ("\n\r Thread="+Thread.CurrentThread.ManagedThreadId+", is connecting...");
        TestInput.Open();
        Test.TryConnect();
    }

    // receive a message from main or from service
    static void OnMessageReceived (ActorMessage msg)
    {
        Console.Write ("\n\r Thread="+Thread.CurrentThread.ManagedThreadId+", received: "+msg.PayloadType);

        Test1CommandMessage testMessage;
        ErrorMessage errorMessage;
        if (msg.IsRequest && msg.TryConvertPayload(out testMessage))
        {
            PortState s = Test.OutputState;
            if (s == PortState.Disconnected || s == PortState.Faulted)
            {
                OnStartup();
            }
            else if (s == PortState.Connecting)
            {
                Console.Write (" - cannot send, still connecting...");
            }
            else
            {
                int sendContextNumber = Test.LastRequestIdSent + 1000;
                Test.SendOut(testMessage, 
                        delegate (ReadyMessage response, ActorMessage rsp)
                        {
                            Console.Write ("\n\r Thread="+Thread.CurrentThread.ManagedThreadId);
                            Console.WriteLine (", received idle message in sending context #"+sendContextNumber);
                            Console.Write ("\n\r\n\rSend command > ");
                        });
  
                Console.Write (", sending context #"+sendContextNumber+"...");
                return;
            }
        }
        else if (msg.TryConvertPayload(out errorMessage))
        {
            RaTrc.Error(msg.CltRcvId, errorMessage.ToString() + "\r\n" + errorMessage.StackTrace);
        }
        else
        {
            Console.Write (", from "+msg.Source.Name);
        }

        Console.Write ("\n\r\n\rSend command > ");
    }
  }
  #endregion
}
