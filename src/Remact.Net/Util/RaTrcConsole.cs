
// Copyright (c) 2014, github.com/steforster/Remact.Net

using System;
using System.Diagnostics;   // Trace Listener, Trace...
using System.Reflection;    // Assembly, Attributes


namespace Remact.Net
{
public partial class RaTrc
{
	/// <summary>
    /// <para>The default implementation of a ITracePlugin</para>
    /// <para>Writes to visual studio diagnostic console or to Terminal/Console.</para>
    /// <para>You can easyly write a similar adapter class to redirect trace output to your own logging framework.</para>
    /// </summary>
	public class PluginConsole: ITracePlugin
	{
    //------------------------------------------------------------
    // implement ITracePlugin
    /// <summary>
    /// Write trace header and store 'ApplicationInstance'
    /// </summary>
    /// <param name="appInstance">a number to identify the application instance, see RemactDefaults</param>
    public void Start (int appInstance)
    {
      ApplicationInstance = appInstance;
    }

    /// <summary>
    /// Call it periodically (e.g. 5 sec.) to flush buffer to tracefile.
    /// </summary>
    public void Run () { }

    /// <summary>
    /// Write trace footer.
    /// </summary>
    public void Stop () { }

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
                  +"\r\n   " + ex.Message
                  +"\r\n"    + ex.StackTrace );
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
      if (OutputWriter == null) System.Diagnostics.Trace.Write (s);
                           else OutputWriter.Write (s); // for MonoDevelop 2010-04
    }

    //------------------------------------------------------------
    // Class data
    private static string m_TraceFormat; 
    private static bool   m_boDisplayDate = (DisplayDate = false);

    /// <summary>
    /// use null, Console.Out or Console.Error to specify where the trace should go.
    /// null: output to the developments system diagnostic console.
    /// </summary>
    public  static System.IO.TextWriter OutputWriter = null;

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
    
  }//class PluginConsole
}// partial class RaTrc
}// namespace
