using System;
using System.Threading;
using System.ServiceModel;
using Nito.Async;              // Copyright (c) 2009, Nito Programs, <http://nitoasync.codeplex.com>
using SourceForge.AsyncWcfLib; // Copyright (c) 2012, <http://asyncwcflib.sourceforge.net>
using Test1.Messages;

namespace Test1.ServiceActive
{
  //----------------------------------------------------------------------------------------------
  #region == Program startup thread ==

  class Program
  {
    static void Main (string[] args)
    {
      // Commandlinearguments:
      // 0: Application instance id.   Default = 0 --> our active service is named "AS0".
      // 1: Servicename to connect to. Default = "AS1", one instance higher is used.
      // 2: Hostname    to connect to. Default = "localhost".
      //
      // This test uses WcfRouter and automatically selects its TCP port.

      WcfDefault.ApplicationStart (args, new WcfTrc.PluginFile(), /*ExitHandler=*/true);
      WcfApplication.ApplicationExit += ApplicationExitHandler;
      Test1CommandMessage.AddKnownMessageTypes ();

      string myService   = "AS" +  WcfDefault.Instance.ApplicationInstance;
      string nextService = "AS" + (WcfDefault.Instance.ApplicationInstance+1);
      string nextHost    = "localhost";
      string prevService = myService;
      if (WcfDefault.Instance.ApplicationInstance > 1) prevService = "AS" + (WcfDefault.Instance.ApplicationInstance-1);

      if (args.Length > 1) nextService = args[1];
      if (args.Length > 2) nextHost    = args[2];

      Console.Title = WcfDefault.Instance.AppIdentification;
      Console.WriteLine ("Commandline arguments:   ActiveServiceInstance="+WcfDefault.Instance.ApplicationInstance
                        +"   NextServiceName='"+nextService+"'"
                        +"   NextHostName='"+nextHost+"'\r\n");

      Console.WriteLine ("This service is using Nito.Async.ActionThread to queue and synchronize responses and requests on the same thread.");
      Console.WriteLine ("This service uses WcfRouter to lookup its partner and automatically selects its own TCP port.\r\n");

      Console.WriteLine ("Starting active service '"+myService+"' and connecting to '"+nextService+"' on '"+nextHost+"'...");
      Console.WriteLine ("The active service is listening on input from console or from service '"+prevService+"'");
      Console.WriteLine ("Press 'q' to quit.");

      Test1ServiceActive test = new Test1ServiceActive (myService);

      // configure system topology and start the service
      test.Input1.LinkInputToNetwork (myService+"/i1");
      test.Input2.LinkInputToNetwork (myService+"/i2");
      test.Output1.LinkOutputToRemoteService (nextHost, nextService+"/i1");
      test.Output2.LinkOutputToRemoteService (nextHost, nextService+"/i2");
      test.Start();

      string command = string.Empty;
      while (command != null && command.ToLower() != "q")
      {
        Console.Write (String.Format ("\r\n\r\nThread={0}: Enter command > ",
                                       Thread.CurrentThread.ManagedThreadId,
                                       0));//test.PartnerCount));
        WcfTrc.Run();
        command = Console.ReadLine();
        if (command == null)
        { // ctrl-c in Windows, process without terminal in Linux
          command = "auto";
          Thread.Sleep(10000);
        }

        WcfMessage msg = new Test1.Messages.Test1CommandMessage("(a)"+command);
        test.Input1.PostInput (msg);

        msg = new Test1.Messages.Test1CommandMessage("(b)"+command);
        test.Input2.PostInput (msg);
        Thread.Sleep(1000);
      }
      
      WcfApplication.Exit (0);
    }// Main
    

    // called for all normal and exceptional close types
    static void ApplicationExitHandler (WcfApplication.CloseType closeType, ref bool goExit)
    {
      if (goExit)
      {
        ActorPort.DisconnectAll ();
        if (WcfApplication.IsRunningWithMono) Console.WriteLine("\n\r---application ended---");
      }
    }

  }//class Program

  #endregion
  //----------------------------------------------------------------------------------------------
  #region == Active service ==

  /// <summary>
  /// Active services are waiting for requests (as normal services)
  /// but they also may send requests to other services (as normal clients).
  ///
  /// One thread is receiving requests, sending responses and sending other requests (asynchronously).
  /// This means the data accessed exclusively from the active service must not be protected
  /// against multithreading issues.
  ///
  /// AsyncWcfLib allows active services to be implemented independently in a library assembly.
  /// Later, when using the service in an application, the system topology is defined by linking
  /// service output ports to other service input ports.
  /// </summary>
  public class Test1ServiceActive
  {
    private ActionThread        m_Thread;
    private ActorOutput         m_Output1;
    private ActorOutput         m_Output2;
    private ActorInput<ICtx>    m_Input1;
    private ActorInput<ICtx>    m_Input2;

    // Constructor
    public Test1ServiceActive(string serviceName)
    {
      m_Output1   = new ActorOutput     (serviceName+"/o1", DefaultResponseHandler);
      m_Output2   = new ActorOutput     (serviceName+"/o2", DefaultResponseHandler);
      m_Input1    = new ActorInput<ICtx>(serviceName+"/i1", OnInput1);
      m_Input2    = new ActorInput<ICtx>(serviceName+"/i2", OnInput2);
      m_Input1.OnInputConnected    += OnConnect;
      m_Input1.OnInputDisconnected += OnDisconnect;
      m_Input2.OnInputConnected    += OnConnect;
      m_Input2.OnInputDisconnected += OnDisconnect;
    }

    // Public input/output pins
    public IActorInput  Input1  {get{return m_Input1;}} // Note: ICtx is not public
    public IActorInput  Input2  {get{return m_Input2;}}
    public IActorOutput Output1 {get{return m_Output1;}}
    public IActorOutput Output2 {get{return m_Output2;}}


    // Open and start the service after linking input / output connections
    public void Start()
    {
      if (m_Thread != null) return;
      m_Thread = new ActionThread();
      m_Thread.Start();
      m_Thread.Do(()=> // Lambda expression called on the active services own m_Thread.
      { // pickup m_Threads synchronization context when opening the proxy and service.
        m_Output1.TryConnect();
        m_Output2.TryConnect();
        m_Input1.Open();
        m_Input2.Open();
      });
    }

    
    // Input context for connected client.
    public class ICtx
    {
      public int    numnum;
      public string blabla;
    }


    // Connecting Input 1 or 2
    public void OnConnect (WcfReqIdent req, ICtx senderContext)
    {
      WcfPartnerMessage con = req.Message as WcfPartnerMessage;
      Console.Write (String.Format ("\r\nThread={0}  : {1} gets {2} from client[{3}] {4}",
                     Thread.CurrentThread.ManagedThreadId,
                     req.Input.Name,
                     con.Usage,
                     req.ClientId, req.Sender.Uri));
    }


    // Disconnecting Input 1 or 2
    public void OnDisconnect (WcfReqIdent req, ICtx senderContext)
    {
      WcfPartnerMessage con = req.Message as WcfPartnerMessage;
      Console.Write (String.Format ("\r\nThread={0}  : {1} gets {2} from client[{3}] {4}",
                     Thread.CurrentThread.ManagedThreadId,
                     req.Input.Name,
                     con.Usage,
                     req.ClientId, req.Sender.Uri));
    }


    // Request from Input 1
    public void OnInput1 (WcfReqIdent req, ICtx senderContext)
    {
      OnAnyInput (req, senderContext, "Input1", m_Output1);
    }


    // Request from Input 2
    public void OnInput2 (WcfReqIdent req, ICtx senderContext)
    {
      OnAnyInput (req, senderContext, "Input2", m_Output2);
    }

    
    // Request from Input 1 or 2
    public void OnAnyInput (WcfReqIdent req, ICtx senderContext, string inpName, ActorOutput output)
    {
      string request = string.Empty;
      if (req.On<Test1CommandMessage> (cmd => {request = cmd.Command; // trace incoming command
                                               cmd.Command += "+";})  // add a'+' to outgoing command
         != null)
      {
        request = req.Message.ToString ();
      }
      Console.Write (String.Format ("\r\nThread={0}  : {1} request = '{2}' from client[{3}] {4}",
                     Thread.CurrentThread.ManagedThreadId,
                     inpName,
                     request,
                     req.ClientId, req.Sender.Uri));

      WcfState con = output.OutputState;
      if (con != WcfState.Ok)
      {
        if (con == WcfState.Disconnected || con == WcfState.Faulted)
        {
          Console.Write ("\r\n            Try connect '"+output.Name+"' to '"+output.OutputSidePartner.Uri+"'");
          output.TryConnect ();
          senderContext.numnum = req.ClientId;
          senderContext.blabla = inpName;
        }
      }
      else
      {
        output.SendOut (req.Message,
        rsp =>
        {
          Console.Write (String.Format ("\r\n  Thread={0}: {1} got response '{2}' from '{3}'",
                         Thread.CurrentThread.ManagedThreadId,
                         inpName,
                         rsp.Message,
                         rsp.Sender.Uri));
          return null; // response handled
        });
      }
      // response is an automatically generated WcfIdleMessage
    }


    // Responses to output requests.
    public void DefaultResponseHandler (WcfReqIdent rsp)
    {
      string response = string.Empty;
      if (rsp.On<WcfIdleMessage>  (idle => response = "Ok")
             .On<WcfErrorMessage>  (err => response = err.Error.ToString()+": "+err.Message+"\r\n      ")
             .On<WcfPartnerMessage>(msg => response = msg.Usage.ToString())
         != null)
      {
          response = rsp.Message.ToString();
      }
      
      Console.Write (String.Format("\r\n  Thread={0}: Response {1} from '{2}'",
                     Thread.CurrentThread.ManagedThreadId,
                     response,
                     rsp.Sender.Uri));
    }

  }//class Test1ServiceActive
  #endregion
}
