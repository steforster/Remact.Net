
// Copyright (c) 2012  AsyncWcfLib.sourceforge.net

using System;

namespace SourceForge.AsyncWcfLib
{
	/// <summary>
  /// <para>Use these static methods to write debugging trace or logs.</para> 
  /// <para>This way you are able to redirect your trace anywhere.</para>
  /// <para>How to run it:</para>
  /// <para>1. call WcfTrc.UsePlugin(x) when you don't want to use WcfTrcPluginDefault</para>
  /// <para>2. call WcfTrc.Start(appInstance) to direct output to the correct file and write a trace-header</para> 
  /// <para>3. periodically call WcfTrc.Run() to write the filebuffer to disk</para>
  /// <para>4. calling WcfTrc.Stop() during shutdown writes the trace-footer</para>
	/// </summary>
  public partial class WcfTrc
  {
    //------------------------------------------------------------
    /// <summary>
    /// Set the trace plugin.
    /// </summary>
    /// <param name="p">Plugin, implementing 'ITracePlugin'</param>
    public static void UsePlugin(ITracePlugin p) {ms_Plugin = p;}

    /// <summary>
    /// Write trace header and store 'ApplicationInstance'
    /// </summary>
    /// <param name="appInstance">a number to identify the application instance, see WcfDefault</param>
    public static void Start (int appInstance)   {ApplicationInstance = appInstance;
                                                  if (ms_Plugin != null) ms_Plugin.Start (appInstance);}

    /// <summary>
    /// Call it periodically (e.g. 5 sec.) to flush buffer to tracefile
    /// </summary>
    public static void Run()                     {if (ms_Plugin != null) ms_Plugin.Run();}
    
    /// <summary>
    /// Write trace footer
    /// </summary>
    public static void Stop ()                   {if (ms_Plugin != null) {ms_Plugin.Stop();}}

    //------------------------------------------------------------
    /// <summary>
    /// Write informative trace about program flow.
    /// </summary>
    /// <param name="group">defining the object or object group</param>
    /// <param name="text">Trace text</param>
    /// <param name="logger">Any optional logger framework object to write trace to.</param>
    public static void Info( string group, string text, object logger = null )
    {
        if( ms_Plugin != null ) ms_Plugin.Info( group, text, logger );
    }

    /// <summary>
    /// Write trace for unexpected, but still accepted, recoverable and tested condition.
    /// </summary>
    /// <param name="group">defining the object or object group</param>
    /// <param name="text">Trace text</param>
    /// <param name="logger">Any optional logger framework object to write trace to.</param>
    public static void Warning( string group, string text, object logger = null )
    {
        WarningCount++;
        if( ms_Plugin != null ) ms_Plugin.Warning( group, text, logger );
    }

    /// <summary>
    /// Write trace for unexpected condition that cannot be handled properly.
    /// </summary>
    /// <param name="group">defining the object or object group</param>
    /// <param name="text">Trace text</param>
    /// <param name="logger">Any optional logger framework object to write trace to.</param>
    public static void Error( string group, string text, object logger = null )
    {
        ErrorCount++;
        if (ms_Plugin != null) ms_Plugin.Error( group, text, logger);
    }

    /// <summary>
    /// Write trace about a handled program exception.
    /// </summary>
    /// <param name="text">Trace text.</param>
    /// <param name="ex">The caugth exception object.</param>
    /// <param name="logger">Any optional logger framework object to write trace to.</param>
    public static void Exception( string text, Exception ex, object logger = null )
    {
        ErrorCount++;
        if (ms_Plugin != null) ms_Plugin.Exception( text, ex, logger);
    }
    
    
    //------------------------------------------------------------
    /// <summary>
    /// Trace plugins must implement this interface
    /// </summary>
    public interface ITracePlugin
    {
      /// <summary>
      /// Write trace header and store 'ApplicationInstance'
      /// </summary>
      /// <param name="appInstance">A number to identify the application instance, see WcfDefault</param>
      void Start (int appInstance);
      
      /// <summary>
      /// Call it periodically (e.g. 5 sec.) to flush buffer to tracefile.
      /// </summary>
      void Run ();
      
      /// <summary>
      /// Write an info-trace statement.
      /// </summary>
      /// <param name="group">A mark to group trace source.</param>
      /// <param name="text">The trace line(s).</param>
      /// <param name="logger">Any optional logger framework object to write trace to.</param>
      void Info( string group, string text, object logger );

      /// <summary>
      /// Write a warning-trace statement.
      /// </summary>
      /// <param name="group">A mark to group trace source.</param>
      /// <param name="text">The trace line(s).</param>
      /// <param name="logger">Any optional logger framework object to write trace to.</param>
      void Warning( string group, string text, object logger );

      /// <summary>
      /// Write an error-trace statement.
      /// </summary>
      /// <param name="group">A mark to group trace source.</param>
      /// <param name="text">The trace line(s).</param>
      /// <param name="logger">Any optional logger framework object to write trace to.</param>
      void Error( string group, string text, object logger );

      /// <summary>
      /// Write an exception-trace statement.
      /// </summary>
      /// <param name="text">The trace line(s).</param>
      /// <param name="ex">The exception.</param>
      /// <param name="logger">Any optional logger framework object to write trace to.</param>
      void Exception( string text, Exception ex, object logger );

      /// <summary>
      /// Write trace footer.
      /// </summary>
      void Stop ();
    }

    //------------------------------------------------------------
    /// <summary>
    /// Common application info: ApplicationInstance
    /// </summary>
    public static int ApplicationInstance  {get; private set;}

    private static ITracePlugin ms_Plugin   = null;

    //------------------------------------------------------------
    /// <summary>
    /// Count of warning traces (for unit tests).
    /// </summary>
    public static int WarningCount;

    /// <summary>
    /// Count of warning traces (for unit tests).
    /// </summary>
    public static int ErrorCount;

    /// <summary>
    /// Reset the trace counters to zero.
    /// </summary>
    public static void ResetCount()
    {
        WarningCount = 0;
        ErrorCount = 0;
    }
  }//class WcfTrc
}//namespace

