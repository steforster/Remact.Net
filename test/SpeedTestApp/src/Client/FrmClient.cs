
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Windows.Forms;
using Remact.Net;
using Remact.Net.Remote;
using System.Threading.Tasks;
using Remact.SpeedTest.Contracts;

namespace Remact.SpeedTest.Client
{
    /// <summary>
    /// The main form for the speed test client application.
    /// </summary>
    public partial class FrmClient : Form
    {
        #region Program start

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                RemactDesktopApp.ApplicationStart(args, new RaLog.PluginFile());
                RaLog.Info("Clt1", "Start");

                Application.Run(new FrmClient());
                Environment.ExitCode = 0;
            }
            catch (Exception ex) // any Exception
            {
                RaLog.Exception("Clt1: Fatal error", ex);
            }

            RemactConfigDefault.Instance.Shutdown();
            RaLog.Info("Clt1", "Stop");
            RaLog.Stop();
        }
        #endregion
        //----------------------------------------------------------------------------------------------
        #region Fields and constructor

        private Test2Client m_clientActor;
        private Task<bool> m_connectionTask;


        /// <summary>
        /// Initializes a new instance of the FrmMain class.
        /// </summary>
        public FrmClient()
        {
            InitializeComponent();
            m_clientActor = new Test2Client();
            m_clientActor.UpdateView += OnUpdateView;
            labelClient.Text = m_clientActor.Output.ToString("Client", 3);
            comboBoxTransport.SelectedIndex = 0;
            this.Text = m_clientActor.Output.AppIdentification;
        }

        #endregion
        //----------------------------------------------------------------------------------------------
        #region Event Handlers

        private void comboBoxTransport_SelectedIndexChanged(object sender, EventArgs e)
        {
            PluginSelector.LoadRemactConfigDefault(comboBoxTransport.Text);
        }

        private void FrmClient_FirstShown(object sender, EventArgs e)
        {
            checkBoxService.Focus();
        }

        private void FrmClient_FormClosing(object sender, FormClosingEventArgs e)
        {
            m_clientActor.Output.Disconnect();
        }


        // event from client actor to UI
        private void OnUpdateView()
        {
            if (!m_clientActor.SpeedTest)
            {
                m_clientActor.Log.Append(textBoxService.Text);
                int len = m_clientActor.Log.Length;
                if (len > 10000) len = 10000;
                textBoxService.Text = m_clientActor.Log.ToString(0, len);
                m_clientActor.Log.Length = 0;

                if (labelService.Text.Length == 0)
                {
                    labelClient.Text = m_clientActor.Output.ClientIdent.ToString("Client", 3);
                    labelService.Text = m_clientActor.Output.ToString("Service", 3);
                }
            }
        }


        private int m_Seconds;

        private void Timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                if (m_Seconds % 5 == 0) RaLog.Run();
                m_Seconds++;

                if (!checkBoxService.Checked)
                {
                    if (m_clientActor.Output.OutputState != PortState.Disconnected)
                    {
                        if (m_clientActor.Output.OutputState != PortState.Faulted) labelState.Text = "disconnected";
                        m_clientActor.Output.Disconnect();
                    }
                    return;
                }

                if (m_clientActor.Output.OutputState == PortState.Faulted)
                {
                    checkBoxService.Checked = false;
                    labelState.Text = "-FAULT-";
                    if (m_connectionTask.Exception != null)
                    {
                        RaLog.PluginConsole.AppendFullMessage(m_clientActor.Log, m_connectionTask.Exception);
                        m_clientActor.Log.AppendLine();
                        m_clientActor.SpeedTest = checkBoxSpeedTest.Checked;
                        OnUpdateView();
                    }
                }
                else if (m_clientActor.Output.OutputState == PortState.Disconnected
                      || m_clientActor.Output.OutputState == PortState.Unlinked)
                {
                    RaLog.Info("Clt1", "open S1");
                    labelState.Text = "connecting ...";
                    labelService.Text = string.Empty;
                    if (RemactConfigDefault.Instance.CatalogHost != textBoxCatalogHost.Text)
                    {
                        RemactConfigDefault.Instance.CatalogHost = textBoxCatalogHost.Text;
                        RemactCatalogClient.Instance.Reconnect();
                    }

                    m_clientActor.Output.LinkOutputToRemoteService("Test2.Service");
                    m_connectionTask = m_clientActor.TryConnect();
                    m_clientActor.ResponseCount = 0;
                }
                else if (m_clientActor.Output.OutputState == PortState.Ok)
                {
                    if (m_clientActor.SpeedTest)
                    {
                        if (m_Seconds % 3 == 0)
                        {
                            if (m_clientActor.ResponseCount > 150)
                            {
                                textBoxService.Text = (m_clientActor.ResponseCount / 3).ToString() + " Requests / sec";
                            }
                            else
                            {
                                textBoxService.Text = Math.Round((float)m_clientActor.ResponseCount / 3.0, 1).ToString() + " Requests / sec";
                            }

                            m_clientActor.ResponseCount = 0;
                        }
                    }

                    labelState.Text = "CltReq=" + m_clientActor.LastRequestIdSent;

                    // In speed test mode: Every second an additional request is injected into the request/response stream
                    m_clientActor.SpeedTest = checkBoxSpeedTest.Checked;
                    m_clientActor.Output.TraceSend = !m_clientActor.SpeedTest;
                    m_clientActor.SendPeriodicMessage();
                }
            }
            catch (Exception ex)
            {
                RaLog.Exception("during timerevent", ex);
            }
        }

        #endregion
    }
}
