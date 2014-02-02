using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using Remact.Net;
using Test2.Contracts;


namespace Test2.Client
{
  /// <summary>
  /// 
  /// </summary>
  public partial class FrmClient : Form
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
            RemactConfigDefault.ApplicationStart (args, new RaLog.PluginFile());
            RaLog.Info( "Clt1", "Start" );

            Application.Run(new FrmClient());
        }
        catch (Exception ex) // any Exception
        {
            RaLog.Exception ("Clt1: Fatal error", ex);
        }

        RemactConfigDefault.Instance.Shutdown();
        RaLog.Info( "Clt1", "Stop" );
        RaLog.Stop ();
    }
    #endregion
    //----------------------------------------------------------------------------------------------
    #region Fields and constructor

    private Test2Client Client1;
  

    /// <summary>
    /// Initializes a new instance of the FrmMain class.
    /// </summary>
    public FrmClient ()
    {
        InitializeComponent ();
        Client1 = new Test2Client();
        Client1.UpdateView += OnUpdateView;
        lbClient.Text = Client1.Output.ToString("Client", 20);
        this.Text = Client1.Output.AppIdentification;
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Event Handlers

    // event from client
    void OnUpdateView()
    {
        if (!Client1.SpeedTest)
        {
            Client1.Log.Append(tbService1.Text);
            int len = Client1.Log.Length;
            if (len > 10000) len = 10000;
            tbService1.Text = Client1.Log.ToString(0, len);

            if (lbService1.Text.Length == 0)
            {
                lbClient.Text = Client1.Output.ToString("Client", 20);
                lbService1.Text = Client1.Output.OutputSidePartner.ToString("Service", 20);
            }
        }
    }


    private int  m_Seconds;

    private void Timer1_Tick (object sender, EventArgs e)
    {
      try
      {
        if (m_Seconds % 5 == 0) RaLog.Run ();
        m_Seconds++;

        if (!cbService1.Checked)
        {
            if (Client1.Output.OutputState != PortState.Disconnected)
            {
                if (Client1.Output.OutputState != PortState.Faulted) lbState1.Text = "disconnected";
                Client1.Output.Disconnect();
            }
            return;
        }
            
        if (Client1.Output.OutputState == PortState.Faulted)
        {
            cbService1.Checked = false;
            lbState1.Text = "-FAULT-";
        }
        else if (Client1.Output.OutputState == PortState.Disconnected 
              || Client1.Output.OutputState == PortState.Unlinked)
        {
            RaLog.Info( "Clt1", "open S1" );
            tbService1.Text = string.Empty;
            lbService1.Text = string.Empty;
            lbState1.Text   = "connecting ...";
            //Client1.LinkOutputToRemoteService (tbCatalogHost.Text, "Test2.Service");
            ActorInput.DisableCatalogClient = true; // prov.
            Client1.Output.LinkOutputToRemoteService(new Uri("ws://" + tbCatalogHost.Text + "/Remact/Test2.Service"));
            Client1.TryConnect();
            Client1.ResponseCount = 0;
        }
        else if (Client1.Output.OutputState == PortState.Ok)
        {
            if (Client1.SpeedTest)
            {
                if (m_Seconds % 3 == 0)
                {
                    //lbClient.Text   = Client1.Output.ToString  ("Client", 20);
                    //lbService1.Text = Client1.Output.OutputSidePartner.ToString("Service", 20);
                    if (Client1.ResponseCount > 150)
                    {
                        tbService1.Text = (Client1.ResponseCount / 3).ToString() + " Requests / sec";
                    }
                    else
                    {
                        tbService1.Text = Math.Round((float)Client1.ResponseCount / 3.0, 1).ToString() + " Requests / sec";
                    }

                    Client1.ResponseCount = 0;
                }
            }

            lbState1.Text = "CltReq="+Client1.LastRequestIdSent;
            Client1.SpeedTest = cbSpeedTest1.Checked;
            Client1.SendPeriodicMessage();
            Client1.Output.TraceSend = !Client1.SpeedTest;
        }
      }
      catch (Exception ex)
      {
          RaLog.Exception ("during timerevent", ex);
      }
    }// Timer1_Tick
    
    
    private void FrmClient_FormClosing(object sender, FormClosingEventArgs e)
    {
      Client1.Output.Disconnect();
    }

    #endregion
  }
}
