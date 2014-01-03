using System;
//    System.Core.dll must be referenced in order to use extension methods
using System.Windows.Forms;    // used for the message queue
using System.Threading;
using SourceForge.AsyncWcfLib; // Copyright (c) 2012, <http://asyncwcflib.sourceforge.net>
using SourceForge.AsyncWcfLib.Basic;
using Test1.Messages;


namespace Test1.ClientWinForm
{
  class Test1ClientWinForm : Form
  {
    static ActorOutput         m_Client;
    static string              m_Command = string.Empty;
    static string              m_ServiceUri;
    
    //----------------------------------------------------------------------------------------------
    #region == Program startup thread ==

    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
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


      m_Client = new ActorOutput("Winnie", OnMessageReceived);
      m_Client.TraceSend = true;
      m_Client.LinkOutputToRemoteService(new Uri(m_ServiceUri));

      Console.Title = m_Client.AppIdentification;
      Console.WriteLine ("Commandline arguments:   ClientInstance="+WcfDefault.Instance.ApplicationInstance
                        +"   ServiceHostname:Port='"+host+"'\r\n");
      Console.WriteLine ("Starting client '"+m_Client.Name+"' for service '"+m_ServiceUri+"'\r\n");
      Console.WriteLine ("The client is using Windows.Forms to queue and synchronize responses on the same thread as the request was sent.\r\n");
      Console.WriteLine ("Thread="+Thread.CurrentThread.ManagedThreadId+", wait for the system to initialize...");

      // Create a communication thread that is running the communication object.
      Test1ClientWinForm commObject = null;
      Thread commThread = new Thread(()=>
      { // this lamda expression is executed on commThread, when starting it.
        commObject = new Test1ClientWinForm(); // pick up the synchronization context of commThread
        Application.Run (commObject);          // start WinForms environment and run the message pump
      });
      commThread.Start();
      // it takes a while until the commThread has created the Windows handle and calls OnActivated

      while (m_Command != null && m_Command.ToLower() != "q")
      {
        // 'ReadLine' waits for user input (synchronious call), the startup thread is blocked here.
        m_Command = Console.ReadLine();
        // 'BeginInvoke' posts an event into the message pump. It does not wait for a response (asynchronious call).
        commObject.BeginInvoke (new Action(OnSendAction));
        WcfTrc.Run();
      }
      
      WcfApplication.Exit (0);
    }// Main

    protected override void OnLoad (EventArgs e)
    {
      base.OnLoad (e);
      this.Visible = false; // There is no WinForm needed, we just use the message pump.
      this.ShowInTaskbar = false;
      Console.Write ("\r\nPress 'q' to quit or enter first command > ");
    }

    protected override void OnActivated (EventArgs e)
    {
      this.Visible = false; // A short blink cannot be eliminated under mono
      base.OnActivated (e);
    }

    // called for all normal and exceptional close types
    static void ApplicationExitHandler (WcfApplication.CloseType closeType, ref bool goExit)
    {
      //if (closeType == WcfApplication.CloseType.CtrlC) goExit = true; // test application cancellation

      WcfTrc.Info ("ApplicationExitHandler", "handling "+closeType+", terminating="+goExit);
      if (goExit)
      {
        m_Client.Disconnect();
        if (WcfApplication.IsRunningWithMono) Console.WriteLine("\n\r---application ended---"); // helpful, when started from MonoDevelop
      }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == Communication thread ==

    static void OnSendAction ()
    {
      Console.Write (" Thread="+Thread.CurrentThread.ManagedThreadId);
      if (m_Client.MustConnectOutput)
      {
        Console.Write (", connecting...");
        m_Client.TryConnect ();
      }
      else
      {
        uint sendContextNumber = m_Client.LastRequestIdSent + 1000;
        m_Client.SendOut (new Test1CommandMessage (m_Command),
          rsp =>
          rsp.On<WcfIdleMessage>(idle =>
          {
            Console.Write ("\n\r Thread="+Thread.CurrentThread.ManagedThreadId);
            Console.WriteLine (", received idle message in sending context #"+sendContextNumber);
            Console.Write ("\n\r\n\rSend command > ");
          }));
        Console.Write (", sending context#"+sendContextNumber+"...");
      }
    }


    // receive a message from service...
    static void OnMessageReceived (WcfReqIdent rsp)
    {
      Console.Write ("\n\r Thread="+Thread.CurrentThread.ManagedThreadId);
      Console.WriteLine (", received from "+rsp.Sender.Name+": "+rsp.Message.ToString ());
      Console.Write ("\n\r\n\rEnter command > ");

      if (rsp.Message is WcfErrorMessage)
      {
        WcfTrc.Error (rsp.CltRcvId, rsp.Message+"\r\n"+(rsp.Message as WcfErrorMessage).StackTrace);
      }
    }

    #endregion
  }// class
}
