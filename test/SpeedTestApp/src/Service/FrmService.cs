using System;
using System.Windows.Forms;
using Remact.Net;


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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            RemactDesktopApp.ApplicationStart(args, new RaLog.PluginFile());
            RaLog.Info("Svc1", "Start");
            RemactConfigDefault.Instance = new Remact.Net.Json.Msgpack.Alchemy.JsonProtocolConfig();
            try
            {
                Application.Run(new FrmService());
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
        public FrmService()
        {
            InitializeComponent();

            m_Service = new Test2Service();
            m_Service.Input.LinkInputToNetwork("Test2.Service");
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
