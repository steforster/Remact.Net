using System;
using System.Threading;
using System.Threading.Tasks;
using Nito.Async;              // Copyright (c) 2009, Nito Programs, <http://nitoasync.codeplex.com>
using Remact.Net;              // Copyright (c) 2014, <http://github.com/steforster/Remact.Net>
using Test1.Messages;


namespace Test1.Client
{
    //----------------------------------------------------------------------------------------------
    #region == Program startup thread ==

    class Test1Client
    {
        public static RemactPortService TestInput;
        public static RemactPortProxy TestOutput;

        static void Main(string[] args)
        {
            // Commandlinearguments:
            // 0: Application instance id. Default = 0 --> process id is used.
            // 1: Hostname and TCP port for the service to connect to. Default: "localhost:40001" is used.

            RemactConfigDefault.ApplicationStart(args, new RaLog.PluginFile());
            RemactApplication.ApplicationExit += ApplicationExitHandler;

            string host = "localhost:40001";
            if (args.Length > 1 && args[1].Length > 0) host = args[1];
            string serviceUri = "ws://" + host + "/Remact/Test1.Service";

            // define the input and output ports of our test actor
            TestInput = new RemactPortService("NitoIn", OnMessageReceived);
            TestOutput = new RemactPortProxy("Nito", OnMessageReceived);
            TestOutput.LinkOutputToRemoteService(new Uri(serviceUri));
            TestOutput.TraceSend = true;
            ActionThread actionThread = new ActionThread();

            Console.Title = TestOutput.AppIdentification;
            Console.WriteLine("Commandline arguments:   ClientInstance=" + RemactConfigDefault.Instance.ApplicationInstance
                           + "   ServiceHostname:Port='" + host + "'\r\n");
            Console.WriteLine("Starting client '" + TestOutput.ClientIdent.Name + "' for service '" + TestOutput.ClientIdent.Uri + "'\r\n");
            Console.WriteLine("Press 'q' to quit.");
            Console.WriteLine("The client is using Nito.Async.ActionThread to queue and synchronize responses on the same thread as the request was sent.\r\n");

            actionThread.Start();
            actionThread.Do(OnStartup);

            Console.Write("\n\r Thread=" + Thread.CurrentThread.ManagedThreadId + ", is reading commands from console...");
            while (true)
            {
                RaLog.Run();
                string command = Console.ReadLine();
                if (command == null)
                {
                    Thread.Sleep(8000); // Mono debugging returns null and does not wait
                    command = "xyz";
                }

                if (command.ToLower() == "q") break; // from while
                TestInput.PostFromAnonymous(new Test1CommandMessage(command));
            }

            RemactApplication.Exit(0);
        }// Main


        // called for all normal and exceptional close types
        static void ApplicationExitHandler(RemactApplication.CloseType closeType, ref bool goExit)
        {
            //if (closeType == RemactApplication.CloseType.CtrlC) goExit = true; // test application cancellation

            RaLog.Info("ApplicationExitHandler", "handling " + closeType + ", terminating=" + goExit);
            if (goExit)
            {
                RemactConfigDefault.Instance.Shutdown();
                if (RemactApplication.IsRunningWithMono) Console.WriteLine("\n\r---application ended---"); // helpful, when started from MonoDevelop
            }
        }

        #endregion
        //----------------------------------------------------------------------------------------------
        #region == Nito.Async.ActionThread ==

        // picks up this thread synchronization context and builds connection to service
        static void OnStartup()
        {
            Console.Write("\n\r Thread=" + Thread.CurrentThread.ManagedThreadId + ", is connecting...");
            TestInput.Open();
            var task = TestOutput.ConnectAsync();
            task.ContinueWith(t =>
                {
                    Console.WriteLine("\n\r Thread=" + Thread.CurrentThread.ManagedThreadId + ", connected.");
                    Console.Write("\n\r\n\rSend command > ");
                }, TaskContinuationOptions.ExecuteSynchronously);
        }

        // receive a message from main thread or from remote service
        static void OnMessageReceived(RemactMessage msg)
        {
            Console.Write("\n\r Thread=" + Thread.CurrentThread.ManagedThreadId + ", received: " + msg.ToString());

            Test1CommandMessage testMessage;
            ErrorMessage errorMessage;
            if (msg.IsNotification && msg.TryConvertPayload(out testMessage))
            {
                PortState s = TestOutput.OutputState;
                if (s == PortState.Disconnected || s == PortState.Faulted)
                {
                    OnStartup();
                }
                else if (s == PortState.Connecting)
                {
                    Console.Write(" - cannot send, still connecting...");
                }
                else
                {
                    int sendContextNumber = TestOutput.LastRequestIdSent + 1000;
                    TestOutput.SendReceiveAsync("OnMessageReceived", testMessage,

                            delegate (ReadyMessage response, RemactMessage rsp)
                            {
                                Console.Write("\n\r Thread=" + Thread.CurrentThread.ManagedThreadId);
                                Console.WriteLine(", received ready message in sending context #" + sendContextNumber);
                                Console.Write("\n\r\n\rSend command > ");
                            });

                    Console.Write(", sending context #" + sendContextNumber + "...");
                    return;
                }
            }
            else if (msg.TryConvertPayload(out errorMessage))
            {
                RaLog.Error(msg.CltRcvId, errorMessage.ToString() + "\r\n" + errorMessage.StackTrace);
            }
            else
            {
                Console.Write(", from " + msg.Source.Name);
            }

            Console.Write("\n\r\n\rSend command > ");
        }
    }
    #endregion
}
