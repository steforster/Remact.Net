using System;
using System.Threading;
using SourceForge.AsyncWcfLib; // Copyright (c) 2012, <http://asyncwcflib.sourceforge.net>
using SourceForge.AsyncWcfLib.Basic;
using Test1.Messages;


namespace Test1.ClientNoSync
{
  class Test1ClientNoSync
  {
    static ActorOutput         m_Client;
    static string              m_Command = string.Empty;
    static string              m_ServiceUri;
    
    //----------------------------------------------------------------------------------------------
    #region == Program startup thread ==

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
      m_ServiceUri = "http://"+host+"/AsyncWcfLib/Test1.Service";


      m_Client = new ActorOutput("Nosy", OnMessageReceived);
      m_Client.TraceSend = true;
      m_Client.IsMultithreaded = true; // has no synchronization context
      m_Client.LinkOutputToRemoteService(new Uri(m_ServiceUri));

      Console.Title = m_Client.AppIdentification;
      Console.WriteLine ("Commandline arguments:   ClientInstance="+WcfDefault.Instance.ApplicationInstance
                        +"   ServiceHostname:Port='"+host+"'\r\n");
      Console.WriteLine ("Starting client '"+m_Client.Name+"' for service '"+m_ServiceUri+"'\r\n");
      Console.WriteLine ("Press 'q' to quit.");
      Console.WriteLine ("Responses are executed on any threadpool thread. No synchronization takes place...\r\n");

      while (m_Command != null && m_Command.ToLower() != "q")
      {
        Console.Write (" Thread="+Thread.CurrentThread.ManagedThreadId);

        if (m_Client.MustConnectOutput)
        {
          m_Client.TryConnect();
          Console.Write (", connecting in main()...");
        }
        else
        {
          Console.Write (", sending in main()...");
          m_Client.SendOut (new Test1CommandMessage (m_Command));
        }
        WcfTrc.Run();
        m_Command = Console.ReadLine();
      }

      WcfApplication.Exit (0);
    }// Main


    // called for all normal and exceptional close types
    static void ApplicationExitHandler (WcfApplication.CloseType closeType, ref bool goExit)
    {
      WcfTrc.Info ("ApplicationExitHandler", "handling "+closeType+", terminating="+goExit);
      if (goExit)
      {
        m_Client.Disconnect();
        if (WcfApplication.IsRunningWithMono) Console.WriteLine("\n\r---application ended---");
      }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == Threadpool thread ==

    // receive a message from service...
    static void OnMessageReceived (WcfReqIdent rsp)
    {
      Console.Write ("\n\r Thread="+Thread.CurrentThread.ManagedThreadId);
      Console.WriteLine(", received from "+rsp.Sender.Name+": "+rsp.Message.ToString());
      Console.Write ("\n\r\n\rSend command > ");

      if (rsp.Message is WcfErrorMessage)
      {
        WcfTrc.Error (rsp.CltRcvId, rsp.Message+"\r\n"+(rsp.Message as WcfErrorMessage).StackTrace);
      }
    }

    #endregion
  }// class
}
