using System;
using System.Threading;
using Nito.Async;              // Copyright (c) 2009, Nito Programs, <http://nitoasync.codeplex.com>
using SourceForge.AsyncWcfLib; // Copyright (c) 2012, <http://asyncwcflib.sourceforge.net>
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

      WcfDefault.ApplicationStart (args, new WcfTrc.PluginFile(), /*ExitHandler=*/true);
      WcfApplication.ApplicationExit += ApplicationExitHandler;
      Test1CommandMessage.AddKnownMessageTypes ();

      string host = "localhost:40001";
      if (args.Length > 1 && args[1].Length > 0) host = args[1];
      string serviceUri = "http://"+host+"/AsyncWcfLib/Test1.Service";

      TestInput = new ActorInput ("NitoIn", OnMessageReceived);
      Test      = new ActorOutput("Nito",   OnMessageReceived);
      Test.LinkOutputToRemoteService (new Uri(serviceUri));
      Test.TraceSend = true;
      ActionThread actionThread = new ActionThread();

      Console.Title = Test.AppIdentification;
      Console.WriteLine ("Commandline arguments:   ClientInstance="+WcfDefault.Instance.ApplicationInstance
                        +"   ServiceHostname:Port='"+host+"'\r\n");
      Console.WriteLine ("Starting client '"+Test.Name+"' for service '"+Test.OutputSidePartner.Uri+"'\r\n");
      Console.WriteLine ("Press 'q' to quit.");
      Console.WriteLine ("The client is using Nito.Async.ActionThread to queue and synchronize responses on the same thread as the request was sent.\r\n");

      actionThread.Start();
      actionThread.Do (OnStartup);

      Console.Write ("\n\r Thread="+Thread.CurrentThread.ManagedThreadId+", is reading commands from console...");
      while (true)
      {
        WcfTrc.Run();
        string command = Console.ReadLine();
        if (command == null)
        {
          Thread.Sleep (3000); // Mono debugging returns null and does not wait
          command = "xyz";
        }

        if (command.ToLower() == "q") break; // from while
        TestInput.PostInput(new Test1CommandMessage(command));
      }
      
      WcfApplication.Exit (0);
    }// Main


    // called for all normal and exceptional close types
    static void ApplicationExitHandler (WcfApplication.CloseType closeType, ref bool goExit)
    {
      //if (closeType == WcfApplication.CloseType.CtrlC) goExit = true; // test application cancellation

      WcfTrc.Info ("ApplicationExitHandler", "handling "+closeType+", terminating="+goExit);
      if (goExit)
      {
        Test.Disconnect();
        if (WcfApplication.IsRunningWithMono) Console.WriteLine("\n\r---application ended---"); // helpful, when started from MonoDevelop
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
    static void OnMessageReceived (WcfReqIdent id)
    {
      Console.Write ("\n\r Thread="+Thread.CurrentThread.ManagedThreadId+", received: "+id.Message.ToString());

      if (id.Message is Test1CommandMessage)
      {
        WcfState s = Test.OutputState;
        if (s == WcfState.Disconnected || s == WcfState.Faulted)
        {
          OnStartup();
        }
        else if (s == WcfState.Connecting)
        {
          Console.Write (" - cannot send, still connecting...");
        }
        else
        {
          uint sendContextNumber = Test.LastSentId + 1000;
          Test.SendOut (id.Message, rsp =>
            rsp.On<WcfIdleMessage>(idle =>
            {
              Console.Write ("\n\r Thread="+Thread.CurrentThread.ManagedThreadId);
              Console.WriteLine (", received idle message in sending context #"+sendContextNumber);
              Console.Write ("\n\r\n\rSend command > ");
            }));
  
          Console.Write (", sending context#"+sendContextNumber+"...");
          return;
        }
      }
      else if (id.Message is WcfErrorMessage)
      {
        WcfTrc.Error (id.CltRcvId, id.Message+"\r\n"+(id.Message as WcfErrorMessage).StackTrace);
      }
      else
      {
        Console.Write (", from "+id.Sender.Name);
      }

      Console.Write ("\n\r\n\rSend command > ");
    }

  }// class
  #endregion
}
