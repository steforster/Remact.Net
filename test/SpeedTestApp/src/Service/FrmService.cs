
// Copyright (c) https://github.com/steforster/Remact.Net

using System;
using System.Windows.Forms;
using Remact.Net;
using Remact.SpeedTest.Contracts;

namespace Remact.SpeedTest.Service
{
    /// <summary>
    /// The main form of the speed test service application.
    /// </summary>
    public partial class FrmService : Form
    {
        #region Program start
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // Commandline arguments:
            // 0: Application instance id. Default = 0 --> process id is used.
            // 1: TCP port number for this service (on localhost). Default = 0, publish to catalog.
            // 2: Transport protocol plugin: 'BMS' or 'JSON'
            RemactDesktopApp.ApplicationStart(args, new RaLog.PluginFile());

            int tcpPort; // the second commandline argument
            if (args.Length < 2 || !int.TryParse(args[1], out tcpPort))
            {
                tcpPort = 40001;
            }

            string transportPlugin = "JSON"; // default third commandline argument
            if (args.Length >= 3)
            {
                transportPlugin = args[2];
            }

            RaLog.Info("Svc1", "Commandline arguments:   ServiceInstance=" + RemactConfigDefault.Instance.ApplicationInstance + " (0=use process id)"
                            + "   ServiceTcpPort=" + tcpPort + " (0=auto, published to catalog)"
                            + "   Transport=" + transportPlugin);
            try
            {
                PluginSelector.LoadRemactConfigDefault(transportPlugin);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new FrmService(tcpPort));
                Environment.ExitCode = 0;
            }
            catch (Exception ex) // any Exception
            {
                RaLog.Exception("Svc1: Fatal error", ex);
            }

            RemactConfigDefault.Instance.Shutdown();
            RaLog.Info("Svc1", "Stop");
            RaLog.Stop();
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
        public FrmService(int tcpPort)
        {
            InitializeComponent();

            m_Service = new Test2Service();
            m_Service.Input.LinkInputToNetwork("Test2.Service", tcpPort);
            this.Text = m_Service.Input.AppIdentification;
            _milliseconds = Environment.TickCount;
        }

        #endregion
        //----------------------------------------------------------------------------------------------
        #region Event Handlers

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                timer1.Stop();
                RemactPort.DisconnectAll(); // Close+Dispose the ServiceHost and CatalogClient.
                m_Service = null;
            }
            catch (Exception ex)
            {
                RaLog.Exception("Svc1: Error while closing the service", ex);
            }
        }

        private int m_Seconds = -1;
        private int _milliseconds;
        //---------------------------------------------------
        private void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Stop();
            if (m_Seconds % 5 == 0) RaLog.Run();
            m_Seconds++;

            try
            {
                m_Service.DoPeriodicTasks();

                int requestCount = m_Service.Requests;
                int current = Environment.TickCount;
                int dt = current - _milliseconds;

                if (dt > 2500)
                {
                    m_Service.Requests -= requestCount;
                    _milliseconds = current;
                    tbStatus.Text = "listening on '" + m_Service.Input.Uri;
                    if (requestCount > 150)
                    {
                        tbStatus.Text += "'\r\n" + (requestCount * 1000 / dt).ToString() + " Requests / sec";
                    }
                    else
                    {
                        tbStatus.Text += "'\r\n" + Math.Round((float)requestCount * 1000.0 / dt, 1).ToString() + " Requests / sec";
                    }
                }
            }
            catch (Exception ex)
            {
                RaLog.Exception("during timerevent", ex);
                tbStatus.Text = ex.Message;
            }
            timer1.Start();
        }

        #endregion
    }
}
