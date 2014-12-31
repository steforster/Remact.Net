
// Copyright (c) https://github.com/steforster/Remact.Net

using System;

namespace Remact.Net
{
    /// <summary>
    /// Static members defining the application environment.
    /// Supports Microsoft, Linux and Android operating systems.
    /// For desktop applications, a separate helper assembly is provided: Remact.DesktopApp.
    /// </summary>
    public class RemactApplication
    {
        /// <summary>
        /// Same as Windows.Forms.Application.ExecutablePath
        /// </summary>
        public static string ExecutablePath
        {
            get { return Environment.GetCommandLineArgs()[0]; }
        }

        /// <summary>
        /// Set when running as Windows service.
        /// </summary>
        public static string ServiceName = "";

        /// <summary>
        /// Get or set the folder name where log files are stored (null by default).
        /// Initialized, when using Remact.DesktopApp.dll.
        /// </summary>
        public static string LogFolder { get; set; }

        /// <summary>
        /// Set your logging object here (null by default).
        /// It is passed to the logging methods of RaLog.ILogPlugin.
        /// You will use it when writing your own adapter class based on RaLog.ILogPlugin.
        /// The adapter class is needed to redirect log output to your own logging/tracing framework.
        /// </summary>
        public static object Logger { get; set; }

        /// <summary>
        /// returns true, when running with mono framework instead of Mocrosoft .NET (on Windows or Unix)
        /// </summary>
        public static bool IsRunningWithMono
        {
            get { return Type.GetType("Mono.Runtime") != null; }
        }
    }
}
