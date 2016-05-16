
// Copyright (c) https://github.com/steforster/Remact.Net

using Remact.Net.CatalogApp.Properties;
using System;
using System.Reflection;
using System.Windows.Forms;

namespace Remact.Net.CatalogApp
{
    static class Program
    {
        public static Catalog Catalog { get; private set; }
        public static string TransportPluginDll { get; private set; }

        [STAThread]
        static void Main (string[] args)
        {
            // Configuration of the single application instance is in App.config
            RemactApplication.LogFolder = RemactDesktopApp.GetLogFolder();
            RaLog.UsePlugin (new RaLog.PluginFile ());
            int appInstance = 1;
            try
            {
                if (args.Length > 0) appInstance = Convert.ToInt32 (args[0]); // by default the first commandline argument
            }
            catch { }
            RaLog.Start (appInstance);
            RemactDesktopApp.InstallExitHandler ();
            RaLog.Info( "Catalog", "Start" );

            foreach (string path in Settings.Default.TransportPlugins)
            {
                var disposable = RemactConfigDefault.LoadPluginAssembly(path);
                if (disposable == null)
                {
                    RaLog.Error("Remact.Net.CatalogApp.exe.config", "cannot dynamically load TransportPlugin: " + path);
                }
                else
                {
                    var loadedAssembly = Assembly.GetAssembly(disposable.GetType());
                    RaLog.Info("Remact.Net.CatalogApp.exe.config", "loaded TransportPlugin and its dependencies: " + loadedAssembly.CodeBase);
                    TransportPluginDll = path;
                }
            }

            try
            {
                Catalog = new Catalog();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault (false);
                RaLog.Run (); // open file and write first messages
                Application.Run (new FrmCatalog());
                Catalog.Dispose();
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                RaLog.Exception("Catalog: Fatal error", ex);
                Catalog.Dispose();
            }

            RemactConfigDefault.Instance.Shutdown();
            RaLog.Info("Catalog", "Stop");
            RaLog.Stop();
        }
    }
}
