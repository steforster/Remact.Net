
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Diagnostics;   // Trace Listener, Trace...
using System.Reflection;
using System.Text;    // Assembly, Attributes


namespace Remact.Net
{
public partial class RaLog
{
	/// <summary>
    /// <para>The default implementation of a ILogPlugin</para>
    /// <para>Writes to visual studio diagnostic console or to Terminal/Console.</para>
    /// <para>You can easely write a similar adapter class to redirect log output to your own logging framework.</para>
    /// </summary>
	public class PluginConsole: ILogPlugin
	{
    //------------------------------------------------------------
    // implement ILogPlugin
    /// <summary>
    /// Write log header and store 'ApplicationInstance'
    /// </summary>
    /// <param name="appInstance">a number to identify the application instance, see RemactDefaults</param>
    public void Start (int appInstance)
    {
      ApplicationInstance = appInstance;
    }

    /// <summary>
    /// Call it periodically (e.g. 5 sec.) to flush buffer to log file.
    /// </summary>
    public void Run () { }

    /// <summary>
    /// Write log footer.
    /// </summary>
    public void Stop () { }

    /// <inheritdoc/>
    public void Info( string group, string text, object logger )
    {
        Log( "..", group, text );
    }

    /// <inheritdoc/>
    public void Warning( string group, string text, object logger )
    {
        Log("!!", group, text);
    }

    /// <inheritdoc/>
    public void Error( string group, string text, object logger )
    {
        Log("##", group, text);
    }

    /// <inheritdoc/>
    public void Exception( string text, Exception ex, object logger )
    {
        var sb = new StringBuilder(500);
        sb.AppendLine(text);
        sb.Append("  ");
        AppendFullMessage(sb, ex);
        sb.AppendLine();
        sb.Append(ex.StackTrace);
        Log("##", "EXCEPT", sb.ToString());
    }

    public static void AppendFullMessage(StringBuilder sb, Exception ex)
    {
        while (ex != null)
        {
            if (ex.InnerException != null
            && (ex is AggregateException || ex is TargetInvocationException))
            {
                ex = ex.InnerException;
                continue; // skip one exception log
            }

            sb.Append(ex.GetType().Name);
            sb.Append(": ");
            sb.Append(ex.Message);
            ex = ex.InnerException;
            if (ex != null)
            {
                sb.AppendLine();
                sb.Append("  Inner ");
            }
        }
    }

    /// <summary>
    /// Write a log statement.
    /// </summary>
    /// <param name="severity">A mark to distiguish log severity.</param>
    /// <param name="group">A mark to group log source.</param>
    /// <param name="text">The log line(s).</param>
    private void Log(string severity, string group, string text)
    {
      string s = String.Format (m_LogFormat, severity, DateTime.Now, group, text);
      if (OutputWriter == null) System.Diagnostics.Trace.WriteLine (s);
                           else OutputWriter.WriteLine (s); // for MonoDevelop 2010-04
    }

    //------------------------------------------------------------
    // Class data
    private static string m_LogFormat; 
    private static bool   m_boDisplayDate = (DisplayDate = false);

    /// <summary>
    /// use null, Console.Out or Console.Error to specify where the log should go.
    /// null: output to the developments system diagnostic console.
    /// </summary>
    public  static System.IO.TextWriter OutputWriter = null;

    ///--------------------------------------------------------------------------
    /// <summary>
    /// choose whether to log date on each line or not
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
                        else m_LogFormat =       "{0}{1:HH:mm:ss.fff}, {2,-6}, {3}";
      }
    }
    
  }//class PluginConsole
}// partial class RaLog
}// namespace
