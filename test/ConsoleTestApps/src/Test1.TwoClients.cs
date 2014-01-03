using System;
//    System.Core.dll must be referenced in order to use extension methods
using System.Threading;
using Nito.Async;              // Copyright (c) 2009, Nito Programs, <http://nitoasync.codeplex.com>
using SourceForge.AsyncWcfLib; // Copyright (c) 2012, <http://asyncwcflib.sourceforge.net>
using SourceForge.AsyncWcfLib.Basic;
using Test1.Messages;

namespace Test1.TwoClients
{
  class TwoClients
  {
    ActorOutput         m_ClientOne;
    ActorInput          m_ClientTwoInput;
    ActorOutput         m_ClientTwo;
    string              m_Command = "";
    static string       m_ServiceUri;

    //----------------------------------------------------------------------------------------------
    #region == Program startup thread ==

    static void Main (string[] args)
    {
      // Commandlinearguments:
      // 0: Application instance id. Default = 0 --> process id is used.
      // 1: Hostname and TCP port for the service to connect to. Default: "localhost:40001" is used.

      WcfDefault.ApplicationStart (args, new WcfTrc.PluginFile(), /*ExitHandler=*/true);
      Test1CommandMessage.AddKnownMessageTypes ();

      string host = "localhost:40001";
      if (args.Length > 1 && args[1].Length > 0) host = args[1];
      m_ServiceUri = "http://"+host+"/AsyncWcfLib/Test1.Service";

      TwoClients test = new TwoClients();
      test.m_ClientTwoInput = new ActorInput ("TWOinp", test.MessageHandlerTwo);
      test.m_ClientTwo      = new ActorOutput("TWO",    test.MessageHandlerTwo);
      test.m_ClientTwo.TraceSend = true;
      test.m_ClientTwo.LinkOutputToRemoteService(new Uri(m_ServiceUri));

      test.m_ClientOne = new ActorOutput("ONE");
      test.m_ClientOne.TraceSend = true;
      test.m_ClientOne.LinkOutputTo(test.m_ClientTwoInput);

      WcfApplication.ApplicationExit += test.ApplicationExitHandler;

      Console.Title = test.m_ClientTwo.AppIdentification;
      Console.WriteLine ("Commandline arguments:   ClientInstance="+WcfDefault.Instance.ApplicationInstance
                        +"   ServiceHostname:Port='"+host+"'\r\n");
      Console.WriteLine ("Starting client 'ONE' internally connected to 'TWO'");
      Console.WriteLine ("Starting client 'TWO' for service '"+m_ServiceUri+"'\r\n");
      Console.WriteLine ("The client is using Nito.Async.ActionThread to queue and synchronize responses on the same thread as the request was sent.\r\n");
      Console.WriteLine ("Clients ONE and TWO are running on different threads.");
      Console.WriteLine ("Press 'q' to quit, 'e' or 'E' to test general exception handling.\r\n");
      
      ActionThread actionThreadOne = new ActionThread();
      actionThreadOne.IsBackground = true;
      actionThreadOne.Start();

      ActionThread actionThreadTwo = new ActionThread();
      actionThreadTwo.IsBackground = true;
      actionThreadTwo.Start();
      actionThreadTwo.Do (test.OnConnectAction); // in order to pickup the synchronization context it must send a message.

      while (test.m_Command.ToLower () != "q")
      {
        if (test.m_Command =="E") throw new Exception("test main exception");
        test.m_Command = Console.ReadLine();
        if (test.m_Command == null)
        {
          WcfTrc.Info("Main","Running without console (Console.ReadLine returned null)");
          test.m_Command = string.Empty; // ctrl-c in Windows, process without terminal in Linux
          Thread.Sleep(5000);
          Console.Write ("\n\r");
        }
        actionThreadOne.Do (test.OnSendAction);
      }

      WcfApplication.Exit (0);
    }// Main

    
    // called for all normal and exceptional close types
    void ApplicationExitHandler (WcfApplication.CloseType closeType, ref bool goExit)
    {
      if (closeType == WcfApplication.CloseType.CtrlC) goExit = true; // test application cancellation
      
      if (goExit)
      {
        m_ClientOne.Disconnect();
        if (WcfApplication.IsRunningWithMono) Console.WriteLine("\n\r---application ended---");
      }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == ActionThread ONE ==

    void OnSendAction ()
    {
      Console.Write (" ONE Thread="+Thread.CurrentThread.ManagedThreadId);
      uint sendContextNumber = m_ClientOne.LastRequestIdSent + 1000;

      if (m_Command =="e") throw new Exception("test thread exception");

      if( m_ClientOne.MustConnectOutput ) m_ClientOne.TryConnect();

      WcfMessage msg = new Test1CommandMessage (m_Command);
      m_ClientOne.SendOut(msg, rsp =>
        rsp.On<WcfIdleMessage>(idle =>
        {
          Console.Write ("\n\r ONE Thread="+Thread.CurrentThread.ManagedThreadId);
          Console.WriteLine (", received idle message in sending context #"+sendContextNumber);
          Console.Write ("\n\rSend command > ");
        }));

      Console.Write (", sending context#"+sendContextNumber+"...");
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == ActionThread TWO ==

    void OnConnectAction()
    {
      Console.WriteLine ("\n\r TWO Thread="+Thread.CurrentThread.ManagedThreadId+", connecting...");
      m_ClientTwoInput.Open();
      m_ClientTwo.TryConnect (); // must be called from correct SynchronizationContext
    }
    
    
    void MessageHandlerTwo (WcfReqIdent req)
    {
      Console.Write ("\n\r TWO Thread="+Thread.CurrentThread.ManagedThreadId);
      Console.Write (", received from "+req.Sender.Name+": "+req.Message.ToString());

      if ((m_ClientTwo.MustConnectOutput)
      && !(req.Message is WcfErrorMessage))
      {
        // got a message from client ONE, but TWO is not yet connected
        OnConnectAction();
      }
      else if (req.Message is Test1CommandMessage)
      {
        // got a message from client ONE
        uint sendContextNumber = m_ClientTwo.LastRequestIdSent + 2000;
        // msg carries response address and handler, this may not be changed!
        // therefore we create a copy
        Test1CommandMessage cmd = new Test1CommandMessage ((req.Message as Test1CommandMessage).Command);
        m_ClientTwo.SendOut (cmd,
          rsp =>
          rsp.On<WcfIdleMessage>(idle =>
          { // got a normal response from connected service, we send a response to ONE
            Console.Write ("\n\r TWO Thread="+Thread.CurrentThread.ManagedThreadId);
            Console.Write (", received idle message in sending context #"+sendContextNumber+" passing it to "+rsp.Sender.Name);
            req.SendResponse (idle);
          }));
        Console.Write (", sending context#"+sendContextNumber+"...");
      }
      else
      {
        // got a connect response or error message from service, no response to ONE
        Console.Write ("\r\nSend command > ");
      }

      WcfTrc.Run();
    }

    #endregion
  }//MainClass
}
