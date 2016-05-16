
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Threading;
using Nito.Async;              // Copyright (c) 2009, Nito Programs, <http://nitoasync.codeplex.com>
using Remact.Net;
using Test1.Contracts;
using Remact.TestUtilities;

namespace Test1.Client
{
    class Test1Client
    {
        static void Main(string[] args)
        {
            // Use commandline arguments to setup objects...
            // 0: Application instance id. Default = 0 --> process id is used.
            // 1: Hostname and TCP port for the service to connect to. Default: "localhost:40001" is used.
            // 2: Transport protocol plugin: 'BMS'(default) or 'JSON'
            RemactDesktopApp.ApplicationStart(args, new RaLog.PluginFile());
            RemactDesktopApp.ApplicationExit += ApplicationExitHandler;
            
            string transportPlugin = "BMS";
            if (args.Length >= 3)
            {
                transportPlugin = args[2];
            }
            PluginSelector.LoadRemactConfigDefault(transportPlugin);
            Remact.Net.Remote.RemactCatalogClient.IsDisabled = true; // Test1.Client does not use Remact.Net.CatalogApp.exe.

            string host = "localhost:40001";
            if (args.Length > 1 && args[1].Length > 0) host = args[1];
            string serviceUri = RemactConfigDefault.Instance.PreferredUriScheme + host + "/Remact/Test1.Service";


            Console.WriteLine("Commandline arguments:   ClientInstance=" + RemactConfigDefault.Instance.ApplicationInstance
                            + "   ServiceHostname:Port='" + host
                           + "'   Transport=" + transportPlugin + "\r\n");

            // Now we create myActor. It runs locally on its own thread in our application. 
            // It has one input- and one output port called "NitoIn" and "Nito".
            // Later on, we will send commands to myActor input from the main thread we are currently running on.
            // myActor will then pass the received command to the output port. From there it is sent to a remote server actor.
            // The remote server actor will return a response. The response comes in through the output port of myActor.
            var myActor = new MyActor("NitoIn", "Nito");
            myActor.TestOutput.LinkOutputToRemoteService(new Uri(serviceUri));
            myActor.TestOutput.TraceSend = true;

            Console.Title = myActor.TestOutput.AppIdentification;
            Console.WriteLine("Starting client '" + myActor.TestOutput.ClientIdent.Name + "' for service '" + myActor.TestOutput.ClientIdent.Uri + "'\r\n");
            Console.WriteLine("Press 'q' to quit.");
            Console.WriteLine("The client is using Nito.Async.ActionThread to queue and synchronize responses on the same thread as the request was sent.\r\n");

            // The thread for myActor needs a System.Threading.SynchronizationContext otherwise, the message processing will not be synchronized to a single thread.
            // The main thread and threadpool threads do not have a SynchronizationContext. Nito.Async.ActionThread has one.
            var actionThread = new ActionThread();
            actionThread.Start();
            actionThread.Do(async() => await myActor.OnStartup());

            Console.Write("\n\rMain thread=" + Thread.CurrentThread.ManagedThreadId + ", is reading commands from console...");
            while (true)
            {
                RaLog.Run();
                string command = Console.ReadLine(); // wait for the user to enter any command string
                if (command == null)
                {
                    Thread.Sleep(8000); // Mono debugging returns null and does not wait for the user
                    command = "xyz";
                }

                if (command.ToLower() == "q") break; // from while

                // The command is entered by the user, we send it to the input port of myActor.
                // Because we have no own output port linked to myActor, we have to send as an anonymous client and
                // can not expect a response here. 
                // The actor writes further traces on the console and into the logfile...
                myActor.TestInput.PostFromAnonymous(new Test1CommandMessage(command));
            }

            RemactDesktopApp.Exit(0);
        }


        // called for all normal and exceptional close types
        static void ApplicationExitHandler(RemactDesktopApp.CloseType closeType, ref bool goExit)
        {
            //if (closeType == RemactApplication.CloseType.CtrlC) goExit = true; // test application cancellation

            RaLog.Info("ApplicationExitHandler", "handling " + closeType + ", terminating=" + goExit);
            if (goExit)
            {
                RemactConfigDefault.Instance.Shutdown();
                if (RemactApplication.IsRunningWithMono) Console.WriteLine("\n\r---application ended---"); // helpful, when started from MonoDevelop
            }
        }
    }
}
