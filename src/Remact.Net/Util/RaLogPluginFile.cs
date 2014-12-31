
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.IO;            // Files
using System.Diagnostics;   // Trace Listener, Trace...
using System.Reflection;    // Assembly, Attributes
using System.Text;     // Sleep


namespace Remact.Net
{
    public partial class RaLog
    {
        /// <summary>
        /// <para>The 'file' implementation of a ILogPlugin</para>
        /// <para>Writes 2 log files (.log.txt and .log.old.txt).</para>
        /// <para>Switches to the next log file, when 1MB has been reached.</para>
        /// <para>Finds default log folder.</para>
        /// </summary>
        public class PluginFile : ILogPlugin, IDisposable
        {
            /// <summary>
            /// Write log header and store 'ApplicationInstance'
            /// You can easely write a similar adapter class to redirect log output to your own logging framework. See RaLog.PluginConsole.
            /// </summary>
            /// <param name="appInstance">a number to identify the application instance, see RemactDefaults</param>
            public void Start(int appInstance)
            {
                ApplicationInstance = appInstance;
                SetLogOutput(null);
            }

            /// <summary>
            /// Call it periodically (e.g. 5 sec.) to flush buffer to log file
            /// </summary>
            public void Run() { Refresh(); }

            /// <summary>
            /// Write log footer
            /// </summary>
            public void Stop() { Dispose(); }

            /// <inheritdoc/>
            public void Info(string group, string text, object logger)
            {
                Log("..", group, text);
            }

            /// <inheritdoc/>
            public void Warning(string group, string text, object logger)
            {
                Log("!!", group, text);
            }

            /// <inheritdoc/>
            public void Error(string group, string text, object logger)
            {
                Log("##", group, text);
            }

            /// <inheritdoc/>
            public void Exception(string text, Exception ex, object logger)
            {
                var sb = new StringBuilder(500);
                sb.AppendLine(text);
                sb.Append("  ");
                PluginConsole.AppendFullMessage(sb, ex);
                if (ex.StackTrace != null)
                {
                    sb.AppendLine();
                    sb.Append(ex.StackTrace);
                }
                Log("##", "EXCEPT", sb.ToString());
            }

            /// <summary>
            /// Write a log statement.
            /// </summary>
            /// <param name="severity">A mark to distiguish log severity.</param>
            /// <param name="group">A mark to group log source.</param>
            /// <param name="text">The log line(s).</param>
            private void Log(string severity, string group, string text)
            {
                string s = String.Format(m_LogFormat, severity, DateTime.Now, group, text);
                if (m_LogFile != null) System.Diagnostics.Trace.WriteLine(s);
                else LoggingException(s);
                m_boLogReady = true;
                ++m_nLogLines;
            }


            //------------------------------------------------------------
            // Class data
            /// <summary>
            /// Problem report of the tracing itself, for debugging purpose.
            /// </summary>
            public static string sLastLoggingProblem = "";

            private static TextWriterTraceListener m_LogFile = null;
            private static string m_LogFileName = "";
            private static StreamWriter m_DirectStreamWriter = null;

            private static bool m_boLogReady;
            private const int c_nMaxFileLength = 1000000; // 1MB
            private static int m_nLogLines = 0;
            private static string m_LogFormat;
            private static bool m_boDisplayDate = (DisplayDate = false);

            //--------------------------------------------------------------------------
            /// <summary>
            /// Graceful application shutdown, called by stop.
            /// </summary>
            public void Dispose() { Dispose(true); }

            private void Dispose(bool calledByUser)
            {
                try
                {
                    if (m_LogFile != null)
                    {
                        if (calledByUser)
                        {
                            System.Diagnostics.Trace.WriteLine(String.Format("| {0:D} {0:T} {1} stopped, Exit code = {2}",
                                                           DateTime.Now, RemactConfigDefault.Instance.AppIdentification, Environment.ExitCode));
                        }
                        else
                        {
                            System.Diagnostics.Trace.WriteLine(String.Format("| {0:D} {0:T} {1} interrupted, Exit code = {2}",
                                                           DateTime.Now, RemactConfigDefault.Instance.AppIdentification, Environment.ExitCode));
                        }
                        System.Diagnostics.Trace.WriteLine("+-------------------------------------------------------------------------------------------------");
                        System.Diagnostics.Trace.Flush();
                    }
                }
                catch (Exception ex)
                {
                    LoggingException("##,Error while disposing\r\n   " + ex.Message);
                }
            }

            //--------------------------------------------------------------------------
            /// <summary>
            /// Set or get the current log file path and name.
            /// </summary>
            public static string FileName
            {
                get { return m_LogFileName; }
                set { SetLogOutput(value); }
            }

            ///--------------------------------------------------------------------------
            /// <summary>
            /// choose wether to log date on each line.
            /// </summary>
            public static bool DisplayDate
            {
                get { return m_boDisplayDate; }
                set
                {
                    m_boDisplayDate = value;
                    // {0} = mark e.g. "##," 
                    // {1} = DateTime.Now
                    // {2} = group, Format string: {Num,FieldLen:Format}
                    // {3} = info
                    if (m_boDisplayDate) m_LogFormat = "{0}{1:d} {1:HH:mm:ss.fff}, {2,-6}, {3}";
                    else m_LogFormat = "{0}{1:HH:mm:ss.fff}, {2,-6}, {3}";
                }
            }

            //--------------------------------------------------------------------------
            /// <summary>
            /// Set the current log file path and name.
            /// </summary>
            public static void SetLogOutput(string i_FileName)
            {
                bool boStartup = true;

                try
                {
                    if (i_FileName == null || i_FileName.Length == 0)
                    {
                        i_FileName = RemactApplication.LogFolder;

                        // files older than 30 days will be deleted
                        DateTime tooOld = DateTime.Now.AddDays(-30);
                        string[] files = Directory.GetFiles(i_FileName, "*.log.*", SearchOption.TopDirectoryOnly);
                        foreach (string filename in files)
                        {
                            try
                            {
                                FileInfo fi = new FileInfo(filename);
                                if (fi.LastWriteTime < tooOld)
                                {
                                    fi.Delete();
                                }
                            }
                            catch (Exception) { }
                        }
                        i_FileName += "/" + RemactConfigDefault.Instance.AppIdentification + ".log.txt";
                    }
                }
                catch (Exception ex)
                {
                    LoggingException("##,Error while creating directory " + i_FileName + "\r\n   " + ex.Message);
                }


                if (m_LogFile != null)
                {
                    boStartup = false;
                    System.Diagnostics.Trace.WriteLine(String.Format("| {0:D} {0:T} Next log file://{1}", DateTime.Now, i_FileName));
                    System.Diagnostics.Trace.Flush();
                    System.Diagnostics.Trace.Listeners.Remove(m_LogFile);
                    m_LogFileName = "";
                    m_LogFile.Close();
                    m_LogFile.Dispose();
                }
                if (m_DirectStreamWriter != null) m_DirectStreamWriter.Dispose();

                // Rename old file
                try
                {
                    FileInfo fi = new FileInfo(i_FileName);
                    if (fi.Exists)
                    {
                        if (fi.Length > c_nMaxFileLength)
                        {
                            string NewFileName = Path.ChangeExtension(i_FileName, "old.txt");
                            File.Delete(NewFileName);
                            fi.MoveTo(NewFileName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggingException("##,Error while renaming log file://" + i_FileName + "\r\n   " + ex.Message);
                }

                // Create or Append file
                try
                {
                    m_LogFileName = i_FileName;
                    m_DirectStreamWriter = File.AppendText(i_FileName);

                    //Create a new text writer using the output stream, and add it to the trace listeners.
                    m_LogFile = new TextWriterTraceListener(m_DirectStreamWriter);
                    System.Diagnostics.Trace.Listeners.Add(m_LogFile);
                    System.Diagnostics.Trace.WriteLine("\n+-------------------------------------------------------------------------------------------------");
                    if (boStartup)
                    {
                        System.Diagnostics.Trace.WriteLine(String.Format("| LOG  {0:D} {0:T}: Starting application", DateTime.Now));
                    }
                    else
                    {
                        System.Diagnostics.Trace.WriteLine(String.Format("| LOG  {0:D} {0:T}: Continue logging ", DateTime.Now));
                    }
                }
                catch (Exception ex)
                {
                    // Wenn die Trace.Listener Property null zurückgibt ist ev. kein EXE.config file vorhanden
                    LoggingException("##,Error while opening trace listener file://" + i_FileName + "\r\n   " + ex.Message);
                    if (m_LogFile != null) m_LogFile.Dispose(); // schliesst auch den m_DirectStreamWriter
                    m_LogFile = null;    // Trace Listener funktioniert nicht
                    try
                    {
                        m_DirectStreamWriter = File.AppendText(i_FileName); // Notbetrieb direkt ins File
                    }
                    catch (Exception ex2)
                    {
                        LoggingException("##,Error while opening log file://" + i_FileName + "\r\n   " + ex2.Message);
                    }
                }

                String s = AppInfo();
                s += "\r\n|   Log file  \t: " + m_LogFileName;
                s += "\r\n+-------------------------------------------------------------------------------------------------";

                System.Diagnostics.Trace.WriteLine(s);
                m_boLogReady = true;
                m_nLogLines = 0;

            }// SetLogOutput (i_FileName)


            ///--------------------------------------------------------------------------
            /// <summary>
            /// must be called periodically from Window-Main-Thread
            /// </summary>
            public static void Refresh()
            {
                try
                {
                    if (m_boLogReady)
                    {
                        System.Diagnostics.Trace.Flush();
                        m_boLogReady = false;
                        if (m_nLogLines > 200)
                        {
                            m_nLogLines = 0;
                            FileInfo fi = new FileInfo(m_LogFileName);
                            if (fi.Length > c_nMaxFileLength)
                            {
                                SetLogOutput(m_LogFileName);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Can't do anything against some other process locking the file !
                    sLastLoggingProblem = ex.Message;
                }
            }// Refresh


            //--------------------------------------------------------------------------
            /// <summary>
            /// Get log file header, may be used for Help - About box.
            /// </summary>
            /// <returns>log file header</returns>
            public static String AppInfo()
            {
                string s = "| ";
                try
                {
                    Assembly myApp = Assembly.GetEntryAssembly(); // LCsPak
                    Assembly thisA = Assembly.GetCallingAssembly(); // GUI MP
                    if (myApp == null) { myApp = thisA; }
                    if (myApp == thisA) { thisA = null; }

                    s += "\r\n|   Application\t: ";
                    s += (Attribute.GetCustomAttribute(myApp, typeof(AssemblyTitleAttribute)) as AssemblyTitleAttribute).Title;
                    s += ", Instance " + ApplicationInstance.ToString();
                    s += "\r\n|              \t  ";
                    s += (Attribute.GetCustomAttribute(myApp, typeof(AssemblyCopyrightAttribute)) as AssemblyCopyrightAttribute).Copyright;
                    s += "\r\n|              \t  ";
                    s += (Attribute.GetCustomAttribute(myApp, typeof(AssemblyDescriptionAttribute)) as AssemblyDescriptionAttribute).Description;
                    s += "\r\n|";
                    s += "\r\n|   Assembly   \t: " + myApp.GetName().Name;
                    s += ", Version " + AssemblyVersion(myApp);

                    if (thisA != null)
                    {
                        s += "\r\n|   Assembly   \t: " + thisA.GetName().Name;
                        s += ", Version " + AssemblyVersion(thisA);
                    }

                    s += "\r\n|";
                    s += "\r\n|   Machine    \t: " + Environment.MachineName + " (Process Id " + RemactConfigDefault.Instance.ProcessId + ")";
                    s += "\r\n|   OS         \t: " + Environment.OSVersion;
                    s += "\r\n|   Framework  \t: ";
                    if (RemactApplication.IsRunningWithMono) s += "Mono"; else s += ".NET";
                    s += " Version " + Environment.Version;
                    s += "\r\n|   User       \t: " + Environment.UserName;
                    if (Environment.UserInteractive) s += " (interactive)";
                    else s += " (not interactive)";
                    if (RemactApplication.ServiceName.Length > 0) s += ", running as service " + RemactApplication.ServiceName;

                    string[] arg = Environment.GetCommandLineArgs();
                    s += "\r\n|";
                    s += "\r\n|   Executable \t: " + arg[0];   // = Application.ExecutablePath
                    s += "\r\n|   Current dir\t: " + Environment.CurrentDirectory;
                    for (int i = 1; i < arg.GetLength(0); i++)
                    {
                        if (i == 1) s += "\r\n|   Commandline\t: ";
                        s += arg[i] + " ";
                    }
                }
                catch (Exception ex)
                {
                    LoggingException("##,Error while generating application info:" + s + ex.Message);
                }
                return s;
            }// static AppInfo


            private static string AssemblyVersion(Assembly A)
            {
                string s;
                Version V = A.GetName().Version;
                if (V.Revision == 0) s = V.ToString(3);
                else s = V.ToString(4);
                s += ", " + (Attribute.GetCustomAttribute(A, typeof(AssemblyConfigurationAttribute)) as AssemblyConfigurationAttribute).Configuration;
                try
                {
                    s += " " + File.GetLastWriteTime(A.Location).ToString("g"); // general = short date " " short time format
                }
                catch (Exception ex)
                {
                    string s2 = ex.Message;
                    if (s2 != null) s2 = null; // remove a warning
                }
                return s;
            }


            private static void LoggingException(string Message)
            {
                if (m_DirectStreamWriter != null)
                    try
                    {
                        m_DirectStreamWriter.WriteLine(Message);
                    }
                    catch (Exception)
                    {
                    }
            }
        }
    }
}
