
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;

namespace Remact.Net
{
    /// <summary>
    /// <para>Use these static methods to write debugging trace or logs.</para> 
    /// <para>This way you are able to redirect your logs anywhere.</para>
    /// <para>How to run it:</para>
    /// <para>1. call RaLog.UsePlugin(x) when you don't want to use RaLog.PluginConsole</para>
    /// <para>2. call RaLog.Start(appInstance) to direct output to the correct file and write a log-header</para> 
    /// <para>3. periodically call RaLog.Run() to write the filebuffer to disk</para>
    /// <para>4. calling RaLog.Stop() during shutdown writes the log-footer</para>
    /// </summary>
    public partial class RaLog
    {
        //------------------------------------------------------------
        /// <summary>
        /// Set the logger plugin.
        /// </summary>
        /// <param name="p">Plugin, implementing 'ILogPlugin'</param>
        public static void UsePlugin(ILogPlugin p) { ms_Plugin = p; }

        /// <summary>
        /// Write log header and store 'ApplicationInstance'
        /// </summary>
        /// <param name="appInstance">a number to identify the application instance, see RemactDefaults</param>
        public static void Start(int appInstance)
        {
            ApplicationInstance = appInstance;
            if (ms_Plugin != null) ms_Plugin.Start(appInstance);
        }

        /// <summary>
        /// Call it periodically (e.g. 5 sec.) to flush buffer to log file
        /// </summary>
        public static void Run() { if (ms_Plugin != null) ms_Plugin.Run(); }

        /// <summary>
        /// Write log footer
        /// </summary>
        public static void Stop() { if (ms_Plugin != null) { ms_Plugin.Stop(); } }

        //------------------------------------------------------------
        /// <summary>
        /// Write informative log about program flow.
        /// </summary>
        /// <param name="group">defining the object or object group</param>
        /// <param name="text">log text</param>
        /// <param name="logger">Any optional logger framework object to write log to.</param>
        public static void Info(string group, string text, object logger = null)
        {
            if (ms_Plugin != null) ms_Plugin.Info(group, text, logger);
        }

        /// <summary>
        /// Write log for unexpected, but still accepted, recoverable and tested condition.
        /// </summary>
        /// <param name="group">defining the object or object group</param>
        /// <param name="text">log text</param>
        /// <param name="logger">Any optional logger framework object to write log to.</param>
        public static void Warning(string group, string text, object logger = null)
        {
            WarningCount++;
            if (ms_Plugin != null) ms_Plugin.Warning(group, text, logger);
        }

        /// <summary>
        /// Write log for unexpected condition that cannot be handled properly.
        /// </summary>
        /// <param name="group">defining the object or object group</param>
        /// <param name="text">log text</param>
        /// <param name="logger">Any optional logger framework object to write log to.</param>
        public static void Error(string group, string text, object logger = null)
        {
            ErrorCount++;
            if (ms_Plugin != null) ms_Plugin.Error(group, text, logger);
        }

        /// <summary>
        /// Write log about a handled program exception.
        /// </summary>
        /// <param name="text">log text.</param>
        /// <param name="ex">The caugth exception object.</param>
        /// <param name="logger">Any optional logger framework object to write log to.</param>
        public static void Exception(string text, Exception ex, object logger = null)
        {
            ErrorCount++;
            if (ms_Plugin != null) ms_Plugin.Exception(text, ex, logger);
        }


        //------------------------------------------------------------
        /// <summary>
        /// Log plugins must implement this interface
        /// </summary>
        public interface ILogPlugin
        {
            /// <summary>
            /// Write log header and store 'ApplicationInstance'
            /// </summary>
            /// <param name="appInstance">A number to identify the application instance, see RemactDefaults</param>
            void Start(int appInstance);

            /// <summary>
            /// Call it periodically (e.g. 5 sec.) to flush buffer to log file.
            /// </summary>
            void Run();

            /// <summary>
            /// Write an info log statement.
            /// </summary>
            /// <param name="group">A mark to group log source.</param>
            /// <param name="text">The log line(s).</param>
            /// <param name="logger">Any optional logger framework object to write log to.</param>
            void Info(string group, string text, object logger);

            /// <summary>
            /// Write a warning log statement.
            /// </summary>
            /// <param name="group">A mark to group log source.</param>
            /// <param name="text">The log line(s).</param>
            /// <param name="logger">Any optional logger framework object to write log to.</param>
            void Warning(string group, string text, object logger);

            /// <summary>
            /// Write an error log statement.
            /// </summary>
            /// <param name="group">A mark to group log source.</param>
            /// <param name="text">The log line(s).</param>
            /// <param name="logger">Any optional logger framework object to write log to.</param>
            void Error(string group, string text, object logger);

            /// <summary>
            /// Write an exception log statement.
            /// </summary>
            /// <param name="text">The log line(s).</param>
            /// <param name="ex">The exception.</param>
            /// <param name="logger">Any optional logger framework object to write log to.</param>
            void Exception(string text, Exception ex, object logger);

            /// <summary>
            /// Write log footer.
            /// </summary>
            void Stop();
        }

        //------------------------------------------------------------
        /// <summary>
        /// Common application info: ApplicationInstance
        /// </summary>
        public static int ApplicationInstance { get; private set; }

        private static ILogPlugin ms_Plugin = null;

        //------------------------------------------------------------
        /// <summary>
        /// Count of warning logs (for unit tests).
        /// </summary>
        public static int WarningCount;

        /// <summary>
        /// Count of error logs (for unit tests).
        /// </summary>
        public static int ErrorCount;

        /// <summary>
        /// Reset the log counters to zero.
        /// </summary>
        public static void ResetCount()
        {
            WarningCount = 0;
            ErrorCount = 0;
        }
    }
}

