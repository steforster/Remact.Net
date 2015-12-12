
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Windows.Forms;

namespace Remact.Net.CatalogApp
{
    static class Program
    {
        public static Catalog Catalog { get; private set; }

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
            RaLog.UsePlugin (new RaLog.PluginFile ());
            RaLog.Start (appInstance);
            RemactDesktopApp.InstallExitHandler ();
            RaLog.Run (); // open file and write first messages
            RaLog.Info( "Catalog", "Start" );
            RemactConfigDefault.Instance = new Remact.Net.Json.Msgpack.Alchemy.JsonProtocolConfig();
            try
            {
                Catalog = new Catalog();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault (false);
                Application.Run (new FrmCatalog());
                Catalog.Dispose();
                Environment.ExitCode = 0;
            }
            catch (Exception ex) // any Exception
            {
                RaLog.Exception("Catalog: Fatal error", ex);
                Catalog.Dispose();
            }

            RemactConfigDefault.Instance.Shutdown();
            RaLog.Info("Catalog", "Stop");
            RaLog.Stop();
        }// Main
    }
}