
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.IO;            // Files
using System.Diagnostics;   // Trace Listener, Trace...
using System.Reflection;    // Assembly, Attributes
using System.Windows.Forms; // MessageBox
using System.Threading;     // Sleep


namespace Remact.Net
{
public partial class WcfTrc
{
	/// <summary>
  /// <para>The 'file' implementation of a ITracePlugin</para>
  /// <para>Writes 2 tracefiles.</para>
  /// <para>Switches to the next tracefile, when 1MB has been reached.</para>
  /// <para>Finds default trace folder.</para>
  /// </summary>
	public class PluginFile: ITracePlugin, IDisposable
	{
    //------------------------------------------------------------
    /// <summary>
    /// Write trace header and store 'ApplicationInstance'
    /// You can easyly write a similar adapter class to redirect trace output to your own logging framework. See WcfTrcPluginDefault.
    /// </summary>
    /// <param name="appInstance">a number to identify the application instance, see WcfDefault</param>
    public void Start (int appInstance)
    {
      ApplicationInstance = appInstance;
      SetTraceOutput(null);
    }

    /// <summary>
    /// Call it periodically (e.g. 5 sec.) to flush buffer to tracefile
    /// </summary>
    public void Run () { Refresh (); }

    /// <summary>
    /// Write trace footer
    /// </summary>
    public void Stop () { Dispose (); }

    /// <inheritdoc/>
    public void Info( string group, string text, object logger )
    {
        Trace( "..", group, text );
    }

    /// <inheritdoc/>
    public void Warning( string group, string text, object logger )
    {
        Trace( "!!", group, text );
    }

    /// <inheritdoc/>
    public void Error( string group, string text, object logger )
    {
        Trace( "##", group, text );
    }

    /// <inheritdoc/>
    public void Exception( string text, Exception ex, object logger )
    {
        Trace( "##", "EXCEPT", text
                  + "\r\n   " + ex.Message
                  + "\r\n" + ex.StackTrace );
    }

    /// <summary>
    /// Write a trace statement.
    /// </summary>
    /// <param name="severity">A mark to distiguish trace severity.</param>
    /// <param name="group">A mark to group trace source.</param>
    /// <param name="text">The trace line(s).</param>
    private void Trace (string severity, string group, string text)
    {
      string s = String.Format (m_TraceFormat, severity, DateTime.Now, group, text);
      if (m_TraceFile != null) System.Diagnostics.Trace.Write (s);
                          else TracingException (s); // Notfall Trace, um Aufstartprobleme zu sehen
      m_boTraceReady = true;
      ++m_nTraceLines;
    }


    //------------------------------------------------------------
    // Class data
    /// <summary>
    /// Problem report of the tracing itself, for debugging purpose.
    /// </summary>
    public  static string                              sLastTraceProblem = "";
    
    private static TextWriterTraceListener             m_TraceFile = null;
    private static string                              m_TraceFileName="";
    private static StreamWriter                        m_DirectStreamWriter = null;
 
    private static bool                                m_boTraceReady;
    private const  int                                 c_nMaxFileLength = 1000000; // 1MB
    private static int                                 m_nTraceLines = 0;
    private static string                              m_TraceFormat; 
    private static bool                                m_boDisplayDate = (DisplayDate = false);

    //--------------------------------------------------------------------------
    /// <summary>
    /// Graceful application shutdown, called by stop.
    /// </summary>
	public void  Dispose()    {Dispose(true );}

	private void Dispose (bool calledByUser)
	{
      try
      {
        if (m_TraceFile != null)
        {
          if (calledByUser)
          {
            System.Diagnostics.Trace.WriteLine (String.Format ("| {0:D} {0:T} {1} stopped, Exit code = {2}", 
                                           DateTime.Now, WcfDefault.Instance.AppIdentification, Environment.ExitCode));
          }
          else
          {
            System.Diagnostics.Trace.WriteLine (String.Format ("| {0:D} {0:T} {1} interrupted, Exit code = {2}", 
                                           DateTime.Now, WcfDefault.Instance.AppIdentification, Environment.ExitCode));
          }
          System.Diagnostics.Trace.WriteLine ("+-------------------------------------------------------------------------------------------------");
          System.Diagnostics.Trace.Flush ();
        }
      }
      catch (Exception ex)
      {
        TracingException ("##,Error while disposing\r\n   " + ex.Message);
      }
    }
		
    //--------------------------------------------------------------------------
    /// <summary>
    /// Set or get the current tracefile path and name.
    /// </summary>
	public static string FileName
	{
		get{return m_TraceFileName;}
		set{SetTraceOutput (value);}
	}

    ///--------------------------------------------------------------------------
    /// <summary>
    /// choose whether to trace date on each line or not
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
        if (m_boDisplayDate) m_TraceFormat = "{0}{1:d} {1:HH:mm:ss.fff}, {2,-6}, {3}\r\n";
                        else m_TraceFormat =       "{0}{1:HH:mm:ss.fff}, {2,-6}, {3}\r\n";
      }
    }

    //--------------------------------------------------------------------------
    /// <summary>
    /// Set the current tracefile path and name.
    /// </summary>
    public static void SetTraceOutput (string i_FileName)
    {
      bool boStartup = true;
      
      try
      {
        if (i_FileName == null || i_FileName.Length == 0)
        {
          i_FileName = WcfDefault.Instance.TraceFolder;
          // i_FileName += "/" + Path.GetFileNameWithoutExtension (WcfApplication.ExecutablePath);
          // if (!Directory.Exists(i_FileName)) Directory.CreateDirectory(i_FileName);

          // older files than 30 days will be deleted
          DateTime tooOld = DateTime.Now.AddDays(-30);  
          string[] files = Directory.GetFiles (i_FileName, "*.trace.*", SearchOption.TopDirectoryOnly);
          foreach (string filename in files)
          {
            try
            {
              FileInfo fi = new FileInfo(filename);
              if (fi.LastWriteTime < tooOld) {
                fi.Delete();
              }
            }
            catch (Exception) {}
          }
          i_FileName += "/" + WcfDefault.Instance.AppIdentification + ".trace.txt";
        }
      }
      catch (Exception ex)
      {
        TracingException("##,Error while creating directory " + i_FileName +"\r\n   " + ex.Message);
      }

      
      if (m_TraceFile != null)
      {
        boStartup = false;
        System.Diagnostics.Trace.WriteLine (String.Format ("| {0:D} {0:T} Next trace file://{1}", DateTime.Now, i_FileName));
        System.Diagnostics.Trace.Flush ();
        System.Diagnostics.Trace.Listeners.Remove (m_TraceFile);
        m_TraceFileName = "";
        m_TraceFile.Close();
        m_TraceFile.Dispose();
      }
      if (m_DirectStreamWriter != null) m_DirectStreamWriter.Dispose();
      
      // Rename old file
      try
      {
        FileInfo fi = new FileInfo (i_FileName);
        if (fi.Exists)
        {
          if (fi.Length > c_nMaxFileLength)
          {
            string NewFileName = Path.ChangeExtension (i_FileName, "old.txt");
            File.Delete (NewFileName);
            fi.MoveTo   (NewFileName);
          }
        }
      }
      catch (Exception ex)
      {
        TracingException("##,Error while renaming trace file://" + i_FileName +"\r\n   " + ex.Message);
      }

      // Create or Append file
      try
      {
        m_TraceFileName      = i_FileName;
        m_DirectStreamWriter = File.AppendText(i_FileName);
   
        //Create a new text writer using the output stream, and add it to the trace listeners.
        m_TraceFile = new TextWriterTraceListener(m_DirectStreamWriter);
        System.Diagnostics.Trace.Listeners.Add (m_TraceFile);
        System.Diagnostics.Trace.WriteLine ("\n+-------------------------------------------------------------------------------------------------");
        if (boStartup)
        {
          System.Diagnostics.Trace.WriteLine (String.Format ("| TRACE  {0:D} {0:T}: Starting application", DateTime.Now));
        }
        else
        {
          System.Diagnostics.Trace.WriteLine (String.Format ("| TRACE  {0:D} {0:T}: Continue tracing ", DateTime.Now));
        }
      }
      catch (Exception ex)
      {
        // Wenn die Trace.Listener Property null zurückgibt ist ev. kein EXE.config file vorhanden
        TracingException("##,Error while opening trace listener file://" + i_FileName +"\r\n   " + ex.Message);
        if (m_TraceFile != null) m_TraceFile.Dispose(); // schliesst auch den m_DirectStreamWriter
        m_TraceFile = null;    // Trace Listener funktioniert nicht
        try
        {
          m_DirectStreamWriter = File.AppendText(i_FileName); // Notbetrieb direkt ins File
        }
        catch (Exception ex2)
        {
          TracingException("##,Error while opening trace file://" + i_FileName +"\r\n   " + ex2.Message);
        }
      }
      
      String s = AppInfo ();
      s += "\r\n|   CurrentDir \t: " + Environment.CurrentDirectory;
      s += "\r\n|   Tracefile  \t: " + m_TraceFileName;
      s += "\r\n+-------------------------------------------------------------------------------------------------";
      
      System.Diagnostics.Trace.WriteLine (s);
      m_boTraceReady = true;
      m_nTraceLines = 0;
      
    }// SetTraceOutput (i_FileName)


    ///--------------------------------------------------------------------------
    /// <summary>
    /// must be called periodically from Window-Main-Thread
    /// </summary>
    public  static void Refresh ()
    {
      try
      {
        if (m_boTraceReady)
        {
          System.Diagnostics.Trace.Flush ();
          m_boTraceReady = false;
          if (m_nTraceLines > 200)
          {
            m_nTraceLines = 0;
            FileInfo fi = new FileInfo(m_TraceFileName);
            if (fi.Length > c_nMaxFileLength)
            {
              SetTraceOutput (m_TraceFileName);
            }
          }
        }
      }
      catch (Exception ex)
      {
        // Can't do anything against some other process locking the file !
        sLastTraceProblem = ex.Message;
      }
    }// Refresh
    
    
    //--------------------------------------------------------------------------
    /// <summary>
    /// Get tracefile header, may be used for Help - About box.
    /// </summary>
    /// <returns>tracefile header</returns>
    public static String AppInfo ()
    {
      string s = "| ";
      try
      {
        Assembly           myApp = Assembly.GetEntryAssembly   (); // LCsPak
        Assembly           thisA = Assembly.GetCallingAssembly (); // GUI MP
        if (myApp == null)  {myApp = thisA;}
        if (myApp == thisA) {thisA = null; }
        
        s += "\r\n|   Application\t: ";
        s += (Attribute.GetCustomAttribute(myApp, typeof(AssemblyTitleAttribute))        as AssemblyTitleAttribute).Title;
        s += ", Instance " + ApplicationInstance.ToString ();
        s += "\r\n|              \t  ";
        s += (Attribute.GetCustomAttribute(myApp, typeof(AssemblyCopyrightAttribute))    as AssemblyCopyrightAttribute).Copyright;
        s += "\r\n|              \t  ";
        s += (Attribute.GetCustomAttribute(myApp, typeof(AssemblyDescriptionAttribute))  as AssemblyDescriptionAttribute).Description;
        s += "\r\n|";
        s += "\r\n|   Assembly   \t: " + myApp.GetName ().Name;
        s += ", Version "+AssemblyVersion (myApp);

        if (thisA != null)
        {
          s += "\r\n|   Assembly   \t: " + thisA.GetName ().Name;
          s += ", Version " + AssemblyVersion(thisA);
        }
        
        s += "\r\n|";
        s += "\r\n|   Machine    \t: " + Environment.MachineName + " (Process Id " + WcfDefault.Instance.ProcessId + ")";
        s += "\r\n|   OS         \t: " + Environment.OSVersion;
        s += "\r\n|   Framework  \t: ";
        if (WcfApplication.IsRunningWithMono) s+="Mono"; else s+=".NET";
        s += " Version " + Environment.Version;
        s += "\r\n|   User       \t: " + Environment.UserName;
        if (Environment.UserInteractive) s += " (interactive)";
                                    else s += " (not interactive)";
        if (WcfApplication.ServiceName.Length > 0) s += ", running as service "+WcfApplication.ServiceName;
                                   
        string[] arg = Environment.GetCommandLineArgs();
        s += "\r\n|";                            
        s += "\r\n|   Executable \t: " + arg [0];   // = Application.ExecutablePath
        for (int i=1; i < arg.GetLength(0); i++)
        {
          if (i == 1) s += "\r\n|   Commandline\t: ";
          s += arg[i] + " ";
        }
        s += "\r\n|";
      }
      catch (Exception ex)
      {
        TracingException("##,Error while generating application info:" + s + ex.Message);
      }
      return s;
    }// static AppInfo
    
    
    private static string AssemblyVersion (Assembly A)
    {
      string s;
      Version V = A.GetName().Version;
      if (V.Revision == 0) s = V.ToString (3);
                      else s = V.ToString (4);
      s += ", " + (Attribute.GetCustomAttribute( A, typeof(AssemblyConfigurationAttribute))as AssemblyConfigurationAttribute).Configuration;
      try
      {
        s += " "+File.GetLastWriteTime (A.Location).ToString("g"); // general = short date " " short time format
      }
      catch (Exception ex)
      {
        string s2=ex.Message;
        if (s2!=null)s2=null; // remove a warning
      }
      return s;
    }// static AssemblyVersion
    
    
    private static void TracingException (string Message)
    {
      if (m_DirectStreamWriter != null) try
      {
        m_DirectStreamWriter.WriteLine (Message);
      }
      catch (Exception ex)
      {
        MessageBox.Show(Message+"\r\n-------\r\n"+ex.Message, 
                        "Exception while tracing for "+WcfDefault.Instance.AppIdentification,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
      }
    }// static TracingExeception

  }//class PluginFile
}// partial class WcfTrc
}// namespace
