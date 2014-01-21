using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.ServiceModel;
using System.Threading;
using SourceForge.AsyncWcfLib;
using SourceForge.AsyncWcfLib.Basic;
using Test2.Messages;


namespace Test2.ClientAsyncAwait
{
  /// <summary>
  /// see http://sourceforge.net/apps/mediawiki/asyncwcflib/index.php?title=Run_AsyncWcfLib.Test2
  /// </summary>
  public partial class FrmClientAsyncAwait : Form
  {
    //----------------------------------------------------------------------------------------------
    #region Program start
    
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main (string[] args)
    {
      try
      {
        Application.EnableVisualStyles ();
        Application.SetCompatibleTextRenderingDefault (false);
        WcfDefault.ApplicationStart (args, new WcfTrc.PluginFile(), /*ExitHandler=*/true);
        WcfTrc.Info( "CltAsyncAwait", "Start" );

        Application.Run(new FrmClientAsyncAwait());
      }
      catch (Exception ex) // any Exception
      {
          WcfTrc.Exception("CltAsyncAwait: Fatal error", ex);
      }
      WcfTrc.Info( "CltAsyncAwait", "Stop" );
      WcfTrc.Stop ();
    }
    #endregion
    //----------------------------------------------------------------------------------------------
    #region Fields
    
    Client ClientOld;
    Client ClientNew;
    
    #endregion
    //----------------------------------------------------------------------------------------------
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the FrmMain class.
    /// </summary>
    public FrmClientAsyncAwait ()
    {
        InitializeComponent ();
        Test2Rsp.AddKnownMessageTypes();

        ClientOld = new Client("ClientOld", "ServiceOld")
        {
            ServiceHostTextBox = tbServiceHost,
            ServiceNameLabel = lbService0,
            LogTextBox = tbService0,
            ConnectCheckBox = cbService0,
            StateLabel = lbState0,
            SpeedTestCheckBox = null
        };
        ClientNew = new Client("ClientNew", "ServiceNew")
        {
            ServiceHostTextBox = tbServiceHost,
            ServiceNameLabel = lbService1,
            LogTextBox = tbService1,
            ConnectCheckBox = cbService1,
            StateLabel = lbState1,
            SpeedTestCheckBox = cbSpeedTest1
        };
        lbClient.Text = ClientNew.Output.ToString(":",3);
        this.Text     = ClientNew.Output.AppIdentification;
        ClientOld.RunAsync();
        ClientNew.RunAsync();
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Event Handlers



    private int  m_Seconds=0;
    //---------------------------------------------------
    private void timer1_Tick (object sender, EventArgs e)
    {
      if (m_Seconds % 5 == 0) WcfTrc.Run ();
      m_Seconds++;
      try
      {
          ClientOld.Tick();
          ClientNew.Tick();
      }
      catch (Exception ex)
      {
        WcfTrc.Exception ("during timerevent", ex);
      }
    }// timer1_Tick
    
    
    private void FrmClient_FormClosing(object sender, FormClosingEventArgs e)
    {
      ClientOld.Close();
      ClientNew.Close();
    }
    #endregion

  }// class FrmClient
}// namespace
