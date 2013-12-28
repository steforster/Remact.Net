
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.IO;

namespace SourceForge.AsyncWcfLib
{
  static class Program
  {
    public static Router Router { get; private set; }

    /// <summary>
    /// The main entry point for the WcfRouter application.
    /// </summary>
    [STAThread]
    static void Main (string[] args)
    {
      int appInstance = 1;
      try
      {
        if (args.Length > 0) appInstance = Convert.ToInt32 (args[0]); // by default the first commandline argument
      }
      catch { }
      WcfTrc.UsePlugin (new WcfTrc.PluginFile ());
      WcfTrc.Start (appInstance);
      WcfApplication.InstallExitHandler ();
      WcfTrc.Run (); // open file and write first messages
      WcfTrc.Info( "Router", "Start" );
      try
      {
        Router = new Router();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault (false);
        Application.Run (new FrmRouter());
        Router.Dispose();
      }
      catch (Exception ex) // any Exception
      {
        WcfTrc.Exception("Router: Fatal error", ex);
        Router.Dispose();
      }
      WcfTrc.Info( "Router", "Stop" );
      WcfTrc.Stop();
    }// Main
  }
}
