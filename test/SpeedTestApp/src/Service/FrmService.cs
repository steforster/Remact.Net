using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Remact.Net;
using Test2.Contracts;


namespace Test2.Service
{
  /// <summary>
  /// 
  /// </summary>
  public partial class FrmService : Form
  {
    //----------------------------------------------------------------------------------------------
    #region Program start
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main (string[] args)
    {
      Application.EnableVisualStyles ();
      Application.SetCompatibleTextRenderingDefault (false);
      RemactConfigDefault.ApplicationStart(args, new RaLog.PluginFile());
      RaLog.Info( "Svc1", "Start" );
      try
      {
        Application.Run (new FrmService ());
      }
      catch (Exception ex) // any Exception
      {
        RaLog.Exception ("Svc1: Fatal error", ex);
      }
      RemactConfigDefault.Instance.Shutdown();
      RaLog.Info( "Svc1", "Stop" );
      RaLog.Stop ();
    }
    #endregion
    //----------------------------------------------------------------------------------------------
    #region Fields
    
    private Test2Service m_Service;
    
    #endregion
    //----------------------------------------------------------------------------------------------
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the Form1 class.
    /// </summary>
    public FrmService ()
    {
      InitializeComponent ();
      
      m_Service = new Test2Service ();
      m_Service.Input.LinkInputToNetwork ("Test2.Service", 40002);
      this.Text = m_Service.Input.AppIdentification;
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Event Handlers

    private void Form1_FormClosing(object sender, FormClosingEventArgs e)
    {
      try
      {
        timer1.Stop ();
        ActorPort.DisconnectAll (); // Close+Dispose the ServiceHost and RouterClient.
        m_Service = null;
      }
      catch (Exception ex)
      {
        RaLog.Exception ("Svc1: Error while closing the service", ex);
      }
    }

    private int  m_Seconds=-1;
    //---------------------------------------------------
    private void timer1_Tick (object sender, EventArgs e)
    {
      timer1.Stop();
      if (m_Seconds % 5 == 0) RaLog.Run ();
      m_Seconds++;

      try
      {
        m_Service.DoPeriodicTasks ();

        if (m_Seconds % 3 == 0)
        {
          tbStatus.Text  = "listening on '"+m_Service.Input.Uri;
          if (m_Service.Requests > 150)
          {
              tbStatus.Text += "'\r\n" + (m_Service.Requests / 3).ToString() + " Requests / sec";
          }
          else
          {
              tbStatus.Text += "'\r\n" + Math.Round((float)m_Service.Requests / 3.0, 1).ToString() + " Requests / sec";
          }
          m_Service.Requests = 0;
        }
      }
      catch (Exception ex)
      {
        RaLog.Exception ("during timerevent", ex);
        tbStatus.Text = ex.Message;
      }
      timer1.Start();
    }

    #endregion
  }
}
