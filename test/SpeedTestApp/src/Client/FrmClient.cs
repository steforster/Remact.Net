using System;
using System.Windows.Forms;
using Remact.Net;
using Remact.Net.Remote;
using System.Threading.Tasks;


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
                RemactConfigDefault.Instance = new Remact.Net.Json.Msgpack.Alchemy.JsonProtocolConfig();

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

        private Test2Client _client;
        private Task<bool> _connectionTask;


        /// <summary>
        /// Initializes a new instance of the FrmMain class.
        /// </summary>
        public FrmClient()
        {
            InitializeComponent();
            _client = new Test2Client();
            _client.UpdateView += OnUpdateView;
            labelClient.Text = _client.Output.ToString("Client", 3);
            this.Text = _client.Output.AppIdentification;
        }

        #endregion
        //----------------------------------------------------------------------------------------------
        #region Event Handlers

        // event from client
        void OnUpdateView()
        {
            if (!_client.SpeedTest)
            {
                _client.Log.Append(textBoxService.Text);
                int len = _client.Log.Length;
                if (len > 10000) len = 10000;
                textBoxService.Text = _client.Log.ToString(0, len);
                _client.Log.Length = 0;

                if (labelService.Text.Length == 0)
                {
                    labelClient.Text = _client.Output.ClientIdent.ToString("Client", 3);
                    labelService.Text = _client.Output.ToString("Service", 3);
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
                    if (_client.Output.OutputState != PortState.Disconnected)
                    {
                        if (_client.Output.OutputState != PortState.Faulted) labelState.Text = "disconnected";
                        _client.Output.Disconnect();
                    }
                    return;
                }

                if (_client.Output.OutputState == PortState.Faulted)
                {
                    checkBoxService.Checked = false;
                    labelState.Text = "-FAULT-";
                    if (_connectionTask.Exception != null)
                    {
                        RaLog.PluginConsole.AppendFullMessage(_client.Log, _connectionTask.Exception);
                        _client.Log.AppendLine();
                        _client.SpeedTest = checkBoxSpeedTest.Checked;
                        OnUpdateView();
                    }
                }
                else if (_client.Output.OutputState == PortState.Disconnected
                      || _client.Output.OutputState == PortState.Unlinked)
                {
                    RaLog.Info("Clt1", "open S1");
                    labelState.Text = "connecting ...";
                    labelService.Text = string.Empty;
                    if (RemactConfigDefault.Instance.CatalogHost != textBoxCatalogHost.Text)
                    {
                        RemactConfigDefault.Instance.CatalogHost = textBoxCatalogHost.Text;
                        RemactCatalogClient.Instance.Reconnect();
                    }

                    _client.Output.LinkOutputToRemoteService("Test2.Service");
                    _connectionTask = _client.TryConnect();
                    _client.ResponseCount = 0;
                }
                else if (_client.Output.OutputState == PortState.Ok)
                {
                    if (_client.SpeedTest)
                    {
                        if (m_Seconds % 3 == 0)
                        {
                            //lbClient.Text   = Client1.Output.ToString  ("Client", 20);
                            //lbService1.Text = Client1.Output.OutputSidePartner.ToString("Service", 20);
                            if (_client.ResponseCount > 150)
                            {
                                textBoxService.Text = (_client.ResponseCount / 3).ToString() + " Requests / sec";
                            }
                            else
                            {
                                textBoxService.Text = Math.Round((float)_client.ResponseCount / 3.0, 1).ToString() + " Requests / sec";
                            }

                            _client.ResponseCount = 0;
                        }
                    }

                    labelState.Text = "CltReq=" + _client.LastRequestIdSent;

                    // In speed test mode: Every second an additional request is injected into the request/response stream
                    _client.SpeedTest = checkBoxSpeedTest.Checked;
                    _client.Output.TraceSend = !_client.SpeedTest;
                    _client.SendPeriodicMessage();
                }
            }
            catch (Exception ex)
            {
                RaLog.Exception("during timerevent", ex);
            }
        }// Timer1_Tick


        private void FrmClient_FormClosing(object sender, FormClosingEventArgs e)
        {
            _client.Output.Disconnect();
        }

        #endregion
    }
}
