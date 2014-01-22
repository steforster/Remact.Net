using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SourceForge.AsyncWcfLib;
using SourceForge.AsyncWcfLib.Basic;
using Test2.Messages;

namespace Test2.ClientAsyncAwait
{
    class Client
    {
        public TextBox         ServiceHostTextBox;
        public Label           ServiceNameLabel;
        public TextBox         LogTextBox;
        public CheckBox        ConnectCheckBox;
        public Label           StateLabel;
        public CheckBox        SpeedTestCheckBox;
        public ActorOutput     Output { get; private set; }

        private StringBuilder  m_log;
        private int            m_responsesCount;
        private string         m_serviceName;
        private bool           m_run;


        public Client( string clientName, string serviceName )
        {
            Output = new ActorOutput(clientName, OnUnexpectedMessageFromService);
            Output.TraceSend = true;
            m_serviceName = serviceName;
            m_log = new StringBuilder(11000);
        }


        public void Close()
        {
            m_run = false;
            Output.Disconnect();
        }


        public async void RunAsync()
        {
            m_run = true;
            while (m_run)
            {
                bool delay = true;
                if (ConnectCheckBox.Checked)
                {
                    if (Output.OutputState == WcfState.Faulted)
                    {
                        ConnectCheckBox.Checked = false;
                        StateLabel.Text += " -FAULT-";
                    }
                    else if (Output.OutputState == WcfState.Disconnected || Output.OutputState == WcfState.Unlinked)
                    {
                        await Connect();
                    }
                    else if (Output.IsOutputConnected)
                    {
                        if (SpeedTestCheckBox.Checked)
                        {
                            await RunSpeedTest();
                            delay = false;
                        }
                        else
                        {
                            await RunRequestResponseTest();
                        }
                    }
                }
                else if (Output.OutputState != WcfState.Disconnected)
                {
                    if (Output.OutputState != WcfState.Faulted) StateLabel.Text = "disconnected";
                    Output.Disconnect();
                }

                if (delay)
                {
                    await Task.Delay(1000);
                }
            }
        }


        public async Task Connect()
        {
            WcfTrc.Info( "CltAsyncAwait", "open S1" );
            LogTextBox.Text = string.Empty;
            StateLabel.Text = "connecting ...";
            Output.LinkOutputToRemoteService(ServiceHostTextBox.Text, "Test2." + m_serviceName);
            ServiceNameLabel.Text = Output.OutputSidePartner.Uri.ToString();
            await Output.TryConnectAsync();

            ServiceNameLabel.Text = Output.OutputSidePartner.ToString(" ", 3);
            m_responsesCount = 0;
        }


        public async Task RunRequestResponseTest()
        {
            Output.TraceSend = true;
            var req = new WcfIdleMessage();
            var rsp = await Output.SendReceiveAsync(req);

            StateLabel.Text = "CltReq=" + Output.LastRequestIdSent;

            if (rsp.Message is Test2Rsp)
            {
                DisplayMessage( rsp );
                Test2Rsp t2r = rsp.Message as Test2Rsp;
                WcfTrc.Info(rsp.CltRcvId, "Test2Rsp = " + t2r.ToString());
            }
            else
            {
                OnUnexpectedMessageFromService(rsp);
            }
        }


        public async Task RunSpeedTest()
        {
            Output.TraceSend = false;
            var req = new Test2Req (Test2Req.ERequestCode.Normal);
            var rsp = await Output.SendReceiveAsync (req);
            m_responsesCount++;
        }


        private void OnUnexpectedMessageFromService(WcfReqIdent rsp)
        {
            DisplayMessage( rsp );
            if( !(rsp.Message is WcfIdleMessage) )
            {
                WcfTrc.Error(rsp.CltRcvId, "Unexpected message: "+rsp.Message.ToString());
            }
        }


        private void DisplayMessage( WcfReqIdent id )
        {
            m_log.Length = 0;
            m_log.AppendFormat( "{0} {1}, thd={2}",
                id.CltRcvId, id.Message.ToString(), Thread.CurrentThread.ManagedThreadId.ToString() );
            if( Output.OutstandingResponsesCount != 0 )
            {
                m_log.Append( ", out=" );
                m_log.Append( Output.OutstandingResponsesCount );
            }
            m_log.AppendLine();
            m_log.Append( LogTextBox.Text );
            int len = m_log.Length;
            if( len > 10000 ) len = 10000;

            LogTextBox.Text = m_log.ToString( 0, len );
        }


        private int m_Seconds = 0;
        //---------------------------------------------------
        public void Tick()
        {
            m_Seconds++;
            if (m_Seconds % 10 == 0 && SpeedTestCheckBox != null && SpeedTestCheckBox.Checked)
            {
                StateLabel.Text = "CltReq=" + Output.LastRequestIdSent;
                ServiceNameLabel.Text = Output.OutputSidePartner.ToString(" ", 3);
                LogTextBox.Text = ((float)m_responsesCount / 10.0).ToString() + " Responses / sec";
                m_responsesCount = 0;
            }
        }// Tick


    }
}
