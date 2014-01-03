using System;
using System.Threading;
using System.ServiceModel;
using SourceForge.AsyncWcfLib; // Copyright (c) 2012, <http://asyncwcflib.sourceforge.net>
using SourceForge.AsyncWcfLib.Basic;
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

      WcfDefault.ApplicationStart (args, new WcfTrc.PluginFile(), /*ExitHandler=*/true);
      WcfApplication.ApplicationExit += ApplicationExitHandler;
      Test1CommandMessage.AddKnownMessageTypes ();

      int tcpPort = 40001;
      if (args.Length > 1) try
      {
        tcpPort = Convert.ToInt32(args[1]);
      }catch{}

      //ActorInput.DisableRouterClient = true; // Test1.Client does not use WcfRouter, but we publish this service anyway
      Console.WriteLine ("Commandline arguments:   ServiceInstance="+WcfDefault.Instance.ApplicationInstance
                        +"   ServiceTcpPort="+tcpPort+"\r\n");

      Test1Service  test = new Test1Service();
      ActorInput service = new ActorInput("Test1.Service", test.WcfRequest);
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
        WcfTrc.Run ();
        Thread.Sleep (10000);
      }
    }// Service.Main
    

    // called for all normal and exceptional close types
    static void ApplicationExitHandler (WcfApplication.CloseType closeType, ref bool goExit)
    {
      //if (closeType == WcfApplication.CloseType.CtrlC) goExit = true; // test application cancellation
      if (goExit)
      {
        ActorPort.DisconnectAll ();
        if (WcfApplication.IsRunningWithMono) Console.WriteLine(Environment.NewLine+"---application ended---"); // helpful, when started from MonoDevelop
      }
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region == Requesting thread ==

    // receive a message from client...
    public void WcfRequest (WcfReqIdent req)
    {
      IWcfMessage response;
      if (req.Message is Test1.Messages.Test1CommandMessage)
      {
        Console.WriteLine (String.Format("Thread={0} --> received command '{1}' from client[{2}] {3}",
                           Thread.CurrentThread.ManagedThreadId,
                          (req.Message as Test1.Messages.Test1CommandMessage).Command,
                           req.ClientId, req.Sender.Uri));

        response = new WcfIdleMessage();
      }
      else
      {
        response = new WcfErrorMessage (WcfErrorMessage.Code.AppRequestNotAcceptedByService, "");
      }
      req.SendResponse (response);
    }// WcfRequest

    #endregion
  }
}
