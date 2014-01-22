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
        RemactConfigDefault.ApplicationStart (args, new RaLog.PluginFile(), /*ExitHandler=*/true);
        RaLog.Info( "Clt1", "Start" );

        Application.Run(new FrmClient());
      }
      catch (Exception ex) // any Exception
      {
        RaLog.Exception ("Clt1: Fatal error", ex);
      }
      RaLog.Info( "Clt1", "Stop" );
      RaLog.Stop ();
    }
    #endregion
    //----------------------------------------------------------------------------------------------
    #region Fields
    
    ActorOutput    Client1;
    StringBuilder  Log1;
    int            m_nResponses1;
    bool           m_boSpeedTest = false;
    
    #endregion
    //----------------------------------------------------------------------------------------------
    #region Constructors

    /// <summary>
    /// Initializes a new instance of the FrmMain class.
    /// </summary>
    public FrmClient ()
    {
      InitializeComponent ();

      Client1 = new ActorOutput("Client1", OnMessageFromService);
      Client1.TraceSend = true;
      Log1 = new StringBuilder(11000);
      lbClient.Text = Client1.ToString("Client ", 20);
      this.Text     = Client1.AppIdentification;
    }

    #endregion
    //----------------------------------------------------------------------------------------------
    #region Event Handlers

    // This handler is used for Test2.Service.
    private void OnMessageFromService (ActorMessage msg)
    {
      if (!m_boSpeedTest)
      {
        Log1.Length=0;
        Log1.AppendFormat("{0} {1}, thd={2}", msg.CltRcvId, msg.Payload.ToString(), Thread.CurrentThread.ManagedThreadId.ToString());
        if (Client1.OutstandingResponsesCount != 0) { Log1.Append(", out="); Log1.Append(Client1.OutstandingResponsesCount); }
      }
      
      if (msg.IsError)
      {
          ErrorMessage error;
          msg.TryConvertPayload(out error);
          RaLog.Warning(msg.CltRcvId, error.ToString());
      }
      else if (m_boSpeedTest)
      {
          m_nResponses1++;
          // send payload to the destination method, do not handle the response here - handle it in the default handler 'OnMessageFromService'
          Client1.Ask<object>("Test2Req", new Test2Req(Test2Req.ERequestCode.Normal), null);
      }
      else
      {
          Test2Rsp t2r;
          if (msg.TryConvertPayload(out t2r))
          {
              RaLog.Info(msg.CltRcvId, "Test2Rsp = " + t2r.ToString());
              string s = string.Empty;
              foreach (var item in t2r.Items)
              {
                  s += ", " + item.ItemName;
              }
              RaLog.Info(msg.CltRcvId, "Test2Rsp contains " + t2r.Items.Count + " items" + s);
          }
          else
          {
              RaLog.Info(msg.CltRcvId, msg.Payload.ToString());
          }
      }

      if (!m_boSpeedTest)
      {
        Log1.Append("\r\n");
        Log1.Append(tbService1.Text);
        int len = Log1.Length;
        if (len > 10000) len = 10000;
        tbService1.Text = Log1.ToString(0, len);

        if (lbService1.Text.Length == 0)
        {
          lbClient.Text   = Client1.ToString ("Client  new", 20);
          lbService1.Text = Client1.OutputSidePartner.ToString("Service new", 20);
        }
      }
    }


    private int  m_Seconds=0;
    //---------------------------------------------------
    private void timer1_Tick (object sender, EventArgs e)
    {
      try
      {
        if (m_Seconds % 5 == 0) RaLog.Run ();
        m_Seconds++;

        if (cbService1.Checked)
        {
          if (Client1.OutputState == PortState.Faulted)
          {
            cbService1.Checked = false;
            lbState1.Text = "-FAULT-";
          }
          else if (Client1.OutputState == PortState.Disconnected || Client1.OutputState == PortState.Unlinked)
          {
              RaLog.Info( "Clt1", "open S1" );
              tbService1.Text = string.Empty;
              lbService1.Text = string.Empty;
              lbState1.Text   = "connecting ...";
              //Client1.LinkOutputToRemoteService (tbCatalogHost.Text, "Test2.Service");
              Client1.LinkOutputToRemoteService(new Uri("ws://"+tbCatalogHost.Text+"/Remact/Test2.Service"));
              ActorInput.DisableRouterClient = true;
              Client1.TryConnect();
              m_nResponses1 = 0;
              m_boSpeedTest = cbSpeedTest1.Checked;
          }
          else if (Client1.OutputState == PortState.Ok)
          {
            if (m_boSpeedTest)
            {
              if (m_Seconds % 10 == 0)
              {
                lbState1.Text   = "CltReq="+Client1.LastRequestIdSent;
                lbClient.Text   = Client1.ToString  ("Client  new", 20);
                lbService1.Text = Client1.OutputSidePartner.ToString ("Service new", 20);
                tbService1.Text = ((float)m_nResponses1 / 10.0).ToString ()+" Responses / sec";
                m_nResponses1   = 0;
                //this.Refresh ();
              }
            }
            else
            {
              lbState1.Text = "CltReq="+Client1.LastRequestIdSent;
              if (cbSpeedTest1.Checked) Client1.Ask<object> ("Test2Req",     new Test2Req (Test2Req.ERequestCode.Normal), null);
                                   else Client1.Ask<object> ("ReadyMessage", new ReadyMessage (), null);
            }

            m_boSpeedTest = cbSpeedTest1.Checked;
            Client1.TraceSend = !m_boSpeedTest;
          }
        }
        else if (Client1.OutputState != PortState.Disconnected)
        {
            if (Client1.OutputState != PortState.Faulted) lbState1.Text = "disconnected";
            Client1.Disconnect();
        }
      }
      catch (Exception ex)
      {
        RaLog.Exception ("during timerevent", ex);
      }
    }// timer1_Tick
    
    
    private void FrmClient_FormClosing(object sender, FormClosingEventArgs e)
    {
      Client1.Disconnect();
    }
    #endregion

  }// class FrmClient
}// namespace
