
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Diagnostics;   // EventLog
using System.Windows.Forms; // MessageBox
using System.Threading;     // ThreadExceptionEventArgs
using System.Runtime.InteropServices; // DllImport
using Mono.Unix;            // UnixSignal in "Mono.Posix.dll"
using Mono.Unix.Native;     // Signum


namespace Remact.Net
{
  /// <summary>
  /// Static members to handle application start and shutdown in a compatible way.
  /// Supports Microsoft and Linux operating systems.
  /// </summary>
  public class WcfApplication
  {
    private static bool m_boKernel32Available = false;
    private static bool m_boUnixSignalsAvailable = false;
    private static bool m_boExitRunning = false;
    private static int  m_nUnixSignal;

    //--------------------------------------------------------------------------
    /// <summary>
    /// Same as Windows.Forms.Application.ExecutablePath
    /// </summary>
    public static string ExecutablePath 
    {
      get { return Environment.GetCommandLineArgs()[0]; }
    }

    //--------------------------------------------------------------------------
    /// <summary>
    /// Set when running as Windows service.
    /// </summary>
    public static string ServiceName = "";

    /// <summary>
    /// Set your logging object here (null by default).
    /// It is passed to the logging methods of WcfTrc.ITracePlugin.
    /// You will use it when writing your own adapter class based on WcfTrc.ITracePlugin.
    /// The adapter class is needed to redirect trace output to your own logging/tracing framework.
    /// </summary>
    public static object Logger { get; set; }

    ///---------------------------------------------------
    /// <summary>
    /// Handle the UI exceptions by showing a dialog box, and asking the user whether
    /// or not they wish to abort execution.
    ///
    /// Before 'Application.Run' add the event handler for handling UI thread exceptions: 
    ///   Application.ThreadException += new ThreadExceptionEventHandler(WcfTrc.DefaultTracePlugin.WinForms_ThreadException);
    /// Set the unhandled exception mode to force all Windows Forms errors to go through our handler.
    ///   Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
    /// </summary>
    public static void WinForms_ThreadException (object sender, ThreadExceptionEventArgs e)
    {
      DialogResult result;
      try
      {
        string errorMsg = "An application error occurred. Please contact the adminstrator with the following information:";
        WcfTrc.Exception( errorMsg, e.Exception, Logger );
        WcfTrc.Run ();
        result = MessageBox.Show (errorMsg+ "\n\n" + e.Exception.Message + "\n\nStack Trace:\n" + e.Exception.StackTrace,
                     "Windows Forms Error in "+WcfDefault.Instance.AppIdentification,
                      MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Stop);
      }
      catch
      {
        result = DialogResult.Abort;
      }

      // Exits the program when the user clicks Abort.
      if (result == DialogResult.Abort) Exit (CloseType.ApplicationError, true);
    }


    ///--------------------------------------------------------------------------
    /// <summary>
    /// Handle the non-UI exceptions by showing a dialog box, and asking the user whether
    /// or not they wish to abort execution.
    /// NOTE: This exception cannot be kept from terminating the application - it can only 
    /// log the event, and inform the user about it.
    /// 
    /// Before 'Application.Run' add the handler for handling non-UI thread exceptions to the event:
    ///   AppDomain.CurrentDomain.UnhandledException 
    ///     += new UnhandledExceptionEventHandler(WcfTrc.DefaultTracePlugin.CurrentDomain_UnhandledException);
    /// </summary>
    public static void CurrentDomain_UnhandledException (object sender, UnhandledExceptionEventArgs e)
    {
      Exception    ex = (Exception)e.ExceptionObject;
      if (m_boUnixSignalsAvailable && (ex is System.IO.IOException))
      {
        Thread.Sleep(20); // let the signal handler start, this may be Console.Read that throws an exception after terminal is closed
        if (m_boExitRunning)
        { // "Invalid handle to path [Unknown]"-exception when Unix signals "Hangup" of hosting terminal process.
          WcfTrc.Error("Exception", ex.GetType().ToString(), Logger);
          return;
        }
      }

      string errorMsg = "An application error occurred. Please contact the adminstrator with the following information:";
      try
      {
          WcfTrc.Exception( errorMsg, ex, Logger );
        WcfTrc.Run ();

        // Since we can't prevent the app from terminating, log this to the event log.
        if (!EventLog.SourceExists ("ThreadException"))
        {
          EventLog.CreateEventSource ("ThreadException", "Application");
        }

        // Create an EventLog instance and assign its source.
        EventLog myLog = new EventLog ();
        myLog.Source = "ThreadException";
        myLog.WriteEntry (errorMsg + "\n\n" + ex.Message + "\n\nStack Trace:\n" + ex.StackTrace);
      }
      catch { }

      DialogResult result;
      try
      {
        MessageBoxButtons buttons = MessageBoxButtons.AbortRetryIgnore;
        if (e.IsTerminating) buttons = MessageBoxButtons.OK;
        result = MessageBox.Show (errorMsg + "\n\n" + ex.Message + "\n\nStack Trace:\n" + ex.StackTrace,
                        "Error in "+WcfDefault.Instance.AppIdentification,
                        buttons, MessageBoxIcon.Error);
      }
      catch
      {
        result = DialogResult.Abort;
      }

      // Exits the program when the user clicks Abort.
      if (result == DialogResult.Abort || e.IsTerminating) Exit (CloseType.ApplicationError, true);
    }


    // called on normal application exit or for unhandled exceptions
    private static void Exit (CloseType closeType, bool mustExit)
    {
      if (m_boExitRunning)
      {
          WcfTrc.Warning( "WcfApplication", "detected multiple exit calls in Exit", Logger );
        return;
      }
      m_boExitRunning = true;

      bool goExit = mustExit;
      if (ApplicationExit != null) try
      {
        ApplicationExit (closeType, ref goExit); // call application exit handlers
      }
      catch (Exception ex)
      {
          WcfTrc.Exception( "in application exit handler for " + closeType, ex, Logger );
        goExit = true;
      }

      if (goExit || mustExit) try
      {
        if (IsRunningWithMono)
        {
          ExitToUnix ("exit to Unix after "+closeType); // never returning exit to Unix. Mono.Posix.dll must be available to process this call.
        }
        WcfTrc.Info( "WcfApplication", "exit to Windows after " + closeType, Logger );
        WcfTrc.Stop ();
        Environment.Exit (Environment.ExitCode); // never returning exit to Windows
      }
      catch (Exception ex)
      {
          WcfTrc.Exception( "when exiting application", ex, Logger );
          Environment.Exit (Environment.ExitCode); // never returning exit
      }

      WcfTrc.Info( "WcfApplication", "continue after " + closeType, Logger );
      WcfTrc.Run();
      m_boExitRunning = false;
    }

    // remarks concerning finalizers:
    //ms_Plugin = null; // in order to be finalized by the GC, all (static) references must be removed! --> this is not practicable!
    //GC.Collect ();    // all generations - is it possible to cleanup all unmanaged resources at program shutdown ?
    //GC.WaitForPendingFinalizers ();


//    private static void ExitEventHandler (object sender, EventArgs e)
//    {
//      Console.WriteLine("**ExitEventHandler**");
//      WcfTrc.Mark("WcfApplication","ExitEventHandler");
//      WcfTrc.Run();
//    }

    // CTRL+C pressed on Windows console or Unix terminal
    private static void Console_CancelKeyPress (object sender, ConsoleCancelEventArgs e)
    {
      if (e.SpecialKey == ConsoleSpecialKey.ControlBreak) 
      {
        Exit (CloseType.CtrlBreak, /*mustExit=*/true);  // Windows applications may not prevent Ctrl-Break from termination their process
      }
      else
      {
        Exit (CloseType.CtrlC,     /*mustExit=*/false);
      }
      
      // when we return from Exit, the application should continue
      e.Cancel = true;
    }

    /// <summary>
    /// Install handlers for normal or abnormal application termination on Windows or Unix.
    /// </summary>
    public static void InstallExitHandler ()
    {
     // not working ???
//      Process p = Process.GetCurrentProcess();
//      p.Exited  += new EventHandler (ExitEventHandler);
//      p.EnableRaisingEvents = true;
//      p.Dispose();
//      AppDomain.CurrentDomain.DomainUnload += new EventHandler (ExitEventHandler);
//      AppDomain.CurrentDomain.ProcessExit  += new EventHandler (ExitEventHandler);

      // Windows+Unix: Add the event handler for Console CTRL+C input, this interrupt may end the application
      // slows down exit to windows: Console.CancelKeyPress += new ConsoleCancelEventHandler (Console_CancelKeyPress);

      // Windows only
      try
      {
        m_ConsoleCtrlHandler  = new ConsoleCtrlHandler (ConsoleCtrlEventHandler);
        m_boKernel32Available = SetConsoleCtrlHandler (m_ConsoleCtrlHandler, /*add to list=*/true);
      }
      catch //(Exception ex)
      {
        //WcfTrc.Exception("cannot install ConsoleCtrlHandler",ex);
      }

      // Unix only
      if (!m_boKernel32Available) try
      {
        Console.CancelKeyPress += new ConsoleCancelEventHandler (Console_CancelKeyPress);
        m_boUnixSignalsAvailable = InstallUnixSignalHandler ();
      }
      catch (Exception ex)
      {
          WcfTrc.Exception( "cannot install signal handler", ex, Logger );
      }

      // Windows+Unix: Add the event handler for Console or Service thread exceptions:
      AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler (CurrentDomain_UnhandledException);

      // Add the event handler for System.Windows.Forms exit and thread exceptions:
    //Application.ApplicationExit += new EventHandler (ExitEventHandler);
      Application.ThreadException += new ThreadExceptionEventHandler (WinForms_ThreadException);
      Application.SetUnhandledExceptionMode (UnhandledExceptionMode.CatchException);

      if( m_boKernel32Available ) WcfTrc.Info( "WcfApplication", "windows exit handlers are ready", Logger );
      else if( !m_boUnixSignalsAvailable ) WcfTrc.Info( "WcfApplication", "application exception handlers are ready", Logger );

      Environment.ExitCode = 1; // = Stdlib.EXIT_FAILURE, default on Unix, set on Windows for compatibility with Unix.
    }



    //---------------------- portable definitions -------------------------
    /// <summary>
    /// The eventhandler type raised at application end.
    /// </summary>
    /// <param name="closeType">Application close reason</param>
    /// <param name="goExit">May be set to true in order to terminate an application interrupted by CTRL+C. Is already true, when the application may not be continued.</param>
    public delegate void  ExitHandler (CloseType closeType, ref bool goExit);

    /// <summary>
    /// Event is raised for cleanup on application end.
    /// Environment.ExitCode must be set to 0 when application ends successful.
    /// Environment.ExitCode has been initialized to 1, indicating an application failure.
    /// </summary>
    public  static  event ExitHandler ApplicationExit;

    /// <summary>
    /// Normal application end.
    /// </summary>
    /// <param name="exitCode">the code returned by an application, 0=success, 1=failure</param>
    public static void Exit (int exitCode)
    {
      Environment.ExitCode = exitCode;
      Exit (CloseType.ApplicationEnd, true);
    }

    /// <summary>
    /// An enumerated type sent to the ApplicationExit handler.
    /// </summary>
    public enum CloseType
    {
      /// <summary>
      /// CTRL+C Key pressed on Windows console or Unix terminal. Default is application continuation.
      /// </summary>
      CtrlC=0,    // 0...6 are Kernel32 definitions!

      /// <summary>
      /// Windows only: CTRL+BREAK Key pressed, default is application continuation.
      /// </summary>
      CtrlBreak=1,

      /// <summary>
      /// Hosting Windows console or Unix terminal is closed or process is stopped from task manager or system monitor. Application must exit.
      /// </summary>
      CloseEvent=2,

      /// <summary>
      /// Windows only: User logs off. Application must exit.
      /// </summary>
      LogoffEvent=5,

      /// <summary>
      /// Windows only: System shutdown. Application must exit.
      /// </summary>
      ShutdownEvent=6,

      /// <summary>
      /// Programmed application exit.
      /// </summary>
      ApplicationEnd=100,  // not set by Kernel32

      /// <summary>
      /// Application exit after unhandled exception. User may have choosen to exit the application (Windows or Unix).
      /// </summary>
      ApplicationError=101
    }

    
    //---------------------- windows definitions -------------------------
    // http://geekswithblogs.net/mrnat/archive/2004/09/23/11594.aspx
    [DllImport ("Kernel32")]
    private static extern bool SetConsoleCtrlHandler (ConsoleCtrlHandler handler, bool add);

    // A delegate type to be used as the handler routine for SetConsoleCtrlHandler.
    private delegate bool ConsoleCtrlHandler (CloseType closeType);
    
    // A delegate object used to store the delegate as long as the application is running, the GC must not collect this memory!
    private static        ConsoleCtrlHandler m_ConsoleCtrlHandler;

    // called from Windows, Kernel32
    private static bool ConsoleCtrlEventHandler (CloseType closeType)
    {
      //if (closeType == CloseType.CtrlC || closeType == CloseType.CtrlBreak) return false; // handled in Console_CancelKeyPress
      if (m_boExitRunning)
      {
          WcfTrc.Warning( "WcfApplication", "detected multiple exit calls in ConsoleCtrlEventHandler", Logger );
          return false; // next Win32 handler -> exit
      }
      // when cleanup takes too long, windows displays a message box asking whether to exit or wait.
      bool mustExit = closeType != CloseType.CtrlC;
      Exit (closeType, mustExit);
      return false; // next Win32 handler -> exit
    }

    
    //---------------------- unix definitions -------------------------
    // http://www.mono-project.com/FAQ:_Technical, Mono.Posix.dll
    // http://www.jprl.com/Blog/archive/development/mono/2008/Feb-08.html

    /// <summary>
    /// returns true, when running with mono framework instead of Mocrosoft .NET (on Windows or Unix)
    /// </summary>
    public static bool IsRunningWithMono
    {
      get {return Type.GetType ("Mono.Runtime") != null;}
    }

    private static bool InstallUnixSignalHandler()
    {
      // Mono.Unix.UnixSignal in "Mono.Posix.dll", see Mono.Unix.Native.Signum enumeration
      UnixSignal[] sigArray = new UnixSignal [2];
        sigArray[0] = new UnixSignal (Signum.SIGTERM);  // Termination signal: End process in Ubuntu System Monitor.
        sigArray[1] = new UnixSignal (Signum.SIGHUP);   // Hangup signal     : Gnome terminal is closed
//      sigArray[2] = new UnixSignal (Signum.SIGQUIT);  // Quit from keyboard.
//      sigArray[3] = new UnixSignal (Signum.SIGABRT);  // Abort signal from abort.
      //Signum.SIGINT          : CTRL+C is handled in
      //Signum.SIGSTOP, SIGCONT: Stop and continue process - handler not installable
      //Signum.SIGKILL:          Kill process in Ubuntu System Monitor - handler not installable

      Thread signal_thread = new Thread (
      delegate ()
      {
        while (true)
        {
          WcfTrc.Info( "WcfApplication", "unix signal handlers are ready", Logger );
          m_nUnixSignal = -1;
          int index = UnixSignal.WaitAny (sigArray, -1);

          Mono.Unix.Native.Signum signum = sigArray [index].Signum;
          m_nUnixSignal = (int)signum;

          // Notify the main thread that a signal was received, you can use things like:
          //    Application.Invoke () for Gtk#
          //    Control.Invoke on Windows.Forms
          //    Write to a pipe created with UnixPipes for server apps.
          //    Use an AutoResetEvent
          // For example, this works with Gtk#
          //Application.Invoke (delegate () { ReceivedSignal (signal); });

          CloseType closeType = CloseType.ApplicationEnd;
          switch (signum)
          {
          case Signum.SIGQUIT: closeType = CloseType.CtrlBreak; break;        // ???CtrlBreak
          case Signum.SIGABRT: closeType = CloseType.ApplicationError; break; // ???Abort signal from abort.
          default:             closeType = CloseType.CloseEvent; break;       // SIGTERM, SIGHUP
          }
          //WcfTrc.Mark ("WcfApplication","received unix signal "+signum+ ", starting event handlers for "+closeType);

          Exit (closeType, true); // will never return
        }
      });

      signal_thread.IsBackground = true;
      signal_thread.Start();
      Thread.Sleep(10);
      return signal_thread != null; // signal_thread keeps running
    }

    // separate call to assembly Mono.Posix.dll. Call to functions referencing this assembly will throw an exception if mono is not available.
    private static void ExitToUnix(string exitReason)
    {
      if (m_nUnixSignal >= 0) exitReason += " (received unix signal " + m_nUnixSignal + " = " + (Signum)m_nUnixSignal + ")";
      WcfTrc.Info( "WcfApplication", exitReason, Logger );
      WcfTrc.Stop ();
      Stdlib.exit (Environment.ExitCode); // never returning exit to Unix (or Stdlib.abort or Stdlib.raise..)
    }
    
  }// class WcfApplication
}
