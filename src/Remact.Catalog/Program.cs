
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using Remact.Net;

namespace Remact.Catalog
{
  static class Program
  {
    public static Router Router { get; private set; }

    /// <summary>
    /// The main entry point for the Remact.Catalog application.
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
      RaTrc.UsePlugin (new RaTrc.PluginFile ());
      RaTrc.Start (appInstance);
      RemactApplication.InstallExitHandler ();
      RaTrc.Run (); // open file and write first messages
      RaTrc.Info( "Router", "Start" );
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
        RaTrc.Exception("Router: Fatal error", ex);
        Router.Dispose();
      }
      RaTrc.Info( "Router", "Stop" );
      RaTrc.Stop();
    }// Main
  }
}
